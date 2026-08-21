# Copilot Guidance

Read the system specification before changing behavior: [DESIGN.md](../documentation/DESIGN.md).

## Backend conventions

- Target .NET 10 and keep the API controller-based.
- Respect the repository's `.editorconfig` settings when editing or formatting code.
- Keep code clean and readable; do not put multiple statements on a single line.
- Keep responsibilities separated by namespace: `Domain`, `Contracts`, `Application`, `Infrastructure`, and `Api.Controllers`.
- Use EF Core for persistence and preserve lowercase database table and column names.
- Scope authenticated data access to the tenant from the JWT claims.
- Keep UTC for timestamps and use `DateOnly` for timezone-agnostic dates.
- Put reusable business rules in application services and cover them with xUnit tests.
- Keep complex logic in application services rather than controllers.
- Document non-obvious code segments and important implementation choices with concise comments in the code.
- Use migrations for schema changes; do not replace the database with an in-memory store.
- Preserve the public live-score and scorekeeper endpoints described in the specification.

## Validation

Run `dotnet build` and `dotnet test CentaurScores.Api.Tests/CentaurScores.Api.Tests.csproj` after backend changes.

## Documentation

After finishing an activity, update COPILOT.md, MEMORY.md and if it impacted the API always update API_ENDPOINTS.md
