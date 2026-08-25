import 'package:json_annotation/json_annotation.dart';

part 'participant_category_value.g.dart';

/// A participant's chosen value for one of the match's categories.
///
/// [id] is the category's id (matches [ScorekeeperCategory.id]).
/// [value] is the chosen value's *name* string (e.g. "Barebow"), never the
/// value's numeric id - that's how the server represents and expects it.
@JsonSerializable(explicitToJson: true)
class ParticipantCategoryValue {
  late String id;
  late String name;
  String? value;

  ParticipantCategoryValue();
  ParticipantCategoryValue.create(this.id, this.name, this.value);

  factory ParticipantCategoryValue.fromJson(Map<String, dynamic> json) =>
      _$ParticipantCategoryValueFromJson(json);
  Map<String, dynamic> toJson() => _$ParticipantCategoryValueToJson(this);
}
