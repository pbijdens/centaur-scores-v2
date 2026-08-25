import 'package:flutter/material.dart';

import 'package:centaur_scores/src/i18n/translations.dart';
import 'package:centaur_scores/src/model/scorekeeper_match.dart';
import 'package:centaur_scores/src/repository/app_navigator.dart';
import 'package:centaur_scores/src/repository/repository.dart';

/// Sticky header shown above every screen once the device is paired: home
/// icon + match name, language switcher, sync-status indicator, and an
/// overflow menu with the "re-scan QR / change match" action. A second row
/// (an "enter scores now" shortcut) shows only on Home when there are
/// participants to score.
class AppHeader extends StatelessWidget {
  const AppHeader({super.key});

  @override
  Widget build(BuildContext context) {
    return ListenableBuilder(
      listenable: Listenable.merge([MatchRepository(), AppNavigator()]),
      builder: (context, _) {
        final match = MatchRepository().currentMatchOrNull;
        final screen = AppNavigator().current;

        return Material(
          elevation: 4,
          color: Theme.of(context).colorScheme.surface,
          child: SafeArea(
            bottom: false,
            child: Column(
              children: [
                SizedBox(
                  height: 56,
                  child: Row(children: [
                    Expanded(
                      child: InkWell(
                        onTap: () =>
                            AppNavigator().navigate(const HomeScreen(), resetStack: true),
                        child: Padding(
                          padding: const EdgeInsets.symmetric(horizontal: 12),
                          child: Row(children: [
                            const Icon(Icons.home),
                            const SizedBox(width: 8),
                            Expanded(
                                child: Text(match?.match ?? t('appTitle'),
                                    style: Theme.of(context).textTheme.titleMedium,
                                    overflow: TextOverflow.ellipsis)),
                          ]),
                        ),
                      ),
                    ),
                    _LanguageButton(),
                    _SyncStatusButton(),
                    PopupMenuButton<String>(
                      icon: const Icon(Icons.more_vert),
                      onSelected: (value) {
                        if (value == 'repair') _confirmRepair(context);
                      },
                      itemBuilder: (context) => [
                        PopupMenuItem(value: 'repair', child: Text(t('rescanQr'))),
                      ],
                    ),
                  ]),
                ),
                if (screen is HomeScreen && (match?.participants.isNotEmpty ?? false))
                  _EnterScoresRow(match: match!),
              ],
            ),
          ),
        );
      },
    );
  }

  Future<void> _confirmRepair(BuildContext context) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text(t('rescanQrConfirmTitle')),
        content: Text(t('rescanQrConfirmBody')),
        actions: [
          TextButton(
              onPressed: () => Navigator.of(context).pop(false), child: Text(t('cancel'))),
          TextButton(onPressed: () => Navigator.of(context).pop(true), child: Text(t('ok'))),
        ],
      ),
    );
    if (confirmed == true) {
      await MatchRepository().resetPairing();
    }
  }
}

class _LanguageButton extends StatelessWidget {
  // Deliberately not const: build() reads MatchRepository()'s mutable
  // singleton state directly, so a const instance would canonicalize and
  // Flutter's element diffing would skip rebuilding it on notifyListeners().
  // ignore: prefer_const_constructors_in_immutables
  _LanguageButton();

  @override
  Widget build(BuildContext context) {
    final lang = MatchRepository().language;
    return PopupMenuButton<String>(
      tooltip: t('language'),
      onSelected: (value) => MatchRepository().setLanguage(value),
      itemBuilder: (context) => const [
        PopupMenuItem(value: 'NL', child: Text('🇳🇱 Nederlands')),
        PopupMenuItem(value: 'EN', child: Text('🇬🇧 English')),
      ],
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Text(lang == 'EN' ? '🇬🇧' : '🇳🇱', style: const TextStyle(fontSize: 20)),
      ),
    );
  }
}

class _SyncStatusButton extends StatelessWidget {
  // Deliberately not const: build() reads MatchRepository()'s mutable
  // singleton state directly, so a const instance would canonicalize and
  // Flutter's element diffing would skip rebuilding it on notifyListeners().
  // ignore: prefer_const_constructors_in_immutables
  _SyncStatusButton();

  @override
  Widget build(BuildContext context) {
    final repo = MatchRepository();
    final status = repo.syncStatus;
    // Tapping always attempts a sync now (a harmless no-op when there's
    // nothing pending); only disabled mid-flight to avoid piling up
    // redundant requests behind an in-progress one.
    final tappable = status != SyncStatus.syncing;

    late Color bg;
    late IconData icon;
    switch (status) {
      case SyncStatus.idle:
        bg = const Color(0xFF1E7E34);
        icon = Icons.wifi;
        break;
      case SyncStatus.pending:
        bg = const Color(0xFFE07C00);
        icon = Icons.wifi;
        break;
      case SyncStatus.syncing:
        bg = const Color(0xFFE07C00);
        icon = Icons.more_horiz;
        break;
      case SyncStatus.error:
        bg = const Color(0xFFC0392B);
        icon = Icons.wifi;
        break;
    }
    return Padding(
      padding: const EdgeInsets.all(8),
      child: InkWell(
        onTap: tappable ? () => repo.forceSync() : null,
        borderRadius: BorderRadius.circular(20),
        child: CircleAvatar(
          radius: 16,
          backgroundColor: bg,
          child: Icon(icon, color: Colors.white, size: 18),
        ),
      ),
    );
  }
}

class _EnterScoresRow extends StatelessWidget {
  final ScorekeeperMatch match;

  const _EnterScoresRow({required this.match});

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(12, 0, 12, 8),
      child: SizedBox(
        width: double.infinity,
        child: ElevatedButton(
          onPressed: () => AppNavigator().navigate(const ScoreCardScreen()),
          child: Text(t('enterScoresNow')),
        ),
      ),
    );
  }
}
