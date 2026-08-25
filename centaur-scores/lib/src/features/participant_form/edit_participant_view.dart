import 'package:flutter/material.dart';

import 'package:centaur_scores/src/i18n/translations.dart';
import 'package:centaur_scores/src/model/scorekeeper_participant_update.dart';
import 'package:centaur_scores/src/repository/app_navigator.dart';
import 'package:centaur_scores/src/repository/repository.dart';

import 'participant_form.dart';

class EditParticipantView extends StatelessWidget {
  const EditParticipantView({super.key});

  @override
  Widget build(BuildContext context) {
    final screen = AppNavigator().current;
    final matchParticipantId =
        screen is EditParticipantScreen ? screen.matchParticipantId : null;

    return ListenableBuilder(
      listenable: MatchRepository(),
      builder: (context, _) {
        final match = MatchRepository().currentMatchOrNull;
        final participant = match?.participants
            .where((p) => p.matchParticipantId == matchParticipantId)
            .firstOrNull;
        if (match == null || participant == null) {
          return Center(child: Text(t('unknownParticipant')));
        }

        return ParticipantForm(
          match: match,
          initialFederationNumber: participant.federationNumber,
          initialName: participant.name,
          initialCategoryValues: {
            for (final c in participant.categories)
              if (c.value != null) c.id: c.value!,
          },
          onCancel: () => AppNavigator().goToParent(),
          onSave: (federationNumber, name, categories) async {
            final payload = match.participants
                .map((p) => p.matchParticipantId == matchParticipantId
                    ? ScorekeeperParticipantUpdate.custom(
                        federationNumber: federationNumber,
                        name: name,
                        categories: categories,
                        existingMatchParticipantId: matchParticipantId)
                    : ScorekeeperParticipantUpdate.fromExisting(p))
                .toList();
            final ok = await MatchRepository().submitParticipantsList(payload);
            if (ok) {
              AppNavigator().goToParent();
            } else if (context.mounted) {
              await showDialog<void>(
                context: context,
                builder: (context) => AlertDialog(
                  title: Text(t('saveFailedTitle')),
                  content: Text(t('saveFailedBody')),
                  actions: [
                    TextButton(
                        onPressed: () => Navigator.of(context).pop(), child: Text(t('ok'))),
                  ],
                ),
              );
            }
          },
        );
      },
    );
  }
}
