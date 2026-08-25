#!/bin/bash
# Configures ufw to allow only SSH and HTTP/HTTPS. This is a lock-out risk if
# your SSH port differs from the OpenSSH default, so it asks for explicit
# confirmation before enabling the firewall.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
source ../lib/common.sh
require_root

echo
echo "This will enable ufw with only OpenSSH and 'Nginx Full' (80/443) allowed."
echo "If you connect over SSH on a NON-STANDARD PORT, edit this script to"
echo "'ufw allow <your-port>/tcp' first, or you may lock yourself out."
echo

ufw allow OpenSSH
ufw allow 'Nginx Full'

if confirm "Enable ufw now with the rules above?"; then
    ufw --force enable
    ufw status verbose
else
    log "skipped enabling ufw. Rules were added but the firewall is not active; run 'ufw enable' manually when ready."
fi

log "07-configure-firewall.sh done."
