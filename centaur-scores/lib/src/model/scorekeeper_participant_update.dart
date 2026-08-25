import 'package:json_annotation/json_annotation.dart';

import 'participant_category_value.dart';
import 'scorekeeper_match_participant.dart';
import 'scorekeeper_participant_info.dart';

part 'scorekeeper_participant_update.g.dart';

/// One entry of a `PUT /scorekeeper/{tenantId}/{matchId}/{deviceId}/participants`
/// request body.
///
/// Field-nulling rules per PUBLIC-API-DESIGN.md: when [tenantParticipantId]
/// is set, [federationNumber]/[name]/[categories]/[matchParticipantId] are
/// ignored server-side and must be null; otherwise [matchParticipantId]
/// identifies an existing match-local participant to update (or is null to
/// create a new one), and the server rejects the change with
/// PARTICIPANT_UPDATE_NOT_ALLOWED unless the participant is genuinely
/// match-local. `availableKeyIDs`/`arrowScores` are always sent as null -
/// the server ignores them, but the reference client sends them explicitly.
@JsonSerializable(explicitToJson: true)
class ScorekeeperParticipantUpdate {
  String? federationNumber;
  String? name;
  String? info;
  List<ParticipantCategoryValue>? categories;
  String? matchParticipantId;
  String? tenantParticipantId;
  @JsonKey(includeFromJson: false)
  final List<String>? availableKeyIDs = null;
  @JsonKey(includeFromJson: false)
  final List<String?>? arrowScores = null;

  ScorekeeperParticipantUpdate();

  /// Re-send an existing device participant unchanged (used when building the
  /// full replacement list for a PUT after adding/editing/removing one entry).
  factory ScorekeeperParticipantUpdate.fromExisting(
      ScorekeeperMatchParticipant participant) {
    final update = ScorekeeperParticipantUpdate();
    if (participant.tenantParticipantId != null) {
      update.tenantParticipantId = participant.tenantParticipantId;
    } else {
      update.matchParticipantId = participant.matchParticipantId;
      update.federationNumber = participant.federationNumber;
      update.name = participant.name;
      update.categories = participant.categories;
    }
    return update;
  }

  /// Add a participant chosen from `GET .../participant-options`.
  factory ScorekeeperParticipantUpdate.fromOption(
      ScorekeeperParticipantInfo option) {
    final update = ScorekeeperParticipantUpdate();
    if (option.tenantParticipantId != null) {
      update.tenantParticipantId = option.tenantParticipantId;
    } else {
      update.matchParticipantId = option.matchParticipantId;
      update.federationNumber = option.federationNumber;
      update.name = option.name;
      update.categories = option.categories;
    }
    return update;
  }

  /// Create or edit an unlisted/custom match-local participant.
  factory ScorekeeperParticipantUpdate.custom({
    String? federationNumber,
    required String name,
    required List<ParticipantCategoryValue> categories,
    String? existingMatchParticipantId,
  }) {
    final update = ScorekeeperParticipantUpdate();
    update.matchParticipantId = existingMatchParticipantId;
    update.federationNumber = federationNumber;
    update.name = name;
    update.categories = categories;
    return update;
  }

  factory ScorekeeperParticipantUpdate.fromJson(Map<String, dynamic> json) =>
      _$ScorekeeperParticipantUpdateFromJson(json);
  Map<String, dynamic> toJson() => _$ScorekeeperParticipantUpdateToJson(this);
}
