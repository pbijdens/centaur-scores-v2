import 'package:centaur_scores/src/repository/app_navigator.dart';
import 'package:centaur_scores/src/style/style_helper.dart';
import 'package:flutter/material.dart';

import '../../model/scorekeeper_match.dart';
import '../../model/scorekeeper_match_participant.dart';

class SingleParticipantHeaderLineTwo extends StatelessWidget {
  final ScorekeeperMatch model;
  final ScorekeeperMatchParticipant participant;
  final int index;

  const SingleParticipantHeaderLineTwo(
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
          height: StyleHelper.scLine2Height(context),
          child: Container(
              alignment: Alignment.topLeft,
              color: StyleHelper.colorForColumnFooter(index),
              child: Padding(
                  padding: const EdgeInsets.all(4),
                  child: Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        if (participant.federationNumber?.isNotEmpty ?? false)
                          RichText(
                              text: TextSpan(
                            text: '',
                            style:
                                StyleHelper.scoreFormHeaderLineTwoTextStyle(
                                    context),
                            children: <TextSpan>[
                              TextSpan(
                                  text: 'Nr: ',
                                  style: StyleHelper
                                      .scoreFormHeaderLineTwoBoldTextStyle(
                                          context)),
                              TextSpan(text: participant.federationNumber),
                            ],
                          )),
                        Expanded(
                            child: Align(
                                alignment: Alignment.centerRight,
                                child: RichText(
                                  textAlign: TextAlign.right,
                                  text: TextSpan(
                                    text:
                                        restrictLength(participant.info, 26) ??
                                            '',
                                    style: StyleHelper
                                        .scoreFormHeaderLineTwoTextStyle(
                                            context),
                                  ),
                                ))),
                      ])))),
    );
  }

  String? restrictLength(String? input, int maxLength) {
    if (input == null) return null;
    if (input.length > maxLength) return "${input.substring(0, maxLength)}...";
    return input;
  }
}
