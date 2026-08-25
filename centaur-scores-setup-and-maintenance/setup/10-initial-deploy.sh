#!/bin/bash
# Builds and deploys the first release into the 'blue' slot, which is the
# default active slot with 0% candidate traffic - so blue immediately starts
# serving 100% of requests. This is just the first call to the same
# update/deploy.sh script used for every later update.
#
# Usage: setup/10-initial-deploy.sh [branch]   (default branch: main)
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"

branch="${1:-main}"
exec ../update/deploy.sh blue "${branch}"
