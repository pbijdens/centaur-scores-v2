#!/bin/bash
# Installs Node.js 24.x (matching the node:24 image used by the Docker build)
# from the NodeSource apt repository. Node is needed because update/deploy.sh
# builds both Svelte frontends locally on this server via `npm run build`.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
source ../lib/common.sh
require_root

NODE_MAJOR="24"

curl -fsSL "https://deb.nodesource.com/setup_${NODE_MAJOR}.x" | bash -
apt-get install -y nodejs

log "installed: node $(node --version), npm $(npm --version)"
log "04-install-nodejs.sh done."
