import 'package:json_annotation/json_annotation.dart';

part 'scorekeeper_key.g.dart';

@JsonSerializable(explicitToJson: true)
class ScorekeeperKey {
  late String id;
  late String label;
  late int value;
  // Kept as a raw string (not an enum) so an unrecognized color from the
  // server degrades gracefully instead of failing to parse.
  late String color;

  ScorekeeperKey();

  factory ScorekeeperKey.fromJson(Map<String, dynamic> json) =>
      _$ScorekeeperKeyFromJson(json);
  Map<String, dynamic> toJson() => _$ScorekeeperKeyToJson(this);
}
