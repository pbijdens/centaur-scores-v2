import 'package:flutter_test/flutter_test.dart';
import 'package:centaur_scores/src/model/scorekeeper_key.dart';
import 'package:centaur_scores/src/model/scorekeeper_match.dart';
import 'package:centaur_scores/src/model/scorekeeper_match_participant.dart';
import 'package:centaur_scores/src/scoring/scoring.dart' as scoring;

ScorekeeperKey _key(String id, String label, int value, String color) {
  final key = ScorekeeperKey();
  key.id = id;
  key.label = label;
  key.value = value;
  key.color = color;
  return key;
}

ScorekeeperMatch _buildMatch({int ends = 4, int arrowsPerEnd = 3, int? groupEnds}) {
  final match = ScorekeeperMatch();
  match.device = 'Test device';
  match.match = 'Test match';
  match.ends = ends;
  match.arrowsPerEnd = arrowsPerEnd;
  match.groupEnds = groupEnds;
  match.categories = [];
  match.allowModifyParticipants = true;
  match.allowCustomParticipants = true;
  match.keyboard = [
    _key('X', 'X', 10, 'Yellow'),
    _key('10', '10', 10, 'Yellow'),
    _key('9', '9', 9, 'Yellow'),
    _key('7', '7', 7, 'Red'),
    _key('M', 'M', 0, 'White'),
  ];
  match.participants = [];
  return match;
}

ScorekeeperMatchParticipant _buildParticipant(String id, List<String?> arrowScores,
    {List<String>? availableKeyIDs}) {
  final p = ScorekeeperMatchParticipant();
  p.name = 'Participant $id';
  p.categories = [];
  p.matchParticipantId = id;
  p.availableKeyIDs = availableKeyIDs;
  p.arrowScores = arrowScores;
  return p;
}

void main() {
  group('scoring', () {
    test('keyValue looks up point values, null/unknown -> 0', () {
      final match = _buildMatch();
      expect(scoring.keyValue(match, 'X'), 10);
      expect(scoring.keyValue(match, '7'), 7);
      expect(scoring.keyValue(match, null), 0);
      expect(scoring.keyValue(match, 'unknown-key'), 0);
    });

    test('arrowsShot counts non-null arrows', () {
      final p = _buildParticipant('p1', ['X', '9', null, '7', null, null]);
      expect(scoring.arrowsShot(p), 3);
    });

    test('totalScore sums point values across every arrow', () {
      final match = _buildMatch(ends: 2, arrowsPerEnd: 3);
      final p = _buildParticipant('p1', ['X', '9', null, '7', 'M', null]);
      expect(scoring.totalScore(match, p), 10 + 9 + 7 + 0);
    });

    test('endArrows slices the flat array per end', () {
      final match = _buildMatch(ends: 2, arrowsPerEnd: 3);
      final p = _buildParticipant('p1', ['X', '9', '7', 'M', null, '10']);
      expect(scoring.endArrows(match, p, 0), ['X', '9', '7']);
      expect(scoring.endArrows(match, p, 1), ['M', null, '10']);
    });

    test('endTotal and runningTotalThroughEnd treat null arrows as 0', () {
      final match = _buildMatch(ends: 2, arrowsPerEnd: 3);
      final p = _buildParticipant('p1', ['X', '9', null, '7', 'M', '10']);
      expect(scoring.endTotal(match, p, 0), 19); // 10 + 9 + 0
      expect(scoring.endTotal(match, p, 1), 17); // 7 + 0 + 10
      expect(scoring.runningTotalThroughEnd(match, p, 0), 19);
      expect(scoring.runningTotalThroughEnd(match, p, 1), 36);
    });

    test('splitScores chunks ends into groupEnds-sized totals', () {
      // 4 ends, groupEnds=2, every end scores 10+9+7=26 -> two splits of 52.
      final match = _buildMatch(ends: 4, arrowsPerEnd: 3, groupEnds: 2);
      final arrows = List.generate(4, (_) => ['X', '9', '7']).expand((e) => e).toList();
      final p = _buildParticipant('p1', arrows);
      expect(scoring.splitScores(match, p), [52, 52]);
    });

    test('splitScores is empty when groupEnds is not configured', () {
      final match = _buildMatch();
      final p = _buildParticipant('p1', List.filled(12, null));
      expect(scoring.splitScores(match, p), isEmpty);
    });

    test('groupRunningTotal resets at each group boundary', () {
      final match = _buildMatch(ends: 4, arrowsPerEnd: 3, groupEnds: 2);
      final arrows = List.generate(4, (_) => ['X', '9', '7']).expand((e) => e).toList();
      final p = _buildParticipant('p1', arrows);
      expect(scoring.groupRunningTotal(match, p, 0), 26);
      expect(scoring.groupRunningTotal(match, p, 1), 52);
      expect(scoring.groupRunningTotal(match, p, 2), 26); // new group starts
      expect(scoring.groupRunningTotal(match, p, 3), 52);
    });

    test('availableKeys filters by availableKeyIDs, null means all', () {
      final match = _buildMatch();
      final unrestricted = _buildParticipant('p1', []);
      expect(scoring.availableKeys(match, unrestricted).length, 5);

      final restricted = _buildParticipant('p2', [], availableKeyIDs: ['X', 'M']);
      expect(scoring.availableKeys(match, restricted).map((k) => k.id), ['X', 'M']);
    });

    test('firstNullIndexInEnd / firstNullIndex find the first unscored arrow', () {
      final match = _buildMatch(ends: 2, arrowsPerEnd: 3);
      final p = _buildParticipant('p1', ['X', null, '7', null, null, null]);
      expect(scoring.firstNullIndexInEnd(match, p, 0), 1);
      expect(scoring.firstNullIndex(p), 1);

      final full = _buildParticipant('p2', ['X', '9', '7']);
      expect(scoring.firstNullIndexInEnd(match, full, 0), isNull);
      expect(scoring.firstNullIndex(full), isNull);
    });

    test('firstParticipantNeedingScore prefers an unfinished participant, else the first', () {
      final match = _buildMatch(ends: 1, arrowsPerEnd: 2);
      final done = _buildParticipant('done', ['X', '9']);
      final notDone = _buildParticipant('notDone', ['X', null]);
      match.participants = [done, notDone];
      expect(scoring.firstParticipantNeedingScore(match)?.matchParticipantId, 'notDone');

      match.participants = [done];
      expect(scoring.firstParticipantNeedingScore(match)?.matchParticipantId, 'done');

      match.participants = [];
      expect(scoring.firstParticipantNeedingScore(match), isNull);
    });
  });
}
