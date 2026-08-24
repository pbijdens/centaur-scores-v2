import 'package:centaur_scores/src/repository/app_navigator.dart';
import 'package:centaur_scores/src/style/style_helper.dart';
import 'package:flutter/material.dart';

import '../../model/scorekeeper_match.dart';
import '../../model/scorekeeper_match_participant.dart';

class SingleParticipantHeaderLineOne extends StatelessWidget {
  final ScorekeeperMatch model;
  final ScorekeeperMatchParticipant participant;
  final int index;

  const SingleParticipantHeaderLineOne(
      {super.key,
      required this.participant,
      required this.model,
      required this.index});

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: () {
        AppNavigator().navigate(const HomeScreen(), resetStack: true);
      },
      child: SizedBox(
          width: StyleHelper.scoreCardColumnWidth(context, model),
          height: StyleHelper.scLine1Height(context),
          child: Container(
              alignment: Alignment.topLeft,
              color: StyleHelper.colorForColumn(index),
              child: Padding(
                  padding: const EdgeInsets.all(4),
                  child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(participant.name,
                            textAlign: TextAlign.left,
                            style: StyleHelper
                                .scoreFormHeaderParticipantNameTextStyle(
                                    context)),
                      ])))),
    );
  }
}
