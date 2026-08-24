import 'package:centaur_scores/src/repository/repository.dart';

/// Flat NL/EN dictionary, ported from the pattern used by
/// `centaur-scores-mobile-web-scoring/src/lib/i18n.ts` - language is a
/// runtime toggle seeded from the QR/API URL, unrelated to device locale,
/// so this deliberately doesn't use Flutter's ARB/gen-l10n pipeline.
const Map<String, Map<String, String>> _dict = {
  'appTitle': {'NL': 'Centaur Scores', 'EN': 'Centaur Scores'},
  'home': {'NL': 'Start', 'EN': 'Home'},
  'noActiveMatchTitle': {'NL': 'Geen actieve wedstrijd', 'EN': 'No active match'},
  'noActiveMatchBody': {
    'NL': 'Er is momenteel geen actieve wedstrijd voor dit apparaat gevonden.',
    'EN': 'No active match could be found for this device right now.'
  },
  'retry': {'NL': 'Opnieuw proberen', 'EN': 'Retry'},
  'score': {'NL': 'Score', 'EN': 'Score'},
  'arrowsShot': {'NL': 'Pijlen geschoten', 'EN': 'Arrows shot'},
  'split': {'NL': 'Deel', 'EN': 'Split'},
  'addParticipant': {'NL': 'Deelnemer toevoegen', 'EN': 'Add participant'},
  'editParticipant': {'NL': 'Deelnemer bewerken', 'EN': 'Edit participant'},
  'search': {'NL': 'Zoeken', 'EN': 'Search'},
  'unassigned': {'NL': 'Niet toegewezen', 'EN': 'Unassigned'},
  'available': {'NL': 'Beschikbaar', 'EN': 'Available'},
  'alreadyAssigned': {'NL': 'Al toegewezen', 'EN': 'Already assigned'},
  'addUnlistedParticipant': {
    'NL': 'Niet-vermelde deelnemer toevoegen',
    'EN': 'Add unlisted participant'
  },
  'federationNumber': {'NL': 'Bondsnummer', 'EN': 'Federation number'},
  'fullName': {'NL': 'Volledige naam', 'EN': 'Full name'},
  'save': {'NL': 'Opslaan', 'EN': 'Save'},
  'cancel': {'NL': 'Annuleren', 'EN': 'Cancel'},
  'remove': {'NL': 'Verwijderen', 'EN': 'Remove'},
  'edit': {'NL': 'Bewerken', 'EN': 'Edit'},
  'removeFailedTitle': {'NL': 'Verwijderen mislukt', 'EN': 'Remove failed'},
  'removeFailedBody': {
    'NL': 'Deze deelnemer kon niet worden verwijderd.',
    'EN': 'This participant could not be removed.'
  },
  'saveFailedTitle': {'NL': 'Opslaan mislukt', 'EN': 'Save failed'},
  'saveFailedBody': {
    'NL': 'De wijziging kon niet worden opgeslagen.',
    'EN': 'The change could not be saved.'
  },
  'ok': {'NL': 'OK', 'EN': 'OK'},
  'conflictTitle': {'NL': 'Synchronisatieconflict', 'EN': 'Sync conflict'},
  'conflictBody': {
    'NL': 'Deze pijl is ondertussen ook op een ander apparaat gewijzigd.',
    'EN': 'This arrow was also changed on another device in the meantime.'
  },
  'useMine': {'NL': 'Mijn waarde gebruiken', 'EN': 'Use mine'},
  'useTheirs': {'NL': 'Server waarde gebruiken', 'EN': 'Use theirs'},
  'serverValue': {'NL': 'Op server', 'EN': 'On server'},
  'myValue': {'NL': 'Mijn waarde', 'EN': 'My value'},
  'discardChange': {'NL': 'Wijziging negeren', 'EN': 'Discard change'},
  'participantConflictBody': {
    'NL':
        'Deze deelnemer is inmiddels aan een ander apparaat toegewezen. Niet-gesynchroniseerde wijzigingen kunnen niet worden verzonden.',
    'EN':
        'This participant has been reassigned to another device. Unsynced changes for them cannot be sent.'
  },
  'unknownParticipant': {'NL': 'Onbekende deelnemer', 'EN': 'Unknown participant'},
  'loading': {'NL': 'Laden...', 'EN': 'Loading...'},
  'categoryRequired': {'NL': 'Kies een waarde', 'EN': 'Choose a value'},
  'enterScoresNow': {'NL': 'Scores invoeren', 'EN': 'Enter scores now'},
  'rescanQr': {'NL': 'Opnieuw koppelen (QR)', 'EN': 'Re-scan QR / change match'},
  'rescanQrConfirmTitle': {'NL': 'Opnieuw koppelen?', 'EN': 'Re-pair device?'},
  'rescanQrConfirmBody': {
    'NL':
        'Hiermee wordt de koppeling met de huidige wedstrijd verbroken en keer je terug naar de QR-scanner.',
    'EN': 'This disconnects the current match pairing and returns you to the QR scanner.'
  },
  'scanQrTitle': {'NL': 'Scan de QR-code', 'EN': 'Scan the QR code'},
  'scanQrInstructions': {
    'NL': 'Richt de camera op de QR-code die bij dit apparaat/deze lijn hoort.',
    'EN': "Point the camera at the QR code printed for this device/lane."
  },
  'enterUrlManually': {'NL': 'URL handmatig invoeren', 'EN': 'Enter URL manually'},
  'manualUrlLabel': {'NL': 'Koppel-URL', 'EN': 'Pairing URL'},
  'manualUrlSubmit': {'NL': 'Koppelen', 'EN': 'Pair'},
  'invalidUrl': {
    'NL': 'Deze QR-code/URL kon niet worden herkend.',
    'EN': 'This QR code/URL could not be recognized.'
  },
  'cameraPermissionDenied': {
    'NL': 'Geen cameratoegang. Voer de URL handmatig in.',
    'EN': 'No camera access. Enter the URL manually instead.'
  },
  'noParticipantsYet': {
    'NL': 'Nog geen deelnemers op dit apparaat.',
    'EN': 'No participants on this device yet.'
  },
  'language': {'NL': 'Taal', 'EN': 'Language'},
  'scanForMatch': {'NL': 'Scan QR-code', 'EN': 'Scan QR code'},
  'pairingErrorTitle': {'NL': 'Verbinden mislukt', 'EN': 'Connection failed'},
};

String translate(String key, String lang) {
  final entry = _dict[key];
  if (entry == null) return key;
  return entry[lang] ?? entry['EN'] ?? key;
}

/// Convenience for use inside `build()` methods, reading the app's current
/// language directly off [MatchRepository].
String t(String key) => translate(key, MatchRepository().language);
