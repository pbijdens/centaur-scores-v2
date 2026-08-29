# Backup and restore feature

Lets a tenant administrator export a tenant (optionally with its whole sub-tenant tree) to a downloadable ZIP file, and restore such a ZIP back in as a brand-new sub-tenant. Restore is strictly additive: it can never overwrite, modify, or link to data that already existed before the restore.

## Where to find it

On the home screen, administrators see a "Backup and restore" tile (alongside the other admin-only tiles: Accounts, Sub-tenants) that opens `/backup-restore` (`BackupRestoreView.svelte`). Both actions require Administrator authorization on the tenant you're currently acting as.

## Creating a backup

1. Open "Backup and restore".
2. Optionally tick "Include sub-tenants" — this walks the entire sub-tenant tree under the current tenant (recursively) and includes every tenant found.
3. Click "Create backup". The server builds the ZIP synchronously, in memory, in a single request, and the browser downloads it as `centaur-backup-{sanitized-tenant-name}-{yyyyMMdd-HHmmss}.zip`.

There's no progress indicator, chunking, or background job — the whole export happens in one request/response. Fine for the data volumes seen so far; a very large tenant (many matches/scores) could make this slow or memory-heavy since the entire ZIP is assembled in a `MemoryStream` before being returned.

## What's inside the ZIP

- `index.json` — export metadata: format version (currently `1`), export timestamp (UTC), the root tenant's id, the full list of included tenant ids, whether sub-tenants were included, how many personal-best discipline mappings were dropped because they pointed outside the exported tenant set, and the API's assembly version that produced the file.
- One folder per entity family, one JSON file per record (file name is the record's original database GUID):
  - `tenants/{guid}.json` — tenant metadata for every tenant in the export (parent/child relationships included)
  - `accounts/{guid}.json` — one file per account; includes the password **hash**, never the plaintext password
  - `categories/{guid}.json` — one file per category, with its values nested inside
  - `participant-lists/{guid}.json` — one file per participant list, with all its members nested inside
  - `templates/{guid}.json` — one file per match template
  - `matches/{guid}.json` — one file per match, with its participants, their arrow scores, score devices, and live-score scopes all nested inside
  - `competitions/{guid}.json` — one file per competition, with its rounds (including round → match links) and scoring rules nested inside
  - `personal-best-info/{tenant-guid}.json` — one file **per tenant** (not per record), bundling that tenant's personal-best classifiers, disciplines (with their category mappings), export config, import config, cached archer names, and the full personal-best log. Transient state (in-progress import batches/conflicts) is deliberately excluded — it's operational state, not durable data.

This list will grow as the system grows; treat it as a snapshot of the current format, not a frozen contract. The format is versioned via `index.json.formatVersion` and is not guaranteed to stay backward/forward compatible — a backup made by a newer server version may be rejected by an older one.

## Restoring a backup

1. Open "Backup and restore", click "Restore backup".
2. Confirm the warning dialog ("This creates a new sub-tenant with a full copy of the data in this backup. Continue?").
3. Pick a `.zip` file (server-enforced upload limit: 200 MB).
4. The server validates the file (see "Failure modes" below) and, if valid, imports it inside a single database transaction — either everything commits, or nothing does.
5. On success the page shows "Restored into new sub-tenant "&lt;name&gt;"", plus any warnings collected along the way.

### What actually happens on restore

- A brand-new sub-tenant is created **under whichever tenant the restoring administrator is currently logged into** — not necessarily back under the same parent it had in the source system. Its name is the original tenant's name with a `" (restored yyyy-MM-dd HH:mm)"` suffix.
- If the backup includes multiple tenants (sub-tenants were included at export time), the whole tree is recreated with the same parent/child structure, rooted under the admin's current tenant.
- **Every object gets a brand-new database id.** A single old-id → new-id map spans the whole restore run (every entity type, including tenants themselves), and every reference inside the backup is rewritten through that map before being saved.
- Restored accounts keep their original password hash — existing passwords keep working — but every username is prefixed with a random per-restore prefix (`r-xxxxxx-`, six random lowercase letters/digits) so a restored username can never collide with an existing one.
- The import never creates, modifies, or points at anything that already existed in the system before the restore. Every reference is either resolved to a freshly minted id from within the same ZIP, or dropped.
- If a reference was legitimately out of scope for this particular backup — e.g. a personal-best discipline mapping pointing at a category owned by a tenant that wasn't included because "Include sub-tenants" was left unchecked, or a competition round linking to a match that fell outside the exported set — only that one reference is dropped, not the whole restore. A human-readable warning is recorded and surfaced in the success result rather than failing the import outright.

### Failure modes

A restore is rejected before anything is written (no partial import) when:

| Code | Cause |
|---|---|
| `RESTORE_FILE_MISSING` | No file was uploaded |
| `RESTORE_INVALID_FILE` | The upload isn't a valid ZIP archive |
| `RESTORE_MISSING_INDEX` | The ZIP has no `index.json` |
| `RESTORE_INVALID_INDEX` | `index.json` couldn't be parsed |
| `RESTORE_UNSUPPORTED_VERSION` | `index.json`'s format version doesn't match what this server supports |
| `RESTORE_INVALID_ROOT_TENANT` | The backup's declared root tenant couldn't be resolved during import |

## What administrators should keep in mind

- **Restore is additive only.** Restoring the same backup any number of times just keeps creating new sub-tenants — it never overwrites or de-duplicates against existing data.
- **The new sub-tenant lands under whoever is running the restore**, not automatically back under its original parent tenant — if hierarchy matters, restore from the account/tenant you actually want as the new parent.
- **Restored accounts are unusable under their original username** until someone looks up the generated prefix (visible in the tenant's account list) and tells the affected user their new username, or an admin renames/resets the account from the Accounts screen. Passwords are untouched by the restore.
- **Backups can get large and slow to produce** for tenants with a lot of match/score history, since export is synchronous and fully in-memory with no progress feedback.
- **Upload is capped at 200 MB** for restore; larger files are rejected outright before any processing.
- **Warnings after a successful restore are informational, not blockers** — they call out data that was legitimately dropped (out-of-scope references), not a failed restore.

## Where the code lives

- Backend: `CentaurScores.Api/Application/BackupService.cs`, `RestoreService.cs`, `BackupHandlers.cs`, `BackupModels.cs`, and one `*BackupHandler.cs` per entity family (`TenantBackupHandler`, `AccountBackupHandler`, `CategoryBackupHandler`, `ParticipantListBackupHandler`, `MatchTemplateBackupHandler`, `MatchBackupHandler`, `CompetitionBackupHandler`, `PersonalBestInfoBackupHandler`); HTTP endpoints in `CentaurScores.Api/Api/Controllers/BackupController.cs` (`POST /api/backup/export`, `POST /api/backup/restore`).
- Tests: `CentaurScores.Api.Tests/BackupRestoreServiceTests.cs`.
- Frontend: `centaur-scores-web-ui/src/lib/views/BackupRestoreView.svelte`, route `/backup-restore`.

Adding a new entity type to the backup format only requires writing one more `IBackupHandler` implementation and appending it to `BackupService.Handlers` — the handler list's order is also the restore dependency order (a tenant's accounts, categories, participant lists, etc. must all resolve before anything that references them), so a new handler must be inserted after everything it can reference and before everything that references it.
