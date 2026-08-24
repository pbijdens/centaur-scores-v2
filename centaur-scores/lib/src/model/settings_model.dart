import 'package:json_annotation/json_annotation.dart';

part 'settings_model.g.dart';

@JsonSerializable(explicitToJson: true)
class SettingsModel {
  // Null means "not paired yet" - this is the signal the QR-scan gate keys
  // off of. Never auto-seed a default here.
  String? apiBaseUrl;

  @JsonKey(defaultValue: 'NL')
  String language = 'NL';

  SettingsModel();
  SettingsModel.create(this.apiBaseUrl, this.language);

  factory SettingsModel.fromJson(Map<String, dynamic> json) =>
      _$SettingsModelFromJson(json);
  Map<String, dynamic> toJson() => _$SettingsModelToJson(this);
}
