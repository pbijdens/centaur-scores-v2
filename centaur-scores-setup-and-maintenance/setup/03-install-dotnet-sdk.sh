#!/bin/bash
# Installs the .NET 10 SDK from Microsoft's official install script. The SDK
# (not just the runtime) is needed because update/deploy.sh builds the API
# locally on this server via `dotnet publish`.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
source ../lib/common.sh
require_root

DOTNET_INSTALL_DIR="/opt/dotnet"
DOTNET_CHANNEL="10.0"

if [[ -x "${DOTNET_INSTALL_DIR}/dotnet" ]]; then
    log "dotnet already installed at ${DOTNET_INSTALL_DIR}, checking for updates..."
fi

tmp_script="$(mktemp)"
curl -fsSL https://dot.net/v1/dotnet-install.sh -o "${tmp_script}"
chmod +x "${tmp_script}"
"${tmp_script}" --channel "${DOTNET_CHANNEL}" --install-dir "${DOTNET_INSTALL_DIR}"
rm -f "${tmp_script}"

ln -sf "${DOTNET_INSTALL_DIR}/dotnet" /usr/local/bin/dotnet

log "installed: $(dotnet --version)"
log "if 'dotnet --info' fails with a missing library error, consult"
log "https://learn.microsoft.com/dotnet/core/install/linux-debian for the"
log "exact runtime dependency package names for your Debian release."
log "03-install-dotnet-sdk.sh done."
