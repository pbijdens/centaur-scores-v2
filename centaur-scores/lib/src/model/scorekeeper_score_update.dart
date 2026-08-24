import 'package:json_annotation/json_annotation.dart';

part 'scorekeeper_score_update.g.dart';

@JsonSerializable(explicitToJson: true)
class ScoreUpdate {
  late int index;
  String? old;

  @JsonKey(name: 'new')
  String? newValue;

  ScoreUpdate();
  ScoreUpdate.create(this.index, this.old, this.newValue);

  factory ScoreUpdate.fromJson(Map<String, dynamic> json) =>
      _$ScoreUpdateFromJson(json);
  Map<String, dynamic> toJson() => _$ScoreUpdateToJson(this);
}

@JsonSerializable(explicitToJson: true)
class ParticipantScoreUpdates {
  late String matchParticipantId;
  late List<ScoreUpdate> updates;

  ParticipantScoreUpdates();
  ParticipantScoreUpdates.create(this.matchParticipantId, this.updates);

  factory ParticipantScoreUpdates.fromJson(Map<String, dynamic> json) =>
      _$ParticipantScoreUpdatesFromJson(json);
  Map<String, dynamic> toJson() => _$ParticipantScoreUpdatesToJson(this);
}
