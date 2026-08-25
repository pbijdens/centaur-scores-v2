import 'package:centaur_scores/src/features/score_card/score_column_keyboard.dart';
import 'package:centaur_scores/src/repository/repository.dart';
import 'package:centaur_scores/src/mvvm/events/loading_event.dart';
import 'package:centaur_scores/src/mvvm/observer.dart';
import 'package:centaur_scores/src/features/score_card/scores_viewmodel.dart';
import 'package:centaur_scores/src/features/score_card/single_participant_footer.dart';
import 'package:centaur_scores/src/features/score_card/single_participant_header_line_two.dart';
import 'package:centaur_scores/src/features/score_card/single_participant_header_line_one.dart';
import 'package:centaur_scores/src/features/score_card/single_participant_score_form.dart';
import 'package:centaur_scores/src/style/loading_screen.dart';
import 'package:centaur_scores/src/style/style_helper.dart';
import 'package:flutter/material.dart';

import '../../model/scorekeeper_match.dart';
import '../../model/scorekeeper_match_participant.dart';

class ScoreEntryFullPageWidget extends StatefulWidget {
  const ScoreEntryFullPageWidget({super.key});

  @override
  ScoreEntryFullPageWidgetState createState() {
    return ScoreEntryFullPageWidgetState();
  }
}

class ScoreEntryFullPageWidgetState extends State<ScoreEntryFullPageWidget>
    implements EventObserver {
  final _formKey = GlobalKey<FormState>();
  final ScoresViewmodel _viewModel = ScoresViewmodel(MatchRepository());

  @override
  void initState() {
    super.initState();
    _viewModel.subscribe(this);
    _viewModel.load();
  }

  @override
  void dispose() {
    super.dispose();
    _viewModel.unsubscribe(this);
  }

  @override
  void notify(ViewEvent event) {
    if (event is LoadingEvent) {
      setState(() {
        _isLoading = event.isLoading;
      });
    } else if (event is ScoresViewmodelLoadedEvent) {
      setState(() {});
    } else if (event is ScoresViewmodelUpdatedEvent) {
      setState(() {});
    } else if (event is ArrowStateChangedEvent) {
      setState(() {});
    } else if (event is KeyboardShownEvent) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        final context = _keyboardScrollKey.currentContext;
        if (context != null) {
          Scrollable.ensureVisible(context,
              duration: const Duration(milliseconds: 500),
              curve: Curves.easeInOut);
        }
      });
    } else if (event is ActiveArrowChangedEvent) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        final context = _scrollKey.currentContext;
        if (context != null) {
          Scrollable.ensureVisible(context,
              duration: const Duration(milliseconds: 500),
              curve: Curves.easeInOut);
        }
      });
    }
  }

  bool _isLoading = false;
  final GlobalKey _scrollKey = GlobalKey();
  final GlobalKey _keyboardScrollKey = GlobalKey();

  // Read live off MatchRepository on every build - the background sync
  // engine replaces the whole ScorekeeperMatch object on every poll, so
  // caching a snapshot (as the old MatchModel-based version did) would go
  // stale after the first 60s poll.
  ScorekeeperMatch? get model => MatchRepository().currentMatchOrNull;
  List<ScorekeeperMatchParticipant> get participants => model?.participants ?? [];

  @override
  Widget build(BuildContext context) {
    if (_isLoading || model == null) {
      return const LoadingScreen();
    }
    return Form(
        key: _formKey,
        child: SingleChildScrollView(
            scrollDirection: Axis.horizontal,
            child: SizedBox(
              child: Column(children: [
                IntrinsicHeight(
                    child: Row(
                        crossAxisAlignment: CrossAxisAlignment.end,
                        children: singeParticipantNames())),
                Expanded(
                    flex: 10,
                    child: Align(
                        alignment: Alignment.topCenter,
                        child: Row(children: singeParticipantScoreForms()))),
                IntrinsicHeight(
                    child: Row(children: singeParticipantSummaries()))
              ]),
            )));
  }

  List<Widget> singeParticipantScoreForms() {
    final currentModel = model!;
    return participants
        .map((participant) => Column(children: [
              Expanded(
                  flex: 10,
                  child: SingeParticipantScoreForm(
                      scrollKey: _scrollKey,
                      viewModel: _viewModel,
                      model: currentModel,
                      participant: participant,
                      onSelect: (BuildContext currentContext, int endNo,
                          int? arrowNo) {
                        int activeKeyboard = participants.indexWhere(
                            (e) => e.matchParticipantId == participant.matchParticipantId);
                        _viewModel.activateKeyboard(
                            currentModel,
                            activeKeyboard >= 0 ? activeKeyboard : null,
                            endNo,
                            arrowNo);
                      },
                      index: participants.indexWhere(
                          (e) => e.matchParticipantId == participant.matchParticipantId))),
              keyboard(
                  context,
                  currentModel,
                  participant,
                  participants.indexWhere(
                      (e) => e.matchParticipantId == participant.matchParticipantId))
            ]))
        .toList();
  }

  List<Widget> singeParticipantNames() {
    final currentModel = model!;
    return participants
        .map((participant) => SingleParticipantHeaderLineOne(
            model: currentModel,
            participant: participant,
            index: participants.indexWhere(
                (e) => e.matchParticipantId == participant.matchParticipantId)))
        .toList();
  }

  List<Widget> singeParticipantDisciplines() {
    final currentModel = model!;
    return participants
        .map((participant) => SingleParticipantHeaderLineTwo(
            model: currentModel,
            participant: participant,
            index: participants.indexWhere(
                (e) => e.matchParticipantId == participant.matchParticipantId)))
        .toList();
  }

  List<Widget> singeParticipantSummaries() {
    final currentModel = model!;
    return participants
        .map((participant) => Column(children: [
              SingeParticipantFooter(
                  model: currentModel,
                  participant: participant,
                  viewmodel: _viewModel,
                  index: participants.indexWhere(
                      (e) => e.matchParticipantId == participant.matchParticipantId))
            ]))
        .toList();
  }

  Widget keyboard(BuildContext context, ScorekeeperMatch model,
      ScorekeeperMatchParticipant participantModel, int participantIndex) {
    if (participantIndex == _viewModel.activeKeyboard) {
      return IntrinsicHeight(
          key: _keyboardScrollKey,
          child: ScoreColumnKeyboard(_viewModel, model, participantModel));
    }
    return SizedBox(
      width: StyleHelper.scoreCardColumnWidth(context, model),
      height: 0,
      child: Container(color: Colors.transparent),
    );
  }
}
