#!/bin/bash
# Creates the unprivileged system user/group that owns and runs the
# application, and the on-disk directory layout used by every other script.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
source ../lib/common.sh
require_root
load_config

if ! getent group "${CENTAUR_SERVICE_GROUP}" >/dev/null; then
    log "creating group ${CENTAUR_SERVICE_GROUP}..."
    groupadd --system "${CENTAUR_SERVICE_GROUP}"
fi

if ! getent passwd "${CENTAUR_SERVICE_USER}" >/dev/null; then
    log "creating system user ${CENTAUR_SERVICE_USER} (no login, home ${CENTAUR_BASE_DIR})..."
    useradd --system --gid "${CENTAUR_SERVICE_GROUP}" --home-dir "${CENTAUR_BASE_DIR}" \
        --shell /usr/sbin/nologin "${CENTAUR_SERVICE_USER}"
fi

log "creating directory layout under ${CENTAUR_BASE_DIR}..."
install -d -o "${CENTAUR_SERVICE_USER}" -g "${CENTAUR_SERVICE_GROUP}" -m 0750 "${CENTAUR_BASE_DIR}"
install -d -o "${CENTAUR_SERVICE_USER}" -g "${CENTAUR_SERVICE_GROUP}" -m 0750 "${CENTAUR_LOG_DIR}"
install -d -o "${CENTAUR_SERVICE_USER}" -g "${CENTAUR_SERVICE_GROUP}" -m 0750 "${STATE_DIR}"
install -d -o "${CENTAUR_SERVICE_USER}" -g "${CENTAUR_SERVICE_GROUP}" -m 0750 "${BUILD_DIR}"
for component in api web-ui scoring; do
    install -d -o "${CENTAUR_SERVICE_USER}" -g "${CENTAUR_SERVICE_GROUP}" -m 0750 "${RELEASES_DIR}/${component}/_builds"
done

install -d -o root -g root -m 0755 /etc/centaur-scores

log "02-create-service-user-and-dirs.sh done."
