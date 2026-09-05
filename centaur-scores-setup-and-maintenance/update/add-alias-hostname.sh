#!/bin/bash
# Adds a secondary public hostname (an "alias") to the site, serving the same
# blue/green web-ui and API as PUBLIC_HOSTNAME. The web-ui served under the
# alias still calls the API at the original PUBLIC_HOSTNAME - that absolute
# URL is baked into the build at compile time (see lib/common.sh's
# PUBLIC_API_URL) - which works cross-origin because the API's CORS policy
# allows any origin. No rebuild/redeploy is needed to add an alias.
#
# What this does:
#   1. renders a new nginx server block for the alias hostname
#      (/etc/nginx/sites-available/centaur-scores-alias-<hostname>.conf)
#   2. requests certbot to add the alias hostname as a SAN on the existing
#      PUBLIC_HOSTNAME certificate (--expand), which also edits both site
#      confs in place to add "listen 443 ssl;" and an http->https redirect
#
# Usage: update/add-alias-hostname.sh <alias-hostname>
#
# Safe to re-run: if the alias site conf already exists, it is left
# untouched (it may already carry certbot's TLS edits) and only the certbot
# step is repeated, which is a no-op if the hostname is already on the cert.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
source ../lib/common.sh
require_root
load_config

alias_hostname="${1:-}"
[[ -n "${alias_hostname}" ]] || die "usage: $0 <alias-hostname>"
[[ "${alias_hostname}" != "${PUBLIC_HOSTNAME}" ]] || die "'${alias_hostname}' is already PUBLIC_HOSTNAME."

log "checking DNS for ${alias_hostname}..."
if command -v dig >/dev/null 2>&1; then
    dig +short "${alias_hostname}" || true
fi
echo
if ! confirm "Does the DNS output above point at this server's public IP?"; then
    die "fix DNS first, then re-run this script."
fi

site_conf="/etc/nginx/sites-available/centaur-scores-alias-${alias_hostname}.conf"
if [[ -f "${site_conf}" ]]; then
    log "${site_conf} already exists - leaving it untouched (it may already carry certbot's TLS edits)."
else
    ALIAS_HOSTNAME="${alias_hostname}" render_template \
        "${CS_TEMPLATES_DIR}/nginx-alias-site.conf.tmpl" "${site_conf}" \
        ALIAS_HOSTNAME PUBLIC_API_VDIR PUBLIC_APP_VDIR \
        PUBLIC_API_VDIR_NOSLASH PUBLIC_APP_VDIR_NOSLASH CENTAUR_LOG_DIR CENTAUR_BASE_DIR
fi
ln -sf "${site_conf}" "/etc/nginx/sites-enabled/centaur-scores-alias-${alias_hostname}.conf"

nginx_test_and_reload

log "requesting certbot to add ${alias_hostname} to the ${PUBLIC_HOSTNAME} certificate..."
certbot --nginx \
    -d "${PUBLIC_HOSTNAME}" -d "${alias_hostname}" \
    --cert-name "${PUBLIC_HOSTNAME}" --expand \
    --non-interactive --agree-tos --redirect \
    -m "${LETSENCRYPT_EMAIL}"

nginx_test_and_reload

log "alias hostname https://${alias_hostname} is now live alongside https://${PUBLIC_HOSTNAME}."
log "it serves the same web-ui/API as the primary hostname; no rebuild or redeploy was needed."
log "add-alias-hostname.sh done."
