#!/bin/bash
# Shared functions for the centaur-scores-setup-and-maintenance scripts.
# Source this file, do not execute it directly.

set -euo pipefail

# Absolute path to the centaur-scores-setup-and-maintenance checkout, regardless
# of where the sourcing script was invoked from.
CS_ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CS_TEMPLATES_DIR="${CS_ROOT_DIR}/templates"

log()  { echo "[centaur-scores] $*"; }
warn() { echo "[centaur-scores] WARNING: $*" >&2; }
die()  { echo "[centaur-scores] ERROR: $*" >&2; exit 1; }

require_root() {
    if [[ "${EUID}" -ne 0 ]]; then
        die "this script must be run as root (e.g. with sudo)."
    fi
}

confirm() {
    # confirm "question text" - returns 0 if the operator typed 'yes'
    local prompt="$1"
    local reply
    read -r -p "${prompt} [type 'yes' to continue] " reply
    [[ "${reply}" == "yes" ]]
}

# Loads config.env, exports every variable in it, validates the required
# ones are non-empty, and derives a couple of computed values used throughout
# the scripts.
load_config() {
    local config_file="${CS_ROOT_DIR}/config.env"
    [[ -f "${config_file}" ]] || die "missing ${config_file} - copy config.env.example to config.env and edit it first."

    set -a
    # shellcheck disable=SC1090
    source "${config_file}"
    set +a

    local required=(
        PUBLIC_HOSTNAME LETSENCRYPT_EMAIL PUBLIC_API_VDIR PUBLIC_APP_VDIR
        GITHUB_REPO_URL CENTAUR_BASE_DIR CENTAUR_SERVICE_USER CENTAUR_SERVICE_GROUP
        API_BLUE_PORT API_GREEN_PORT RELEASES_TO_KEEP
        MYSQL_DATABASENAME MYSQL_USERNAME_APP MYSQL_PASSWORD_APP JWT_SECRET
        CENTAUR_LOG_DIR LOG_MAX_FILE_SIZE LOG_RETENTION_DAYS LOG_TOTAL_BUDGET_BYTES
    )
    local var
    for var in "${required[@]}"; do
        [[ -n "${!var:-}" ]] || die "config.env is missing a value for ${var}."
    done

    case "${PUBLIC_API_VDIR}" in /*/) ;; *) die "PUBLIC_API_VDIR must start and end with '/' (got '${PUBLIC_API_VDIR}')." ;; esac
    case "${PUBLIC_APP_VDIR}" in /*/) ;; *) die "PUBLIC_APP_VDIR must start and end with '/' (got '${PUBLIC_APP_VDIR}')." ;; esac

    # Public API URL baked into the web-ui build at compile time. Derived, not
    # user-configurable, so both slots always agree on the value.
    PUBLIC_API_URL="https://${PUBLIC_HOSTNAME}${PUBLIC_API_VDIR%/}"
    export PUBLIC_API_URL

    STATE_DIR="${CENTAUR_BASE_DIR}/state"
    RELEASES_DIR="${CENTAUR_BASE_DIR}/releases"
    BUILD_DIR="${CENTAUR_BASE_DIR}/build"
    export STATE_DIR RELEASES_DIR BUILD_DIR
}

other_slot() {
    case "$1" in
        blue) echo green ;;
        green) echo blue ;;
        *) die "unknown slot '$1' (expected 'blue' or 'green')." ;;
    esac
}

validate_slot() {
    case "$1" in
        blue|green) ;;
        *) die "unknown slot '$1' (expected 'blue' or 'green')." ;;
    esac
}

slot_port() {
    case "$1" in
        blue) echo "${API_BLUE_PORT}" ;;
        green) echo "${API_GREEN_PORT}" ;;
        *) die "unknown slot '$1'." ;;
    esac
}

get_active_slot() {
    local f="${STATE_DIR}/active-slot"
    [[ -f "${f}" ]] || die "no active slot recorded yet at ${f} - run setup/08-render-service-configs.sh first."
    cat "${f}"
}

set_active_slot() {
    validate_slot "$1"
    echo -n "$1" > "${STATE_DIR}/active-slot"
}

get_candidate_weight() {
    local f="${STATE_DIR}/candidate-weight"
    [[ -f "${f}" ]] && cat "${f}" || echo 0
}

set_candidate_weight() {
    local weight="$1"
    [[ "${weight}" =~ ^[0-9]+$ ]] && (( weight >= 0 && weight <= 100 )) || die "weight must be an integer between 0 and 100 (got '${weight}')."
    echo -n "${weight}" > "${STATE_DIR}/candidate-weight"
}

# render_template TEMPLATE_FILE OUTPUT_FILE VAR1 [VAR2 ...]
# envsubst is given an explicit list of ${VAR} names to substitute so that
# nginx's own $variables (e.g. $remote_addr, $host) in the template are left
# untouched.
render_template() {
    local template="$1" output="$2"
    shift 2
    local varlist=""
    local v
    for v in "$@"; do varlist="${varlist}\${${v}} "; done
    mkdir -p "$(dirname "${output}")"
    envsubst "${varlist}" < "${template}" > "${output}.tmp"
    mv -f "${output}.tmp" "${output}"
    log "rendered ${output}"
}

render_nginx_upstreams() {
    local active candidate weight rules
    active="$(get_active_slot)"
    candidate="$(other_slot "${active}")"
    weight="$(get_candidate_weight)"

    # nginx's split_clients rejects a literal "0%" entry, so 0 and 100 are
    # special-cased to a single catch-all "*" rule instead of a percentage.
    if [[ "${weight}" -eq 0 ]]; then
        rules="    *    ${active};"
    elif [[ "${weight}" -eq 100 ]]; then
        rules="    *    ${candidate};"
    else
        rules="    ${weight}%    ${candidate};
    *    ${active};"
    fi

    SPLIT_CLIENTS_RULES="${rules}" \
        render_template "${CS_TEMPLATES_DIR}/nginx-upstreams.conf.tmpl" \
        "/etc/nginx/conf.d/centaur-scores-upstreams.conf" \
        API_BLUE_PORT API_GREEN_PORT SPLIT_CLIENTS_RULES CENTAUR_BASE_DIR
}

nginx_test_and_reload() {
    nginx -t
    systemctl reload nginx
}

# atomic_symlink TARGET_DIR LINK_PATH
# Points LINK_PATH at TARGET_DIR using a rename() so nginx / systemd never see
# a moment where the symlink is missing.
atomic_symlink() {
    local target="$1" link="$2"
    local tmp="${link}.new.$$"
    ln -sfn "${target}" "${tmp}"
    # mv -T refuses to overwrite an existing plain directory with a symlink.
    # That should never be the steady-state case (link is always a symlink
    # once a slot has been deployed at least once) but clear it defensively
    # if it somehow is one, so this self-heals instead of failing.
    if [[ -e "${link}" && ! -L "${link}" ]]; then
        rm -rf "${link}"
    fi
    mv -Tf "${tmp}" "${link}"
}

deployed_version_file() {
    echo "${STATE_DIR}/deployed-$1.txt"
}
