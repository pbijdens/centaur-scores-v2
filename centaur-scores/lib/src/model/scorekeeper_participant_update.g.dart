// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'scorekeeper_participant_update.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

ScorekeeperParticipantUpdate _$ScorekeeperParticipantUpdateFromJson(
        Map<String, dynamic> json) =>
    ScorekeeperParticipantUpdate()
      ..federationNumber = json['federationNumber'] as String?
      ..name = json['name'] as String?
      ..info = json['info'] as String?
      ..categories = (json['categories'] as List<dynamic>?)
          ?.map((e) =>
              ParticipantCategoryValue.fromJson(e as Map<String, dynamic>))
          .toList()
      ..matchParticipantId = json['matchParticipantId'] as String?
      ..tenantParticipantId = json['tenantParticipantId'] as String?;

Map<String, dynamic> _$ScorekeeperParticipantUpdateToJson(
        ScorekeeperParticipantUpdate instance) =>
    <String, dynamic>{
      'federationNumber': instance.federationNumber,
      'name': instance.name,
      'info': instance.info,
      'categories': instance.categories?.map((e) => e.toJson()).toList(),
      'matchParticipantId': instance.matchParticipantId,
      'tenantParticipantId': instance.tenantParticipantId,
    };
