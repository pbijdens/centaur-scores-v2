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
  <div><p class="eyebrow">MATCH</p><h1>{match.name}</h1><p class="muted">{formatLocalDate(match.date, language)}</p></div>
  <button class="primary" on:click={onToggleOpen}>{match.isOpen ? 'Deactivate' : 'Activate'}</button>
</div>
<div class="detail-grid">
  <section class="panel">
    <p class="eyebrow">PARTICIPANTS</p>
    <h2>{match.participantCount ?? 0}</h2>
    <p class="muted">Manage assignments, devices and entered scores.</p>
  </section>
  <section class="panel">
    <p class="eyebrow">RESULTS</p>
    <h2>Live</h2>
    <p class="muted">Export and printable results are available from this workspace.</p>
  </section>
</div>
