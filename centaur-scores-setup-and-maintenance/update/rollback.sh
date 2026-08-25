#!/bin/bash
# Emergency rollback: immediately swaps the active slot back to the other
# one and sets candidate traffic to 0%, sending all NEW sessions away from
# whatever is currently active. Use this if the active slot (just promoted,
# or mid-canary) turns out to be broken. Does not touch either slot's
# deployed code - it only changes routing.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
source ../lib/common.sh
require_root
load_config

active="$(get_active_slot)"
other="$(other_slot "${active}")"

[[ -e "$(deployed_version_file "${other}")" ]] || die "slot '${other}' has no deployed release to roll back to."

warn "EMERGENCY ROLLBACK: switching active slot from '${active}' to '${other}' right now."
confirm "Proceed?" || die "aborted."

set_active_slot "${other}"
set_candidate_weight 0
render_nginx_upstreams
nginx_test_and_reload

log "'${other}' is now active (100%). '${active}' is now the candidate slot at 0%."
log "rollback.sh done."
