import 'package:flutter/material.dart';

import 'package:centaur_scores/src/features/home/home_view.dart';
import 'package:centaur_scores/src/features/no_active_match/no_active_match_view.dart';
import 'package:centaur_scores/src/features/pairing/pairing_error_view.dart';
import 'package:centaur_scores/src/features/pairing/qr_scan_view.dart';
import 'package:centaur_scores/src/features/participant_form/add_participant_view.dart';
import 'package:centaur_scores/src/features/participant_form/edit_participant_view.dart';
import 'package:centaur_scores/src/features/score_card/scores_view.dart';
import 'package:centaur_scores/src/repository/app_navigator.dart';
import 'package:centaur_scores/src/repository/repository.dart';

import 'app_header.dart';
import 'conflict_dialog.dart';

/// Root widget: shows the QR-pairing flow when the device isn't configured
/// yet, otherwise the shared sticky header plus whichever body
/// [AppNavigator] currently points at, plus a conflict-resolution dialog
/// whenever there are unresolved sync conflicts.
class AppShell extends StatefulWidget {
  const AppShell({super.key});

  @override
  State<AppShell> createState() => _AppShellState();
}

class _AppShellState extends State<AppShell> {
  bool _conflictDialogShowing = false;

  @override
  Widget build(BuildContext context) {
    return ListenableBuilder(
      listenable: Listenable.merge([MatchRepository(), AppNavigator()]),
      builder: (context, _) {
        if (!MatchRepository().isConfigured) {
          return const QrScanView();
        }

        _maybeShowConflictDialog();

        return PopScope(
          canPop: false,
          onPopInvokedWithResult: (didPop, result) {
            if (!didPop) AppNavigator().goToParent();
          },
          child: Scaffold(
            body: SafeArea(
              child: Column(children: [
                const AppHeader(),
                Expanded(child: _buildBody()),
              ]),
            ),
          ),
        );
      },
    );
  }

  void _maybeShowConflictDialog() {
    final hasConflicts = MatchRepository().conflicts?.isNotEmpty ?? false;
    if (hasConflicts && !_conflictDialogShowing) {
      _conflictDialogShowing = true;
      WidgetsBinding.instance.addPostFrameCallback((_) async {
        if (!mounted) return;
        await showDialog<void>(
          context: context,
          barrierDismissible: false,
          builder: (context) => const ConflictDialog(),
        );
        _conflictDialogShowing = false;
      });
    }
  }

  Widget _buildBody() {
    final screen = AppNavigator().current;
    // Deliberately not `const` here: this method reruns on every
    // MatchRepository/AppNavigator change (including a language switch),
    // but a `const` widget is canonicalized to the same instance every
    // time, so Flutter's element diffing (`child.widget == newWidget`)
    // would skip calling build() on it again and the screen would look
    // stuck on whatever language it last rendered in.
    return switch (screen) {
      AppLoadingScreen() => const Center(child: CircularProgressIndicator()),
      NoActiveMatchScreen() => NoActiveMatchView(),
      PairingErrorScreen() => PairingErrorView(message: screen.message),
      HomeScreen() => HomeView(),
      AddParticipantScreen() => AddParticipantView(),
      EditParticipantScreen() => EditParticipantView(),
      ScoreCardScreen() => ScoresView(),
    };
  }
}
