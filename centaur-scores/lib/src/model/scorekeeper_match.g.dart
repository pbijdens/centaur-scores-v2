// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'scorekeeper_match.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

ScorekeeperMatch _$ScorekeeperMatchFromJson(Map<String, dynamic> json) =>
    ScorekeeperMatch()
      ..device = json['device'] as String
      ..match = json['match'] as String
      ..ends = (json['ends'] as num).toInt()
      ..arrowsPerEnd = (json['arrowsPerEnd'] as num).toInt()
      ..groupEnds = (json['groupEnds'] as num?)?.toInt()
      ..categories = (json['categories'] as List<dynamic>)
          .map((e) => ScorekeeperCategory.fromJson(e as Map<String, dynamic>))
          .toList()
      ..allowModifyParticipants = json['allowModifyParticipants'] as bool
      ..allowCustomParticipants = json['allowCustomParticipants'] as bool
      ..keyboard = (json['keyboard'] as List<dynamic>)
          .map((e) => ScorekeeperKey.fromJson(e as Map<String, dynamic>))
          .toList()
      ..participants = (json['participants'] as List<dynamic>)
          .map((e) =>
              ScorekeeperMatchParticipant.fromJson(e as Map<String, dynamic>))
          .toList();

Map<String, dynamic> _$ScorekeeperMatchToJson(ScorekeeperMatch instance) =>
    <String, dynamic>{
      'device': instance.device,
      'match': instance.match,
      'ends': instance.ends,
      'arrowsPerEnd': instance.arrowsPerEnd,
      'groupEnds': instance.groupEnds,
      'categories': instance.categories.map((e) => e.toJson()).toList(),
      'allowModifyParticipants': instance.allowModifyParticipants,
      'allowCustomParticipants': instance.allowCustomParticipants,
      'keyboard': instance.keyboard.map((e) => e.toJson()).toList(),
      'participants': instance.participants.map((e) => e.toJson()).toList(),
    };
