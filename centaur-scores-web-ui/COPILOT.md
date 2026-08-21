# Copilot Guidance

Read the system specification before changing behavior: [DESIGN.md](../documentation/DESIGN.md).

The management UI's own requirements and use cases live in [DESIGN.md](DESIGN.md) in this project; the root specification no longer covers them.

## API

Documentation for the REST API is here [API-ENDPOINTS>md](../centaur-scores-api-v2/API_ENDPOINTS.md)

## Frontend conventions

- Keep the management UI in Svelte with TypeScript.
- Preserve the mobile-friendly management workflows described in the specification.
- Use the typed API boundary and keep authentication state in local storage as specified.
- Use English and Dutch translations, remembering the selected language locally.
- Never hardcode user-facing labels in views/components; always add them to the [i18n.ts](src/lib/i18n.ts) label dictionary and update every language.
- Render API timestamps as UTC-aware values and render date-only values without timezone shifts.
- Keep live score and scorekeeper pages free of the management header.
- Prefer small reusable components and utility modules over adding more monolithic page markup.
- Use standard browser controls for forms, filters, dates, and option selection.

## Validation

Run `npm test`, `npm run check`, and `npm run build` after frontend changes.
