<script lang="ts">
  import { formatLocalDate } from './date'
  import type { Language, Match } from './types'

  export let match: Match
  export let language: Language
  export let labels: Record<string, string>
  export let onBack: () => void
  export let onToggleOpen: () => void
</script>

<button class="back-link" on:click={onBack}>← {labels.matches}</button>
<div class="page-intro">
  <div><p class="eyebrow">{labels.eyebrowMatch}</p><h1>{match.name}</h1><p class="muted">{formatLocalDate(match.date, language)}</p></div>
  <button class="primary" on:click={onToggleOpen}>{match.isOpen ? labels.deactivate : labels.activate}</button>
</div>
<div class="detail-grid">
  <section class="panel">
    <p class="eyebrow">{labels.eyebrowParticipants}</p>
    <h2>{match.participantCount ?? 0}</h2>
    <p class="muted">{labels.matchParticipantsDescription}</p>
  </section>
  <section class="panel">
    <p class="eyebrow">{labels.eyebrowResults}</p>
    <h2>{labels.matchResultsLive}</h2>
    <p class="muted">{labels.matchResultsDescription}</p>
  </section>
</div>
