// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'scorekeeper_key.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

ScorekeeperKey _$ScorekeeperKeyFromJson(Map<String, dynamic> json) =>
    ScorekeeperKey()
      ..id = json['id'] as String
      ..label = json['label'] as String
      ..value = (json['value'] as num).toInt()
      ..color = json['color'] as String;

Map<String, dynamic> _$ScorekeeperKeyToJson(ScorekeeperKey instance) =>
    <String, dynamic>{
      'id': instance.id,
      'label': instance.label,
      'value': instance.value,
      'color': instance.color,
    };
