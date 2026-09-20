# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Flutter (Dart) Android/iOS/desktop app for entering archery scores on the "Centaur Scores" network. It talks over HTTP to a companion .NET backend that lives in the sibling repo `../centaur-scores-api-v2` (run via that repo's `run-dev.sh`, default `http://localhost:5080`). The app is offline-first: it keeps its own local copy of the active match and periodically syncs dirty state to the server.

There is also a running project memory file at [MEMORY.md](MEMORY.md) in this repo — check it for decisions/gotchas beyond what's summarized here.

## Commands

```bash
flutter pub get                      # install dependencies
flutter analyze                      # static analysis (flutter_lints, see analysis_options.yaml)
flutter test                         # run all tests in test/
flutter test test/unit_test.dart     # run a single test file
flutter run                          # run on a connected device/emulator
flutter build apk --debug            # debug APK (CI builds this because target devices are min-SDK 23, which can't run release builds)
flutter build apk --release          # release APK
```

Regenerate JSON (de)serialization code after changing any `@JsonSerializable` model (anything with a matching `*.g.dart` file):

```bash
flutter packages pub run build_runner build
# or, to overwrite stale generated files without prompting:
flutter packages pub run build_runner build --delete-conflicting-outputs
```

**Note on running this app in this sandbox**: `flutter run -d web-server` boots and renders fine (it's Flutter CanvasKit web, driveable via Playwright by clicking the `flt-semantics-placeholder` element to enable the accessibility/DOM tree - see MEMORY.md), but every real network call fails at runtime with `Unsupported operation: Platform._version` - `CentaurScoresAPI` builds its HTTP client on `dart:io`'s `HttpClient`/`IOClient` (see the pairing/QR section below), which does not exist on the web platform at all. This is a pre-existing limitation of the app's HTTP layer, not something to "fix" incidentally while working on an unrelated feature. A Linux desktop build needs `clang`/`cmake`/`ninja-build`/`libgtk-3-dev` that aren't installed by default here. In practice, verifying real pairing/sync/scoring behavior needs a real Android/iOS device or emulator outside this sandbox - static analysis (`flutter analyze`), `flutter test`, and careful code review are the ceiling for automated verification here.

CI (`.github/workflows/main.yaml`) runs on push to `main`: `flutter pub get`, builds debug and release APKs, uploads them as artifacts, and cuts a GitHub release tagged `v<version>` from `pubspec.yaml` (appending `-build-<run_number>` if that tag already exists).

## Architecture

**MVVM with a manual observer/event bus** (`lib/src/mvvm/`), not `Provider`/`Bloc`/`Riverpod`:
- `EventViewModel` (`viewmodel.dart`) — base class for all viewmodels; holds a list of `EventObserver`s and exposes `notify(ViewEvent event)`.
- Each feature under `lib/src/features/<feature>/` typically has a `*_view.dart`/`*_widget.dart` (a `StatefulWidget` that implements `EventObserver` and subscribes/unsubscribes to its viewmodel in `initState`/`dispose`) and a `*_viewmodel.dart` (extends `EventViewModel`, defines feature-specific `ViewEvent` subclasses at the bottom of the same file, e.g. `ScoresViewmodelLoadedEvent`, `KeyboardShownEvent`).
- Views react to events in their `notify()` override, usually by calling `setState`.

**QR-pairing, not tenant/login.** The app has no login screen - `AppShell` shows `QrScanView` (`lib/src/features/pairing/`) whenever `MatchRepository().isConfigured` is false. Pairing is a URL of the shape `http://host/scores?api=<url-encoded-api-base>&language=NL|EN` (scanned via `mobile_scanner`, or entered manually), parsed by the pure, tested `parsePairingUrl` in `pairing_url_parser.dart`. The parsed `api` value is the full scorekeeper base URL `<serverURL>/scorekeeper/{tenantId}/{matchId}/{deviceId}`, stored via `ModelStore.setApiBaseUrl` and prefixed onto every subsequent `CentaurScoresAPI` call. Re-pairing (`MatchRepository.resetPairing`/`configure`) discards all cached match/pending-edit/pending-signature state - see MEMORY.md's "Tenant-less..." precedent in the web-ui repo for the analogous idea here, though this app never had per-tenant accounts to begin with.

**Singleton repository/data layer** (`lib/src/repository/`), everything reachable through `MatchRepository()`:
- `MatchRepository` — singleton (`factory` constructor returning a static `_instance`), also a `ChangeNotifier` so `AppShell`'s root `ListenableBuilder` can rebuild the whole navigation stack when the active match changes. Owns two `Timer.periodic`s: a 60s poll (`fetchMatchInfo`, replaces the whole `ScorekeeperMatch` object) and an 8s retry (`flushPendingScores` + `flushPendingSignatures`, each independently guarded against overlapping runs).
- `ScorekeeperMatch`/`ScorekeeperMatchParticipant` (`lib/src/model/`) are the single source of truth for the active match - response shape of `GET /scorekeeper/{tenantId}/{matchId}/{deviceId}`, matching `PUBLIC-API-DESIGN.md` in `../documentation/`. Arrow scores are a flat `List<String?>` (key ID per shot arrow, or null), sliced into ends via `arrowsPerEnd`.
- `CentaurScoresAPI` — singleton HTTP client wrapper scoped under whatever `<serverURL>/scorekeeper/{tenantId}/{matchId}/{deviceId}` was obtained during pairing (re-read from `ModelStore` on every call, so a re-pair takes effect immediately). Built on `dart:io`'s `HttpClient`/`IOClient` with certificate verification disabled (see the code comment - min-SDK-23 CA trust store issue) - **this is why it cannot run on Flutter Web**, see the sandbox note above.
- `ModelStore` — singleton wrapper around `LocalStorage` (from the `localstorage` package) for persisting the `ScorekeeperMatch` JSON, pending score edits, pending signatures, and `SettingsModel` (API base URL, language) between app runs. All reads/writes are serialized through a manual `_enqueue` queue (`localstorage` rewrites its whole backing file per write and doesn't tolerate concurrent calls).
- Model classes in `lib/src/model/` use `json_serializable`/`json_annotation` (`@JsonSerializable(explicitToJson: true)`, `part '<file>.g.dart'`); regenerate the `.g.dart` file after editing any of them (see build_runner command above). `PendingUpdates`/`PendingSignatures` are hand-written (de)serialization instead (JSON object keys are always strings; `PendingUpdates` needs an explicit int parse for arrow indexes).

**Navigation is a custom logical stack, not Flutter's `Navigator`.** `AppNavigator` (`lib/src/repository/app_navigator.dart`) is a singleton `ChangeNotifier` holding `current: AppScreen` (a sealed class - `HomeScreen`, `ScoreCardScreen`, `AddParticipantScreen`, etc.) plus a small logical parent-history list; `AppShell._buildBody()` switches on `AppNavigator().current`. `navigate()`/`goToParent()` are how screens change - there's no named-route table and no `Navigator.push`. The real Flutter `Navigator` is still used for true modal overlays (`showDialog` - the sync-conflict dialog, and the signing confirmation/signature-capture dialogs, see below) rather than full-screen navigation.

**Score entry**: `ScoreCardScreen` → `ScoresView`/`ScoreEntryFullPageWidget` (`lib/src/features/score_card/`) - a single screen showing every participant on this device as a side-by-side horizontally-scrollable column (unlike the mobile-web app's one-participant-at-a-time Score Card), each with a score-entry grid, a collapsible numeric keypad (`ScoreColumnKeyboard`, shown only for the currently `activeKeyboard` column), and a totals footer (`SingeParticipantFooter`). All edits go through `ScoresViewmodel.setScore` → `MatchRepository.setArrow`/`recordScoreEdit`, which mutates the local model and marks it dirty for the next 8s sync tick.

**Scorecard signing** (see `../documentation/SIGNING-SCORECARDS.md`): each participant column has a third, always-visible slot below the keypad, `SignArea` (`lib/src/features/score_card/sign_area.dart`) - hidden entirely when `ScorekeeperMatch.signatureMode == 'none'`; otherwise a persistent "Sign" button until `participant.signed`, then either the recorded signature images (`signature` mode) or a plain read-only notice (`confirm` mode, which never captures images). Tapping Sign shows a plain `AlertDialog` (`confirm` mode) or a full-screen `SignatureCaptureDialog` with two hand-rolled ink-capture `SignaturePad`s (`signature_pad.dart` - `Listener` + `CustomPainter` + `RepaintBoundary.toImage()`, no third-party signature package, consistent with this app's minimal-dependency posture), both via `showDialog` (real Flutter `Navigator`, not `AppNavigator`). Confirming calls `MatchRepository.recordSignature`, which queues + optimistically applies the signed state exactly like `recordScoreEdit` does for scores, except there is deliberately no conflict resolution (per the spec) - `flushPendingSignatures` just retries until it lands, treating a `SCORECARD_SIGNED` response as success (idempotent, not a failure) rather than retrying forever. Once `participant.signed` is true, the keypad can never reopen for that participant again (`score_entry_fullpage_widget.dart`'s `keyboard()` gate, and `single_participant_score_form.dart`'s `onTapScoreField` no-ops) - there is no anonymous unsign; withdrawing a signature is manager-only via the full (non-scorekeeper) API, not exposed to this app at all.

**Config**: no `.env`/flavors - everything (API base URL, language) comes from the QR/manual pairing flow above and is stored per-device in `SettingsModel`.
