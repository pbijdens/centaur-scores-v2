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
