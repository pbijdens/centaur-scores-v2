<script lang="ts">
  import { formatLocalDate } from '../date'
  import { competitionPath, matchPath, narrowcastPath, navigateOnClick } from '../router'
  import type { Competition, Language, MatchListItem } from '../types'

  export let matches: MatchListItem[]
  export let competitions: Competition[]
  export let language: Language
  export let labels: Record<string, string>
  export let quickLinks: { path: string; icon: string; title: string; description: string }[]
  export let onOpenMatch: (match: MatchListItem) => void
  export let onNavigate: (path: string) => void
  export let onDeactivateAll: () => void

  function localToday(): string {
    const now = new Date()
    return new Date(now.getTime() - now.getTimezoneOffset() * 60000).toISOString().slice(0, 10)
  }

  $: today = localToday()
  $: narrowcastScopes = [...new Set(
    matches
      .filter((match) => match.isOpen || match.date >= today)
      .flatMap((match) => (match.liveScopes ?? []).map((scope) => scope.scope))
  )].sort((a, b) => a.localeCompare(b))
</script>

<div class="page-intro">
  <div><p class="eyebrow">{labels.eyebrowTenantHome}</p><h1>{labels.home}</h1><p class="muted">{labels.manage}</p></div>
  <a class="primary" href="/matches" on:click={(event) => navigateOnClick(event, () => onNavigate('/matches'))}>{labels.matches}</a>
</div>
<div class="dashboard-grid">
  <section class="panel">
    <div class="panel-heading">
      <div><p class="eyebrow">{labels.eyebrowLive}</p><h2>{labels.open}</h2></div>
      <button class="text-button" on:click={onDeactivateAll}>{labels.deactivateAll}</button>
    </div>
    {#each matches.filter((match) => match.isOpen) as match}
      <a class="match-row" href={matchPath(match.id)} on:click={(event) => navigateOnClick(event, () => onOpenMatch(match))}>
        <span class="status-dot"></span>
        <span><strong>{match.name}</strong><small>{formatLocalDate(match.date, language)} · {match.participantCount} {labels.participantsCountLabel}</small></span>
        <span class="arrow">→</span>
      </a>
    {/each}
  </section>
  <section class="panel">
    <div class="panel-heading">
      <div><p class="eyebrow">{labels.eyebrowNext}</p><h2>{labels.upcoming}</h2></div>
      <a class="text-button" href="/matches" on:click={(event) => navigateOnClick(event, () => onNavigate('/matches'))}>{labels.seeAll}</a>
    </div>
    {#each matches.filter((match) => !match.isOpen).slice(0, 3) as match}
      <a class="match-row" href={matchPath(match.id)} on:click={(event) => navigateOnClick(event, () => onOpenMatch(match))}>
        <span class="date-block"><b>{match.date.slice(8, 10)}</b><small>{match.date.slice(5, 7)}</small></span>
        <span><strong>{match.name}</strong><small>{formatLocalDate(match.date, language)}</small></span>
        <span class="arrow">→</span>
      </a>
    {/each}
  </section>
  <section class="panel wide">
    <div class="panel-heading">
      <div><p class="eyebrow">{labels.eyebrowSeason}</p><h2>{labels.competitions}</h2></div>
      <a class="text-button" href="/competitions" on:click={(event) => navigateOnClick(event, () => onNavigate('/competitions'))}>{labels.seeAll}</a>
    </div>
    {#each competitions.slice(0, 3) as competition}
      <a class="match-row" href={competitionPath(competition.id)} on:click={(event) => navigateOnClick(event, () => onNavigate(competitionPath(competition.id)))}>
        <span class="competition-icon">◎</span>
        <span><strong>{competition.name}</strong><small>{formatLocalDate(competition.startDate, language)} – {formatLocalDate(competition.endDate, language)}</small></span>
        <span class="arrow">→</span>
      </a>
    {/each}
  </section>
  <section class="panel wide">
    <div class="panel-heading">
      <div><p class="eyebrow">{labels.eyebrowLive}</p><h2>{labels.narrowcastLinksLabel}</h2><p class="muted">{labels.narrowcastLinksHint}</p></div>
    </div>
    {#if narrowcastScopes.length === 0}
      <p class="muted">{labels.noNarrowcastLinks}</p>
    {:else}
      {#each narrowcastScopes as scope}
        <a class="match-row" href={narrowcastPath(scope)} target="_blank" rel="noopener">
          <span class="competition-icon">▶</span>
          <span><strong>{scope}</strong><small>{`/narrowcast/${scope}`}</small></span>
          <span class="arrow">→</span>
        </a>
      {/each}
    {/if}
  </section>
</div>
<div class="pb-tile-grid">
  {#each quickLinks as item}
    <a class="pb-tile" href={`/${item.path}`} on:click={(event) => navigateOnClick(event, () => onNavigate(`/${item.path}`))}>
      <span class="pb-tile-icon" aria-hidden="true">{item.icon}</span>
      <span class="pb-tile-title">{item.title}</span>
      <span class="pb-tile-description">{item.description}</span>
    </a>
  {/each}
</div>
