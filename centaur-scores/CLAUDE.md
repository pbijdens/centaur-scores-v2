# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Flutter (Dart) Android/iOS/desktop app for entering archery scores on the "Centaur Scores" network. It talks over HTTP to a companion .NET backend that lives in the sibling repo `../centaur-scores-api-v2` (run via that repo's `run-dev.sh`, default `http://localhost:5080`). The app is offline-first: it keeps its own local copy of the active match and periodically syncs dirty state to the server.

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

Localized strings are generated from `lib/src/localization/app_en.arb` (see `l10n.yaml`) into `package:flutter_gen/gen_l10n/app_localizations.dart`; this happens automatically as part of `flutter pub get` / build because `generate: true` is set in `pubspec.yaml`.

CI (`.github/workflows/main.yaml`) runs on push to `main`: `flutter pub get`, builds debug and release APKs, uploads them as artifacts, and cuts a GitHub release tagged `v<version>` from `pubspec.yaml` (appending `-build-<run_number>` if that tag already exists).

## Architecture

**MVVM with a manual observer/event bus** (`lib/src/mvvm/`), not `Provider`/`Bloc`/`Riverpod`:
- `EventViewModel` (`viewmodel.dart`) — base class for all viewmodels; holds a list of `EventObserver`s and exposes `notify(ViewEvent event)`.
- Each feature under `lib/src/features/<feature>/` typically has a `*_view.dart` (a `StatefulWidget` that implements `EventObserver` and subscribes/unsubscribes to its viewmodel in `initState`/`dispose`) and a `*_viewmodel.dart` (extends `EventViewModel`, defines feature-specific `ViewEvent` subclasses at the bottom of the same file, e.g. `ScoresViewmodelLoadedEvent`, `KeyboardShownEvent`).
- Views react to events in their `notify()` override, usually by calling `setState`.

**Singleton repository/data layer** (`lib/src/repository/`), everything reachable through `MatchRepository()`:
- `MatchRepository` — singleton (`factory` constructor returning a static `_instance`), also a `ChangeNotifier` so `MyApp`'s root `ListenableBuilder` can rebuild the whole navigation stack when the active match changes (see `onModelReplaced` / `MyApp.onRepositoryChanged`). Owns a `Timer.periodic` (every 10s) that pushes local edits to the server when the model is dirty, checks whether the active match on the server changed (swaps the whole model if so), and checks whether the server flagged this device for a forced resync.
- `MatchModel` is the single source of truth for the active match: match metadata, `groups`/`subgroups`/`targets`, `scoreValues` (per-group scoring button layouts), and the `participants` list. It carries an `isDirty` flag used to decide whether to push to the server.
- `CentaurScoresAPI` — singleton HTTP client wrapper. All endpoints are scoped under `<serverURL>/match/...` or `<serverURL>/devices/<deviceID>/sync`; `deviceID` and `serverURL` come from `ModelStore`/`SettingsModel` and are attached to most requests.
- `ModelStore` — singleton wrapper around `LocalStorage` (from the `localstorage` package) for persisting the `MatchModel` JSON, device ID, and `SettingsModel` (server URL) between app runs.
- Model classes in `lib/src/model/` use `json_serializable`/`json_annotation` (`@JsonSerializable(explicitToJson: true)`, `part '<file>.g.dart'`); regenerate the `.g.dart` file after editing any of them (see build_runner command above).

**Navigation**: routes are resolved centrally in `MyApp.onGenerateRoute` (`lib/src/app.dart`) by switching on `RouteSettings.name` against each view's static `routeName`; there's no named-route table elsewhere. `NavigationService` exposes a global `navigatorKey` so non-widget code (e.g. `MatchRepository`) can push/pop without a `BuildContext`.

**Score entry flow**: `ParticipantsView` → `ScoresView` (grid/table of all participants and ends, `lib/src/features/score_card/`) → `ScoreEntryForSingleEndView` (single-end, single-arrow keypad entry, `lib/src/features/score_entry/`). Both entry paths ultimately call into `MatchRepository.setArrow(participantId, endNo, arrowNo, value)`, which mutates the local model and marks it dirty for the next sync cycle.

**Config**: no `.env`/flavors — the server URL is stored per-device in `SettingsModel` (editable from `SettingsView`) and falls back to a hardcoded default (`MatchRepository.hardcodedURL`) on first run.
