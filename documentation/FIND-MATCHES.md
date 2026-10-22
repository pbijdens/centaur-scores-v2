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

# Implementation plan

## Design decisions

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

## Step 1 — Backend: API name on categories (use case 2)
1. **Domain** — [Domain.cs](../centaur-scores-api-v2/CentaurScores.Api/Domain.cs): add `public string? ApiName { get; set; }` to `Category`.
2. **DbContext** — [ApplicationDbContext.cs](../centaur-scores-api-v2/CentaurScores.Api/ApplicationDbContext.cs): max length 64 for `ApiName` (column `api_name` via the snake_case naming convention).
3. **Migration** — generate `AddCategoryApiName` with `dotnet ef migrations add` (do not hand-write it, so `ApplicationDbContextModelSnapshot.cs` stays in sync; see MEMORY.md). Nullable column, no default needed.
4. **Contracts** — [Contracts.cs](../centaur-scores-api-v2/CentaurScores.Api/Contracts.cs): extend `UpdateCategoryRequest(string Name, string? ApiName = null)`. `CreateCategoryRequest` stays unchanged (the create form only asks for a name).
5. **Validation** — new small static helper `Application/CategoryApiNames.cs` with:
   - `ReservedNames` (`name`, `score`, `lidnummer`),
   - `Normalize(string?)` → trimmed value or `null`,
   - `Validate(string?)` → `null` or an `ApiError` (`CATEGORY_API_NAME_RESERVED`, `CATEGORY_API_NAME_INVALID`).
   The endpoint in step 2 reuses `ReservedNames`.
6. **Controller** — [CategoriesController.cs](../centaur-scores-api-v2/CentaurScores.Api/Api/Controllers/CategoriesController.cs) `Update`: normalize and validate the API name, check uniqueness within the tenant (excluding the category itself, case-insensitive) → `409 CATEGORY_API_NAME_TAKEN`, then store. `List` already returns the entity, so `apiName` is included automatically.
7. **Backup/restore** — [BackupModels.cs](../centaur-scores-api-v2/CentaurScores.Api/Application/BackupModels.cs): add `string? ApiName = null` as the last parameter of `CategoryBackup` (optional, so old backups still deserialize); export and import it in [CategoryBackupHandler.cs](../centaur-scores-api-v2/CentaurScores.Api/Application/CategoryBackupHandler.cs).
8. **Tests** — in a new `CategoriesControllerTests.cs`: update stores a trimmed API name; empty clears it; duplicate → 409; reserved name → 400; viewer → 403. Extend `BackupRestoreServiceTests` with an API-name round trip and a restore of a backup entry without `ApiName`.

## Step 2 — Backend: find endpoint (use case 1)
1. **Contracts** — add:
   - `FindMatchRequest(string? Date, string? Starttime)` — strings, so invalid input produces our own `ApiError` instead of a generic model-binding 400. No other properties are modeled; extra body properties are silently ignored by System.Text.Json.
   - `FoundMatch(string Description, string Date, string? ShortCode, IReadOnlyList<Dictionary<string, object?>> Results)`.
2. **Service** — new `Application/MatchFindService.cs` (`IMatchFindService`, registered in [Program.cs](../centaur-scores-api-v2/CentaurScores.Api/Program.cs)) holding all logic so it is unit-testable. It returns a small result type (found / not found / ambiguous with descriptions / invalid input with error code) that the controller maps to a status code:
   1. Parse `date` with `DateOnly.TryParseExact("yyyy-MM-dd")` and `starttime` with `TimeOnly.TryParseExact(["H:mm", "HH:mm"])`.
   2. Get authorized tenant ids/names via `IAuthService.GetAuthorizedTenantsAsync`.
   3. Query the candidates cheaply: `db.Matches.AsNoTracking()` filtered on `tenantIds.Contains(TenantId) && Date == date`, projecting only `Id`, `TenantId`, `Name` and `ShortCode` (no participants or scores yet). Open and closed matches are not filtered.
   4. Keep the candidates whose `ShortCode` parses to the same `TimeOnly`.
   5. None left → not found. More than one left → ambiguous, with the `"{tenantName} - {matchName}"` descriptions for the error message.
   6. Exactly one → load that match with `Participants → Scores` and `Participants → ParticipantListMember` (same includes as the live scoring page).
   7. Load that tenant's categories with values; build the ordered list of `(categoryId, propertyName, valueId → valueName)`, skipping reserved names and duplicate property names (first by category name wins).
   8. `scoring.Rank(participants, match)` for the order and totals; build each result dictionary with `name`, `score`, `lidnummer`, then the category properties from `participant.Categories`.
   9. Build `description` as `"{tenantName} - {matchName}"` and `date` as `yyyy-MM-dd`.
3. **Controller** — new `Api/Controllers/MatchFindController.cs`: `[ApiController]`, `[Authorize]`, `[Route("api/matches")]`, `[HttpPost("find")]`, deriving from `ControllerBase` (not `ApiControllerBase`). Reads the account id from `ITenantContext.AccountId`, delegates to the service and maps the outcome: found → `200` with the `FoundMatch`; not found → `404 MATCH_NOT_FOUND`; ambiguous → `409 MATCH_AMBIGUOUS`; invalid input → `400 INVALID_DATE` / `INVALID_STARTTIME`. Add a comment explaining why it bypasses the tenant guard.
4. **Tests** — `MatchFindServiceTests.cs`:
   - finds a match in the home tenant and in a child tenant; a match in an unrelated tenant is never found (→ 404);
   - works with a login token without a selected tenant (controller test with `TenantId = Guid.Empty`);
   - `9:30` short code matches `09:30`; non-time short code never matches; wrong date does not match;
   - open and closed matches are both found;
   - two matches with the same date and start time (same tenant, and across tenants) → 409 with both descriptions in the message, no results;
   - extra body properties (`verenigingsnummer`, `afstand`, `aantal pijlen`) do not cause an error and do not affect the result;
   - API name used when set, category name otherwise; value name exported; empty categories omitted;
   - reserved/duplicate property names skipped;
   - free participant → `lidnummer` null; participant without scores → score 0; ordering by score then name;
   - invalid date/time → 400 with the right code; no match → 404.
   - Extend `SecurityTests` with: anonymous call → 401.

## Step 3 — Web UI (use case 2)
Run npm commands via WSL (`wsl.exe bash -ic`).
1. [types.ts](../centaur-scores-web-ui/src/lib/types.ts): add `apiName?: string | null` to `Category`.
2. [api.ts](../centaur-scores-web-ui/src/lib/api.ts): `updateCategory(categoryId, name, apiName)` sends `{ name, apiName }`.
3. [CategoryDetailView.svelte](../centaur-scores-web-ui/src/lib/views/CategoryDetailView.svelte): add an **API name** input next to **Name** in the existing form; enable Save when either field changed; pass both to `updateCategory`.
4. [i18n.ts](../centaur-scores-web-ui/src/lib/i18n.ts): labels `categoryApiNameLabel` (+ a short hint "Used only by the external results export"), and error labels for `CATEGORY_API_NAME_TAKEN`, `CATEGORY_API_NAME_RESERVED`, `CATEGORY_API_NAME_INVALID` in every supported language, wired through `labelForError` in [errors.ts](../centaur-scores-web-ui/src/lib/errors.ts).
5. Verify with `npm run check`, `npm test` and `npm run build`, and manually in the running app.

## Step 4 — Documentation
- [API_ENDPOINTS.md](../centaur-scores-api-v2/API_ENDPOINTS.md): document `POST api/matches/find` (request, response, error codes, the fact that no tenant selection is needed) and the new `apiName` field on `PUT api/categories/{id}` with its error codes.
- API [CLAUDE.md](../centaur-scores-api-v2/CLAUDE.md) and [MEMORY.md](../centaur-scores-api-v2/MEMORY.md): note the tenant-less `MatchFindController` exception to the `ApiControllerBase` rule and the start-time normalization.
- [INDEX.md](INDEX.md): add a link to this document.

## Step 5 — Validation
1. `dotnet build` and `dotnet test CentaurScores.Api.Tests/CentaurScores.Api.Tests.csproj`.
2. `dotnet ef database update` on the dev database; check the `api_name` column.
3. End-to-end with the dev seed account: `POST api/auth/login` → `POST api/matches/find` with curl, for a match with an API-named category (200), a match in a child tenant (200), a closed match (200), a date/time with no match (404) and two matches sharing a date/time (409).
