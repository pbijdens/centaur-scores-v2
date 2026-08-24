import 'package:json_annotation/json_annotation.dart';

import 'scorekeeper_participant_info.dart';

part 'scorekeeper_participant_options.g.dart';

@JsonSerializable(explicitToJson: true)
class ScorekeeperParticipantOptions {
  late List<ScorekeeperParticipantInfo> unassigned;
  late List<ScorekeeperParticipantInfo> assigned;
  late List<ScorekeeperParticipantInfo> potential;

  ScorekeeperParticipantOptions();

  factory ScorekeeperParticipantOptions.fromJson(Map<String, dynamic> json) =>
      _$ScorekeeperParticipantOptionsFromJson(json);
  Map<String, dynamic> toJson() => _$ScorekeeperParticipantOptionsToJson(this);
}
