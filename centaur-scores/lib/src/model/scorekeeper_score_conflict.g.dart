// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'scorekeeper_score_conflict.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

ScoreConflict _$ScoreConflictFromJson(Map<String, dynamic> json) =>
    ScoreConflict()
      ..index = (json['index'] as num).toInt()
      ..current = json['current'] as String?
      ..old = json['old'] as String?
      ..newValue = json['new'] as String?;

Map<String, dynamic> _$ScoreConflictToJson(ScoreConflict instance) =>
    <String, dynamic>{
      'index': instance.index,
      'current': instance.current,
      'old': instance.old,
      'new': instance.newValue,
    };

ScoreConflictEntry _$ScoreConflictEntryFromJson(Map<String, dynamic> json) =>
    ScoreConflictEntry()
      ..matchParticipantId = json['matchParticipantId'] as String
      ..error = json['error'] as String
      ..conflicts = (json['conflicts'] as List<dynamic>)
          .map((e) => ScoreConflict.fromJson(e as Map<String, dynamic>))
          .toList();

Map<String, dynamic> _$ScoreConflictEntryToJson(ScoreConflictEntry instance) =>
    <String, dynamic>{
      'matchParticipantId': instance.matchParticipantId,
      'error': instance.error,
      'conflicts': instance.conflicts.map((e) => e.toJson()).toList(),
    };
