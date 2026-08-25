import 'package:centaur_scores/src/features/score_card/score_entry_fullpage_widget.dart';
import 'package:flutter/material.dart';

/// The kept grid score-entry screen body, rendered under [AppShell]'s
/// shared sticky header - not its own Scaffold/AppBar/Drawer anymore.
class ScoresView extends StatelessWidget {
  const ScoresView({super.key});

  @override
  Widget build(BuildContext context) {
    return const Padding(
        padding: EdgeInsets.zero, child: ScoreEntryFullPageWidget());
  }
}
