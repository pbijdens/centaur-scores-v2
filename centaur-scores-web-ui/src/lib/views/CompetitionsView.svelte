<script lang="ts">
  import type { ApiClient } from '../api'
  import { formatLocalDate } from '../date'
  import { labelForError } from '../errors'
  import { competitionPath, navigateOnClick } from '../router'
  import type { Competition, Language } from '../types'

  export let api: ApiClient
  export let competitions: Competition[]
  export let language: Language
  export let labels: Record<string, string>
  export let onOpenCompetition: (competition: Competition) => void
  export let onChanged: () => void

  let filter = ''
  let filterMode: 'all' | 'future' | 'past' = 'all'
  let showAddForm = false
  let newName = ''
  let newStartDate = ''
  let newEndDate = ''
  let createError = ''

  function localToday(): string {
    const now = new Date()
    return new Date(now.getTime() - now.getTimezoneOffset() * 60000).toISOString().slice(0, 10)
  }

  $: today = localToday()
  $: named = competitions.filter((competition) => competition.name.toLowerCase().includes(filter.toLowerCase()))
  $: activeCompetitions = named.filter((competition) => competition.startDate <= today && competition.endDate >= today).sort((a, b) => a.name.localeCompare(b.name))
  $: futureCompetitions = named.filter((competition) => competition.startDate > today).sort((a, b) => a.startDate.localeCompare(b.startDate))
  $: pastCompetitions = named.filter((competition) => competition.endDate < today).sort((a, b) => b.endDate.localeCompare(a.endDate))
  $: showFuture = filterMode !== 'past'
  $: showPast = filterMode !== 'future'

  async function submitAdd() {
    if (!newName.trim() || !newStartDate || !newEndDate) return
    createError = ''
    try {
      const competition = await api.createCompetition({ name: newName.trim(), startDate: newStartDate, endDate: newEndDate, groupByCategoryIds: [] })
      newName = ''
      newStartDate = ''
      newEndDate = ''
      showAddForm = false
      onChanged()
      onOpenCompetition(competition)
    } catch (error) {
      createError = labelForError(error, labels, 'competitionCreateError')
    }
  }
</script>

<div class="page-intro">
  <div><p class="eyebrow">{labels.eyebrowTenantData}</p><h1>{labels.competitions}</h1><p class="muted">{labels.competitionsDescription}</p></div>
  <button class="primary" on:click={() => (showAddForm = !showAddForm)}>+ {labels.newCompetition}</button>
</div>
{#if showAddForm}
  <form class="inline-form" on:submit|preventDefault={submitAdd}>
    <label>{labels.competitionNameLabel}<input bind:value={newName} /></label>
    <label>{labels.competitionStartDateLabel}<input type="date" bind:value={newStartDate} /></label>
    <label>{labels.competitionEndDateLabel}<input type="date" bind:value={newEndDate} /></label>
    <button class="primary" type="submit" disabled={!newName.trim() || !newStartDate || !newEndDate}>{labels.save}</button>
  </form>
  {#if createError}<p class="error">{createError}</p>{/if}
{/if}
<div class="toolbar">
  <input placeholder={labels.filterCompetitionsPlaceholder} bind:value={filter} />
  <select bind:value={filterMode}>
    <option value="all">{labels.allCompetitions}</option>
    <option value="future">{labels.futureCompetitions}</option>
    <option value="past">{labels.pastCompetitions}</option>
  </select>
</div>
<section class="list-panel">
  {#if activeCompetitions.length === 0 && futureCompetitions.length === 0 && pastCompetitions.length === 0}<p class="empty-state">{labels.emptyState}</p>{/if}
  {#each activeCompetitions as competition}
    <a class="list-row" href={competitionPath(competition.id)} on:click={(event) => navigateOnClick(event, () => onOpenCompetition(competition))}>
      <span class="competition-icon">◎</span>
      <span><strong>{competition.name}</strong><small>{formatLocalDate(competition.startDate, language)} – {formatLocalDate(competition.endDate, language)} · {competition.rounds?.length ?? 0} rounds</small></span>
      <span class="tag">{labels.statusActive}</span>
      <span class="arrow">→</span>
    </a>
  {/each}
  {#if showFuture}
    {#each futureCompetitions as competition}
      <a class="list-row" href={competitionPath(competition.id)} on:click={(event) => navigateOnClick(event, () => onOpenCompetition(competition))}>
        <span class="competition-icon">◎</span>
        <span><strong>{competition.name}</strong><small>{formatLocalDate(competition.startDate, language)} – {formatLocalDate(competition.endDate, language)} · {competition.rounds?.length ?? 0} rounds</small></span>
        <span class="tag">{labels.statusPlanned}</span>
        <span class="arrow">→</span>
      </a>
    {/each}
  {/if}
  {#if showPast}
    {#each pastCompetitions as competition}
      <a class="list-row past" href={competitionPath(competition.id)} on:click={(event) => navigateOnClick(event, () => onOpenCompetition(competition))}>
        <span class="competition-icon">◎</span>
        <span><strong>{competition.name}</strong><small>{formatLocalDate(competition.startDate, language)} – {formatLocalDate(competition.endDate, language)} · {competition.rounds?.length ?? 0} rounds</small></span>
        <span class="tag">{labels.statusPast}</span>
        <span class="arrow">→</span>
      </a>
    {/each}
  {/if}
</section>

<style>
  .list-row .tag {
    flex: 0 0 72px;
    text-align: right;
  }

  .list-row.past {
    opacity: .65;
  }
</style>
