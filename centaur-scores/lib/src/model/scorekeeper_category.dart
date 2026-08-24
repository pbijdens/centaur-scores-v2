import 'package:json_annotation/json_annotation.dart';

part 'scorekeeper_category.g.dart';

@JsonSerializable(explicitToJson: true)
class ScorekeeperCategoryValue {
  late int id;
  late String name;

  ScorekeeperCategoryValue();

  factory ScorekeeperCategoryValue.fromJson(Map<String, dynamic> json) =>
      _$ScorekeeperCategoryValueFromJson(json);
  Map<String, dynamic> toJson() => _$ScorekeeperCategoryValueToJson(this);
}

@JsonSerializable(explicitToJson: true)
class ScorekeeperCategory {
  late String id;
  late String name;
  late List<ScorekeeperCategoryValue> values;

  ScorekeeperCategory();

  factory ScorekeeperCategory.fromJson(Map<String, dynamic> json) =>
      _$ScorekeeperCategoryFromJson(json);
  Map<String, dynamic> toJson() => _$ScorekeeperCategoryToJson(this);
}
