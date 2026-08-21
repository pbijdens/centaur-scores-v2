<script lang="ts">
  import { formatLocalDate } from './date'
  import type { Language, Match } from './types'

  export let matches: Match[]
  export let language: Language
  export let labels: Record<string, string>
  export let onOpenMatch: (match: Match) => void

  let filter = ''
  $: filteredMatches = matches.filter((match) => match.name.toLowerCase().includes(filter.toLowerCase()))
</script>

<div class="page-intro">
  <div><p class="eyebrow">{labels.eyebrowTenantData}</p><h1>{labels.matches}</h1><p class="muted">{labels.matchesDescription}</p></div>
  <button class="primary">+ {labels.add}</button>
</div>
<div class="toolbar">
  <input placeholder={labels.filterMatchesPlaceholder} bind:value={filter} />
  <select><option>{labels.allMatches}</option><option>{labels.futureMatches}</option><option>{labels.pastMatches}</option></select>
</div>
<section class="list-panel">
  {#each filteredMatches as match}
    <button class="list-row" on:click={() => onOpenMatch(match)}>
      <span class:live={match.isOpen} class="list-indicator"></span>
      <span><strong>{match.name}</strong><small>{formatLocalDate(match.date, language)} · {match.participantCount ?? 0} participants</small></span>
      <span class="tag">{match.isOpen ? labels.statusOpen : labels.statusPlanned}</span>
      <span class="arrow">→</span>
    </button>
  {/each}
</section>
