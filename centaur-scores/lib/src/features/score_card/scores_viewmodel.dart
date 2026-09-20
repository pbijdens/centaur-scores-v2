import 'package:centaur_scores/src/model/scorekeeper_match.dart';
import 'package:centaur_scores/src/model/scorekeeper_match_participant.dart';
import 'package:centaur_scores/src/repository/repository.dart';
import 'package:centaur_scores/src/mvvm/events/loading_event.dart';
import 'package:centaur_scores/src/mvvm/observer.dart';
import 'package:centaur_scores/src/mvvm/viewmodel.dart';
import 'package:centaur_scores/src/scoring/scoring.dart' as scoring;

class ScoresViewmodel extends EventViewModel {
  final MatchRepository _repository;

  int? activeKeyboard;
  int editingEnd = -1;
  int? editingArrow = -1;

  ScoresViewmodel(this._repository);

  void notifyViewmodelUpdated() {
    notify(ScoresViewmodelUpdatedEvent());
  }

  void load() {
    notify(LoadingEvent(isLoading: true));
    final model = _repository.currentMatchOrNull;
    if (model != null) {
      notify(ScoresViewmodelLoadedEvent(model: model));
    }
    notify(LoadingEvent(isLoading: false));
  }

  void hideKeyboard() {
    activeKeyboard = null;
    notifyViewmodelUpdated();
    notifyKeyboardShown();
  }

  void nextKeyboard(ScorekeeperMatch model, int endNo, int? arrowNumber) {
    if (activeKeyboard == null) return;
    var participants = model.participants;
    activeKeyboard = activeKeyboard! + 1;
    if (activeKeyboard! >= participants.length) {
      activeKeyboard = 0;
    }

    // Resume at this participant's own first unscored arrow, searching the
    // whole match rather than just `editingEnd` - that end may already be
    // fully scored for this participant (e.g. entered out of order), in
    // which case the cursor must move on to the next gap instead of
    // defaulting back to arrow 0 of an already-completed end.
    final participant = participants[activeKeyboard!];
    final firstNull = scoring.firstNullIndex(participant);
    if (firstNull != null) {
      editingEnd = firstNull ~/ model.arrowsPerEnd;
      editingArrow = firstNull % model.arrowsPerEnd;
    } else {
      editingEnd = model.ends - 1;
      editingArrow = model.arrowsPerEnd - 1;
    }

    notifyViewmodelUpdated();
    notifyKeyboardShown();
    notifActiveArrowChanged();
  }

  void activateKeyboard(ScorekeeperMatch model, int? index, int endNo, int? arrowNumber) {
    bool activeKeyboardChanged = false;
    bool activeArrowChanged = false;

    if (editingEnd != endNo || editingArrow != arrowNumber) {
      activeArrowChanged = true;
      editingEnd = endNo;
      editingArrow = arrowNumber;
    }
    if (activeKeyboard != index) {
      activeKeyboardChanged = true;
      activeKeyboard = index;
    }

    if (editingEnd >= 0 &&
        (editingArrow == null ||
            editingArrow! < 0 ||
            editingArrow! >= model.arrowsPerEnd)) {
      var firstNull =
          scoring.firstNullIndexInEnd(model, model.participants[index!], editingEnd);
      if (firstNull != editingArrow) {
        activeArrowChanged = true;
      }
      editingArrow = firstNull ?? 0;
    }

    if (activeArrowChanged || activeKeyboardChanged) {
      notifyViewmodelUpdated();
      if (activeKeyboardChanged) notifyKeyboardShown();
      if (activeArrowChanged) notifActiveArrowChanged();
    }
  }

  /// [keyId] is the pressed key's id (or null for the delete key).
  void setScore(
      String? keyId, ScorekeeperMatch model, ScorekeeperMatchParticipant participant) {
    if (null != editingArrow && null != activeKeyboard) {
      final index = editingEnd * model.arrowsPerEnd + editingArrow!;
      final previousValue = participant.arrowScores[index];
      _repository.recordScoreEdit(participant.matchParticipantId, index, previousValue, keyId);

      if (editingArrow! < (model.arrowsPerEnd - 1)) {
        editingArrow = editingArrow! + 1;
      }

      notify(ArrowStateChangedEvent(
          participant: participant, end: editingEnd, arrow: editingArrow!));

      // The card just became fully filled in while already active: the
      // keyboard is about to collapse in favor of the Sign area (see
      // ScoreEntryFullPageWidgetState._requiresSignFocus) - scroll/focus to
      // it the same way activating an already-complete card does.
      if (model.signatureMode != 'none' &&
          !participant.signed &&
          scoring.firstNullIndex(participant) == null) {
        notifyKeyboardShown();
      }
    }
  }

  void nextArrow(ScorekeeperMatch model, ScorekeeperMatchParticipant participant) {
    bool activeArrowChanged = false;
    if (editingEnd >= 0 && (editingArrow == null)) {
      final firstNull = scoring.firstNullIndexInEnd(model, participant, editingEnd);
      if (firstNull != null) {
        editingArrow = firstNull;
        activeArrowChanged = true;
      }
    }
    if (activeArrowChanged) notifActiveArrowChanged();
  }

  void notifyKeyboardShown() {
    notify(KeyboardShownEvent());
  }

  void notifActiveArrowChanged() {
    notify(ActiveArrowChangedEvent());
  }
}

class ScoresViewmodelLoadedEvent extends ViewEvent {
  final ScorekeeperMatch model;

  ScoresViewmodelLoadedEvent({required this.model})
      : super("ScoresViewmodelLoadedEvent");
}

class ScoresViewmodelUpdatedEvent extends ViewEvent {
  ScoresViewmodelUpdatedEvent() : super("ScoresViewmodelUpdatedEvent");
}

class KeyboardShownEvent extends ViewEvent {
  KeyboardShownEvent() : super("KeyboardShownEvent");
}

class ActiveArrowChangedEvent extends ViewEvent {
  ActiveArrowChangedEvent() : super("ActiveArrowChangedEvent");
}

class ArrowStateChangedEvent extends ViewEvent {
  final ScorekeeperMatchParticipant participant;
  final int end;
  final int arrow;

  ArrowStateChangedEvent(
      {required this.participant, required this.end, required this.arrow})
      : super("ArrowStateChangedEvent");
}
