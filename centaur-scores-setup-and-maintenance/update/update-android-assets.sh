#!/bin/bash
# Downloads the pre-built debug and release APKs from the latest GitHub
# release of the monorepo and publishes them as static files at
# /android/app-debug.apk and /android/app-release.apk.
#
# This is entirely separate from the blue/green web/API deployment: there is
# no slot, no traffic split, no restart - just a flat pair of files nginx
# serves directly (see the "location ^~ /android/" block rendered by
# setup/08-render-service-configs.sh). Re-run this script any time you want
# to pick up a newer release; it does nothing else.
#
# Usage: update/update-android-assets.sh
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
source ../lib/common.sh
require_root
load_config

repo_path="$(echo "${GITHUB_REPO_URL}" | sed -E 's#^(https?://github\.com/|git@github\.com:)##; s#\.git$##')"
api_url="https://api.github.com/repos/${repo_path}/releases/latest"
android_dir="${CENTAUR_BASE_DIR}/android"

install -d -o "${CENTAUR_SERVICE_USER}" -g "${CENTAUR_SERVICE_GROUP}" -m 0750 "${android_dir}"

log "checking ${api_url} for the latest release..."
release_json="$(curl -sf "${api_url}")" || die "failed to query ${api_url} - check network access and GITHUB_REPO_URL."

fetch_asset_url() {
    local asset_name="$1"
    echo "${release_json}" | grep browser_download_url | grep "${asset_name}" | cut -d '"' -f4
}

cleanup_tmp() { rm -f "${android_dir}"/*.tmp.$$; }
trap cleanup_tmp EXIT

for kind in debug release; do
    asset_name="app-${kind}.apk"
    url="$(fetch_asset_url "${asset_name}")"
    [[ -n "${url}" ]] || die "the latest GitHub release has no '${asset_name}' asset."

    log "downloading ${asset_name} from ${url}..."
    tmp="${android_dir}/${asset_name}.tmp.$$"
    curl -sSLf -o "${tmp}" "${url}"
    chown "${CENTAUR_SERVICE_USER}:${CENTAUR_SERVICE_GROUP}" "${tmp}"
    chmod 0640 "${tmp}"
    mv -f "${tmp}" "${android_dir}/${asset_name}"
    log "installed ${android_dir}/${asset_name}"
done

log "update-android-assets.sh done."
