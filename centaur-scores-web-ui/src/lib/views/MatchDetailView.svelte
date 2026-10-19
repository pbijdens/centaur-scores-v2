<script lang="ts">
  import type { ApiClient } from '../api'
  import { formatLocalDate } from '../date'
  import { labelForError } from '../errors'
  import { parseMatchKeyboardConfig } from '../matchConfig'
  import MatchPageHeader from '../MatchPageHeader.svelte'
  import type { MatchHeaderHandlers } from '../pageActions'
  import { participantMatchesFilter } from '../participantFilter'
  import { matchAddParticipantsPath, matchParticipantPath, navigateOnClick } from '../router'
  import type { Category, Language, Match, MatchParticipant, ScopeConflict } from '../types'

  export let api: ApiClient
  export let match: Match
  export let categories: Category[]
  export let language: Language
  export let labels: Record<string, string>
  export let headerHandlers: MatchHeaderHandlers
  export let onBack: () => void
  export let onAddParticipants: () => void
  export let onOpenParticipant: (participantId: string) => void

  type ResultRow = { participantId: string; total: number }
  type SortBy = 'name' | 'score'
  type GroupBy = 'none' | 'category' | 'device' | 'signed'

  const storedSortBy = localStorage.getItem('centaur-match-sort-by')
  const storedGroupBy = localStorage.getItem('centaur-match-group-by')
  let sortBy: SortBy = storedSortBy === 'score' ? 'score' : 'name'
  let groupBy: GroupBy = storedGroupBy === 'category' || storedGroupBy === 'device' || storedGroupBy === 'signed' ? storedGroupBy : 'none'
  let unlistedOnly = false
  let participantFilter = ''
  let results: ResultRow[] = []
  let scopeConflicts: ScopeConflict[] = []
  let claimingScope = false
  let claimScopeError = ''

  $: participants = match.participants ?? []
  $: devices = match.devices ?? []

  $: keyboardConfig = parseMatchKeyboardConfig(match.keyboardJson)
  $: matchCategories = keyboardConfig.categoryOrder.map((id) => categories.find((category) => category.id === id)).filter((category): category is Category => !!category)

  function categoryLabel(participantCategories: Record<string, number>): string {
    return matchCategories
      .map((category) => category.values.find((value) => value.valueId === participantCategories[category.id])?.name)
      .filter((value): value is string => !!value)
      .join(' / ')
  }

  function participantDetailLabel(participant: MatchParticipant): string {
    return [participant.federationNumber, categoryLabel(participant.categories)].filter((part): part is string => !!part).join(' / ')
  }

  $: totalsByParticipantId = new Map(results.map((row) => [row.participantId, row.total]))

  function participantTotal(participantId: string, totals: Map<string, number>): number {
    return totals.get(participantId) ?? 0
  }

  function deviceName(deviceId: string | null | undefined): string {
    return devices.find((device) => device.id === deviceId)?.name ?? labels.unassignedGroup
  }

  function setSortBy(value: string) {
    sortBy = value === 'score' ? 'score' : 'name'
    localStorage.setItem('centaur-match-sort-by', sortBy)
  }

  function setGroupBy(value: string) {
    groupBy = value === 'category' || value === 'device' || value === 'signed' ? value : 'none'
    localStorage.setItem('centaur-match-group-by', groupBy)
  }

  $: unlistedParticipantCount = participants.filter((participant) => !participant.participantListMemberId).length
  $: visibleParticipants = participants
    .filter((participant) => !unlistedOnly || !participant.participantListMemberId)
    .filter((participant) => participantMatchesFilter(participant, participantFilter, categories))

  $: sortedParticipants = [...visibleParticipants].sort((a, b) => {
    if (sortBy === 'score') return participantTotal(b.id, totalsByParticipantId) - participantTotal(a.id, totalsByParticipantId)
    return (a.fullName || a.lastName).localeCompare(b.fullName || b.lastName)
  })

  $: groupedParticipants = (() => {
    if (groupBy === 'none') return [{ key: '', items: sortedParticipants }]
    if (groupBy === 'signed') {
      // Unsigned always sorts first here (unlike the alphabetical-with-unassigned-last order below),
      // per the spec: unsigned scorecards are the ones needing attention.
      return [
        { key: labels.groupByUnsigned, items: sortedParticipants.filter((participant) => !participant.signed) },
        { key: labels.groupBySigned, items: sortedParticipants.filter((participant) => participant.signed) }
      ].filter((group) => group.items.length > 0)
    }
    const groups = new Map<string, typeof sortedParticipants>()
    for (const participant of sortedParticipants) {
      const key = groupBy === 'category' ? categoryLabel(participant.categories) || labels.unassignedGroup : deviceName(participant.deviceId)
      groups.set(key, [...(groups.get(key) ?? []), participant])
    }
    const unassignedKey = labels.unassignedGroup
    return [...groups.entries()]
      .sort((a, b) => (a[0] === unassignedKey ? 1 : b[0] === unassignedKey ? -1 : a[0].localeCompare(b[0])))
      .map(([key, items]) => ({ key, items }))
  })()

  async function loadResults() {
    try {
      const rows = (await api.fetchMatchResults(match.id)) as { participantId: string; total: number }[]
      results = rows
    } catch { results = [] }
  }
  $: if (match.id) loadResults()

  async function loadScopeConflicts() {
    if (!match.isOpen) { scopeConflicts = []; return }
    try {
      scopeConflicts = await api.fetchScopeConflicts(match.id)
    } catch {
      scopeConflicts = []
    }
  }
  $: if (match.id) loadScopeConflicts()

  async function claimScope() {
    claimScopeError = ''
    claimingScope = true
    try {
      await api.claimScope(match.id)
      await loadScopeConflicts()
    } catch (error) {
      claimScopeError = labelForError(error, labels, 'claimScopeError')
    } finally {
      claimingScope = false
    }
  }

  function scopeConflictRowText(conflict: ScopeConflict): string {
    return labels.scopeConflictRow
      .replace('{tenantName}', conflict.tenantName)
      .replace('{count}', String(conflict.matchCount))
      .replace('{scope}', conflict.scope)
  }
</script>

<MatchPageHeader {api} {match} {language} {labels} handlers={headerHandlers} current="match" title={match.name} subtitle={formatLocalDate(match.date, language)} backLabel={labels.matches} {onBack} />
{#if scopeConflicts.length > 0}
  <div class="panel scope-conflict-warning">
    <strong>{labels.scopeConflictWarningTitle}</strong>
    <ul>
      {#each scopeConflicts as conflict}<li>{scopeConflictRowText(conflict)}</li>{/each}
    </ul>
    {#if claimScopeError}<p class="error">{claimScopeError}</p>{/if}
    <button class="primary" disabled={claimingScope} on:click={claimScope}>{labels.claimScopeAction}</button>
  </div>
{/if}

<section class="panel section-gap">
  <div class="editor-row">
    <label class="participant-filter">{labels.filterLabel}
      <input type="search" placeholder={labels.filterMatchParticipantsPlaceholder} bind:value={participantFilter} />
    </label>
    <label>{labels.sortByLabel}
      <select value={sortBy} on:change={(event) => setSortBy(event.currentTarget.value)}>
        <option value="name">{labels.sortByName}</option>
        <option value="score">{labels.sortByScore}</option>
      </select>
    </label>
    <label>{labels.groupByLabel}
      <select value={groupBy} on:change={(event) => setGroupBy(event.currentTarget.value)}>
        <option value="none">{labels.groupByNone}</option>
        <option value="category">{labels.groupByCategory}</option>
        <option value="device">{labels.groupByDevice}</option>
        {#if match.signatureMode !== 'none'}<option value="signed">{labels.groupBySignedOption}</option>{/if}
      </select>
    </label>
    {#if unlistedParticipantCount > 0}
      <label class="checkbox-label unlisted-filter">
        <input type="checkbox" bind:checked={unlistedOnly} />
        {labels.filterUnlistedLabel} ({unlistedParticipantCount})
      </label>
    {/if}
    <a class="primary" href={matchAddParticipantsPath(match.id)} on:click={(event) => navigateOnClick(event, onAddParticipants)}>+ {labels.addParticipant}</a>
  </div>

  {#if participants.length === 0}<p class="empty-state">{labels.emptyState}</p>{:else if sortedParticipants.length === 0}<p class="empty-state">{participantFilter.trim() ? labels.noParticipantsMatchFilter : labels.noUnlistedParticipants}</p>{/if}
  {#each groupedParticipants as group}
    {#if group.key}<h2 class="group-heading">{group.key}</h2>{/if}
    <div class="list-panel">
      {#each group.items as participant}
        <a class="list-row match-participant-row" class:unlisted-row={!participant.participantListMemberId} href={matchParticipantPath(match.id, participant.id)} on:click={(event) => navigateOnClick(event, () => onOpenParticipant(participant.id))}>
          {#if match.signatureMode !== 'none'}
            <span class="management-icon signed-icon" class:is-signed={participant.signed} role="img" title={participant.signed ? labels.signedBadge : labels.unsignedBadge} aria-label={participant.signed ? labels.signedBadge : labels.unsignedBadge}>{participant.signed ? '✓' : '○'}</span>
          {:else}
            <span class="management-icon">◇</span>
          {/if}
          <span class="participant-name"><strong>{participant.fullName || participant.lastName}</strong>{#if !participant.participantListMemberId}<span class="unlisted-tag">{labels.unlistedParticipantsWarning}</span>{/if}{#if participantDetailLabel(participant)}<span class="member-categories"> ({participantDetailLabel(participant)})</span>{/if}</span>
          <strong class="participant-score">{participantTotal(participant.id, totalsByParticipantId)}</strong>
          <span class="arrow">→</span>
        </a>
      {/each}
    </div>
  {/each}
</section>

<style>
  .section-gap {
    margin-top: 32px;
  }

  .editor-row select {
    min-height: 44px;
  }

  .editor-row {
    display: flex;
    align-items: end;
    gap: 16px;
    flex-wrap: wrap;
  }

  .scope-conflict-warning {
    margin-top: 16px;
    background: #fdeeea;
    border-color: #e8755b;
  }

  .scope-conflict-warning ul {
    margin: 8px 0;
    padding-left: 20px;
  }

  .group-heading {
    margin: 24px 0 4px;
  }

  .member-categories {
    color: var(--muted);
    font-weight: 400;
  }

  .participant-filter {
    flex: 1 1 220px;
  }

  .unlisted-filter {
    margin-bottom: 2px;
  }

  .match-participant-row {
    display: grid;
    grid-template-columns: 24px minmax(0, 1fr) minmax(5ch, 76px) 44px;
    gap: 14px;
  }

  .match-participant-row.unlisted-row {
    margin: 0 -12px;
    padding-left: 12px;
    padding-right: 12px;
    background: #fdeeea;
    border-color: #e8755b;
  }

  /* Reuses the plain, unstyled .management-icon slot every list row already reserves - swapping
     its glyph/color for the signed state, rather than adding a whole new column, is what keeps
     this free on narrow screens where every extra column steals from the name. */
  .management-icon.signed-icon {
    color: var(--muted);
    font-weight: 700;
  }

  .management-icon.signed-icon.is-signed {
    color: var(--green);
  }

  .unlisted-tag {
    margin-left: 8px;
    color: #b84232;
    font: 700 10px 'Space Grotesk';
    letter-spacing: 1px;
    text-transform: uppercase;
    vertical-align: middle;
  }

  .participant-name {
    min-width: 0;
  }

  .participant-score {
    justify-self: stretch;
    text-align: right;
    font: 700 20px 'Space Grotesk', sans-serif;
    font-variant-numeric: tabular-nums;
  }

  .match-participant-row .arrow {
    display: grid;
    place-items: center;
    width: 44px;
    height: 44px;
    margin-left: 0;
    border: 1px solid var(--line);
    background: var(--paper);
  }

  @media (max-width: 720px) {
    .editor-row {
      flex-direction: column;
      align-items: stretch;
    }

    .match-participant-row {
      grid-template-columns: 18px minmax(0, 1fr) minmax(4ch, 64px);
      gap: 10px;
    }

    .match-participant-row .arrow {
      display: none;
    }

    .participant-score {
      font-size: 21px;
    }
  }
</style>

