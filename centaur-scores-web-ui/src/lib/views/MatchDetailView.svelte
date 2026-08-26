<script lang="ts">
  import type { ApiClient } from '../api'
  import { formatLocalDate } from '../date'
  import { labelForError } from '../errors'
  import { parseMatchKeyboardConfig } from '../matchConfig'
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
  let addMode: 'list' | 'manual' = 'list'
  let sourceMemberId = ''
  let manualLastName = ''
  let manualFullName = ''
  let manualFederationNumber = ''
  let manualCategoryValues: Record<string, string> = {}
  let addError = ''

  $: participants = match.participants ?? []
  $: devices = match.devices ?? []
  $: liveScopes = match.liveScopes ?? []
  $: keyboardConfig = parseMatchKeyboardConfig(match.keyboardJson)
  $: matchCategories = keyboardConfig.categoryOrder.map((id) => categories.find((category) => category.id === id)).filter((category): category is Category => !!category)
  $: assignedMemberIds = new Set(participants.map((participant) => participant.participantListMemberId).filter((id): id is string => !!id))
  $: sourceList = participantLists.find((list) => list.id === match.participantListId) ?? null
  $: availableMembers = sourceList
    ? sourceList.members
        .filter((member) => member.isActive && !assignedMemberIds.has(member.id))
        .sort((a, b) => (a.fullName || a.lastName).localeCompare(b.fullName || b.lastName))
    : []
  $: if (!sourceList) addMode = 'manual'

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
    manualLastName = ''
    manualFullName = ''
    manualFederationNumber = ''
    manualCategoryValues = {}
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
    if (!manualLastName.trim() || !manualFullName.trim()) return
    addError = ''
    const categoryValues: Record<string, number> = {}
    for (const category of matchCategories) {
      const value = manualCategoryValues[category.id]
      if (value) categoryValues[category.id] = Number(value)
    }
    try {
      await api.addMatchParticipant(match.id, { participantListMemberId: null, lastName: manualLastName.trim(), fullName: manualFullName.trim(), federationNumber: manualFederationNumber || null, categories: categoryValues })
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
  <button class="primary" on:click={onToggleOpen}>{match.isOpen ? labels.deactivate : labels.activate}</button>
</div>

<div class="editor-actions">
  <button class="primary" on:click={onEditMetadata}>{labels.editMetadata}</button>
  <button class="primary" on:click={onManageDevices}>{labels.manageDevices}</button>
  <button class="primary" on:click={() => window.open(`/matches/${match.id}/qr`, '_blank')}>{labels.viewQrCodes}</button>
  <button class="primary" on:click={exportCsv}>{labels.exportCsv}</button>
  {#if liveScopes.length > 0}
    <select on:change={(event) => { if (event.currentTarget.value) { window.open(`/matches/${match.id}/results/${encodeURIComponent(event.currentTarget.value)}`, '_blank'); event.currentTarget.value = '' } }}>
      <option value="">{labels.resultsLabel}</option>
      {#each liveScopes as scope}<option value={scope.scope}>{scope.scope}</option>{/each}
    </select>
  {/if}
</div>
{#if exportError}<p class="error">{exportError}</p>{/if}

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
    <form class="inline-form" on:submit|preventDefault={addMode === 'list' ? submitAddFromList : submitAddManually}>
      {#if sourceList && match.allowFreeParticipants}
        <label>{labels.addModeLabel}
          <select bind:value={addMode}>
            <option value="list">{labels.selectFromList}</option>
            <option value="manual">{labels.addManually}</option>
          </select>
        </label>
      {/if}
      {#if addMode === 'list' && sourceList}
        <label>{labels.selectParticipantLabel}
          <select bind:value={sourceMemberId}>
            <option value="">{labels.selectValue}</option>
            {#each availableMembers as member}<option value={member.id}>{memberDisplayLabel(member)}</option>{/each}
          </select>
        </label>
        <button class="primary" type="submit" disabled={!sourceMemberId}>{labels.save}</button>
      {:else if match.allowFreeParticipants}
        <label>{labels.lastNameLabel}<input bind:value={manualLastName} /></label>
        <label>{labels.fullNameLabel}<input bind:value={manualFullName} /></label>
        <label>{labels.federationNumberLabel}<input bind:value={manualFederationNumber} /></label>
        {#each matchCategories as category}
          <label>{category.name}
            <select bind:value={manualCategoryValues[category.id]}>
              <option value="">{labels.selectValue}</option>
              {#each [...category.values].sort((a, b) => a.valueId - b.valueId) as value}<option value={String(value.valueId)}>{value.name}</option>{/each}
            </select>
          </label>
        {/each}
        <button class="primary" type="submit" disabled={!manualLastName.trim() || !manualFullName.trim()}>{labels.save}</button>
      {:else}
        <p class="muted">{labels.participantListLockedHint}</p>
      {/if}
    </form>
    {#if addError}<p class="error">{addError}</p>{/if}
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

<button class="danger-button" on:click={remove}>{labels.deleteMatch}</button>
{#if deleteError}<p class="error">{deleteError}</p>{/if}

<style>
  .section-gap {
    margin-top: 32px;
  }

  .editor-actions {
    display: flex;
    justify-content: flex-end;
    flex-wrap: wrap;
    gap: 12px;
    margin-top: 24px;
  }

  .editor-actions button,
  .editor-actions select,
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
    .editor-actions {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
    }

    .editor-actions button,
    .editor-actions select {
      width: 100%;
    }

    .editor-row {
      align-items: stretch;
    }

    .editor-row label {
      flex: 1 1 130px;
    }

    .editor-row > button {
      flex: 1 1 100%;
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

