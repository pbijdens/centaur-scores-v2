// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'scorekeeper_participant_options.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

ScorekeeperParticipantOptions _$ScorekeeperParticipantOptionsFromJson(
        Map<String, dynamic> json) =>
    ScorekeeperParticipantOptions()
      ..unassigned = (json['unassigned'] as List<dynamic>)
          .map((e) =>
              ScorekeeperParticipantInfo.fromJson(e as Map<String, dynamic>))
          .toList()
      ..assigned = (json['assigned'] as List<dynamic>)
          .map((e) =>
              ScorekeeperParticipantInfo.fromJson(e as Map<String, dynamic>))
          .toList()
      ..potential = (json['potential'] as List<dynamic>)
          .map((e) =>
              ScorekeeperParticipantInfo.fromJson(e as Map<String, dynamic>))
          .toList();

Map<String, dynamic> _$ScorekeeperParticipantOptionsToJson(
        ScorekeeperParticipantOptions instance) =>
    <String, dynamic>{
      'unassigned': instance.unassigned.map((e) => e.toJson()).toList(),
      'assigned': instance.assigned.map((e) => e.toJson()).toList(),
      'potential': instance.potential.map((e) => e.toJson()).toList(),
    };
