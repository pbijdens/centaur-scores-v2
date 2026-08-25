#!/bin/bash
# Installs base OS packages needed by every later step: build tools, curl,
# git, gettext-base (for envsubst), and the ICU/SSL libraries the .NET
# runtime needs on bare Debian.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
source ../lib/common.sh
require_root

log "updating apt package index and installed packages..."
apt-get update
apt-get -y upgrade

log "installing base packages..."
apt-get install -y \
    ca-certificates curl wget gnupg git unzip \
    build-essential \
    gettext-base \
    logrotate \
    libicu-dev libssl-dev zlib1g \
    ufw

log "01-install-base-packages.sh done."
