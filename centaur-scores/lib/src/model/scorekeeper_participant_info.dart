import 'package:json_annotation/json_annotation.dart';

import 'participant_category_value.dart';

part 'scorekeeper_participant_info.g.dart';

@JsonSerializable(explicitToJson: true)
class ScorekeeperParticipantInfo {
  // Null only for items in the "potential" list.
  String? matchParticipantId;
  String? tenantParticipantId;
  String? federationNumber;
  late String name;
  String? info;
  late List<ParticipantCategoryValue> categories;

  ScorekeeperParticipantInfo();

  factory ScorekeeperParticipantInfo.fromJson(Map<String, dynamic> json) =>
      _$ScorekeeperParticipantInfoFromJson(json);
  Map<String, dynamic> toJson() => _$ScorekeeperParticipantInfoToJson(this);
}
