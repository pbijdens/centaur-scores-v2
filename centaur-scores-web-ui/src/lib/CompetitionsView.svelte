<script lang="ts">
  import { formatLocalDate } from './date'
  import type { Competition, Language } from './types'

  export let competitions: Competition[]
  export let language: Language
  export let labels: Record<string, string>
</script>

<div class="page-intro">
  <div><p class="eyebrow">{labels.eyebrowTenantData}</p><h1>{labels.competitions}</h1><p class="muted">{labels.competitionsDescription}</p></div>
  <button class="primary">+ {labels.add}</button>
</div>
<section class="list-panel">
  {#each competitions as competition}
    <button class="list-row">
      <span class="competition-icon">◎</span>
      <span><strong>{competition.name}</strong><small>{formatLocalDate(competition.startDate, language)} – {formatLocalDate(competition.endDate, language)} · {competition.rounds?.length ?? competition.roundCount ?? 0} rounds</small></span>
      <span class="tag">{labels.statusActive}</span>
      <span class="arrow">→</span>
    </button>
  {/each}
</section>
