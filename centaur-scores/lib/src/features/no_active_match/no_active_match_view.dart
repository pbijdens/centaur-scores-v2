import 'package:flutter/material.dart';

import 'package:centaur_scores/src/i18n/translations.dart';
import 'package:centaur_scores/src/repository/repository.dart';

class NoActiveMatchView extends StatelessWidget {
  const NoActiveMatchView({super.key});

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.event_busy, size: 64, color: Colors.black45),
            const SizedBox(height: 16),
            Text(t('noActiveMatchTitle'), style: Theme.of(context).textTheme.titleLarge),
            const SizedBox(height: 8),
            Text(t('noActiveMatchBody'), textAlign: TextAlign.center),
            const SizedBox(height: 24),
            ElevatedButton(
              onPressed: () => MatchRepository().fetchMatchInfo(),
              child: Text(t('retry')),
            ),
            const SizedBox(height: 12),
            OutlinedButton.icon(
              onPressed: () => MatchRepository().resetPairing(),
              icon: const Icon(Icons.qr_code_scanner),
              label: Text(t('scanForMatch')),
            ),
          ],
        ),
      ),
    );
  }
}
