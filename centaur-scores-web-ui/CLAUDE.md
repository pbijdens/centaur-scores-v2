# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

The tenant-management web UI for Centaur Scores — a Svelte 5 + TypeScript + Vite single-page app. It is one project inside a larger multi-repo workspace (sibling directories at `../`); the root system spec lives at `../documentation/DESIGN.md`, and the REST API this UI talks to lives at `../centaur-scores-api-v2` (its endpoint docs: `../centaur-scores-api-v2/API_ENDPOINTS.md`). This project's own requirements/use-cases (UC1–UC17) live in [DESIGN.md](DESIGN.md) in this repo — read it before changing UI behavior.

There is also a running project memory file at [MEMORY.md](MEMORY.md) in this repo, written in the same spirit as this file but focused on decisions/conventions discovered while building specific features (JSON blob shapes, API error-code plumbing, per-use-case gotchas). Check it for detail beyond what's summarized here.

## Commands

- `npm run dev` — start the Vite dev server
- `npm run build` — production build
- `npm run preview` — preview a production build
- `npm test` — run the Vitest suite (`vitest run`)
- `npm run check` — type-check: `svelte-check` against `tsconfig.app.json`, then `tsc` against `tsconfig.node.json`

Run `npm test`, `npm run check`, and `npm run build` after any frontend change — this is the project's required validation trio (see COPILOT.md).

To run a single test file: `npx vitest run src/lib/router.test.ts`. Utility test files sit next to the module they cover (`date.test.ts`, `router.test.ts`, `templateConfig.test.ts`).

The API defaults to `http://127.0.0.1:5080`; override with the `VITE_API_BASE_URL` env var.

## Architecture

**No router library, no state library.** `App.svelte` is the composition root: it owns all top-level state (auth token, tenant, entity lists, selection ids) as plain Svelte reactive `let`/`$:` variables, and renders one of ~24 view components (`src/lib/views/*View.svelte`) via a big `{#if view === '...'}` chain. Routing is hand-rolled in `src/lib/router.ts`: `resolveRoute(path)` parses `location.pathname` into a typed `Route` (`{ view, matchId?, tenantId?, ... }`), and `navigateTo(path)` pushes history state and re-resolves. `App.svelte` reacts to route changes in `applyRouteResult()`, which derives selected-entity ids from the route and triggers loads (e.g. `loadSelectedMatch`).

**Layout**: `src/lib/views/` holds page-level `*View.svelte` components (one per route); `src/lib/` itself holds shared utility modules (`api.ts`, `i18n.ts`, `router.ts`, `date.ts`, `errors.ts`, `matchConfig.ts`, `templateConfig.ts`, `tenantLogo.ts`, `types.ts`) plus the one cross-view shared component, `AppHeader.svelte`. Utility modules keep their `.test.ts` colocated; view components import utilities via `../` (e.g. `import { labelForError } from '../errors'`).

**Data flow is parent-owned, not per-view fetching.** `App.svelte.loadData()` eager-loads matches, competitions, profile, current tenant, categories, participant lists, and templates in one pass on login (categories/participant lists come back with nested values/members already included). Views receive data and an `api: ApiClient` instance as props, call mutation methods on `api`, then invoke an `onChanged`/`onSaved` callback prop so the parent refetches (`refreshCategories()`, `refreshParticipantLists()`, `refreshTemplates()`, etc.) — views do not mutate parent-owned arrays directly except for a few documented optimistic-update cases (see `MatchParticipantView` in MEMORY.md).

**`src/lib/api.ts`** is the single typed HTTP boundary (`ApiClient` class, one method per endpoint, JWT bearer auth from `getToken()`, calls `onUnauthorized()` on 401). Server errors that need specific UI handling come back as `{ code, message }` and are wrapped in `ApiRequestError` (carries `.code`); never surface `.message` directly — use `labelForError(error, labels, fallbackKey)` from `src/lib/errors.ts`, which maps `error_<CODE>` keys from `i18n.ts` with a fallback for unknown/network errors.

**i18n**: `src/lib/i18n.ts` holds an EN/NL label dictionary; `translationsFor(language)` returns the active set. Never hardcode user-facing strings in a view — add a key to `i18n.ts` for every language instead. Selected language persists in `localStorage`.

**Free-form JSON config split**: `MatchTemplate.configurationJson` and `Match.keyboardJson`/`Match.scoringRulesJson` are backend-opaque JSON blobs whose shape is defined entirely on the frontend — `src/lib/templateConfig.ts` (templates) and `src/lib/matchConfig.ts` (matches) own the parse/default helpers and pair with the `TemplateConfiguration`/`ScoringRule`/etc. types in `src/lib/types.ts`. When changing these shapes, update the helper module, `types.ts`, and every view that edits them (`TemplateEditView`, `MatchMetadataEditView`, `MatchDevicesView`).

**Auth/session state** lives in `localStorage` under `centaur-token`, `centaur-tenant`, `centaur-language` — read directly in `App.svelte`, not through a store.

**Views without the management chrome**: `LiveScoringView` (narrowcast display) and `MatchQrCodesView` (printable QR sheet) render standalone, without `AppHeader` — keep it that way per the spec.

## Conventions

- Prefer small reusable components/utility modules over growing existing page components.
- Use standard browser controls for forms, filters, dates, and option selection (no custom form-widget library).
- Render API timestamps as UTC-aware; render date-only values using local calendar semantics (see `src/lib/date.ts` / `formatLocalDate`), never shifted by timezone.
- Universal list-rendering rule (applies wherever a participant-list member appears in a selection UI, this web app only — the Flutter and scoring apps have their own established member-selection UI): render as "full name (federation number / concatenated category values)" using the tenant's full `categories` list, and always exclude members with `isActive === false`. Implemented once in `src/lib/participantName.ts` (`memberDisplayLabel`/`memberDetailLabel`/`memberCategoryLabel`) — reuse those rather than reimplementing per view.
