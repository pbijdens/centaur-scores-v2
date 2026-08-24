import 'package:centaur_scores/src/features/score_card/scores_viewmodel.dart';
import 'package:centaur_scores/src/style/style_helper.dart';
import 'package:flutter/material.dart';

import '../../model/scorekeeper_match.dart';
import '../../model/scorekeeper_match_participant.dart';
import '../../scoring/scoring.dart' as scoring;

class SingeParticipantFooter extends StatelessWidget {
  final ScorekeeperMatch model;
  final ScoresViewmodel viewmodel;
  final ScorekeeperMatchParticipant participant;
  final int index;

  const SingeParticipantFooter(
      {super.key,
      required this.participant,
      required this.model,
      required this.viewmodel,
      required this.index});

  @override
  Widget build(BuildContext context) {
    final total = scoring.totalScore(model, participant);
    return Container(
      color: StyleHelper.colorForColumnFooter(index),
      height: StyleHelper.scFooterHeight(context),
      child: SizedBox(
          width: StyleHelper.scoreCardColumnWidth(context, model),
          child: Stack(alignment: Alignment.centerLeft, children: [
            Padding(
                padding: const EdgeInsets.all(4),
                child: Text('Totaal: $total',
                    textAlign: TextAlign.center,
                    style: StyleHelper.scoreFormFooterTextStyle(context))),
            if (index == viewmodel.activeKeyboard)
              SizedBox(
                  height: StyleHelper.scFooterHeight(context),
                  width: StyleHelper.scoreCardColumnWidth(context, model),
                  child: Container(color: const Color.fromARGB(128, 0, 0, 0))),
            if (index == viewmodel.activeKeyboard)
              Padding(
                  padding: const EdgeInsets.fromLTRB(0, 0, 88, 0),
                  child: Align(
                      alignment: Alignment.centerRight,
                      child: SizedBox(
                          width: StyleHelper.scFooterHeight(context) - 10,
                          height: StyleHelper.scFooterHeight(context) - 10,
                          child: ElevatedButton(
                              onPressed: () {
                                viewmodel.hideKeyboard();
                              },
                              style: ElevatedButton.styleFrom(
                                padding:
                                    EdgeInsets.zero, // remove default padding
                                backgroundColor: Colors.white,
                                foregroundColor: Colors.black,
                                shape: RoundedRectangleBorder(
                                  borderRadius: BorderRadius.circular(3.0),
                                ),
                              ),
                              child: const Text("x"))))),
            if (index == viewmodel.activeKeyboard)
              Padding(
                  padding: const EdgeInsets.fromLTRB(0, 0, 6, 0),
                  child: Align(
                      alignment: Alignment.centerRight,
                      child: SizedBox(
                          width: 72,
                          height: StyleHelper.scFooterHeight(context) - 10,
                          child: ElevatedButton(
                              onPressed: () {
                                viewmodel.nextKeyboard(
                                    model, viewmodel.editingEnd, null);
                              },
                              style: ElevatedButton.styleFrom(
                                padding:
                                    EdgeInsets.zero, // remove default padding
                                backgroundColor: Colors.white,
                                foregroundColor: Colors.black,
                                shape: RoundedRectangleBorder(
                                  borderRadius: BorderRadius.circular(3.0),
                                ),
                              ),
                              child: const Text("Volgende")))))
          ])),
    );
  }
}
