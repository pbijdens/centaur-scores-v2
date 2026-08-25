import 'package:flutter/material.dart';

import 'package:centaur_scores/src/i18n/translations.dart';
import 'package:centaur_scores/src/repository/app_navigator.dart';
import 'package:centaur_scores/src/repository/repository.dart';

/// Shown instead of an endless spinner when the match fetch immediately
/// following a fresh pairing fails (see [MatchRepository.fetchMatchInfo]
/// and `PairingErrorScreen`) - surfaces the error and offers a way back to
/// the QR scanner rather than leaving the device stuck.
class PairingErrorView extends StatelessWidget {
  final String message;

  const PairingErrorView({super.key, required this.message});

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.error_outline, size: 64, color: Colors.red),
            const SizedBox(height: 16),
            Text(t('pairingErrorTitle'), style: Theme.of(context).textTheme.titleLarge),
            const SizedBox(height: 8),
            Text(message, textAlign: TextAlign.center),
            const SizedBox(height: 24),
            ElevatedButton(
              onPressed: () {
                // Route back through AppLoadingScreen so a repeat failure
                // re-triggers the same error-screen handling instead of
                // being silently swallowed as a routine background retry.
                AppNavigator().navigate(const AppLoadingScreen(), resetStack: true);
                MatchRepository().fetchMatchInfo();
              },
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
