/// A queued, not-yet-synced arrow edit.
///
/// [old] is the value that was recorded on the server the last time this
/// device knew about it (captured once, on the *first* unsynced edit to a
/// given arrow, and preserved across further local edits to that same
/// arrow) - this is what lets the server detect a genuine conflict instead
/// of rejecting a redundant local overwrite.
class ScoreEdit {
  String? old;
  String? newValue;

  ScoreEdit({this.old, this.newValue});

  factory ScoreEdit.fromJson(Map<String, dynamic> json) => ScoreEdit(
        old: json['old'] as String?,
        newValue: json['newValue'] as String?,
      );

  Map<String, dynamic> toJson() => {'old': old, 'newValue': newValue};
}

/// Locally queued, not-yet-synced score edits, keyed by matchParticipantId
/// and then by flat arrow index.
///
/// Hand-written (de)serialization because JSON object keys are always
/// strings - the arrow index needs an explicit int parse on the way back in.
class PendingUpdates {
  final Map<String, Map<int, ScoreEdit>> byParticipant;

  PendingUpdates({Map<String, Map<int, ScoreEdit>>? byParticipant})
      : byParticipant = byParticipant ?? {};

  factory PendingUpdates.fromJson(Map<String, dynamic> json) {
    final result = <String, Map<int, ScoreEdit>>{};
    for (final participantEntry in json.entries) {
      final perArrow = <int, ScoreEdit>{};
      final rawPerArrow = participantEntry.value as Map<String, dynamic>;
      for (final arrowEntry in rawPerArrow.entries) {
        perArrow[int.parse(arrowEntry.key)] =
            ScoreEdit.fromJson(arrowEntry.value as Map<String, dynamic>);
      }
      result[participantEntry.key] = perArrow;
    }
    return PendingUpdates(byParticipant: result);
  }

  Map<String, dynamic> toJson() {
    final result = <String, dynamic>{};
    for (final participantEntry in byParticipant.entries) {
      final perArrow = <String, dynamic>{};
      for (final arrowEntry in participantEntry.value.entries) {
        perArrow[arrowEntry.key.toString()] = arrowEntry.value.toJson();
      }
      result[participantEntry.key] = perArrow;
    }
    return result;
  }

  bool get isEmpty => byParticipant.values.every((m) => m.isEmpty);
  bool get isNotEmpty => !isEmpty;
}
