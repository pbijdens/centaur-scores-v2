import 'package:flutter/material.dart';

import 'package:centaur_scores/src/i18n/translations.dart';
import 'package:centaur_scores/src/repository/app_navigator.dart';
import 'package:centaur_scores/src/repository/repository.dart';
import 'package:centaur_scores/src/style/style_helper.dart';

import 'participant_tile.dart';

class HomeView extends StatelessWidget {
  const HomeView({super.key});

  @override
  Widget build(BuildContext context) {
    return ListenableBuilder(
      listenable: MatchRepository(),
      builder: (context, _) {
        final match = MatchRepository().currentMatchOrNull;
        if (match == null) {
          return const Center(child: CircularProgressIndicator());
        }
        if (match.participants.isEmpty && !match.allowModifyParticipants) {
          return Center(
              child: Text(t('noParticipantsYet'), style: StyleHelper.baseTextStyle(context)));
        }
        final scale = StyleHelper.scale(context);
        return ListView.separated(
          itemCount: match.participants.length + (match.allowModifyParticipants ? 1 : 0),
          separatorBuilder: (context, index) => const Divider(height: 1),
          itemBuilder: (context, index) {
            if (match.allowModifyParticipants) {
              if (index == 0) {
                return ListTile(
                  onTap: () => AppNavigator().navigate(const AddParticipantScreen()),
                  leading: Icon(Icons.add, size: 32 * scale),
                  title: Text(t('addParticipant'), style: StyleHelper.baseTextStyle(context)),
                );
              }
              index -= 1;
            }
            final participant = match.participants[index];
            return ParticipantTile(
              key: ValueKey(participant.matchParticipantId),
              match: match,
              participant: participant,
              canEdit: match.allowModifyParticipants &&
                  match.allowCustomParticipants &&
                  participant.tenantParticipantId == null,
              canRemove: match.allowModifyParticipants,
            );
          },
        );
      },
    );
  }
}
