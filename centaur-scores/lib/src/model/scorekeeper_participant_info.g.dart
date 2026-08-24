// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'scorekeeper_participant_info.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

ScorekeeperParticipantInfo _$ScorekeeperParticipantInfoFromJson(
        Map<String, dynamic> json) =>
    ScorekeeperParticipantInfo()
      ..matchParticipantId = json['matchParticipantId'] as String?
      ..tenantParticipantId = json['tenantParticipantId'] as String?
      ..federationNumber = json['federationNumber'] as String?
      ..name = json['name'] as String
      ..info = json['info'] as String?
      ..categories = (json['categories'] as List<dynamic>)
          .map((e) =>
              ParticipantCategoryValue.fromJson(e as Map<String, dynamic>))
          .toList();

Map<String, dynamic> _$ScorekeeperParticipantInfoToJson(
        ScorekeeperParticipantInfo instance) =>
    <String, dynamic>{
      'matchParticipantId': instance.matchParticipantId,
      'tenantParticipantId': instance.tenantParticipantId,
      'federationNumber': instance.federationNumber,
      'name': instance.name,
      'info': instance.info,
      'categories': instance.categories.map((e) => e.toJson()).toList(),
    };
