import 'package:flutter/material.dart';

import 'package:centaur_scores/src/i18n/translations.dart';
import 'package:centaur_scores/src/model/scorekeeper_match.dart';
import 'package:centaur_scores/src/model/scorekeeper_match_participant.dart';
import 'package:centaur_scores/src/model/scorekeeper_participant_update.dart';
import 'package:centaur_scores/src/repository/app_navigator.dart';
import 'package:centaur_scores/src/repository/repository.dart';
import 'package:centaur_scores/src/scoring/scoring.dart' as scoring;
import 'package:centaur_scores/src/style/style_helper.dart';

/// A Home-screen participant row: name/number, score/arrows-shot/split
/// stats, and (when the match allows editing) a swipe-left-to-reveal
/// Edit/Remove action pair.
class ParticipantTile extends StatefulWidget {
  final ScorekeeperMatch match;
  final ScorekeeperMatchParticipant participant;
  final bool canEdit;
  final bool canRemove;

  const ParticipantTile({
    super.key,
    required this.match,
    required this.participant,
    required this.canEdit,
    required this.canRemove,
  });

  @override
  State<ParticipantTile> createState() => _ParticipantTileState();
}

class _ParticipantTileState extends State<ParticipantTile> {
  double _actionWidth(double scale) => 88 * scale;

  double _dragExtent = 0;
  double _maxExtent(double scale) =>
      -_actionWidth(scale) * ((widget.canEdit ? 1 : 0) + (widget.canRemove ? 1 : 0));

  @override
  Widget build(BuildContext context) {
    final total = scoring.totalScore(widget.match, widget.participant);
    final shot = scoring.arrowsShot(widget.participant);
    final splits = scoring.splitScores(widget.match, widget.participant);

    final actionsAvailable = widget.canEdit || widget.canRemove;
    final scale = StyleHelper.scale(context);
    final actionWidth = _actionWidth(scale);
    final maxExtent = _maxExtent(scale);

    return ClipRect(
      child: Stack(children: [
        if (actionsAvailable)
          Positioned.fill(
            child: Row(children: [
              const Spacer(),
              if (widget.canEdit)
                _ActionButton(
                  color: Colors.blueGrey,
                  icon: Icons.edit,
                  label: t('edit'),
                  width: actionWidth,
                  scale: scale,
                  onTap: () {
                    setState(() => _dragExtent = 0);
                    AppNavigator()
                        .navigate(EditParticipantScreen(widget.participant.matchParticipantId));
                  },
                ),
              if (widget.canRemove)
                _ActionButton(
                  color: Colors.red.shade700,
                  icon: Icons.delete,
                  label: t('remove'),
                  width: actionWidth,
                  scale: scale,
                  onTap: () => _handleRemove(context),
                ),
            ]),
          ),
        GestureDetector(
          onHorizontalDragUpdate: actionsAvailable
              ? (details) {
                  setState(() {
                    _dragExtent =
                        (_dragExtent + details.delta.dx).clamp(maxExtent, 0.0);
                  });
                }
              : null,
          onHorizontalDragEnd: actionsAvailable
              ? (details) {
                  setState(() {
                    _dragExtent = _dragExtent < maxExtent / 2 ? maxExtent : 0;
                  });
                }
              : null,
          child: Transform.translate(
            offset: Offset(_dragExtent, 0),
            child: Material(
              color: Theme.of(context).colorScheme.surface,
              child: InkWell(
                onTap: _dragExtent != 0
                    ? () => setState(() => _dragExtent = 0)
                    : () => AppNavigator().navigate(const ScoreCardScreen()),
                child: Padding(
                  padding: EdgeInsets.symmetric(horizontal: 16 * scale, vertical: 14 * scale),
                  child: Row(children: [
                    Expanded(
                      flex: 3,
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(widget.participant.name,
                              style: Theme.of(context)
                                  .textTheme
                                  .titleMedium
                                  ?.apply(fontSizeFactor: scale)),
                          if (widget.participant.info?.isNotEmpty ?? false)
                            Text(widget.participant.info!,
                                style: Theme.of(context)
                                    .textTheme
                                    .bodySmall
                                    ?.apply(fontSizeFactor: scale)),
                        ],
                      ),
                    ),
                    Expanded(
                      flex: 2,
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.end,
                        children: [
                          Text('${t('score')}: $total',
                              style: Theme.of(context)
                                  .textTheme
                                  .titleMedium
                                  ?.apply(fontSizeFactor: scale)),
                          Text('${t('arrowsShot')}: $shot',
                              style: StyleHelper.baseTextStyle(context)),
                          if (splits.isNotEmpty)
                            Text(
                                '${t('split')}: ${splits.map((s) => s.toString()).join(' / ')}',
                                style: StyleHelper.baseTextStyle(context)),
                        ],
                      ),
                    ),
                  ]),
                ),
              ),
            ),
          ),
        ),
      ]),
    );
  }

  Future<void> _handleRemove(BuildContext context) async {
    setState(() => _dragExtent = 0);
    final remaining = widget.match.participants
        .where((p) => p.matchParticipantId != widget.participant.matchParticipantId)
        .map((p) => ScorekeeperParticipantUpdate.fromExisting(p))
        .toList();
    final ok = await MatchRepository().submitParticipantsList(remaining);
    if (!ok && context.mounted) {
      await showDialog<void>(
        context: context,
        builder: (context) => AlertDialog(
          title: Text(t('removeFailedTitle')),
          content: Text(t('removeFailedBody')),
          actions: [
            TextButton(onPressed: () => Navigator.of(context).pop(), child: Text(t('ok'))),
          ],
        ),
      );
    }
  }
}

class _ActionButton extends StatelessWidget {
  final Color color;
  final IconData icon;
  final String label;
  final double width;
  final double scale;
  final VoidCallback onTap;

  const _ActionButton({
    required this.color,
    required this.icon,
    required this.label,
    required this.width,
    required this.scale,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: width,
      child: Material(
        color: color,
        child: InkWell(
          onTap: onTap,
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Icon(icon, color: Colors.white, size: 24 * scale),
              Text(label, style: TextStyle(color: Colors.white, fontSize: 12 * scale)),
            ],
          ),
        ),
      ),
    );
  }
}
