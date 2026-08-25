// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'scorekeeper_score_update.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

ScoreUpdate _$ScoreUpdateFromJson(Map<String, dynamic> json) => ScoreUpdate()
  ..index = (json['index'] as num).toInt()
  ..old = json['old'] as String?
  ..newValue = json['new'] as String?;

Map<String, dynamic> _$ScoreUpdateToJson(ScoreUpdate instance) =>
    <String, dynamic>{
      'index': instance.index,
      'old': instance.old,
      'new': instance.newValue,
    };

ParticipantScoreUpdates _$ParticipantScoreUpdatesFromJson(
        Map<String, dynamic> json) =>
    ParticipantScoreUpdates()
      ..matchParticipantId = json['matchParticipantId'] as String
      ..updates = (json['updates'] as List<dynamic>)
          .map((e) => ScoreUpdate.fromJson(e as Map<String, dynamic>))
          .toList();

Map<String, dynamic> _$ParticipantScoreUpdatesToJson(
        ParticipantScoreUpdates instance) =>
    <String, dynamic>{
      'matchParticipantId': instance.matchParticipantId,
      'updates': instance.updates.map((e) => e.toJson()).toList(),
    };
