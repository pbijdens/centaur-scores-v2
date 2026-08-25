#!/bin/bash
# Installs nginx and certbot (with its nginx plugin, which both edits the
# nginx config to add TLS and installs the renewal timer).
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
source ../lib/common.sh
require_root

apt-get install -y nginx certbot python3-certbot-nginx

# The Debian default site would otherwise conflict with our server_name-based
# routing; we serve everything through our own config instead.
rm -f /etc/nginx/sites-enabled/default

systemctl enable --now nginx

log "06-install-nginx-and-certbot.sh done."
