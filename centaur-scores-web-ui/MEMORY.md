# Project Memory

Specification: [DESIGN.md](../documentation/DESIGN.md) (root system design) and [DESIGN.md](DESIGN.md) (this project's requirements and use cases, UC1-UC16).

- This project is the Svelte + TypeScript management UI.
- The API defaults to `http://127.0.0.1:5080`; override it with `VITE_API_BASE_URL`.
- Authentication uses the API's JWT token and local storage.
- The UI supports English and Dutch and remembers the selected language.
- Never hardcode user-facing labels in views/components; add them to `src/lib/i18n.ts` for every language instead (see [COPILOT.md](COPILOT.md)).
- Date-only values must be displayed with local calendar semantics; use the shared date utility.
- Main management areas are overview, matches, competitions, participants, categories, templates, accounts (UC16), profile (UC14), and tenant administration/sub-tenants (UC15).
- Accounts management (UC16) is tenant-administrator only: list shows username/real name/authorization, edit screen changes real name, e-mail, password (only if non-empty) and authorization; a user cannot change their own authorization. Backed by `GET/PUT /api/accounts/{id}`. "Add account" only asks for a username and creates an unprivileged (`Viewer`) account via `POST /api/accounts` (server generates a random initial password); on failure the add-form stays open with a coded error message.
- API errors that the UI must react to specifically (login, change-password, account creation) come back as `{ code, message }` instead of plain text. `api.ts`'s `ApiRequestError` carries `code`; never show the raw `message` to users. Use `labelForError(error, labels, fallbackKey)` from `src/lib/errors.ts`, which looks up an `error_<CODE>` key in `i18n.ts` (add one per language) and falls back to `fallbackKey` for unknown/network errors. Extend this pattern (`ApiError`/`error_<CODE>` keys) whenever a new endpoint's failures need specific UI messaging.
- The header shows the active tenant's own name/logo (falling back to the default "Centaur Scores" branding) and a profile button/avatar next to log out.
- Sub-tenant logos are uploaded client-side (SVG/PNG, 1:1 preferred aspect ratio, 256KB max) and stored as data URLs via the API; the `tenants.logo_url` column is `longtext`.
- UI tests use Vitest; type checking uses `svelte-check`.
- Run `npm test`, `npm run check`, and `npm run build` after frontend changes (also documented in COPILOT.md).
