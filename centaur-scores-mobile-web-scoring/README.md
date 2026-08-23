# Centaur Scores – Mobile Scoring App

Mobile-first Svelte + TypeScript + SCSS application for entering archery
scores on scorekeeping devices. It talks exclusively to the public API
described in `../documentation/PUBLIC-API-DESIGN.md`; behaviour and screens
are specified in `../documentation/MOBILE-SCORE-APP.md`.

## Getting started

```bash
npm install
npm run dev
```

The app needs to be opened once with a startup URL that carries the API base
and language, e.g.:

```
http://localhost:5173/?api=http%3A%2F%2Flocalhost%3A5080%2Fscorekeeper%2F{tenantId}%2F{matchId}%2F{deviceId}&language=NL
```

It remembers this in local storage afterwards, so subsequent loads/refreshes
resume the same device/match without the query string.

## Build

```bash
npm run build
```

The build uses a relative (`base: './'`) output so it can be deployed behind
a reverse proxy under any sub-path (e.g. `https://host/scores/`).

## Type checking

```bash
npm run check
```
