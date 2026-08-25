#!/bin/bash
# Builds and deploys all three components (API, web-ui, mobile scoring) from
# one branch of the monorepo into one blue/green slot.
#
# Usage: update/deploy.sh <blue|green> <branch>
#
# This only ships code into the target slot and restarts its API service -
# it never changes how much public traffic reaches that slot. Use
# update/set-traffic.sh, update/promote.sh and update/rollback.sh for that.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
source ../lib/common.sh
require_root
load_config

slot="${1:-}"
branch="${2:-}"
[[ -n "${slot}" && -n "${branch}" ]] || die "usage: $0 <blue|green> <branch>"
validate_slot "${slot}"

active="$(get_active_slot)"
if [[ "${slot}" == "${active}" ]]; then
    warn "'${slot}' is the currently ACTIVE slot, serving $(( 100 - $(get_candidate_weight) ))% of traffic."
    warn "Deploying here replaces production code immediately on restart (a few seconds of API downtime for this slot)."
    confirm "Deploy '${branch}' directly into the live '${slot}' slot?" || die "aborted."
fi

port="$(slot_port "${slot}")"
timestamp="$(date -u +%Y%m%d%H%M%S)"
build_root="${BUILD_DIR}/${slot}"
src_dir="${build_root}/src"

log "cloning ${GITHUB_REPO_URL} (branch ${branch}) into ${src_dir}..."
rm -rf "${build_root}"
mkdir -p "${build_root}"
git clone --branch "${branch}" --depth 1 "${GITHUB_REPO_URL}" "${src_dir}"
commit_sha="$(git -C "${src_dir}" rev-parse --short HEAD)"

# --- API ----------------------------------------------------------------------
log "building centaur-scores-api-v2 (${branch}@${commit_sha})..."
api_build_dir="${RELEASES_DIR}/api/_builds/${slot}-${timestamp}"
dotnet publish "${src_dir}/centaur-scores-api-v2/CentaurScores.Api/CentaurScores.Api.csproj" \
    --configuration Release \
    --output "${api_build_dir}"

# --- web-ui ---------------------------------------------------------------------
log "building centaur-scores-web-ui (${branch}@${commit_sha})..."
pushd "${src_dir}/centaur-scores-web-ui" >/dev/null
npm ci
VITE_API_BASE_URL="${PUBLIC_API_URL}" npm run build
popd >/dev/null
webui_build_dir="${RELEASES_DIR}/web-ui/_builds/${slot}-${timestamp}"
mkdir -p "${webui_build_dir}"
cp -R "${src_dir}/centaur-scores-web-ui/dist/." "${webui_build_dir}/"

# --- mobile scoring app ----------------------------------------------------------
log "building centaur-scores-mobile-web-scoring (${branch}@${commit_sha})..."
pushd "${src_dir}/centaur-scores-mobile-web-scoring" >/dev/null
npm ci
npm run build
popd >/dev/null
scoring_build_dir="${RELEASES_DIR}/scoring/_builds/${slot}-${timestamp}"
mkdir -p "${scoring_build_dir}"
cp -R "${src_dir}/centaur-scores-mobile-web-scoring/dist/." "${scoring_build_dir}/"

chown -R "${CENTAUR_SERVICE_USER}:${CENTAUR_SERVICE_GROUP}" \
    "${api_build_dir}" "${webui_build_dir}" "${scoring_build_dir}"

# --- flip symlinks atomically -----------------------------------------------------
log "switching '${slot}' to the new build..."
atomic_symlink "${api_build_dir}" "${RELEASES_DIR}/api/${slot}"
atomic_symlink "${webui_build_dir}" "${RELEASES_DIR}/web-ui/${slot}"
atomic_symlink "${scoring_build_dir}" "${RELEASES_DIR}/scoring/${slot}"

echo "branch=${branch} commit=${commit_sha} deployed_at=$(date -u --iso-8601=seconds)" > "$(deployed_version_file "${slot}")"

# --- restart API and wait for it to become healthy ----------------------------------
log "restarting centaur-scores-api-${slot}.service..."
systemctl restart "centaur-scores-api-${slot}.service"

healthy=0
for _ in $(seq 1 30); do
    if curl -sf "http://127.0.0.1:${port}/health" >/dev/null 2>&1; then
        healthy=1
        break
    fi
    sleep 1
done

if [[ "${healthy}" -ne 1 ]]; then
    warn "centaur-scores-api-${slot}.service did not report healthy within 30s."
    warn "check: systemctl status centaur-scores-api-${slot}.service"
    warn "check: journalctl -u centaur-scores-api-${slot}.service -n 100"
    warn "check: tail -n 100 ${CENTAUR_LOG_DIR}/api-${slot}.log"
    die "deploy to '${slot}' did NOT verify healthy - traffic routing was not changed, but this slot's service may be down."
fi
log "centaur-scores-api-${slot}.service is healthy."

# --- prune old builds, keep the last RELEASES_TO_KEEP per component per slot ---------
# (find, not ls+glob, so an empty match set doesn't trip `set -e`/pipefail)
for component in api web-ui scoring; do
    builds_dir="${RELEASES_DIR}/${component}/_builds"
    find "${builds_dir}" -maxdepth 1 -mindepth 1 -type d -name "${slot}-*" -printf '%T@\t%p\n' 2>/dev/null \
        | sort -rn | cut -f2- | tail -n "+$((RELEASES_TO_KEEP + 1))" | while IFS= read -r stale; do
        log "pruning old build ${stale}"
        rm -rf "${stale}"
    done
done

rm -rf "${build_root}"

log "deployed ${branch}@${commit_sha} to slot '${slot}'."
if [[ "${slot}" == "${active}" ]]; then
    log "'${slot}' is the active slot, so this change is already live for its current traffic share."
else
    log "'${slot}' is currently a candidate slot with $(get_candidate_weight)% traffic."
    log "use update/set-traffic.sh <percent> to start sending visitors to it, or update/promote.sh to make it fully active."
fi
log "deploy.sh done."
