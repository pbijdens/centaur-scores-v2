// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'scorekeeper_category.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

ScorekeeperCategoryValue _$ScorekeeperCategoryValueFromJson(
        Map<String, dynamic> json) =>
    ScorekeeperCategoryValue()
      ..id = (json['id'] as num).toInt()
      ..name = json['name'] as String;

Map<String, dynamic> _$ScorekeeperCategoryValueToJson(
        ScorekeeperCategoryValue instance) =>
    <String, dynamic>{
      'id': instance.id,
      'name': instance.name,
    };

ScorekeeperCategory _$ScorekeeperCategoryFromJson(Map<String, dynamic> json) =>
    ScorekeeperCategory()
      ..id = json['id'] as String
      ..name = json['name'] as String
      ..values = (json['values'] as List<dynamic>)
          .map((e) =>
              ScorekeeperCategoryValue.fromJson(e as Map<String, dynamic>))
          .toList();

Map<String, dynamic> _$ScorekeeperCategoryToJson(
        ScorekeeperCategory instance) =>
    <String, dynamic>{
      'id': instance.id,
      'name': instance.name,
      'values': instance.values.map((e) => e.toJson()).toList(),
    };
