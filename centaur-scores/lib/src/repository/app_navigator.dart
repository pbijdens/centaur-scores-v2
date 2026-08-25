import 'package:flutter/foundation.dart';

/// Which screen is currently shown, once the device is paired (before
/// pairing, [AppShell] shows the QR scanner/manual-entry flow directly and
/// doesn't consult this at all - see `MatchRepository.isConfigured`).
sealed class AppScreen {
  const AppScreen();
}

class AppLoadingScreen extends AppScreen {
  const AppLoadingScreen();
}

class NoActiveMatchScreen extends AppScreen {
  const NoActiveMatchScreen();
}

/// Shown when the match fetch that immediately follows a fresh pairing
/// fails for any reason other than "no active match" (404/409) - a network
/// failure, a 5xx, an unparsable response, etc. Without this, [AppShell]
/// would be left showing [AppLoadingScreen]'s spinner forever, since that
/// fetch is fire-and-forget from the background sync timer's point of view.
class PairingErrorScreen extends AppScreen {
  final String message;
  const PairingErrorScreen(this.message);
}

class HomeScreen extends AppScreen {
  const HomeScreen();
}

class AddParticipantScreen extends AppScreen {
  const AddParticipantScreen();
}

class EditParticipantScreen extends AppScreen {
  final String matchParticipantId;
  const EditParticipantScreen(this.matchParticipantId);
}

/// The kept grid score-entry screen. Shows every participant on this device
/// at once (side-by-side columns), so - unlike the mobile web app's
/// per-participant Score Card - there's no per-participant variant here.
class ScoreCardScreen extends AppScreen {
  const ScoreCardScreen();
}

/// Presentation/navigation state, separate from [MatchRepository] (match
/// data) by design - this is purely "what's on screen," modeled as a small
/// logical parent-stack (not Flutter's own Navigator stack) so back
/// navigation always goes to the "logical parent" per MOBILE-SCORE-APP.md,
/// regardless of how a screen was reached.
class AppNavigator with ChangeNotifier {
  static final AppNavigator _instance = AppNavigator._internal();

  factory AppNavigator() {
    return _instance;
  }

  AppNavigator._internal();

  AppScreen current = const AppLoadingScreen();
  final List<AppScreen> _historyStack = [];

  void navigate(AppScreen next, {bool replace = false, bool resetStack = false}) {
    if (resetStack) {
      _historyStack.clear();
    } else if (!replace) {
      _historyStack.add(current);
    }
    current = next;
    notifyListeners();
  }

  /// Returns to the logical parent screen, defaulting to Home when the
  /// history stack is empty (e.g. the hardware back button on a screen
  /// reached via a reset-stack navigation).
  void goToParent() {
    current = _historyStack.isNotEmpty ? _historyStack.removeLast() : const HomeScreen();
    notifyListeners();
  }

  void resetHistoryStack() {
    _historyStack.clear();
  }
}
