import 'package:centaur_scores/src/style/style_helper.dart';
import 'package:centaur_scores/src/features/score_card/scores_viewmodel.dart';
import 'package:flutter/material.dart';

import '../../model/scorekeeper_key.dart';
import '../../model/scorekeeper_match.dart';
import '../../model/scorekeeper_match_participant.dart';
import '../../scoring/scoring.dart' as scoring;

class SingeParticipantScoreForm extends StatelessWidget {
  final ScorekeeperMatch _model;
  final ScorekeeperMatchParticipant _participant;
  final int _index;
  final Function(BuildContext context, int endNo, int? arrowNo) _onSelect;
  final ScoresViewmodel viewModel;
  final GlobalKey _scrollKey;

  const SingeParticipantScoreForm(
      {super.key,
      required this.viewModel,
      required ScorekeeperMatchParticipant participant,
      required ScorekeeperMatch model,
      required Function(BuildContext context, int endNo, int? arrowNo) onSelect,
      required int index,
      required GlobalKey scrollKey})
      : _index = index,
        _participant = participant,
        _onSelect = onSelect,
        _model = model,
        _scrollKey = scrollKey;

  @override
  Widget build(BuildContext context) {
    return Container(
        color: StyleHelper.colorForScoreForm(_index),
        child: SizedBox(
            width: StyleHelper.scoreCardColumnWidth(context, _model),
            child: SingleChildScrollView(
                child: Align(
                    alignment: Alignment.topCenter,
                    child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: buildScoreRows(context))))));
  }

  ScorekeeperKey? _keyFor(String? id) {
    if (id == null) return null;
    for (final key in _model.keyboard) {
      if (key.id == id) return key;
    }
    return null;
  }

  List<Widget> buildScoreRows(BuildContext context) {
    List<Widget> result = [];
    for (var endNo = 0; endNo < _model.ends; endNo++) {
      final subtotal = scoring.runningTotalThroughEnd(_model, _participant, endNo);

      result.add(SizedBox(
        width: StyleHelper.scoreCardColumnWidth(context, _model),
        height: StyleHelper.scoreCardRowHeight(context, _model),
        child: InkWell(
            onTap: () {
              onTapScoreField(context, endNo, -1);
            },
            child: Container(
              color: viewModel.activeKeyboard == _index &&
                      viewModel.editingEnd == endNo
                  ? Colors.amber
                  : Colors.transparent,
              child: viewModel.activeKeyboard == _index &&
                      viewModel.editingEnd == endNo
                  ? Row(
                      key: _scrollKey,
                      children: buildScoreRow(context, endNo, subtotal))
                  : Row(children: buildScoreRow(context, endNo, subtotal)),
            )),
      ));
    }
    return result;
  }

  List<Widget> buildScoreRow(BuildContext context, int endNo, int subtotal) {
    List<Widget> result = [];
    final endArrows = scoring.endArrows(_model, _participant, endNo);
    final anyShot = endArrows.any((a) => a != null);
    final int? endScore = anyShot ? scoring.endTotal(_model, _participant, endNo) : null;

    result.add(SizedBox(
        width: StyleHelper.endNumberWidth,
        height: StyleHelper.preferredCellHeight(context, _model),
        child: Container(
          alignment: Alignment.center,
          color: Colors.transparent,
          child: Text('${endNo + 1}',
              style: StyleHelper.scoreFormEndNumberTextStyle(context)),
        )));

    MediaQueryData q = MediaQuery.of(context);
    double inset = q.size.height > 600 ? 4 : 2;

    for (int arrowNo = 0; arrowNo < _model.arrowsPerEnd; arrowNo++) {
      final keyId = endArrows[arrowNo];
      final key = _keyFor(keyId);
      final colorValue = keyId != null ? scoring.keyValue(_model, keyId) : null;
      final label = keyId == null ? '-' : (key?.label ?? keyId);

      bool isSelected = viewModel.activeKeyboard == _index &&
          viewModel.editingEnd == endNo &&
          arrowNo == viewModel.editingArrow;
      var box = SizedBox(
          width: StyleHelper.preferredCellWidth(context, _model),
          height: StyleHelper.preferredCellHeight(context, _model),
          child: InkWell(
              onTap: () {
                onTapScoreField(context, endNo, arrowNo);
              },
              child: Container(
                  color: isSelected ? Colors.black : Colors.transparent,
                  child: Padding(
                      padding: isSelected
                          ? EdgeInsets.fromLTRB(inset, inset, inset, inset)
                          : const EdgeInsets.fromLTRB(1, 1, 1, 1),
                      child: Container(
                        alignment: Alignment.center,
                        color: isSelected
                            ? StyleHelper.colorForButtonSelected(context)
                            : StyleHelper.colorForButton(context, colorValue),
                        child: Text(label,
                            style: isSelected
                                ? StyleHelper
                                    .scoreFormArrowScoreTextStyleSelected(
                                        context)
                                : StyleHelper.scoreFormArrowScoreTextStyle(
                                    context, colorValue)),
                      )))));
      result.add(box);
    }

    // end total
    result.add(SizedBox(
        width: StyleHelper.endTotalWidth,
        height: StyleHelper.preferredCellHeight(context, _model),
        child: Container(
          alignment: Alignment.center,
          color: Colors.transparent,
          child: Text('${endScore ?? "-"}',
              style: StyleHelper.scoreFormEndTotalTextStyle(context)),
        )));

    // running total through this end
    result.add(SizedBox(
        width: StyleHelper.subTotalWidth,
        height: StyleHelper.preferredCellHeight(context, _model),
        child: Container(
          alignment: Alignment.center,
          color: Colors.transparent,
          child: Text('${endScore != null ? subtotal : "-"}',
              style: StyleHelper.scoreFormEndTotalTextStyle(context)),
        )));

    return result;
  }

  // Opens the score entry on the field that was tapped. If a summary field is
  // tapped, open the editor on the first field.
  void onTapScoreField(BuildContext context, int endNo, int arrowNo) {
    _onSelect(context, endNo,
        arrowNo < 0 || arrowNo >= _model.arrowsPerEnd ? null : arrowNo);
  }
}
