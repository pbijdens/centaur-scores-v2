<script lang="ts">
  import { formatLocalDate } from './date'
  import type { Competition, Language, Match } from './types'

  export let matches: Match[]
  export let competitions: Competition[]
  export let language: Language
  export let labels: Record<string, string>
  export let quickLinks: [string, string][]
  export let onOpenMatch: (match: Match) => void
  export let onNavigate: (path: string) => void
  export let onDeactivateAll: () => void
</script>

<div class="page-intro">
  <div><p class="eyebrow">TENANT HOME</p><h1>{labels.home}</h1><p class="muted">{labels.manage}</p></div>
  <button class="primary" on:click={() => onNavigate('/matches')}>+ {labels.matches}</button>
</div>
<div class="dashboard-grid">
  <section class="panel">
    <div class="panel-heading">
      <div><p class="eyebrow">LIVE</p><h2>{labels.open}</h2></div>
      <button class="text-button" on:click={onDeactivateAll}>Deactivate all</button>
    </div>
    {#each matches.filter((match) => match.isOpen) as match}
      <button class="match-row" on:click={() => onOpenMatch(match)}>
        <span class="status-dot"></span>
        <span><strong>{match.name}</strong><small>{formatLocalDate(match.date, language)} · {match.participantCount ?? 0} participants</small></span>
        <span class="arrow">→</span>
      </button>
    {/each}
  </section>
  <section class="panel">
    <div class="panel-heading">
      <div><p class="eyebrow">NEXT</p><h2>{labels.upcoming}</h2></div>
      <button class="text-button" on:click={() => onNavigate('/matches')}>See all</button>
    </div>
    {#each matches.filter((match) => !match.isOpen).slice(0, 3) as match}
      <button class="match-row" on:click={() => onOpenMatch(match)}>
        <span class="date-block"><b>{match.date.slice(8, 10)}</b><small>{match.date.slice(5, 7)}</small></span>
        <span><strong>{match.name}</strong><small>{formatLocalDate(match.date, language)}</small></span>
        <span class="arrow">→</span>
      </button>
    {/each}
  </section>
  <section class="panel wide">
    <div class="panel-heading">
      <div><p class="eyebrow">SEASON</p><h2>{labels.competitions}</h2></div>
      <button class="text-button" on:click={() => onNavigate('/competitions')}>See all</button>
    </div>
    {#each competitions.slice(0, 3) as competition}
      <button class="match-row">
        <span class="competition-icon">◎</span>
        <span><strong>{competition.name}</strong><small>{formatLocalDate(competition.startDate, language)} – {formatLocalDate(competition.endDate, language)}</small></span>
        <span class="arrow">→</span>
      </button>
    {/each}
  </section>
</div>
<div class="quick-links">
  {#each quickLinks as item}
    <button on:click={() => onNavigate(`/${item[0]}`)}><span>◇</span>{item[1]}</button>
  {/each}
</div>
