# Centaur Scores API v2

ASP.NET Core 10 controller-based API for tenant administration, matches, competitions, scorekeeper devices, and live score consumers.

The code is organized into `Domain` entities, `Contracts` DTOs, `Application` services/policies, `Infrastructure` EF Core persistence, and `Api/Controllers` HTTP endpoints. All controller queries are tenant scoped and write operations enforce manager or administrator profiles.

The development seed contains tenant `00000000-0000-0000-0000-000000000001` and account `centaurscores` / `centaurscores`. Replace the JWT secret and database credentials before deployment. Database initialization creates all tables with lowercase names and columns.

Run from `CentaurScores.Api/` with `dotnet run`. The API contract is JSON and uses UTC timestamps; date-only fields are represented as ISO `yyyy-MM-dd` values. `dotnet ef database update --project CentaurScores.Api/CentaurScores.Api.csproj` applies the migration to a new database. The supplied development database was baselined against the migration and contains the expanded lowercase schema.

API discovery is available at `/swagger` and `/swagger/v1/swagger.json` when the API is running. The complete human-readable endpoint guide is in [API_ENDPOINTS.md](API_ENDPOINTS.md).

Run backend tests with `dotnet test CentaurScores.Api.Tests/CentaurScores.Api.Tests.csproj`. Run the management UI with `npm run dev` and set `VITE_API_BASE_URL` when the API is not on `http://127.0.0.1:5080`.
