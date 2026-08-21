# Project Memory

Specification: [DESIGN.md](../documentation/DESIGN.md)

- This project is the Centaur Scores .NET API.
- Runtime target is .NET 10.
- Persistence uses EF Core with MySQL through Pomelo.
- Database identifiers must remain lowercase.
- The configured development database is `centaurscores2`; credentials should move to environment configuration before deployment.
- Authentication uses JWT bearer tokens with tenant and account claims.
- Root development login is seeded by `DatabaseInitializer`.
- Business logic belongs in `Application`; HTTP endpoints belong in `Api/Controllers`.
- Respect `.editorconfig` for code style and formatting.
- Keep code clean and readable, with no multiple statements on a single line.
- Keep complex logic in application services instead of controllers.
- Add concise code comments for non-obvious logic and important design choices.
- Backend tests are in `CentaurScores.Api.Tests`.
- Client-actionable error responses (login, change-password, account creation) return a coded `ApiError { code, message }` body instead of a plain message, so the frontend can map `code` to a translated string. `message` is developer-facing only. See `Contracts.ApiError` and `API_ENDPOINTS.md` for the known codes; extend this pattern for new endpoints whose failures the UI needs to react to specifically.
- `ParticipantListMember` has an `IsActive` flag (default `true`); `CreateParticipantRequest.IsActive` defaults to `true` when omitted. Added via the `AddParticipantMemberIsActive` migration.
- `PublicScoresController` already exposes the unauthenticated `scorekeeper/{tenantId}/{matchId}/{deviceId}` and `live-scores/{tenantId}/{scope}` endpoints used by the QR-code-linked scoring device flow and the management UI's results dropdown; real scorekeeper request/response contracts for the mobile app will be added later.
- `PUT /api/matches/{id}/participants/{participantId}/device` assigns/unassigns a participant's score device with `{ deviceId }` (nullable); `ScoreDevice` no longer tracks per-device participant assignment (no `ParticipantOrderJson`) — the participant's own `DeviceId` is the single source of truth.
- **Participant selection is per-match/per-template, not per-device.** `Match` and `MatchTemplate` both have `ParticipantListId` (nullable `Guid`) and `DeviceSelectionMode` (string: `restricted`/`list`/`list-and-free`, default `"restricted"`); `ScoreDevice` is now just `{ MatchId, Name }`. `MatchTemplate.AllowFreeParticipants` (bool) mirrors `Match.AllowFreeParticipants`.
- Changing a match's `ParticipantListId` via `PUT /api/matches/{id}` is rejected with 409 `ApiError("PARTICIPANT_LIST_LOCKED", ...)` once the match already has any `MatchParticipants` rows — the participant list is fixed once populated.
- **Schema-drift lesson (recurring bug class, hit 3 times this project: `logo_url`, `participant_count`, then avoided proactively for `device_selection_mode`)**: whenever an EF migration auto-generates `AddColumn<T>(..., nullable: false)` for a table that may already contain rows, always hand-edit the migration to add an explicit `defaultValue:` — otherwise `dotnet ef database update` succeeds (adds the column with no default) but the next `INSERT` that doesn't set that column fails at runtime with a DB-level "doesn't have a default value" error, not a migration-time error. Check every auto-generated non-nullable `AddColumn` migration for this before applying.
