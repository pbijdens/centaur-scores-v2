#!/bin/bash
# Installs nginx and certbot (with its nginx plugin, which both edits the
# nginx config to add TLS and installs the renewal timer).
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
source ../lib/common.sh
require_root
load_config

apt-get install -y nginx certbot python3-certbot-nginx

# The Debian default site would otherwise conflict with our server_name-based
# routing; we serve everything through our own config instead.
rm -f /etc/nginx/sites-enabled/default

# nginx's workers run as www-data (Debian default) and need to traverse and
# read /srv/centaur-scores/releases/... to serve the static sites - those
# directories are 0750, owned by CENTAUR_SERVICE_GROUP, with no access for
# "other". Add www-data to that group rather than loosening the directories
# to world-readable, so build/state/log directories that also live under
# /srv/centaur-scores stay private to the app.
log "adding www-data to group ${CENTAUR_SERVICE_GROUP} so nginx can read the deployed release directories..."
usermod -aG "${CENTAUR_SERVICE_GROUP}" www-data

systemctl enable --now nginx

log "06-install-nginx-and-certbot.sh done."
