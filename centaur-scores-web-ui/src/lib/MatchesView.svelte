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
  <div><p class="eyebrow">TENANT DATA</p><h1>{labels.matches}</h1><p class="muted">Every match from first arrow to final result.</p></div>
  <button class="primary">+ {labels.add}</button>
</div>
<div class="toolbar">
  <input placeholder="Filter matches" bind:value={filter} />
  <select><option>All matches</option><option>Future matches</option><option>Past matches</option></select>
</div>
<section class="list-panel">
  {#each filteredMatches as match}
    <button class="list-row" on:click={() => onOpenMatch(match)}>
      <span class:live={match.isOpen} class="list-indicator"></span>
      <span><strong>{match.name}</strong><small>{formatLocalDate(match.date, language)} · {match.participantCount ?? 0} participants</small></span>
      <span class="tag">{match.isOpen ? 'OPEN' : 'PLANNED'}</span>
      <span class="arrow">→</span>
    </button>
  {/each}
</section>
