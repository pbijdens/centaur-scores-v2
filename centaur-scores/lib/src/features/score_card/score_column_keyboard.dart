import 'package:centaur_scores/src/features/score_card/scores_viewmodel.dart';
import 'package:centaur_scores/src/style/style_helper.dart';
import 'package:flutter/material.dart';

import '../../model/scorekeeper_key.dart';
import '../../model/scorekeeper_match.dart';
import '../../model/scorekeeper_match_participant.dart';
import '../../scoring/scoring.dart' as scoring;

class ScoreColumnKeyboard extends StatelessWidget {
  final ScoresViewmodel _viewModel;
  final ScorekeeperMatch _model;
  final ScorekeeperMatchParticipant _participant;

  const ScoreColumnKeyboard(this._viewModel, this._model, this._participant,
      {super.key});

  @override
  Widget build(BuildContext context) {
    return Container(
        decoration: BoxDecoration(
            border: Border.all(width: 2.0, color: Colors.transparent),
            color: StyleHelper.colorForColumnFooter(
                _viewModel.activeKeyboard ?? 0)),
        child: SizedBox(
            width: StyleHelper.scoreCardColumnWidth(context, _model),
            child: Padding(
                padding: const EdgeInsets.all(0),
                child: Column(children: buildRows(context)))));
  }

  List<Widget> buildRows(BuildContext context) {
    List<Widget> result = [];
    List<Widget> currentRow = [];

    // The keys available for this participant, from the match's keyboard
    // definition, plus a permanent "delete" key (there's no equivalent
    // dedicated key in the new API's keyboard list, unlike the old
    // scoreValues map which typically included a "DEL" entry).
    List<ScorekeeperKey> keys = scoring.availableKeys(_model, _participant);

    int buttonsPerRow = (keys.length + 1) > 7 ? 4 : 3;

    var rowHeight = StyleHelper.keyboardButtonRowHeight(
        context, _model, buttonsPerRow, keys);

    void addButton({
      required String label,
      required int? colorValue,
      required VoidCallback onPressed,
    }) {
      currentRow.add(Expanded(
          child: Padding(
              padding: const EdgeInsets.all(1.0),
              child: ElevatedButton(
                onPressed: onPressed,
                style: ElevatedButton.styleFrom(
                  padding: EdgeInsets.zero, // remove default padding
                  backgroundColor:
                      StyleHelper.colorForButton(context, colorValue),
                  foregroundColor:
                      StyleHelper.colorForButtonLabel(context, colorValue),
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(5.0),
                  ),
                ),
                child: Container(
                  height: rowHeight,
                  width: double.infinity,
                  decoration: BoxDecoration(
                    color: Colors.transparent,
                    borderRadius: BorderRadius.circular(5.0),
                  ),
                  child: Center(
                    child: Text(
                      label,
                      style: StyleHelper.keypadTextStyleSmall(context, label)
                          ?.apply(
                        color: StyleHelper.colorForButtonLabel(
                            context, colorValue),
                      ),
                    ),
                  ),
                ),
              ))));
      if (currentRow.length == buttonsPerRow) {
        result.add(Container(
            height: rowHeight,
            color: Colors.transparent,
            child: Row(children: currentRow)));
        currentRow = [];
      }
    }

    for (int i = 0; i < keys.length; i++) {
      addButton(
          label: keys[i].label,
          colorValue: keys[i].value,
          onPressed: () => _viewModel.setScore(keys[i].id, _model, _participant));
    }
    addButton(
        label: 'DEL',
        colorValue: null,
        onPressed: () => _viewModel.setScore(null, _model, _participant));

    if (currentRow.isNotEmpty) {
      result.add(Container(
          color: Colors.transparent, child: Row(children: currentRow)));
    }

    return result;
  }
}
