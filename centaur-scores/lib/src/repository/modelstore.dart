import 'package:centaur_scores/src/model/pending_updates.dart';
import 'package:centaur_scores/src/model/scorekeeper_match.dart';
import 'package:centaur_scores/src/model/settings_model.dart';
import 'package:localstorage/localstorage.dart';
import 'dart:developer';

class ModelStore {
  static final ModelStore _instance = ModelStore._internal();

  factory ModelStore() {
    return _instance;
  }

  ModelStore._internal() {
    log("ModelStore was created.");
  }

  // --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- ---

  final LocalStorage storage = LocalStorage('match-repository.json');

  // localstorage rewrites the whole backing file on every setItem, and
  // doesn't serialize concurrent calls against the same File itself - firing
  // two writes back to back (e.g. rapid arrow taps each queuing a
  // savePendingUpdates while a previous save/flush is still in flight)
  // throws "An async operation is currently pending" from dart:io. Route
  // every read/write through this queue so they run strictly one at a time.
  Future<void> _queue = Future.value();

  Future<T> _enqueue<T>(Future<T> Function() action) {
    final result = _queue.then((_) => action());
    _queue = result.then((_) {}, onError: (_) {});
    return result;
  }

  Future<ScorekeeperMatch?> loadMatchData() => _enqueue(() async {
        await storage.ready;

        try {
          var loaded = await storage.getItem('matchData');
          if (null == loaded) {
            return null;
          } else {
            return ScorekeeperMatch.fromJson(loaded);
          }
        } catch (error) {
          log('Error: $error');
          return null;
        }
      });

  Future<void> saveMatchData(ScorekeeperMatch match) => _enqueue(() async {
        await storage.ready;
        await storage.setItem('matchData', match.toJson());
      });

  Future<PendingUpdates> loadPendingUpdates() => _enqueue(() async {
        await storage.ready;

        try {
          var loaded = await storage.getItem('pendingUpdates');
          if (null == loaded) {
            return PendingUpdates();
          } else {
            return PendingUpdates.fromJson(loaded);
          }
        } catch (error) {
          log('Error: $error');
          return PendingUpdates();
        }
      });

  Future<void> savePendingUpdates(PendingUpdates updates) => _enqueue(() async {
        await storage.ready;
        await storage.setItem('pendingUpdates', updates.toJson());
      });

  /// Wipes cached match/edit state, used both when a fresh pairing arrives
  /// (a different apiBaseUrl than before) and when the user explicitly
  /// re-pairs. Deliberately does not touch settings (apiBaseUrl/language) -
  /// callers update those separately.
  Future<void> clearPairingData() => _enqueue(() async {
        await storage.ready;
        await storage.deleteItem('matchData');
        await storage.deleteItem('pendingUpdates');
      });

  Future<String?> getApiBaseUrl() async {
    var settings = await loadSettings();
    var url = settings.apiBaseUrl;
    if (url == null) return null;
    while (url!.endsWith('/')) {
      url = url.substring(0, url.length - 1);
    }
    return url;
  }

  Future<void> setApiBaseUrl(String? url) async {
    var settings = await loadSettings();
    settings.apiBaseUrl = url;
    await saveSettings(settings);
  }

  Future<String> getLanguage() async {
    var settings = await loadSettings();
    return settings.language;
  }

  Future<void> setLanguage(String language) async {
    var settings = await loadSettings();
    settings.language = language;
    await saveSettings(settings);
  }

  Future<SettingsModel> loadSettings() => _enqueue(() async {
        await storage.ready;

        var loadedSettings = await storage.getItem('settings');
        if (loadedSettings == null) {
          // Fresh install: apiBaseUrl stays null (not paired yet), language
          // defaults to NL. No auto-seeded backend URL - see SettingsModel.
          return SettingsModel();
        } else {
          // json_serializable silently drops unrecognized keys from the old
          // shape (deviceID/serverURL), so a device upgrading from the
          // previous app version naturally lands here with apiBaseUrl ==
          // null too - correct, since the old server isn't the new server
          // anyway.
          return SettingsModel.fromJson(loadedSettings);
        }
      });

  Future<void> saveSettings(SettingsModel settings) => _enqueue(() async {
        await storage.ready;
        await storage.setItem('settings', settings.toJson());
      });
}
