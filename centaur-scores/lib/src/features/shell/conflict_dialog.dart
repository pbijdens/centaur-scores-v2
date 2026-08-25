import 'package:flutter/material.dart';

import 'package:centaur_scores/src/i18n/translations.dart';
import 'package:centaur_scores/src/model/scorekeeper_match.dart';
import 'package:centaur_scores/src/model/scorekeeper_score_conflict.dart';
import 'package:centaur_scores/src/repository/repository.dart';

/// Shown by [AppShell] whenever `MatchRepository().conflicts` is non-empty.
/// Rebuilds live as conflicts are resolved and pops itself once none remain.
class ConflictDialog extends StatelessWidget {
  const ConflictDialog({super.key});

  @override
  Widget build(BuildContext context) {
    return ListenableBuilder(
      listenable: MatchRepository(),
      builder: (context, _) {
        final conflicts = MatchRepository().conflicts;
        if (conflicts == null || conflicts.isEmpty) {
          WidgetsBinding.instance.addPostFrameCallback((_) {
            if (Navigator.of(context, rootNavigator: true).canPop()) {
              Navigator.of(context, rootNavigator: true).pop();
            }
          });
          return const SizedBox.shrink();
        }
        final match = MatchRepository().currentMatchOrNull;
        return AlertDialog(
          title: Text(t('conflictTitle')),
          content: SizedBox(
            width: 400,
            child: SingleChildScrollView(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: conflicts
                    .map((entry) => _ConflictEntryView(entry: entry, match: match))
                    .toList(),
              ),
            ),
          ),
        );
      },
    );
  }
}

class _ConflictEntryView extends StatelessWidget {
  final ScoreConflictEntry entry;
  final ScorekeeperMatch? match;

  const _ConflictEntryView({required this.entry, required this.match});

  String get _participantName {
    final participant = match?.participants
        .where((p) => p.matchParticipantId == entry.matchParticipantId)
        .firstOrNull;
    return participant?.name ?? t('unknownParticipant');
  }

  String _keyLabel(String? keyId) {
    if (keyId == null) return '-';
    final key = match?.keyboard.where((k) => k.id == keyId).firstOrNull;
    return key?.label ?? keyId;
  }

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: const EdgeInsets.symmetric(vertical: 6),
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(_participantName, style: Theme.of(context).textTheme.titleSmall),
            const SizedBox(height: 8),
            if (entry.error == 'PARTICIPANT_CONFLICT') ...[
              Text(t('participantConflictBody')),
              const SizedBox(height: 8),
              Align(
                alignment: Alignment.centerRight,
                child: TextButton(
                  onPressed: () =>
                      MatchRepository().discardParticipantConflict(entry.matchParticipantId),
                  child: Text(t('discardChange')),
                ),
              ),
            ] else
              ...entry.conflicts.map((conflict) => Padding(
                    padding: const EdgeInsets.symmetric(vertical: 4),
                    child: Row(
                      children: [
                        Expanded(
                          child: Text(
                              '#${conflict.index + 1}: ${t('serverValue')} ${_keyLabel(conflict.current)}'
                              ' / ${t('myValue')} ${_keyLabel(conflict.newValue)}'),
                        ),
                        TextButton(
                          onPressed: () => MatchRepository().resolveScoreConflict(
                              entry.matchParticipantId,
                              conflict.index,
                              'theirs',
                              conflict.current),
                          child: Text(t('useTheirs')),
                        ),
                        TextButton(
                          onPressed: () => MatchRepository().resolveScoreConflict(
                              entry.matchParticipantId,
                              conflict.index,
                              'mine',
                              conflict.current),
                          child: Text(t('useMine')),
                        ),
                      ],
                    ),
                  )),
          ],
        ),
      ),
    );
  }
}
