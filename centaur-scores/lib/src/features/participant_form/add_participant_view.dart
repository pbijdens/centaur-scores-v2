import 'package:flutter/material.dart';

import 'package:centaur_scores/src/i18n/translations.dart';
import 'package:centaur_scores/src/model/participant_category_value.dart';
import 'package:centaur_scores/src/model/scorekeeper_participant_info.dart';
import 'package:centaur_scores/src/model/scorekeeper_participant_options.dart';
import 'package:centaur_scores/src/model/scorekeeper_participant_update.dart';
import 'package:centaur_scores/src/repository/app_navigator.dart';
import 'package:centaur_scores/src/repository/centaur_scores_api.dart';
import 'package:centaur_scores/src/repository/repository.dart';
import 'package:centaur_scores/src/style/style_helper.dart';

import 'participant_form.dart';

class AddParticipantView extends StatefulWidget {
  const AddParticipantView({super.key});

  @override
  State<AddParticipantView> createState() => _AddParticipantViewState();
}

class _AddParticipantViewState extends State<AddParticipantView> {
  bool _showUnlistedForm = false;
  bool _loading = true;
  String? _loadError;
  ScorekeeperParticipantOptions? _options;
  final _searchController = TextEditingController();

  @override
  void initState() {
    super.initState();
    _load();
    _searchController.addListener(() => setState(() {}));
  }

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _loadError = null;
    });
    try {
      final options = await CentaurScoresAPI().getParticipantOptions();
      setState(() {
        _options = options;
        _loading = false;
      });
    } catch (error) {
      setState(() {
        _loadError = '$error';
        _loading = false;
      });
    }
  }

  bool _matches(ScorekeeperParticipantInfo p, String query) {
    if (query.isEmpty) return true;
    final q = query.toLowerCase();
    return p.name.toLowerCase().contains(q) ||
        (p.info?.toLowerCase().contains(q) ?? false) ||
        (p.federationNumber?.toLowerCase().contains(q) ?? false);
  }

  Future<void> _selectOption(ScorekeeperParticipantInfo option) async {
    final match = MatchRepository().currentMatchOrNull;
    if (match == null) return;
    final payload = [
      ...match.participants.map((p) => ScorekeeperParticipantUpdate.fromExisting(p)),
      ScorekeeperParticipantUpdate.fromOption(option),
    ];
    final ok = await MatchRepository().submitParticipantsList(payload);
    if (ok) {
      if (mounted) AppNavigator().goToParent();
    } else {
      _showSaveFailed();
    }
  }

  Future<void> _saveUnlisted(String? federationNumber, String name,
      List<ParticipantCategoryValue> categories) async {
    final match = MatchRepository().currentMatchOrNull;
    if (match == null) return;
    final payload = [
      ...match.participants.map((p) => ScorekeeperParticipantUpdate.fromExisting(p)),
      ScorekeeperParticipantUpdate.custom(
          federationNumber: federationNumber, name: name, categories: categories),
    ];
    final ok = await MatchRepository().submitParticipantsList(payload);
    if (ok) {
      if (mounted) AppNavigator().goToParent();
    } else {
      _showSaveFailed();
    }
  }

  void _showSaveFailed() {
    if (!mounted) return;
    showDialog<void>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text(t('saveFailedTitle')),
        content: Text(t('saveFailedBody')),
        actions: [
          TextButton(onPressed: () => Navigator.of(context).pop(), child: Text(t('ok'))),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final match = MatchRepository().currentMatchOrNull;
    if (match == null) return const SizedBox.shrink();

    if (_showUnlistedForm) {
      return ParticipantForm(
        match: match,
        onCancel: () => setState(() => _showUnlistedForm = false),
        onSave: _saveUnlisted,
      );
    }

    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_loadError != null) {
      return Center(
        child: Column(mainAxisSize: MainAxisSize.min, children: [
          Text(_loadError!),
          const SizedBox(height: 8),
          ElevatedButton(onPressed: _load, child: Text(t('retry'))),
        ]),
      );
    }

    final options = _options!;
    final query = _searchController.text;
    final sections = <(String, List<ScorekeeperParticipantInfo>)>[
      ('unassigned', options.unassigned.where((p) => _matches(p, query)).toList()),
      ('available', options.potential.where((p) => _matches(p, query)).toList()),
      ('alreadyAssigned', options.assigned.where((p) => _matches(p, query)).toList()),
    ];

    final scale = StyleHelper.scale(context);
    return Column(
      children: [
        Padding(
          padding: EdgeInsets.all(12 * scale),
          child: TextField(
            controller: _searchController,
            style: StyleHelper.baseTextStyle(context),
            decoration: InputDecoration(
              labelText: t('search'),
              labelStyle: StyleHelper.baseTextStyle(context),
              prefixIcon: Icon(Icons.search, size: 24 * scale),
              border: const OutlineInputBorder(),
            ),
          ),
        ),
        Expanded(
          child: ListView(
            children: [
              for (final section in sections)
                if (section.$2.isNotEmpty) ...[
                  Padding(
                    padding: EdgeInsets.fromLTRB(16, 12 * scale, 16, 4),
                    child: Text(t(section.$1),
                        style: Theme.of(context)
                            .textTheme
                            .labelLarge
                            ?.apply(fontSizeFactor: scale)),
                  ),
                  const Divider(height: 1),
                  for (final option in section.$2)
                    ListTile(
                      onTap: () => _selectOption(option),
                      title: Text(option.name, style: StyleHelper.baseTextStyle(context)),
                      subtitle: (option.info?.isNotEmpty ?? false)
                          ? Text(option.info!,
                              style: Theme.of(context)
                                  .textTheme
                                  .bodyMedium
                                  ?.apply(fontSizeFactor: scale))
                          : null,
                      trailing: (option.federationNumber?.isNotEmpty ?? false)
                          ? Text(option.federationNumber!,
                              style: StyleHelper.baseTextStyle(context))
                          : null,
                    ),
                ],
              if (match.allowCustomParticipants)
                ListTile(
                  leading: Icon(Icons.add, size: 24 * scale),
                  title: Text(t('addUnlistedParticipant'),
                      style: StyleHelper.baseTextStyle(context)),
                  onTap: () => setState(() => _showUnlistedForm = true),
                ),
            ],
          ),
        ),
      ],
    );
  }
}
