import { derived } from 'svelte/store';
import { language } from './stores';
import type { Language } from './types';

const dict = {
  home: { NL: 'Start', EN: 'Home' },
  noActiveMatchTitle: { NL: 'Geen actieve wedstrijd', EN: 'No active match' },
  noActiveMatchBody: {
    NL: 'Deze wedstrijd is niet (meer) actief op dit apparaat. Probeer het later opnieuw.',
    EN: 'This match is not (or no longer) active on this device. Please try again later.',
  },
  retry: { NL: 'Opnieuw proberen', EN: 'Retry' },
  score: { NL: 'Score', EN: 'Score' },
  arrowsShot: { NL: 'Pijlen', EN: 'Arrows' },
  split: { NL: 'Deel', EN: 'Split' },
  addParticipant: { NL: 'Deelnemer toevoegen', EN: 'Add participant' },
  editParticipant: { NL: 'Deelnemer bewerken', EN: 'Edit participant' },
  search: { NL: 'Zoeken...', EN: 'Search...' },
  unassigned: { NL: 'Niet toegewezen', EN: 'Unassigned' },
  available: { NL: 'Beschikbaar', EN: 'Available' },
  alreadyAssigned: { NL: 'Al toegewezen', EN: 'Already assigned' },
  addUnlistedParticipant: { NL: 'Onbekende deelnemer toevoegen', EN: 'Add unlisted participant' },
  federationNumber: { NL: 'Bondsnummer', EN: 'Federation number' },
  fullName: { NL: 'Volledige naam', EN: 'Full name' },
  save: { NL: 'Opslaan', EN: 'Save' },
  cancel: { NL: 'Annuleren', EN: 'Cancel' },
  remove: { NL: 'Verwijderen', EN: 'Remove' },
  edit: { NL: 'Bewerken', EN: 'Edit' },
  removeFailedTitle: { NL: 'Verwijderen mislukt', EN: 'Remove failed' },
  removeFailedBody: {
    NL: 'De deelnemer kon niet worden verwijderd. Probeer het later opnieuw.',
    EN: 'The participant could not be removed. Please try again later.',
  },
  saveFailedTitle: { NL: 'Opslaan mislukt', EN: 'Save failed' },
  saveFailedBody: {
    NL: 'De wijziging kon niet worden opgeslagen. Probeer het opnieuw.',
    EN: 'The change could not be saved. Please try again.',
  },
  ok: { NL: 'OK', EN: 'OK' },
  end: { NL: 'Reeks', EN: 'End' },
  runningTotal: { NL: 'Totaal', EN: 'Total' },
  conflictTitle: { NL: 'Synchronisatieconflict', EN: 'Sync conflict' },
  conflictBody: {
    NL: 'Voor een of meer pijlen is de score op de server gewijzigd terwijl dit apparaat ook een wijziging had. Kies welke waarde behouden moet blijven.',
    EN: 'One or more arrow scores were changed on the server while this device also had a pending change. Choose which value to keep.',
  },
  useMine: { NL: 'Gebruik mijn waarde', EN: 'Use mine' },
  useTheirs: { NL: 'Gebruik hun waarde', EN: 'Use theirs' },
  serverValue: { NL: 'Server', EN: 'Server' },
  myValue: { NL: 'Dit apparaat', EN: 'This device' },
  discardChange: { NL: 'Wijziging negeren', EN: 'Discard change' },
  participantConflictBody: {
    NL: 'Deze deelnemer is inmiddels aan een ander apparaat toegewezen. De wijziging van dit apparaat kan niet worden toegepast.',
    EN: 'This participant has since been assigned to a different device. This device’s change can no longer be applied.',
  },
  unknownParticipant: { NL: 'Onbekende deelnemer', EN: 'Unknown participant' },
  hideKeyboard: { NL: 'Toetsenbord verbergen', EN: 'Hide keyboard' },
  deleteKey: { NL: 'Wissen', EN: 'Delete' },
  loading: { NL: 'Laden...', EN: 'Loading...' },
  participantNotAllowed: {
    NL: 'Deze wijziging is niet toegestaan voor deze wedstrijd.',
    EN: 'This change is not allowed for this match.',
  },
  categoryRequired: { NL: 'Verplicht', EN: 'Required' },
} as const;

export type TranslationKey = keyof typeof dict;

export function translate(key: TranslationKey, lang: Language): string {
  return dict[key][lang] ?? dict[key].EN;
}

export const t = derived(language, ($language) => (key: TranslationKey) => translate(key, $language));
