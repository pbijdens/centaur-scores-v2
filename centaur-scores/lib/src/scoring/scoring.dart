import '../model/scorekeeper_key.dart';
import '../model/scorekeeper_match.dart';
import '../model/scorekeeper_match_participant.dart';

/// Pure score-math helpers shared by every screen that displays or edits
/// scores. Ported from the point value/index math in
/// `centaur-scores-mobile-web-scoring/src/lib/scoring.ts`, operating on the
/// new key-ID-based [ScorekeeperMatch]/[ScorekeeperMatchParticipant] models.

/// The point value of a key ID, or 0 for a not-yet-shot (null) arrow or an
/// unrecognized key ID.
int keyValue(ScorekeeperMatch match, String? keyId) {
  if (keyId == null) return 0;
  for (final key in match.keyboard) {
    if (key.id == keyId) return key.value;
  }
  return 0;
}

int arrowsShot(ScorekeeperMatchParticipant participant) {
  return participant.arrowScores.where((a) => a != null).length;
}

int totalScore(ScorekeeperMatch match, ScorekeeperMatchParticipant participant) {
  var sum = 0;
  for (final arrow in participant.arrowScores) {
    sum += keyValue(match, arrow);
  }
  return sum;
}

/// Arrow scores for one end (0-based), sliced from the flat [arrowScores]
/// array - there is no separate "end" model, ends are just
/// [arrowsPerEnd]-sized slices of the flat array.
List<String?> endArrows(ScorekeeperMatch match,
    ScorekeeperMatchParticipant participant, int endIndex) {
  final start = endIndex * match.arrowsPerEnd;
  final end = start + match.arrowsPerEnd;
  return participant.arrowScores.sublist(
    start.clamp(0, participant.arrowScores.length),
    end.clamp(0, participant.arrowScores.length),
  );
}

int endTotal(ScorekeeperMatch match, ScorekeeperMatchParticipant participant,
    int endIndex) {
  var sum = 0;
  for (final arrow in endArrows(match, participant, endIndex)) {
    sum += keyValue(match, arrow);
  }
  return sum;
}

/// Cumulative total through (and including) the given end.
int runningTotalThroughEnd(ScorekeeperMatch match,
    ScorekeeperMatchParticipant participant, int endIndex) {
  var sum = 0;
  for (var i = 0; i <= endIndex; i++) {
    sum += endTotal(match, participant, i);
  }
  return sum;
}

/// Cumulative total within the current [ScorekeeperMatch.groupEnds] group
/// only (e.g. ends 0-9 of a 20-end match with groupEnds=10), falling back to
/// the full running total when no grouping is configured.
int groupRunningTotal(ScorekeeperMatch match,
    ScorekeeperMatchParticipant participant, int endIndex) {
  final groupEnds = match.groupEnds;
  if (groupEnds == null || groupEnds <= 0) {
    return runningTotalThroughEnd(match, participant, endIndex);
  }
  final groupStart = (endIndex ~/ groupEnds) * groupEnds;
  var sum = 0;
  for (var i = groupStart; i <= endIndex; i++) {
    sum += endTotal(match, participant, i);
  }
  return sum;
}

/// Splits the participant's ends into `groupEnds`-sized chunks and returns
/// each chunk's total, e.g. a 20-end match with groupEnds=10 and 27/end
/// returns `[270, 270]`.
List<int> splitScores(
    ScorekeeperMatch match, ScorekeeperMatchParticipant participant) {
  final groupEnds = match.groupEnds;
  if (groupEnds == null || groupEnds <= 0) return [];
  final splits = <int>[];
  for (var groupStart = 0; groupStart < match.ends; groupStart += groupEnds) {
    var sum = 0;
    final groupEnd = (groupStart + groupEnds).clamp(0, match.ends);
    for (var i = groupStart; i < groupEnd; i++) {
      sum += endTotal(match, participant, i);
    }
    splits.add(sum);
  }
  return splits;
}

/// The keys usable for this participant: [ScorekeeperMatch.keyboard]
/// filtered by [ScorekeeperMatchParticipant.availableKeyIDs] when set, else
/// every key (the server always returns a null [availableKeyIDs] today).
List<ScorekeeperKey> availableKeys(
    ScorekeeperMatch match, ScorekeeperMatchParticipant participant) {
  final ids = participant.availableKeyIDs;
  if (ids == null) return match.keyboard;
  return match.keyboard.where((k) => ids.contains(k.id)).toList();
}

/// Local index (0..arrowsPerEnd-1) of the first null arrow within [endIndex],
/// or null if the end is fully scored.
int? firstNullIndexInEnd(ScorekeeperMatch match,
    ScorekeeperMatchParticipant participant, int endIndex) {
  final arrows = endArrows(match, participant, endIndex);
  for (var i = 0; i < arrows.length; i++) {
    if (arrows[i] == null) return i;
  }
  return null;
}

/// Flat index of the first null arrow across the whole match for this
/// participant, or null if fully scored.
int? firstNullIndex(ScorekeeperMatchParticipant participant) {
  for (var i = 0; i < participant.arrowScores.length; i++) {
    if (participant.arrowScores[i] == null) return i;
  }
  return null;
}

/// The first participant (in list order) with at least one unscored arrow,
/// or the first participant if everyone is fully scored, or null if there
/// are no participants.
ScorekeeperMatchParticipant? firstParticipantNeedingScore(
    ScorekeeperMatch match) {
  if (match.participants.isEmpty) return null;
  for (final participant in match.participants) {
    if (firstNullIndex(participant) != null) return participant;
  }
  return match.participants.first;
}
