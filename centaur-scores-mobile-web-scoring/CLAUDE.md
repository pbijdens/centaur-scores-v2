# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Specification documents

This app's behavior and API contract are specified outside this repo, in the sibling `documentation/` folder:

- `../documentation/MOBILE-SCORE-APP.md` — UI/UX and behavioral spec (screens, sync/offline rules, i18n).
- `../documentation/PUBLIC-API-DESIGN.md` — the backend API contract this app consumes.

This module exclusively consumes the API described there — no other backend endpoints. Read the relevant section before changing a screen's behavior or the API client, and note that the *live* API sometimes diverges slightly from the design doc (see the error-shape and category-values gotchas below) — verify against the real server rather than trusting the doc alone when the two disagree.

## Commands

```bash
npm install
npm run dev       # Vite dev server
npm run build     # production build (base: './' — see Deployment below)
npm run preview   # preview a production build locally
npm run check     # svelte-check + tsc --noEmit
```

There is no test suite/framework configured in this repository.

## Architecture

Single-page Svelte 5 (runes) + TypeScript + SCSS app with no router — screen switching is driven entirely by an in-memory/localStorage-persisted `screen` store, not URL routing (per spec, this app has no complex navigation).

### Startup and persistence

`lib/matchService.ts`'s `initializeFromStartupParams()` parses `?api=...&language=...` once on load. From then on, the API base, language, current screen, cached match data, and any unsynced score edits are all persisted to `localStorage` via the `persisted()` wrapper in `lib/stores.ts` (keys prefixed `centaurScores.`, see `lib/storage.ts`), so a refresh resumes exactly where the user left off. Supplying a new `?api=` wipes all cached state except the api base/language themselves (`clearAppState()`).

### Screen navigation without a router

`screen` is a discriminated union (see `Screen` in `lib/types.ts`). `navigate()`/`goToParent()` in `lib/stores.ts` maintain an in-memory history stack and push dummy `history.pushState` entries so that the device/browser back button (`popstate`, wired up in `App.svelte`) is intercepted and mapped to the logical parent screen instead of leaving the app.

### Background sync engine (`lib/syncService.ts`)

This is the core of the app:

- `startBackgroundSync()` runs a 60s match-info poll (`fetchMatchInfo`) and an 8s pending-score-flush retry (`flushPendingScores`); both are also triggered immediately by `recordScoreEdit()` on every local score edit and by the header's manual sync tap (`forceSync`).
- Score edits are queued as `{old, new}` pairs per participant/arrow-index in the persisted `pendingUpdates` store. `old` is only captured on the *first* unsynced edit to a given arrow and preserved across further local edits until it syncs — this is what lets the server detect genuine conflicts vs. redundant updates.
- `mergeMatchData()` (`lib/matchService.ts`) overlays still-pending local edits onto freshly polled server data, so a background poll never clobbers unsynced input.
- Conflict handling: a 409 `UPDATE_SCORE_CONFLICT` response is split per entry into `SCORE_CONFLICT` (per-arrow; resolved via `resolveScoreConflict(id, index, 'mine' | 'theirs', serverValue)`) and `PARTICIPANT_CONFLICT` (participant no longer assigned to this device; resolved via `discardParticipantConflict(id)`), surfaced through the `conflicts` store and rendered by `components/ConflictDialog.svelte`. Non-conflicting parts of a rejected request are dropped from `pendingUpdates` immediately; conflicting parts stay pending until the user resolves them.
- **Gotcha:** the live API's error body is `{ error: { code, message }, conflicts: [...] }`, not a flat `{code, conflicts}` shape as the design doc's phrasing might suggest — `lib/api.ts`'s `parseErrorBody()` has to reach into `body.error.code`. Any change to error handling should be checked against the real server response, not just inferred from the doc.

### API client and types

`lib/api.ts` (`ScorekeeperApi`) is a thin typed wrapper over the five endpoints; `ApiError` (HTTP-level failure, carries `status`/`code`/`conflicts`) is distinguished from `NetworkError` (request never got a response). `lib/types.ts` mirrors the real JSON shapes, which diverge from the design doc in one place: `ScoreKeeperCategory.values` is an array of `{id, name}` objects, not plain strings.

### Participant payload rules

`PUT /participants` has fiddly field-nulling rules depending on whether a participant is tenant-linked (`tenantParticipantId` set) vs. match-local/custom. This is centralized in `lib/matchService.ts` (`toParticipantPayload`, `optionToParticipantPayload`, `customParticipantPayload`) — build payloads through these rather than constructing them ad hoc in views.

### Scoring math

Totals, split/group totals, and per-end arrow slicing live in `lib/scoring.ts` and are shared by `HomeView`, `Header`, and `ScoreCardView`. Changes to how scores are computed belong there, not duplicated in components.

### Views

`views/` implements the five screens from the spec. Notably:

- `AddParticipantView` doubles as the "add unlisted participant" form by toggling an internal `mode` state rather than being a separate screen.
- `ScoreCardView` implements the inline on-screen keyboard (the OS keyboard must never appear) and cyclic swipe-to-switch-participant via manual pointer-event tracking. The swipe handling locks in horizontal-vs-vertical intent early and calls `preventDefault()` once it does — without this, real mobile browsers steal a horizontal swipe for their native back-gesture/scroll before it reaches the app.

### Styling and deployment

- `vite.config.ts` uses `base: './'` (relative asset paths) so the built app works when served from an arbitrary sub-path behind a reverse proxy (e.g. `/scores/`). Don't change this to an absolute base.
- `svelte.config.js` must keep `preprocess: vitePreprocess()`. Without it, SCSS variables inside `@media` queries compile silently wrong and only fail at the final CSS-minify step of `npm run build` — not in dev, and not as a type error.
- Svelte 5 runes (`$state`, `$derived`, `$props`, `$effect`) are used inside components, mixed with classic `svelte/store` (`writable`/`derived`) for cross-module state in `lib/`; both interop fine here.
- Text sizing/contrast targets a 50+ audience — large touch targets and readable type sizes are a spec requirement, not incidental styling (see `styles/_variables.scss` for the size scale already in use).
