// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'settings_model.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

SettingsModel _$SettingsModelFromJson(Map<String, dynamic> json) =>
    SettingsModel()
      ..apiBaseUrl = json['apiBaseUrl'] as String?
      ..language = json['language'] as String? ?? 'NL';

Map<String, dynamic> _$SettingsModelToJson(SettingsModel instance) =>
    <String, dynamic>{
      'apiBaseUrl': instance.apiBaseUrl,
      'language': instance.language,
    };
