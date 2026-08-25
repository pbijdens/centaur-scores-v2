#!/bin/bash
# Installs MariaDB (Debian's native, wire-compatible drop-in for the MySQL
# 8.4 used by the Docker image - see INSTALL.md for why), starts it, and
# creates the application database and user. A fresh Debian MariaDB install
# defaults root to unix_socket auth, so this script (run as root) does not
# need a MySQL root password.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
source ../lib/common.sh
require_root
load_config

apt-get install -y mariadb-server
systemctl enable --now mariadb

log "creating database '${MYSQL_DATABASENAME}' and user '${MYSQL_USERNAME_APP}'@'localhost'..."
mysql -u root <<SQL
CREATE DATABASE IF NOT EXISTS \`${MYSQL_DATABASENAME}\` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER IF NOT EXISTS '${MYSQL_USERNAME_APP}'@'localhost' IDENTIFIED BY '${MYSQL_PASSWORD_APP}';
ALTER USER '${MYSQL_USERNAME_APP}'@'localhost' IDENTIFIED BY '${MYSQL_PASSWORD_APP}';
GRANT ALL PRIVILEGES ON \`${MYSQL_DATABASENAME}\`.* TO '${MYSQL_USERNAME_APP}'@'localhost';
FLUSH PRIVILEGES;
SQL

log "database ready. The API (both blue and green) connects to it over 127.0.0.1:3306."
log "consider running 'mysql_secure_installation' afterwards for extra hardening (optional - not required for the app)."
log "05-install-mysql.sh done."
