// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'participant_category_value.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

ParticipantCategoryValue _$ParticipantCategoryValueFromJson(
        Map<String, dynamic> json) =>
    ParticipantCategoryValue()
      ..id = json['id'] as String
      ..name = json['name'] as String
      ..value = json['value'] as String?;

Map<String, dynamic> _$ParticipantCategoryValueToJson(
        ParticipantCategoryValue instance) =>
    <String, dynamic>{
      'id': instance.id,
      'name': instance.name,
      'value': instance.value,
    };
