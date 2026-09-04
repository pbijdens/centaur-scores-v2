#!/usr/bin/env bash
#
# Builds an uploadable WordPress plugin ZIP for the centaur-scores plugin.
#
# Usage:
#   ./build.sh              # reads the version already in centaur-scores.php, builds it as-is
#   ./build.sh 1.2.0         # bumps the plugin's version to 1.2.0 (header, version constant,
#                             # readme.txt Stable tag) in the source tree, then builds that
#
# The ZIP is written to dist/centaur-scores-<version>.zip, with the plugin
# folder ("centaur-scores/") at the root of the archive - ready to upload via
# Plugins > Add New > Upload Plugin, or to unzip into wp-content/plugins/.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PLUGIN_SLUG="centaur-scores"
SRC_DIR="${SCRIPT_DIR}/${PLUGIN_SLUG}"
DIST_DIR="${SCRIPT_DIR}/dist"

if [ ! -d "${SRC_DIR}" ]; then
	echo "error: plugin source directory not found at ${SRC_DIR}" >&2
	exit 1
fi

MAIN_FILE="${SRC_DIR}/${PLUGIN_SLUG}.php"
README_FILE="${SRC_DIR}/readme.txt"

if [ "$#" -ge 1 ]; then
	VERSION="$1"
	if ! [[ "${VERSION}" =~ ^[0-9]+(\.[0-9]+)*([A-Za-z0-9.\-]*)$ ]]; then
		echo "error: '${VERSION}' doesn't look like a version number" >&2
		exit 1
	fi

	echo "Bumping version to ${VERSION} in ${MAIN_FILE} and ${README_FILE}..."
	sed -i -E "s/^(\s*\*\s*Version:\s*).*/\1${VERSION}/" "${MAIN_FILE}"
	sed -i -E "s/^(define\( 'CENTAUR_SCORES_VERSION', ')[^']*('\s*\);)/\1${VERSION}\2/" "${MAIN_FILE}"
	sed -i -E "s/^(Stable tag:\s*).*/\1${VERSION}/" "${README_FILE}"
else
	VERSION="$(grep -m1 -oP '^\s*\*\s*Version:\s*\K[0-9A-Za-z.\-]+' "${MAIN_FILE}" || true)"
	if [ -z "${VERSION}" ]; then
		echo "error: could not read Version from ${MAIN_FILE}; pass a version explicitly: ./build.sh 1.0.0" >&2
		exit 1
	fi
fi

echo "Building ${PLUGIN_SLUG} ${VERSION}..."

# Sanity-check PHP syntax before packaging anything.
if command -v php >/dev/null 2>&1; then
	while IFS= read -r -d '' php_file; do
		php -l "${php_file}" >/dev/null
	done < <(find "${SRC_DIR}" -name '*.php' -print0)
	echo "PHP syntax OK."
fi

mkdir -p "${DIST_DIR}"
ZIP_PATH="${DIST_DIR}/${PLUGIN_SLUG}-${VERSION}.zip"
rm -f "${ZIP_PATH}"

WORK_DIR="$(mktemp -d)"
trap 'rm -rf "${WORK_DIR}"' EXIT

# Copy only what ships to users - skip dev/editor cruft even if it ends up
# inside the plugin folder later (.DS_Store, editor swap files, etc).
rsync -a \
	--exclude '.DS_Store' \
	--exclude '*.swp' \
	--exclude '.git' \
	--exclude '.gitignore' \
	"${SRC_DIR}/" "${WORK_DIR}/${PLUGIN_SLUG}/"

( cd "${WORK_DIR}" && zip -rq "${ZIP_PATH}" "${PLUGIN_SLUG}" )

echo "Built: ${ZIP_PATH}"
