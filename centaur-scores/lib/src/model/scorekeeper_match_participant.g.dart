// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'scorekeeper_match_participant.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

ScorekeeperMatchParticipant _$ScorekeeperMatchParticipantFromJson(
        Map<String, dynamic> json) =>
    ScorekeeperMatchParticipant()
      ..federationNumber = json['federationNumber'] as String?
      ..name = json['name'] as String
      ..info = json['info'] as String?
      ..categories = (json['categories'] as List<dynamic>)
          .map((e) =>
              ParticipantCategoryValue.fromJson(e as Map<String, dynamic>))
          .toList()
      ..matchParticipantId = json['matchParticipantId'] as String
      ..tenantParticipantId = json['tenantParticipantId'] as String?
      ..availableKeyIDs = (json['availableKeyIDs'] as List<dynamic>?)
          ?.map((e) => e as String)
          .toList()
      ..arrowScores = (json['arrowScores'] as List<dynamic>)
          .map((e) => e as String?)
          .toList()
      ..signed = json['signed'] as bool? ?? false
      ..signedAtUtc = json['signedAtUtc'] as String?
      ..archerSignatureDataUrl = json['archerSignatureDataUrl'] as String?
      ..markerSignatureDataUrl = json['markerSignatureDataUrl'] as String?;

Map<String, dynamic> _$ScorekeeperMatchParticipantToJson(
        ScorekeeperMatchParticipant instance) =>
    <String, dynamic>{
      'federationNumber': instance.federationNumber,
      'name': instance.name,
      'info': instance.info,
      'categories': instance.categories.map((e) => e.toJson()).toList(),
      'matchParticipantId': instance.matchParticipantId,
      'tenantParticipantId': instance.tenantParticipantId,
      'availableKeyIDs': instance.availableKeyIDs,
      'arrowScores': instance.arrowScores,
      'signed': instance.signed,
      'signedAtUtc': instance.signedAtUtc,
      'archerSignatureDataUrl': instance.archerSignatureDataUrl,
      'markerSignatureDataUrl': instance.markerSignatureDataUrl,
    };
