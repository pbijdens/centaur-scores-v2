import 'package:json_annotation/json_annotation.dart';

import 'scorekeeper_category.dart';
import 'scorekeeper_key.dart';
import 'scorekeeper_match_participant.dart';

part 'scorekeeper_match.g.dart';

/// Response of `GET /scorekeeper/{tenantId}/{matchId}/{deviceId}`.
///
/// Not itself dirty-tracked - unsynced local edits are tracked separately
/// (see `PendingUpdates`) so that a background poll can merge server-side
/// changes without ever clobbering an edit that hasn't been pushed yet.
@JsonSerializable(explicitToJson: true)
class ScorekeeperMatch {
  late String device;
  late String match;
  late int ends;
  late int arrowsPerEnd;
  int? groupEnds;
  late List<ScorekeeperCategory> categories;
  late bool allowModifyParticipants;
  late bool allowCustomParticipants;
  late List<ScorekeeperKey> keyboard;
  late List<ScorekeeperMatchParticipant> participants;

  ScorekeeperMatch();

  factory ScorekeeperMatch.fromJson(Map<String, dynamic> json) =>
      _$ScorekeeperMatchFromJson(json);
  Map<String, dynamic> toJson() => _$ScorekeeperMatchToJson(this);
}
