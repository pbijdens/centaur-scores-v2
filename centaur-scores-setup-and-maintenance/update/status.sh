#!/bin/bash
# Prints the current blue/green routing state, each slot's deployed
# version, systemd service status, and a couple of quick health checks.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
source ../lib/common.sh
require_root
load_config

active="$(get_active_slot)"
candidate="$(other_slot "${active}")"
weight="$(get_candidate_weight)"

echo "=== Traffic routing ==="
echo "active slot:    ${active}  (serving $((100 - weight))% of new sessions)"
echo "candidate slot: ${candidate}  (serving ${weight}% of new sessions)"
echo

for slot in blue green; do
    echo "=== Slot: ${slot} (port $(slot_port "${slot}")) ==="
    version_file="$(deployed_version_file "${slot}")"
    if [[ -f "${version_file}" ]]; then
        cat "${version_file}"
    else
        echo "no release deployed yet"
    fi
    systemctl is-active "centaur-scores-api-${slot}.service" 2>/dev/null | sed "s/^/service: /" || true
    if curl -sf "http://127.0.0.1:$(slot_port "${slot}")/health" 2>/dev/null; then
        echo
        echo "health: ok"
    else
        echo "health: unreachable"
    fi
    echo
done

echo "=== nginx ==="
nginx -t
