#!/bin/bash
# Completes a rollout: makes the candidate slot the new active slot (100% of
# NEW sessions; existing sessions already pinned to the candidate slot are
# unaffected, existing sessions pinned to the old active slot keep getting it
# until their csv_slot cookie expires - 30 days - or is cleared). The old
# active slot is left running as the new candidate at 0%, ready for a fast
# rollback with update/rollback.sh; stop it later with update/stop-slot.sh
# once you're confident.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
source ../lib/common.sh
require_root
load_config

active="$(get_active_slot)"
candidate="$(other_slot "${active}")"
weight="$(get_candidate_weight)"

[[ -e "$(deployed_version_file "${candidate}")" ]] || die "candidate slot '${candidate}' has no deployed release - nothing to promote."

echo
echo "This will make '${candidate}' the active slot (100% of new sessions)."
echo "It currently receives ${weight}% of traffic."
[[ "${weight}" -gt 0 ]] || warn "candidate '${candidate}' has received 0% real traffic so far - consider running set-traffic.sh first."
confirm "Promote '${candidate}' to active?" || die "aborted."

set_active_slot "${candidate}"
set_candidate_weight 0
render_nginx_upstreams
nginx_test_and_reload

log "'${candidate}' is now active (100%). '${active}' is now the candidate slot at 0%, still running for quick rollback."
log "promote.sh done."
