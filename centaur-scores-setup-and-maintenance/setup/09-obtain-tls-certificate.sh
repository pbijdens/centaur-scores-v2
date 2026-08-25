#!/bin/bash
# Requests (or renews) the Let's Encrypt certificate for PUBLIC_HOSTNAME via
# certbot's nginx plugin, which edits centaur-scores.conf in place to add the
# "listen 443 ssl;" block and an http->https redirect, and installs the
# certbot renewal systemd timer.
#
# Prerequisite: DNS for PUBLIC_HOSTNAME must already resolve to this server's
# public IP (you manage DNS yourself), and setup/08-render-service-configs.sh
# must already have nginx serving PUBLIC_HOSTNAME on port 80.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
source ../lib/common.sh
require_root
load_config

log "checking DNS for ${PUBLIC_HOSTNAME}..."
if command -v dig >/dev/null 2>&1; then
    dig +short "${PUBLIC_HOSTNAME}" || true
fi
echo
if ! confirm "Does the DNS output above point at this server's public IP?"; then
    die "fix DNS first, then re-run this script."
fi

certbot --nginx \
    -d "${PUBLIC_HOSTNAME}" \
    --non-interactive --agree-tos --redirect \
    -m "${LETSENCRYPT_EMAIL}"

nginx -t
systemctl reload nginx

systemctl enable --now certbot.timer

log "certificate obtained. Renewal is handled automatically by certbot.timer"
log "(test it any time with: certbot renew --dry-run)."
log "09-obtain-tls-certificate.sh done."
