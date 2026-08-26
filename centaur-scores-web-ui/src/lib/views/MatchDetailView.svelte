<script lang="ts">
  import type { ApiClient } from '../api'
  import { formatLocalDate } from '../date'
  import DropdownMenu from '../DropdownMenu.svelte'
  import { labelForError } from '../errors'
  import { parseMatchKeyboardConfig } from '../matchConfig'
  import { deriveLastName } from '../participantName'
  import type { Category, Language, Match, ParticipantList } from '../types'

  export let api: ApiClient
  export let match: Match
  export let categories: Category[]
  export let participantLists: ParticipantList[]
  export let language: Language
  export let labels: Record<string, string>
  export let onBack: () => void
  export let onToggleOpen: () => void
  export let onChanged: () => void
  export let onDeleted: () => void
  export let onEditMetadata: () => void
  export let onManageDevices: () => void
  export let onOpenParticipant: (participantId: string) => void

  type ResultRow = { participantId: string; total: number }
  type SortBy = 'name' | 'score'
  type GroupBy = 'none' | 'category' | 'device'

  const storedSortBy = localStorage.getItem('centaur-match-sort-by')
  const storedGroupBy = localStorage.getItem('centaur-match-group-by')
  let sortBy: SortBy = storedSortBy === 'score' ? 'score' : 'name'
  let groupBy: GroupBy = storedGroupBy === 'category' || storedGroupBy === 'device' ? storedGroupBy : 'none'
  let results: ResultRow[] = []
  let deleteError = ''
  let exportError = ''
  let showAddForm = false
  let showManualCard = false
  let sourceMemberId = ''
  let manualFullName = ''
  let manualFederationNumber = ''
  let manualCategoryValues: Record<string, string> = {}
  let addError = ''

  $: participants = match.participants ?? []
  $: devices = match.devices ?? []
  $: liveScopes = match.liveScopes ?? []
  $: sortedLiveScopes = [...liveScopes].sort((a, b) => a.scope.localeCompare(b.scope))

  function openResults(scope: string) {
    window.open(`/matches/${match.id}/results/${encodeURIComponent(scope)}`, '_blank')
  }
  $: keyboardConfig = parseMatchKeyboardConfig(match.keyboardJson)
  $: matchCategories = keyboardConfig.categoryOrder.map((id) => categories.find((category) => category.id === id)).filter((category): category is Category => !!category)
  $: assignedMemberIds = new Set(participants.map((participant) => participant.participantListMemberId).filter((id): id is string => !!id))
  $: sourceList = participantLists.find((list) => list.id === match.participantListId) ?? null
  $: availableMembers = sourceList
    ? sourceList.members
        .filter((member) => member.isActive && !assignedMemberIds.has(member.id))
        .sort((a, b) => (a.fullName || a.lastName).localeCompare(b.fullName || b.lastName))
    : []
  $: manualAllCategoriesFilled = matchCategories.every((category) => manualCategoryValues[category.id])
  $: canAddManually = manualFullName.trim() !== '' && manualAllCategoriesFilled

  function categoryLabel(participantCategories: Record<string, number>): string {
    return matchCategories
      .map((category) => category.values.find((value) => value.valueId === participantCategories[category.id])?.name)
      .filter((value): value is string => !!value)
      .join(' / ')
  }

  function memberDisplayLabel(member: { fullName: string; lastName: string; categories: Record<string, number> }): string {
    const name = member.fullName || member.lastName
    const values = categories
      .map((category) => category.values.find((value) => value.valueId === member.categories[category.id])?.name)
      .filter((value): value is string => !!value)
      .join(' / ')
    return values ? `${name} (${values})` : name
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
    groupBy = value === 'category' || value === 'device' ? value : 'none'
    localStorage.setItem('centaur-match-group-by', groupBy)
  }

  $: sortedParticipants = [...participants].sort((a, b) => {
    if (sortBy === 'score') return participantTotal(b.id, totalsByParticipantId) - participantTotal(a.id, totalsByParticipantId)
    return (a.fullName || a.lastName).localeCompare(b.fullName || b.lastName)
  })

  $: groupedParticipants = (() => {
    if (groupBy === 'none') return [{ key: '', items: sortedParticipants }]
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

  async function exportCsv() {
    exportError = ''
    try {
      const { blob, filename } = await api.downloadMatchExport(match.id)
      const url = URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.download = filename
      link.click()
      URL.revokeObjectURL(url)
    } catch (error) {
      exportError = labelForError(error, labels, 'exportError')
    }
  }

  async function remove() {
    deleteError = ''
    const message = labels.deleteMatchConfirm.replace('{name}', match.name)
    if (!confirm(message)) return
    try {
      await api.deleteMatch(match.id)
      onDeleted()
    } catch (error) {
      deleteError = labelForError(error, labels, 'matchDeleteError')
    }
  }

  function resetAddForm() {
    sourceMemberId = ''
    manualFullName = ''
    manualFederationNumber = ''
    manualCategoryValues = {}
    showManualCard = false
  }

  async function submitAddFromList() {
    const member = sourceList?.members.find((item) => item.id === sourceMemberId)
    if (!member) return
    addError = ''
    try {
      await api.addMatchParticipant(match.id, { participantListMemberId: member.id, lastName: member.lastName, fullName: member.fullName, federationNumber: member.federationNumber, categories: member.categories })
      resetAddForm()
      showAddForm = false
      onChanged()
    } catch (error) {
      addError = labelForError(error, labels, 'addParticipantError')
    }
  }

  async function submitAddManually() {
    if (!canAddManually) return
    addError = ''
    const categoryValues: Record<string, number> = {}
    for (const category of matchCategories) {
      const value = manualCategoryValues[category.id]
      if (value) categoryValues[category.id] = Number(value)
    }
    try {
      await api.addMatchParticipant(match.id, { participantListMemberId: null, lastName: deriveLastName(manualFullName.trim()), fullName: manualFullName.trim(), federationNumber: manualFederationNumber || null, categories: categoryValues })
      resetAddForm()
      showAddForm = false
      onChanged()
    } catch (error) {
      addError = labelForError(error, labels, 'addParticipantError')
    }
  }
</script>

<button class="back-link" on:click={onBack}>← {labels.matches}</button>
<div class="page-intro">
  <div><p class="eyebrow">{labels.eyebrowMatch}</p><h1>{match.name}</h1><p class="muted">{formatLocalDate(match.date, language)}</p></div>
  <div class="match-header-actions">
    <button class="highlight-button" class:is-live={match.isOpen} on:click={onToggleOpen}>{match.isOpen ? labels.deactivate : labels.activate}</button>
    <button class="qr-button" aria-label={labels.viewQrCodes} title={labels.viewQrCodes} on:click={() => window.open(`/matches/${match.id}/qr`, '_blank')}>▦</button>
    <DropdownMenu ariaLabel={labels.matchActions} buttonClass="actions-trigger" align="right">
      <svelte:fragment slot="trigger">⋯</svelte:fragment>
      <button class="menu-item" on:click={onEditMetadata}>{labels.editMetadata}</button>
      <button class="menu-item" on:click={onManageDevices}>{labels.manageDevices}</button>
      <button class="menu-item" on:click={() => window.open(`/matches/${match.id}/qr`, '_blank')}>{labels.viewQrCodes}</button>
      <button class="menu-item" on:click={exportCsv}>{labels.exportCsv}</button>
      <hr class="menu-separator" />
      <button class="menu-item menu-item-danger" on:click={remove}>{labels.deleteMatch}</button>
    </DropdownMenu>
  </div>
</div>
{#if exportError}<p class="error">{exportError}</p>{/if}
{#if deleteError}<p class="error">{deleteError}</p>{/if}
{#if sortedLiveScopes.length === 1}
  <div class="results-row">
    <button class="actions-trigger results-trigger" on:click={() => openResults(sortedLiveScopes[0].scope)}>{labels.resultsLabel}</button>
  </div>
{:else if sortedLiveScopes.length > 1}
  <div class="results-row">
    <DropdownMenu ariaLabel={labels.resultsLabel} buttonClass="actions-trigger results-trigger" align="right">
      <svelte:fragment slot="trigger">{labels.resultsLabel}</svelte:fragment>
      {#each sortedLiveScopes as scope}
        <button class="menu-item" on:click={() => openResults(scope.scope)}>{scope.scope}</button>
      {/each}
    </DropdownMenu>
  </div>
{/if}

<section class="panel section-gap">
  <div class="editor-row">
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
      </select>
    </label>
    <button class="primary" on:click={() => (showAddForm = !showAddForm)}>+ {labels.addParticipant}</button>
  </div>

  {#if showAddForm}
    <div class="entry-card">
      {#if sourceList}
        <form on:submit|preventDefault={submitAddFromList}>
          <label>{labels.selectParticipantLabel}
            <select bind:value={sourceMemberId}>
              <option value="">{labels.selectValue}</option>
              {#each availableMembers as member}<option value={member.id}>{memberDisplayLabel(member)}</option>{/each}
            </select>
          </label>
          <button class="primary" type="submit" disabled={!sourceMemberId}>{labels.save}</button>
        </form>
        {#if match.allowFreeParticipants}
          <button type="button" class="text-button add-unlisted-button" on:click={() => (showManualCard = !showManualCard)}>+ {labels.addUnlistedParticipant}</button>
        {/if}
      {/if}
      {#if !sourceList || showManualCard}
        {#if match.allowFreeParticipants}
          <form class="manual-card" on:submit|preventDefault={submitAddManually}>
            <label>{labels.fullNameLabel}<input bind:value={manualFullName} autocomplete="off" /></label>
            <label>{labels.federationNumberLabel}<input bind:value={manualFederationNumber} /></label>
            {#each matchCategories as category}
              <label>{category.name}
                <select bind:value={manualCategoryValues[category.id]}>
                  <option value="">{labels.selectValue}</option>
                  {#each [...category.values].sort((a, b) => a.valueId - b.valueId) as value}<option value={String(value.valueId)}>{value.name}</option>{/each}
                </select>
              </label>
            {/each}
            <button class="primary large-submit" type="submit" disabled={!canAddManually}>+ {labels.addThisParticipant}</button>
          </form>
        {:else}
          <p class="muted">{labels.participantListLockedHint}</p>
        {/if}
      {/if}
      {#if addError}<p class="error">{addError}</p>{/if}
    </div>
  {/if}

  {#if participants.length === 0}<p class="empty-state">{labels.emptyState}</p>{/if}
  {#each groupedParticipants as group}
    {#if group.key}<h2 class="group-heading">{group.key}</h2>{/if}
    <div class="list-panel">
      {#each group.items as participant}
        <button class="list-row match-participant-row" on:click={() => onOpenParticipant(participant.id)}>
          <span class="management-icon">◇</span>
          <span class="participant-name"><strong>{participant.fullName || participant.lastName}</strong>{#if categoryLabel(participant.categories)}<span class="member-categories"> ({categoryLabel(participant.categories)})</span>{/if}</span>
          <strong class="participant-score">{participantTotal(participant.id, totalsByParticipantId)}</strong>
          <span class="arrow">→</span>
        </button>
      {/each}
    </div>
  {/each}
</section>

<style>
  .section-gap {
    margin-top: 32px;
  }

  .editor-row button,
  .editor-row select {
    min-height: 44px;
  }

  .editor-row {
    display: flex;
    align-items: end;
    gap: 16px;
    flex-wrap: wrap;
  }

  .qr-button {
    display: grid;
    place-items: center;
    flex: 0 0 auto;
    width: 44px;
    height: 44px;
    border: 1px solid var(--line);
    background: var(--paper);
    color: var(--ink);
    font-size: 20px;
    line-height: 1;
    padding: 0;
  }

  .qr-button:hover,
  .qr-button:focus-visible {
    border-color: var(--green);
    color: var(--green);
  }

  .results-row {
    display: flex;
    justify-content: flex-end;
    margin-top: 16px;
  }

  .add-unlisted-button {
    margin-top: 12px;
  }

  .manual-card {
    margin-top: 16px;
  }

  .entry-card form {
    margin-top: 0;
  }

  .manual-card:not(:first-child) {
    border-top: 1px solid var(--line);
    padding-top: 16px;
  }

  .group-heading {
    margin: 24px 0 4px;
  }

  .member-categories {
    color: var(--muted);
    font-weight: 400;
  }

  .match-participant-row {
    display: grid;
    grid-template-columns: 24px minmax(0, 1fr) minmax(5ch, 76px) 44px;
    gap: 14px;
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

    .match-header-actions {
      flex: 0 0 auto;
    }

    .match-participant-row {
      grid-template-columns: 18px minmax(0, 1fr) minmax(4ch, 64px) 44px;
      gap: 10px;
    }

    .participant-score {
      font-size: 21px;
    }
  }
</style>

