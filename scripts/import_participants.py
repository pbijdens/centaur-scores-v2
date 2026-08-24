#!/usr/bin/env python3
"""Import participants from a local CSV file into an existing participant list."""

import argparse
import csv
import io
import json
import os
from pathlib import Path
import sys
from collections.abc import Iterator
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen

DEFAULT_API_URL = "http://127.0.0.1:5080"
REQUIRED_HEADERS = ("Achternaam", "Voornaam", "Bondsnummer")


def request_json(api_url: str, path: str, method: str = "GET", token: str | None = None, body: object | None = None) -> object:
    data = json.dumps(body).encode("utf-8") if body is not None else None
    headers = {"Accept": "application/json"}
    if data is not None:
        headers["Content-Type"] = "application/json"
    if token:
        headers["Authorization"] = f"Bearer {token}"
    request = Request(f"{api_url.rstrip('/')}{path}", data=data, headers=headers, method=method)
    try:
        with urlopen(request) as response:
            response_body = response.read()
    except HTTPError as error:
        detail = error.read().decode("utf-8", errors="replace").strip()
        raise RuntimeError(f"{method} {path} failed with HTTP {error.code}: {detail}") from error
    except URLError as error:
        raise RuntimeError(f"Could not reach API at {api_url}: {error.reason}") from error
    if not response_body:
        return None
    return json.loads(response_body)


def read_csv(path: str) -> str:
    try:
        return Path(path).read_text(encoding="utf-8-sig")
    except OSError as error:
        raise RuntimeError(f"Could not read CSV file {path}: {error.strerror}") from error


def rows_from_csv(csv_text: str) -> Iterator[dict[str, str]]:
    reader = csv.DictReader(io.StringIO(csv_text))
    headers = reader.fieldnames or []
    missing = [header for header in REQUIRED_HEADERS if header not in headers]
    if missing:
        raise ValueError(f"CSV is missing required column(s): {', '.join(missing)}")
    yield from reader


def category_lookup(categories: list[dict[str, object]]) -> dict[str, tuple[str, dict[str, int]]]:
    lookup: dict[str, tuple[str, dict[str, int]]] = {}
    for category in categories:
        category_id = category.get("id")
        category_name = category.get("name")
        if not isinstance(category_id, str) or not isinstance(category_name, str):
            continue
        values = {
            value["name"]: value["valueId"]
            for value in category.get("values", [])
            if isinstance(value, dict)
            and isinstance(value.get("name"), str)
            and isinstance(value.get("valueId"), int)
        }
        lookup[category_name] = (category_id, values)
    return lookup


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("tenant", help="Tenant GUID used during login")
    parser.add_argument("participant_list", help="Participant list GUID")
    parser.add_argument("username", help="Account username")
    parser.add_argument("password", help="Account password")
    parser.add_argument("csv_file", help="Local path to the CSV file")
    parser.add_argument(
        "--api-url",
        default=os.environ.get("CENTAUR_SCORES_API_URL", DEFAULT_API_URL),
        help=f"API base URL (default: $CENTAUR_SCORES_API_URL or {DEFAULT_API_URL})",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    token_response = request_json(
        args.api_url,
        "/api/auth/login",
        method="POST",
        body={"username": args.username, "password": args.password, "tenantId": args.tenant},
    )
    if not isinstance(token_response, dict) or not isinstance(token_response.get("token"), str):
        raise RuntimeError("Login response did not contain a token")
    token = token_response["token"]

    categories_response = request_json(args.api_url, "/api/categories", token=token)
    lists_response = request_json(args.api_url, "/api/participant-lists?includeInactive=true", token=token)
    if not isinstance(categories_response, list) or not isinstance(lists_response, list):
        raise RuntimeError("API returned an unexpected response for categories or participant lists")

    participant_list = next((item for item in lists_response if isinstance(item, dict) and item.get("id") == args.participant_list), None)
    if participant_list is None:
        raise RuntimeError(f"Participant list {args.participant_list} was not found for this tenant")

    existing_numbers = {
        member.get("federationNumber").strip()
        for member in participant_list.get("members", [])
        if isinstance(member, dict) and isinstance(member.get("federationNumber"), str) and member["federationNumber"].strip()
    }
    categories = category_lookup(categories_response)
    csv_text = read_csv(args.csv_file)

    added = 0
    skipped = 0
    row_number = 1
    for row_number, row in enumerate(rows_from_csv(csv_text), start=2):
        last_name = row.get("Achternaam", "").strip()
        first_name = row.get("Voornaam", "").strip()
        federation_number = row.get("Bondsnummer", "").strip()
        if not last_name or not first_name:
            raise ValueError(f"CSV row {row_number} has an empty Voornaam or Achternaam")
        if federation_number and federation_number in existing_numbers:
            skipped += 1
            continue

        category_values: dict[str, int] = {}
        for header, value in row.items():
            if header not in categories or not value or value not in categories[header][1]:
                continue
            category_id, values = categories[header]
            category_values[category_id] = values[value]

        request_json(
            args.api_url,
            f"/api/participant-lists/{args.participant_list}/members",
            method="POST",
            token=token,
            body={
                "lastName": last_name,
                "fullName": f"{first_name} {last_name}",
                "federationNumber": federation_number or None,
                "categories": category_values,
                "isActive": True,
            },
        )
        if federation_number:
            existing_numbers.add(federation_number)
        added += 1

    print(f"Imported {added} participant(s); skipped {skipped} existing federation number(s).")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (RuntimeError, ValueError) as error:
        print(f"Error: {error}", file=sys.stderr)
        raise SystemExit(1)
