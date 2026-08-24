#!/usr/bin/env bash

set -Eeuo pipefail

readonly API_BASE_URL="http://localhost:5080"
readonly PARTICIPANT_COUNT=100

usage() {
    echo "Usage: $0 <username> <password> <tenant-guid> <participant-list-guid>" >&2
}

require_command() {
    if ! command -v "$1" >/dev/null 2>&1; then
        echo "Required command not found: $1" >&2
        exit 1
    fi
}

api_request() {
    local method=$1
    local path=$2
    local body=${3-}
    local response_file
    local status

    response_file=$(mktemp)
    if [[ -n "$body" ]]; then
        status=$(curl --silent --show-error --output "$response_file" --write-out '%{http_code}' \
            --request "$method" \
            --header 'Content-Type: application/json' \
            --header "Authorization: Bearer $token" \
            --data "$body" \
            "${API_BASE_URL}${path}")
    else
        status=$(curl --silent --show-error --output "$response_file" --write-out '%{http_code}' \
            --request "$method" \
            --header "Authorization: Bearer $token" \
            "${API_BASE_URL}${path}")
    fi

    if [[ ! "$status" =~ ^2 ]]; then
        echo "API request failed: $method $path (HTTP $status)" >&2
        cat "$response_file" >&2
        echo >&2
        rm -f "$response_file"
        exit 1
    fi

    cat "$response_file"
    rm -f "$response_file"
}

if [[ $# -ne 4 ]]; then
    usage
    exit 2
fi

require_command curl
require_command jq

readonly username=$1
readonly password=$2
readonly tenant_id=$3
readonly participant_list_id=$4

login_body=$(jq -n \
    --arg username "$username" \
    --arg password "$password" \
    --arg tenantId "$tenant_id" \
    '{ username: $username, password: $password, tenantId: $tenantId }')

login_response_file=$(mktemp)
trap 'rm -f "${login_response_file:-}"' EXIT
login_status=$(curl --silent --show-error --output "$login_response_file" --write-out '%{http_code}' \
    --request POST \
    --header 'Content-Type: application/json' \
    --data "$login_body" \
    "${API_BASE_URL}/api/auth/login")

if [[ ! "$login_status" =~ ^2 ]]; then
    echo "Login failed (HTTP $login_status)" >&2
    cat "$login_response_file" >&2
    echo >&2
    exit 1
fi

token=$(jq -er '.token' "$login_response_file")
categories=$(api_request GET '/api/categories')

if ! jq -e 'type == "array" and length > 0' <<<"$categories" >/dev/null; then
    echo "The tenant has no categories; no participants were added." >&2
    exit 1
fi

empty_categories=$(jq -r '.[] | select((.values | length) == 0) | .name' <<<"$categories")
if [[ -n "$empty_categories" ]]; then
    echo "Every category must have at least one value. Empty categories:" >&2
    echo "$empty_categories" >&2
    exit 1
fi

echo "Adding $PARTICIPANT_COUNT participants to list $participant_list_id..."

for ((participant_number = 1; participant_number <= PARTICIPANT_COUNT; participant_number++)); do
    category_selection='{}'
    while IFS=$'\t' read -r category_id value_ids_json; do
        value_count=$(jq 'length' <<<"$value_ids_json")
        random_index=$((RANDOM % value_count))
        value_id=$(jq -r ".[$random_index]" <<<"$value_ids_json")
        category_selection=$(jq \
            --arg categoryId "$category_id" \
            --argjson valueId "$value_id" \
            '. + { ($categoryId): $valueId }' <<<"$category_selection")
    done < <(jq -r '.[] | [.id, ([.values[].valueId] | tojson)] | @tsv' <<<"$categories")

    participant_name=$(printf 'Test Participant %03d' "$participant_number")
    federation_number=$(printf 'TEST-%03d' "$participant_number")
    participant_body=$(jq -n \
        --arg lastName "$participant_name" \
        --arg fullName "$participant_name" \
        --arg federationNumber "$federation_number" \
        --argjson categories "$category_selection" \
        '{ lastName: $lastName, fullName: $fullName, federationNumber: $federationNumber, categories: $categories, isActive: true }')

    api_request POST "/api/participant-lists/${participant_list_id}/members" "$participant_body" >/dev/null
    printf '\rAdded %d/%d participants' "$participant_number" "$PARTICIPANT_COUNT"
done

echo
echo "Done. Added $PARTICIPANT_COUNT participants."