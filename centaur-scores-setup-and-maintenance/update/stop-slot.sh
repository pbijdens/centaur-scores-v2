#!/bin/bash
# Stops (and disables) a slot's API systemd service, e.g. to free resources
# once you're confident you no longer need it for a fast rollback. Refuses
# to stop the currently active slot unless --force is given.
#
# Usage: update/stop-slot.sh <blue|green> [--force]
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
source ../lib/common.sh
require_root
load_config

slot="${1:-}"
force="${2:-}"
[[ -n "${slot}" ]] || die "usage: $0 <blue|green> [--force]"
validate_slot "${slot}"

active="$(get_active_slot)"
if [[ "${slot}" == "${active}" && "${force}" != "--force" ]]; then
    die "'${slot}' is the active slot. Pass --force if you really want to stop it (this will take the site down for anyone still routed there)."
fi

systemctl stop "centaur-scores-api-${slot}.service"
systemctl disable "centaur-scores-api-${slot}.service"

log "stopped and disabled centaur-scores-api-${slot}.service. Re-deploy with update/deploy.sh ${slot} <branch> to bring it back."
log "stop-slot.sh done."
