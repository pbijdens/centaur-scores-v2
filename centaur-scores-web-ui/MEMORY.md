# Project Memory

Specification: [DESIGN.md](../documentation/DESIGN.md)

- This project is the Svelte + TypeScript management UI.
- The API defaults to `http://127.0.0.1:5080`; override it with `VITE_API_BASE_URL`.
- Authentication uses the API's JWT token and local storage.
- The UI supports English and Dutch and remembers the selected language.
- Date-only values must be displayed with local calendar semantics; use the shared date utility.
- Main management areas are overview, matches, competitions, participants, categories, templates, and accounts.
- UI tests use Vitest; type checking uses `svelte-check`.
