#!/bin/bash
# Sets what percentage of new visitor sessions are routed to the candidate
# (non-active) slot. Existing visitors already assigned to a slot (via their
# csv_slot cookie) are unaffected until their cookie expires or is cleared.
#
# Usage: update/set-traffic.sh <0-100>
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
source ../lib/common.sh
require_root
load_config

weight="${1:-}"
[[ -n "${weight}" ]] || die "usage: $0 <0-100>"

active="$(get_active_slot)"
candidate="$(other_slot "${active}")"

if [[ "${weight}" != "0" ]] && [[ ! -e "${RELEASES_DIR}/api/${candidate}" || ! -e "$(deployed_version_file "${candidate}")" ]]; then
    die "candidate slot '${candidate}' has no deployed release yet - run update/deploy.sh ${candidate} <branch> first."
fi

set_candidate_weight "${weight}"
render_nginx_upstreams
nginx_test_and_reload

log "active='${active}' now serves $((100 - weight))%, candidate='${candidate}' now serves ${weight}%."
log "set-traffic.sh done."
