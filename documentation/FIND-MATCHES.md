# Feature: Find match and get results


## Overview
The system will support the following flow:
- An external system authenticates (automatically) using the existing auth/login endpoint to obtain a valid Authorization token

- The external system uses an endpoint to send a JSON document with search parameters containing properties:
  - date (YYYY-MM-DD)
  - starttime (hh:mm, in 24h notation)
- Additional properties may be added, these are informative only and will probably not be used in searching for a match
  - verenigingsnummer (1231)
  - afstand (18 or 25)
  - aantal pijlen (30)

In return, the endpoint will first use these values to find the match. The date must match the date of the match, and the starttime must match the short name of the match (a value that we do not use otherwise anyway)
respond with a JSON array containing the participants and their total scores:
  - each object will have as properties all the properties that were categories that are configured for the archer with a non-empty value
    - categories for this purpose are extended with an "apiname" property; if that is specified that is the name that's used in this export
    - both the API+Database and the UI are updated to allow setting the apiname when configuring a category; other than in the catgeory configuration screen and this endpoint, that apiname is never used.
    - category values are exported by their display name (the category value's `Name`), not by their numeric value id
  - the total score for the archer for the match in a property called "score" [if there is a category with that name, do not return that category]

## Use case 1

As an external system I want to retrieve all the scores of a match and programatically process these. For this I first obtain a valid bearer token via api/auth/login 
that I then use as authorization token on a POST request to the endpoint "api/matches/find". In the body of the POST request I specify identifying information of the match 
I want the results for, specifically the JSON objects has properties:
  - date (YYYY-MM-DD)
  - starttime (hh:mm, in 24h notation)
This system will in response check for all tenants that the signed in account has access to all matches that have that date as their match date, and that have that start time (in string format) as their shortCode and, when exactly one match is found, will return a 200-result containing a single JSON object for that match (never more than one match; see the alternative flows for none or several):
- description - the tenant's name followed by " - " and the match's name
- date - the date in YYYY-MM-DD format
- shortCode - as is
- results an array of JSON objects:
  - name - the fullName of the participant
  - score - the total score of the participant in this match
  - lidnummer - the federationNumber of the participant
  - followed by for all categories either the configured API name of the category or the category's name (when no API name is configured) as property name, with the name of the participant's value for that category as property value

### Preconditions
- The external system has a username and password for an account in Centaur Scores. Any authorization profile (Viewer, Manager, Administrator) is sufficient; the endpoint is read-only.
- The token from `POST api/auth/login` is used as-is. The external system does **not** have to call `api/auth/select-tenant`: the endpoint searches every tenant the account is authorized for (its home tenant plus all descendant tenants, the same set returned by `GET api/auth/me`).

### Main flow
1. The external system calls `POST api/auth/login` with `{ "username": "...", "password": "..." }` and receives `{ token, expiresAt, account }`.
2. The external system calls `POST api/matches/find` with header `Authorization: Bearer <token>` and body:
   ```json
   { "date": "2026-10-22", "starttime": "19:30", "verenigingsnummer": 1231, "afstand": 18, "aantal pijlen": 30 }
   ```
3. The system validates `date` (must be a valid `YYYY-MM-DD` date) and `starttime` (must be a valid `hh:mm` time in 24h notation). Only `date` and `starttime` are part of the contract. Any other property in the body (such as `verenigingsnummer`, `afstand` or `aantal pijlen`) is not an error, but its value is not read, mapped or used.
4. The system determines the tenants the account is authorized for.
5. Within those tenants, the system selects every match whose `Date` equals `date` and whose `ShortCode` equals `starttime`. The comparison is done on the normalized time: a short code of `"9:30"` or `" 09:30 "` matches a `starttime` of `"09:30"`; short codes that are not a time never match. Open (active) and closed matches are treated the same way.
6. Exactly one match is found. The system computes, per participant, the total score (sum of all arrow values, identical to the total shown in the web UI and in the CSV export).
7. The system returns `200 OK` with a single JSON object for that match:
   ```json
   {
     "description": "Centaur - Wintercompetitie ronde 3",
     "date": "2026-10-22",
     "shortCode": "19:30",
     "results": [
       { "name": "Amy Archer", "score": 287, "lidnummer": "123456", "klasse": "Senioren", "discipline": "Recurve" },
       { "name": "Bob Bowman", "score": 251, "lidnummer": null, "discipline": "Compound" }
     ]
   }
   ```

### Rules for the `results` objects
- `name`, `score` and `lidnummer` are always present. `lidnummer` is `null` for a free participant without a federation number.
- `results` is ordered by score (highest first), then by name — the same ranking the web UI uses.
- Every participant of the match is returned, including participants without any scores (`score` is `0`) and free participants (not on a participant list).
- For each category of the match's tenant for which the participant has a value, one extra property is added:
  - property name: the category's API name when configured, otherwise the category's name; used exactly as configured (case and spaces are preserved).
  - property value: the name of the category value (e.g. `"Senioren"`), not its numeric id.
  - Categories for which the participant has no value, or whose value no longer exists, are omitted.
  - The participant's categories are the merged categories (participant-list values with per-match overrides), the same values shown in the web UI.
- A category whose resulting property name is `name`, `score` or `lidnummer` (case-insensitive) is not returned, so it can never overwrite one of the fixed properties.
- When two categories resolve to the same property name, only the first one (ordered by category name) is returned.

### Alternative flows
- **No match found**: `404 Not Found` with an `ApiError` (`MATCH_NOT_FOUND`).
- **More than one match found** (e.g. two tenants, or two matches in one tenant, have that date and start time): `409 Conflict` with an `ApiError` (`MATCH_AMBIGUOUS`); no results are returned. The `message` lists the descriptions (`tenant - match`) of the conflicting matches, so an administrator can fix the short codes. The endpoint never returns more than one match.
- **Invalid or missing `date` or `starttime`**: `400 Bad Request` with an `ApiError` (`INVALID_DATE` or `INVALID_STARTTIME`).
- **Missing, invalid or expired token**: `401 Unauthorized`.
- **Token that already has a tenant selected** (e.g. a token used by the web UI): accepted; the search still covers all tenants the account is authorized for, not only the selected one.

## Use case 2

As an administrator I want to configure the API name for a category. For this, from the main menu I select "Categories"; from the list I select my category where I am presented with an editor for "Name" and "API name", and below that with the values for the category. I update the API Name property and press the save button for the block I am editing. As of that moment the `api/matches/find` endpoint (use case 1) uses the API name as property name for that category in the `results` of every match of that tenant, including matches that were played before the change. Nowhere else in the application is the API name shown or used.

### Preconditions
- The user is signed in to the web UI with the Manager or Administrator profile for the tenant that owns the category (the same permission already required to rename a category).

### Main flow
1. The user opens **Categories** from the main menu and selects a category.
2. The category detail screen shows a single edit block with two fields: **Name** (required) and **API name** (optional), with one **Save** button. The values block below is unchanged.
3. The user enters or changes the API name and presses **Save**. The button is enabled as soon as either field differs from the stored value and the name is not empty.
4. The system trims the API name and stores it. An empty API name is stored as "not configured", after which the endpoint falls back to the category name.
5. The screen shows the existing "saved" confirmation.

### Alternative flows
- **API name already used by another category of the same tenant** (case-insensitive): the save is refused (`409 Conflict`, `ApiError` code `CATEGORY_API_NAME_TAKEN`) and the screen shows a translated error message; nothing is stored.
- **API name is a reserved property name** (`name`, `score`, `lidnummer`, case-insensitive): the save is refused (`400 Bad Request`, `ApiError` code `CATEGORY_API_NAME_RESERVED`) and the screen shows a translated error message.
- **API name longer than 64 characters**: refused as `400 Bad Request` (`CATEGORY_API_NAME_INVALID`).
- **Viewer profile**: the edit block is read-only (unchanged from today's behavior); the API refuses the update with `403`.

### Notes
- The API name is part of the category and is therefore included in backup and restore. Backups made before this feature restore with no API name configured.
- Existing categories start without an API name, so the endpoint initially uses the category names.

---

# Design decisions

| Topic | Decision | Reason |
|---|---|---|
| Authentication | New endpoint lives in its own controller that does **not** derive from `ApiControllerBase`, but is `[Authorize]`. | `ApiControllerBase` rejects tokens without a selected tenant (`TENANT_NOT_SELECTED`), and the login token has `tenant_id = Guid.Empty`. Use case 1 requires the plain login token to work. |
| Tenant scope | `IAuthService.GetAuthorizedTenantsAsync(accountId)` | Already resolves home tenant + descendants; same rule as the tenant selector, so no new authorization logic. |
| Route | `POST api/matches/find` in a separate `MatchFindController` | Keeps the route the spec asks for while keeping the tenant-less code path out of `MatchesController` (where every other action relies on the selected tenant). ASP.NET resolves the literal `find` segment without conflicts since `MatchesController` only has `{id:guid}` routes. |
| Response shape | Exactly one match object; `404 MATCH_NOT_FOUND` for none, `409 MATCH_AMBIGUOUS` for more than one. `results` entries are `Dictionary<string, object?>` | The external system needs one unambiguous match. Category properties are dynamic, and dictionary keys are not affected by the camelCase naming policy, so API names come out exactly as configured. |
| Request contract | Only `date` and `starttime` | Other body properties are ignored by System.Text.Json and are not modeled, mapped or used. |
| Open/closed matches | Treated the same | Results must be retrievable both during and after a match. |
| Start time matching | Parse both `starttime` and `ShortCode` with `TimeOnly.TryParseExact` (`H:mm`, `HH:mm`), compare the values | Tolerates `9:30` vs `09:30` and stray whitespace; the short code is free text in the UI. Filtering on date happens in SQL, the time comparison in memory (small set). |
| Score | `IScoringService.Calculate(...).Total` and the existing `Rank` ordering | Single source of truth for totals, consistent with web UI and CSV export. |
| Storage | Nullable column `api_name` (varchar 64) on `categories` | Optional; the snake_case naming convention produces `api_name`. |
