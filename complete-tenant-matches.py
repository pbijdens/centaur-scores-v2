#!/usr/bin/env python3
"""Ensure every match in a tenant has 30-40 archers and a fully entered scorecard.

For each match with a linked participant list, roughly 15 "regulars" are shared
across every match that uses that list, and the remaining archers are drawn from
a fixed pool of up to 40 members of that list, so matches on the same list have
realistic archer overlap. Every participant then gets scores filled in for every
end/arrow they are still missing.

Usage: complete-tenant-matches.py <tenant-guid> <username> <password>
"""
import argparse
import json
import random
import sys
import urllib.error
import urllib.request

CORE_SIZE = 15
POOL_SIZE = 40
MIN_ARCHERS = 30
MAX_ARCHERS = 40

# Used when a match's own keyboard configuration is empty.
DEFAULT_KEYBOARD = [
    {"keyId": "X", "value": 10},
    {"keyId": "10", "value": 10},
    {"keyId": "9", "value": 9},
    {"keyId": "8", "value": 8},
    {"keyId": "7", "value": 7},
    {"keyId": "6", "value": 6},
    {"keyId": "5", "value": 5},
    {"keyId": "4", "value": 4},
    {"keyId": "3", "value": 3},
    {"keyId": "2", "value": 2},
    {"keyId": "1", "value": 1},
    {"keyId": "M", "value": 0},
]


def api_request(api_base_url, token, method, path, body=None, ignore_statuses=()):
    data = json.dumps(body).encode() if body is not None else None
    request = urllib.request.Request(f"{api_base_url}{path}", data=data, method=method)
    if token:
        request.add_header("Authorization", f"Bearer {token}")
    if data is not None:
        request.add_header("Content-Type", "application/json")
    try:
        with urllib.request.urlopen(request) as response:
            payload = response.read()
            return json.loads(payload) if payload else None
    except urllib.error.HTTPError as error:
        if error.code in ignore_statuses:
            return None
        detail = error.read().decode(errors="replace")
        print(f"API request failed: {method} {path} (HTTP {error.code})\n{detail}", file=sys.stderr)
        raise SystemExit(1)


def login(api_base_url, username, password, tenant_id):
    result = api_request(api_base_url, None, "POST", "/api/auth/login", {"username": username, "password": password, "tenantId": tenant_id})
    return result["token"]


def parse_keyboard(keyboard_json):
    try:
        keyboard = json.loads(keyboard_json or "{}").get("keyboard") or []
    except json.JSONDecodeError:
        keyboard = []
    return keyboard if keyboard else DEFAULT_KEYBOARD


def weighted_key(keyboard, rng):
    weights = [max(int(key.get("value", 0)) + 1, 1) for key in keyboard]
    return rng.choices(keyboard, weights=weights, k=1)[0]


def build_pools(members_by_list_id, tenant_id):
    pools = {}
    for list_id, members in members_by_list_id.items():
        active = [member for member in members if member.get("isActive", True)]
        rng = random.Random(f"{tenant_id}:{list_id}:pool")
        rng.shuffle(active)
        core_size = min(CORE_SIZE, len(active))
        pool_size = max(min(POOL_SIZE, len(active)), core_size)
        pools[list_id] = {"core": active[:core_size], "pool": active[:pool_size], "total": len(active)}
    return pools


def select_archers(pool_info, rng):
    core = pool_info["core"]
    target = min(rng.randint(MIN_ARCHERS, MAX_ARCHERS), len(pool_info["pool"]))
    core_ids = {member["id"] for member in core}
    remaining = [member for member in pool_info["pool"] if member["id"] not in core_ids]
    rng.shuffle(remaining)
    return core + remaining[: max(target - len(core), 0)]


def ensure_participants(api_base_url, token, match, archers):
    existing = api_request(api_base_url, token, "GET", f"/api/matches/{match['id']}/participants")
    existing_by_member_id = {p["participantListMemberId"]: p for p in existing if p.get("participantListMemberId")}
    added = 0
    for member in archers:
        if member["id"] in existing_by_member_id:
            continue
        created = api_request(
            api_base_url, token, "POST", f"/api/matches/{match['id']}/participants",
            {
                "participantListMemberId": member["id"],
                "lastName": member["lastName"],
                "fullName": member["fullName"],
                "federationNumber": member.get("federationNumber"),
                "categories": member.get("categories", {}),
            },
            ignore_statuses=(409,),
        )
        if created:
            existing.append(created)
            existing_by_member_id[member["id"]] = created
            added += 1
    return existing, added


def complete_scores(api_base_url, token, match, participants, rng):
    ends = match["ends"]
    arrows_per_end = match["arrowsPerEnd"]
    expected_arrows = ends * arrows_per_end
    keyboard = parse_keyboard(match.get("keyboardJson"))
    scored = 0
    for participant in participants:
        current = participant.get("scores") or []
        if len(current) >= expected_arrows:
            continue
        entered = {(score["end"], score["arrow"]) for score in current}
        for end in range(1, ends + 1):
            for arrow in range(1, arrows_per_end + 1):
                if (end, arrow) in entered:
                    continue
                key = weighted_key(keyboard, rng)
                api_request(
                    api_base_url, token, "POST", f"/api/matches/{match['id']}/participants/{participant['id']}/scores",
                    {"end": end, "arrow": arrow, "keyId": key["keyId"], "value": key["value"]},
                )
                scored += 1
    return scored


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("tenant_id", help="Tenant GUID")
    parser.add_argument("username", help="Account username")
    parser.add_argument("password", help="Account password")
    parser.add_argument("--api-base-url", default="http://localhost:5080", help="API base URL (default: %(default)s)")
    args = parser.parse_args()

    token = login(args.api_base_url, args.username, args.password, args.tenant_id)
    matches = api_request(args.api_base_url, token, "GET", "/api/matches")
    lists = api_request(args.api_base_url, token, "GET", "/api/participant-lists?includeInactive=true")
    lists_by_id = {item["id"]: item for item in lists}

    eligible_matches = []
    for match in matches:
        list_id = match.get("participantListId")
        if not list_id or list_id not in lists_by_id:
            print(f"Skipping '{match['name']}': no linked participant list.")
            continue
        eligible_matches.append(match)

    list_ids_in_use = {match["participantListId"] for match in eligible_matches}
    pools = build_pools({list_id: lists_by_id[list_id]["members"] for list_id in list_ids_in_use}, args.tenant_id)

    for list_id, pool_info in pools.items():
        if pool_info["total"] < CORE_SIZE:
            print(f"Warning: list '{lists_by_id[list_id]['name']}' only has {pool_info['total']} active members; using all of them as regulars.")

    for match in eligible_matches:
        pool_info = pools[match["participantListId"]]
        rng = random.Random(f"{args.tenant_id}:{match['id']}")
        archers = select_archers(pool_info, rng)
        participants, added = ensure_participants(args.api_base_url, token, match, archers)
        scored = complete_scores(args.api_base_url, token, match, participants, rng)
        print(f"{match['name']}: {len(participants)} archers ({added} newly added), {scored} scores entered.")

    print("Done.")


if __name__ == "__main__":
    main()
