# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

The Centaur Scores REST API — an ASP.NET Core 10, controller-based backend for tenant administration, matches, competitions, scorekeeper devices, and live score consumers. It is one project inside a larger multi-repo workspace (sibling directories at `../`); the root system spec lives at `../documentation/DESIGN.md`. The complete human-readable endpoint guide is [API_ENDPOINTS.md](API_ENDPOINTS.md) — keep it up to date whenever a change affects the API surface. [README.md](README.md) covers general project/setup info.

There is also a running project memory file at [MEMORY.md](MEMORY.md) in this repo, written in the same spirit as this file but focused on decisions/conventions/gotchas discovered while building specific features (schema-drift lessons, migration-snapshot gotchas, per-endpoint quirks). Check it for detail beyond what's summarized here.

## Commands

- `dotnet run --project CentaurScores.Api` (or `./run-dev.sh`, which also sets `ASPNETCORE_ENVIRONMENT=Development` and `ASPNETCORE_URLS=http://localhost:5080`) — start the API
- `dotnet build` — build the solution
- `dotnet test CentaurScores.Api.Tests/CentaurScores.Api.Tests.csproj` — run the xUnit test suite

Run `dotnet build` and `dotnet test CentaurScores.Api.Tests/CentaurScores.Api.Tests.csproj` after backend changes — this is the project's required validation pair.

`dotnet ef database update --project CentaurScores.Api/CentaurScores.Api.csproj` applies migrations to a new database. The development seed contains tenant `00000000-0000-0000-0000-000000000001` and account `centaurscores`/`centaurscores`; the dev database at `appsettings.Development.json` is `centaurscores2`. API discovery is available at `/swagger` and `/swagger/v1/swagger.json` when the API is running.

## Architecture

**Layout**: code is organized by namespace under `CentaurScores.Api/` — `Domain` (entities), `Contracts` (DTOs, including `Contracts.ApiError`), `Application` (business logic/services/policies), `Infrastructure` (EF Core persistence), and `Api/Controllers` (HTTP endpoints). Keep responsibilities in the matching namespace; put reusable business rules and complex logic in `Application` services rather than controllers, and cover them with xUnit tests in `CentaurScores.Api.Tests`.

**Persistence**: EF Core through Pomelo for MySQL. Database table and column names must stay lowercase. Use migrations for schema changes — never replace the database with an in-memory store. See MEMORY.md for two recurring migration pitfalls: hand-authored non-nullable `AddColumn` migrations need an explicit `defaultValue:`, and hand-writing migration `.cs` files without regenerating `ApplicationDbContextModelSnapshot.cs` leaves the snapshot stale.

**Tenancy and timestamps**: scope all authenticated data access to the tenant from the JWT claims. Keep timestamps in UTC; use `DateOnly` for timezone-agnostic dates.

**Coded errors**: when an endpoint's failure needs to drive specific UI behavior (auth, account creation/update, restore, etc.), return `Contracts.ApiError { code, message }` instead of a plain `{ message }` — `code` is the stable contract the client switches on, `message` is for logs only. See `API_ENDPOINTS.md` for the known codes; extend this pattern for new endpoints whose failures the UI needs to react to specifically.

**Match CSV export** keeps columns aligned with the ordered `keyboard` and `categoryOrder` arrays in `Match.KeyboardJson`; use `IScoringService` for grouped-end totals.

**Scorekeeper contract**: implemented in `Application/ScorekeeperService.cs` — keep all participant policy, ordering, projection, and optimistic score conflict logic there, with `PublicScoresController` limited to anonymous routing, active-match checks, logging, and response status mapping. Preserve the public live-score and scorekeeper endpoints described in the specification.

## Conventions

- Respect the repository's `.editorconfig` settings when editing or formatting code.
- Keep code clean and readable; no multiple statements on a single line.
- Document non-obvious code segments and important implementation choices with concise comments.
- After finishing an activity, update CLAUDE.md, MEMORY.md, and — if it impacted the API surface — API_ENDPOINTS.md.
