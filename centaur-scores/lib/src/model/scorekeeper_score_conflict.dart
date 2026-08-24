import 'package:json_annotation/json_annotation.dart';

part 'scorekeeper_score_conflict.g.dart';

@JsonSerializable(explicitToJson: true)
class ScoreConflict {
  late int index;
  String? current;
  String? old;

  @JsonKey(name: 'new')
  String? newValue;

  ScoreConflict();

  factory ScoreConflict.fromJson(Map<String, dynamic> json) =>
      _$ScoreConflictFromJson(json);
  Map<String, dynamic> toJson() => _$ScoreConflictToJson(this);
}

/// One participant's worth of conflict information from a 409
/// `UPDATE_SCORE_CONFLICT` response to `PUT .../scores`.
///
/// [error] is either `"SCORE_CONFLICT"` or `"PARTICIPANT_CONFLICT"` (kept as
/// a raw string, compared with `==` at the point of use).
@JsonSerializable(explicitToJson: true)
class ScoreConflictEntry {
  late String matchParticipantId;
  late String error;
  late List<ScoreConflict> conflicts;

  ScoreConflictEntry();

  factory ScoreConflictEntry.fromJson(Map<String, dynamic> json) =>
      _$ScoreConflictEntryFromJson(json);
  Map<String, dynamic> toJson() => _$ScoreConflictEntryToJson(this);
}
