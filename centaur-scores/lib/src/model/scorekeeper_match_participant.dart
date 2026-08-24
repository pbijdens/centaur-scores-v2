import 'package:json_annotation/json_annotation.dart';

import 'participant_category_value.dart';

part 'scorekeeper_match_participant.g.dart';

@JsonSerializable(explicitToJson: true)
class ScorekeeperMatchParticipant {
  String? federationNumber;
  late String name;
  String? info;
  late List<ParticipantCategoryValue> categories;
  late String matchParticipantId;
  String? tenantParticipantId;

  // Always null until the backend implements per-category key filtering;
  // treated as "all keys available" when null.
  List<String>? availableKeyIDs;

  // Key IDs (or null for a not-yet-shot arrow), length ends*arrowsPerEnd.
  late List<String?> arrowScores;

  ScorekeeperMatchParticipant();

  factory ScorekeeperMatchParticipant.fromJson(Map<String, dynamic> json) =>
      _$ScorekeeperMatchParticipantFromJson(json);
  Map<String, dynamic> toJson() => _$ScorekeeperMatchParticipantToJson(this);
}
