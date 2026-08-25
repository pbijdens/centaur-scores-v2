import 'dart:async';
import 'package:flutter/material.dart';

import 'package:centaur_scores/src/model/api_error.dart';
import 'package:centaur_scores/src/model/pending_updates.dart';
import 'package:centaur_scores/src/model/scorekeeper_match.dart';
import 'package:centaur_scores/src/model/scorekeeper_participant_update.dart';
import 'package:centaur_scores/src/model/scorekeeper_score_conflict.dart';
import 'package:centaur_scores/src/model/scorekeeper_score_update.dart';
import 'package:centaur_scores/src/repository/app_navigator.dart';
import 'package:centaur_scores/src/repository/centaur_scores_api.dart';
import 'package:centaur_scores/src/repository/modelstore.dart';

enum SyncStatus { idle, pending, syncing, error }

/// Offline-tolerant, conflict-aware sync engine + current-match holder.
///
/// This is a Dart port of the reference web app's `stores.ts` +
/// `matchService.ts` + `syncService.ts` combined into one singleton, kept
/// in the same "singleton class `with ChangeNotifier`" shape the rest of
/// this app already uses. Screens read match/sync state directly off this
/// class via `ListenableBuilder(listenable: MatchRepository())`.
class MatchRepository with ChangeNotifier {
  static final MatchRepository _instance = MatchRepository._internal();

  factory MatchRepository() {
    return _instance;
  }

  MatchRepository._internal() {
    debugPrint("Match repository was created.");
  }

  final ModelStore _store = ModelStore();
  final CentaurScoresAPI _api = CentaurScoresAPI();

  static const Duration _pollInterval = Duration(seconds: 60);
  static const Duration _retryInterval = Duration(seconds: 8);

  Timer? _pollTimer;
  Timer? _retryTimer;

  bool _isConfigured = false;
  ScorekeeperMatch? _matchData;
  PendingUpdates _pendingUpdates = PendingUpdates();
  List<ScoreConflictEntry>? _conflicts;
  SyncStatus _syncStatus = SyncStatus.idle;
  bool _busy = false;
  String _language = 'NL';

  bool get isConfigured => _isConfigured;
  ScorekeeperMatch? get currentMatchOrNull => _matchData;
  String get language => _language;
  SyncStatus get syncStatus => _syncStatus;
  List<ScoreConflictEntry>? get conflicts => _conflicts;
  bool get busy => _busy;

  // --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- ---
  // Pairing / lifecycle
  // --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- ---

  Future<void> initialize() async {
    final apiBaseUrl = await _store.getApiBaseUrl();
    _language = await _store.getLanguage();
    if (apiBaseUrl != null) {
      _isConfigured = true;
      _matchData = await _store.loadMatchData();
      _pendingUpdates = await _store.loadPendingUpdates();
      AppNavigator().current =
          _matchData != null ? const HomeScreen() : const AppLoadingScreen();
      startBackgroundSync();
    } else {
      _isConfigured = false;
    }
    notifyListeners();
  }

  /// Called once by the pairing flow (QR scan or manual entry) with a newly
  /// obtained API base URL. Always treated as a fresh pairing: any
  /// previously cached match/pending-edit/conflict state is discarded
  /// (matching `clearAppState()` in the reference client), but [language]
  /// is only touched when explicitly provided.
  Future<void> configure(String apiBaseUrl, String? language) async {
    await _store.setApiBaseUrl(apiBaseUrl);
    if (language != null) {
      _language = language;
      await _store.setLanguage(language);
    }
    await _store.clearPairingData();
    _matchData = null;
    _pendingUpdates = PendingUpdates();
    _conflicts = null;
    _syncStatus = SyncStatus.idle;
    _isConfigured = true;
    AppNavigator().navigate(const AppLoadingScreen(), resetStack: true);
    notifyListeners();
    startBackgroundSync();
  }

  /// The header's "re-scan QR / change match" action. Clears everything
  /// except [language] and returns the app to the pairing gate.
  Future<void> resetPairing() async {
    stopBackgroundSync();
    await _store.setApiBaseUrl(null);
    await _store.clearPairingData();
    _matchData = null;
    _pendingUpdates = PendingUpdates();
    _conflicts = null;
    _syncStatus = SyncStatus.idle;
    _isConfigured = false;
    AppNavigator().resetHistoryStack();
    AppNavigator().current = const AppLoadingScreen();
    notifyListeners();
  }

  void setLanguage(String lang) {
    _language = lang;
    _store.setLanguage(lang);
    notifyListeners();
  }

  void startBackgroundSync() {
    stopBackgroundSync();
    _pollTimer = Timer.periodic(_pollInterval, (_) => fetchMatchInfo());
    _retryTimer = Timer.periodic(_retryInterval, (_) => flushPendingScores());
    fetchMatchInfo();
    flushPendingScores();
  }

  void stopBackgroundSync() {
    _pollTimer?.cancel();
    _retryTimer?.cancel();
    _pollTimer = null;
    _retryTimer = null;
  }

  // --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- ---
  // Match data
  // --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- ---

  /// GET the current match and merge it with any unsynced local edits so a
  /// background poll never clobbers an edit that hasn't been pushed yet.
  /// Also drives the loading/no-active-match/home screen transitions.
  Future<bool> fetchMatchInfo() async {
    try {
      final fresh = await _api.getMatchInfo();
      _mergeMatchData(fresh);
      await _store.saveMatchData(_matchData!);
      final current = AppNavigator().current;
      if (current is AppLoadingScreen ||
          current is NoActiveMatchScreen ||
          current is PairingErrorScreen) {
        AppNavigator().navigate(const HomeScreen(), resetStack: true);
      }
      notifyListeners();
      return true;
    } on ApiException catch (e) {
      if (e.status == 404 || e.status == 409) {
        AppNavigator().navigate(const NoActiveMatchScreen(), resetStack: true);
        notifyListeners();
      } else {
        _reportPairingFetchError(e.message);
      }
      return false;
    } catch (error) {
      // Background poll against an already-loaded match: swallow, keep
      // whatever's cached, retry next poll. Only surfaced as a blocking
      // error screen when it's the fetch immediately following a fresh
      // pairing (see _reportPairingFetchError) - otherwise the user would
      // never see it, since nothing else drives this screen forward.
      debugPrint("fetchMatchInfo failed: $error");
      _reportPairingFetchError('$error');
      return false;
    }
  }

  /// Only takes effect while still on [AppLoadingScreen] (i.e. this is the
  /// fetch immediately following a fresh pairing, not a routine background
  /// poll against a match that's already loaded and showing).
  void _reportPairingFetchError(String message) {
    if (AppNavigator().current is AppLoadingScreen) {
      AppNavigator().navigate(PairingErrorScreen(message), resetStack: true);
      notifyListeners();
    }
  }

  void _mergeMatchData(ScorekeeperMatch fresh) {
    for (final participant in fresh.participants) {
      final pending = _pendingUpdates.byParticipant[participant.matchParticipantId];
      if (pending == null) continue;
      for (final entry in pending.entries) {
        if (entry.key >= 0 && entry.key < participant.arrowScores.length) {
          participant.arrowScores[entry.key] = entry.value.newValue;
        }
      }
    }
    _matchData = fresh;
  }

  void _setLocalArrow(String matchParticipantId, int index, String? value) {
    final match = _matchData;
    if (match == null) return;
    for (final participant in match.participants) {
      if (participant.matchParticipantId == matchParticipantId) {
        if (index >= 0 && index < participant.arrowScores.length) {
          participant.arrowScores[index] = value;
        }
        return;
      }
    }
  }

  // --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- ---
  // Participants
  // --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- ---

  /// Replaces this device's participant list, then refetches the match so
  /// the caller sees the server-assigned matchParticipantId(s)/ordering.
  /// Returns false (no state mutated) on any failure.
  Future<bool> submitParticipantsList(List<ScorekeeperParticipantUpdate> payload) async {
    _busy = true;
    notifyListeners();
    try {
      await _api.putParticipants(payload);
      await fetchMatchInfo();
      return true;
    } catch (error) {
      debugPrint("submitParticipantsList failed: $error");
      return false;
    } finally {
      _busy = false;
      notifyListeners();
    }
  }

  // --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- ---
  // Score edits / sync / conflicts
  // --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- ---

  /// Queues an arrow edit for sync and applies it optimistically to the
  /// locally held match data. [previousValue] is only used as `old` the
  /// *first* time this arrow is edited while unsynced - a second edit
  /// before the first has synced keeps the original `old`, so the server
  /// can still tell a genuine conflict from a redundant overwrite.
  void recordScoreEdit(
      String matchParticipantId, int index, String? previousValue, String? newValue) {
    final perParticipant =
        _pendingUpdates.byParticipant.putIfAbsent(matchParticipantId, () => {});
    final existing = perParticipant[index];
    if (existing != null) {
      existing.newValue = newValue;
    } else {
      perParticipant[index] = ScoreEdit(old: previousValue, newValue: newValue);
    }
    _setLocalArrow(matchParticipantId, index, newValue);
    _store.savePendingUpdates(_pendingUpdates);
    if (_syncStatus != SyncStatus.syncing) {
      _syncStatus = SyncStatus.pending;
    }
    notifyListeners();
    flushPendingScores();
  }

  void forceSync() {
    if (_pendingUpdates.isNotEmpty) {
      flushPendingScores();
    }
  }

  List<ParticipantScoreUpdates> _buildScorePayload() {
    final payload = <ParticipantScoreUpdates>[];
    for (final entry in _pendingUpdates.byParticipant.entries) {
      if (entry.value.isEmpty) continue;
      final updates = entry.value.entries
          .map((e) => ScoreUpdate.create(e.key, e.value.old, e.value.newValue))
          .toList();
      payload.add(ParticipantScoreUpdates.create(entry.key, updates));
    }
    return payload;
  }

  Future<void> flushPendingScores() async {
    if (_pendingUpdates.isEmpty) {
      if (_syncStatus != SyncStatus.idle) {
        _syncStatus = SyncStatus.idle;
        notifyListeners();
      }
      return;
    }
    if (_syncStatus == SyncStatus.syncing) return;

    _syncStatus = SyncStatus.syncing;
    notifyListeners();

    final payload = _buildScorePayload();
    try {
      await _api.putScores(payload);
      for (final sent in payload) {
        _dropSentEntriesFor(sent);
      }
      _conflicts = null;
      await _store.savePendingUpdates(_pendingUpdates);
      _syncStatus = _pendingUpdates.isEmpty ? SyncStatus.idle : SyncStatus.pending;
    } on ApiException catch (e) {
      if (e.status == 409 && e.code == 'UPDATE_SCORE_CONFLICT' && e.conflicts != null) {
        _handleConflictResponse(e.conflicts!, payload);
        await _store.savePendingUpdates(_pendingUpdates);
        _syncStatus = _pendingUpdates.isEmpty ? SyncStatus.idle : SyncStatus.pending;
      } else {
        _syncStatus = SyncStatus.error;
      }
    } catch (error) {
      debugPrint("flushPendingScores failed: $error");
      _syncStatus = SyncStatus.error;
    }
    notifyListeners();
  }

  /// Drops every index in [sent] from pendingUpdates whose queued newValue
  /// still matches what was just sent (i.e. it wasn't edited again while
  /// the request was in flight - if it was, leave it pending so the newer
  /// edit gets sent on the next flush).
  void _dropSentEntriesFor(ParticipantScoreUpdates sent) {
    final perParticipant = _pendingUpdates.byParticipant[sent.matchParticipantId];
    if (perParticipant == null) return;
    for (final update in sent.updates) {
      _dropSentEntryFor(sent.matchParticipantId, update.index, update.newValue);
    }
  }

  void _dropSentEntryFor(String matchParticipantId, int index, String? sentNewValue) {
    final perParticipant = _pendingUpdates.byParticipant[matchParticipantId];
    final edit = perParticipant?[index];
    if (edit != null && edit.newValue == sentNewValue) {
      perParticipant!.remove(index);
    }
  }

  void _handleConflictResponse(
      List<ScoreConflictEntry> conflicts, List<ParticipantScoreUpdates> sentPayload) {
    final conflictedParticipantIds = conflicts.map((c) => c.matchParticipantId).toSet();

    // Participants we sent updates for, but that came back with no conflict
    // entry at all, were fully applied.
    for (final sent in sentPayload) {
      if (!conflictedParticipantIds.contains(sent.matchParticipantId)) {
        _dropSentEntriesFor(sent);
      }
    }

    for (final entry in conflicts) {
      if (entry.error == 'PARTICIPANT_CONFLICT') {
        // Leave all pending edits for this participant untouched - the user
        // must explicitly discard them (the participant was reassigned).
        continue;
      }
      // SCORE_CONFLICT: drop the indexes we sent that aren't in this
      // entry's conflict list (those were applied fine); keep the
      // conflicting ones pending until the user resolves them.
      final conflictedIndexes = entry.conflicts.map((c) => c.index).toSet();
      final sentForParticipant = sentPayload.where(
        (s) => s.matchParticipantId == entry.matchParticipantId,
      );
      for (final sent in sentForParticipant) {
        for (final update in sent.updates) {
          if (!conflictedIndexes.contains(update.index)) {
            _dropSentEntryFor(entry.matchParticipantId, update.index, update.newValue);
          }
        }
      }
    }

    _conflicts = conflicts
        .where((c) => c.error == 'PARTICIPANT_CONFLICT' || c.conflicts.isNotEmpty)
        .toList();
    if (_conflicts!.isEmpty) _conflicts = null;
  }

  /// User's resolution of one conflicting arrow: 'mine' retries the local
  /// edit (now that the true current server value is known as `old`);
  /// 'theirs' discards the local edit and accepts [serverValue].
  void resolveScoreConflict(
      String matchParticipantId, int index, String resolution, String? serverValue) {
    final perArrow = _pendingUpdates.byParticipant[matchParticipantId];
    if (resolution == 'theirs') {
      perArrow?.remove(index);
      _setLocalArrow(matchParticipantId, index, serverValue);
    } else {
      final edit = perArrow?[index];
      if (edit != null) {
        edit.old = serverValue;
      }
    }
    _removeResolvedConflictIndex(matchParticipantId, index);
    _store.savePendingUpdates(_pendingUpdates);
    notifyListeners();
    flushPendingScores();
  }

  /// User's resolution of a PARTICIPANT_CONFLICT: discard every unsynced
  /// edit queued for a participant that's been reassigned to another device.
  void discardParticipantConflict(String matchParticipantId) {
    _pendingUpdates.byParticipant.remove(matchParticipantId);
    _conflicts = _conflicts?.where((c) => c.matchParticipantId != matchParticipantId).toList();
    if (_conflicts != null && _conflicts!.isEmpty) _conflicts = null;
    _store.savePendingUpdates(_pendingUpdates);
    notifyListeners();
  }

  void _removeResolvedConflictIndex(String matchParticipantId, int index) {
    final conflicts = _conflicts;
    if (conflicts == null) return;
    final updated = <ScoreConflictEntry>[];
    for (final entry in conflicts) {
      if (entry.matchParticipantId != matchParticipantId) {
        updated.add(entry);
        continue;
      }
      entry.conflicts.removeWhere((c) => c.index == index);
      if (entry.conflicts.isNotEmpty || entry.error == 'PARTICIPANT_CONFLICT') {
        updated.add(entry);
      }
    }
    _conflicts = updated.isEmpty ? null : updated;
  }
}
