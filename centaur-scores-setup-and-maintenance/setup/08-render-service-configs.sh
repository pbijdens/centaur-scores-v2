#!/bin/bash
# Renders and installs every generated config file: the per-slot systemd
# units, the app secrets env files, logrotate configs, the log-budget timer,
# and the nginx site/upstream config. Idempotent - safe to re-run, except it
# will never overwrite an nginx site config that already exists (see the
# warning below), so it won't clobber certbot's edits on re-runs after step 09.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
source ../lib/common.sh
require_root
load_config

# --- initial state -----------------------------------------------------------
if [[ ! -f "${STATE_DIR}/active-slot" ]]; then
    log "no active slot recorded yet - defaulting to 'blue' with 0% candidate traffic."
    echo -n "blue" > "${STATE_DIR}/active-slot"
fi
if [[ ! -f "${STATE_DIR}/candidate-weight" ]]; then
    echo -n "0" > "${STATE_DIR}/candidate-weight"
fi
chown -R "${CENTAUR_SERVICE_USER}:${CENTAUR_SERVICE_GROUP}" "${STATE_DIR}"

# Deliberately not pre-creating releases/<component>/<slot> here: nginx
# tolerates a root/alias pointing at a path that doesn't exist yet (it just
# 404s until update/deploy.sh creates the real symlink), and pre-creating
# them as plain directories would block deploy.sh's atomic symlink swap
# later (mv -T refuses to overwrite a real directory with a symlink).

# --- app secrets / env files, root:service-group readable only -------------
install -d -m 0750 -o root -g "${CENTAUR_SERVICE_GROUP}" /etc/centaur-scores

cat > /etc/centaur-scores/common.env.tmp <<EOF
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__Default=server=127.0.0.1;port=3306;database=${MYSQL_DATABASENAME};user=${MYSQL_USERNAME_APP};password=${MYSQL_PASSWORD_APP}
Jwt__Secret=${JWT_SECRET}
EOF
mv -f /etc/centaur-scores/common.env.tmp /etc/centaur-scores/common.env
chown root:"${CENTAUR_SERVICE_GROUP}" /etc/centaur-scores/common.env
chmod 640 /etc/centaur-scores/common.env

for slot in blue green; do
    port="$(slot_port "${slot}")"
    cat > "/etc/centaur-scores/api-${slot}.env.tmp" <<EOF
ASPNETCORE_URLS=http://127.0.0.1:${port}
EOF
    mv -f "/etc/centaur-scores/api-${slot}.env.tmp" "/etc/centaur-scores/api-${slot}.env"
    chown root:"${CENTAUR_SERVICE_GROUP}" "/etc/centaur-scores/api-${slot}.env"
    chmod 640 "/etc/centaur-scores/api-${slot}.env"
done

# --- systemd units for both API slots ---------------------------------------
for slot in blue green; do
    SLOT="${slot}" render_template "${CS_TEMPLATES_DIR}/systemd-api.service.tmpl" \
        "/etc/systemd/system/centaur-scores-api-${slot}.service" \
        SLOT CENTAUR_SERVICE_USER CENTAUR_SERVICE_GROUP CENTAUR_BASE_DIR CENTAUR_LOG_DIR
done

# --- logrotate + log budget timer --------------------------------------------
render_template "${CS_TEMPLATES_DIR}/logrotate-api.tmpl" /etc/logrotate.d/centaur-scores-api \
    CENTAUR_LOG_DIR LOG_MAX_FILE_SIZE LOG_RETENTION_DAYS
render_template "${CS_TEMPLATES_DIR}/logrotate-nginx.tmpl" /etc/logrotate.d/centaur-scores-nginx \
    CENTAUR_LOG_DIR LOG_MAX_FILE_SIZE LOG_RETENTION_DAYS

render_template "${CS_TEMPLATES_DIR}/log-prune.sh.tmpl" /usr/local/bin/centaur-scores-log-prune.sh \
    CENTAUR_LOG_DIR LOG_TOTAL_BUDGET_BYTES
chmod +x /usr/local/bin/centaur-scores-log-prune.sh

render_template "${CS_TEMPLATES_DIR}/systemd-logs.service.tmpl" /etc/systemd/system/centaur-scores-logs.service \
    CENTAUR_BASE_DIR
render_template "${CS_TEMPLATES_DIR}/systemd-logs.timer.tmpl" /etc/systemd/system/centaur-scores-logs.timer

systemctl daemon-reload
systemctl enable --now centaur-scores-logs.timer
for slot in blue green; do
    systemctl enable "centaur-scores-api-${slot}.service"
done

# --- nginx --------------------------------------------------------------------
render_nginx_upstreams

site_conf=/etc/nginx/sites-available/centaur-scores.conf
if [[ -f "${site_conf}" ]]; then
    log "${site_conf} already exists - leaving it untouched (it may already carry certbot's TLS edits)."
else
    render_template "${CS_TEMPLATES_DIR}/nginx-site.conf.tmpl" "${site_conf}" \
        PUBLIC_HOSTNAME PUBLIC_API_VDIR PUBLIC_APP_VDIR \
        PUBLIC_API_VDIR_NOSLASH PUBLIC_APP_VDIR_NOSLASH CENTAUR_LOG_DIR
fi
ln -sf "${site_conf}" /etc/nginx/sites-enabled/centaur-scores.conf

nginx_test_and_reload

log "08-render-service-configs.sh done."
