<script lang="ts">
  import type { ApiClient, MatchInput } from '../api'
  import { formatLocalDate } from '../date'
  import DropdownMenu from '../DropdownMenu.svelte'
  import { labelForError } from '../errors'
  import { parseMatchKeyboardConfig } from '../matchConfig'
  import { matchAddParticipantsPath, matchDevicesPath, matchEditPath, matchParticipantPath, matchPrintPath, matchQrPath, matchResultsPath, navigateOnClick } from '../router'
  import type { Category, Language, Match, MatchParticipant, ScopeConflict } from '../types'

  export let api: ApiClient
  export let match: Match
  export let categories: Category[]
  export let language: Language
  export let labels: Record<string, string>
  export let onBack: () => void
  export let onToggleOpen: () => void
  export let onDeleted: () => void
  export let onEditMetadata: () => void
  export let onManageDevices: () => void
  export let onPrintScorecards: () => void
  export let onAddParticipants: () => void
  export let onOpenParticipant: (participantId: string) => void
  export let onCopied: (match: Match) => void

  type ResultRow = { participantId: string; total: number }
  type SortBy = 'name' | 'score'
  type GroupBy = 'none' | 'category' | 'device' | 'signed'

  const storedSortBy = localStorage.getItem('centaur-match-sort-by')
  const storedGroupBy = localStorage.getItem('centaur-match-group-by')
  let sortBy: SortBy = storedSortBy === 'score' ? 'score' : 'name'
  let groupBy: GroupBy = storedGroupBy === 'category' || storedGroupBy === 'device' || storedGroupBy === 'signed' ? storedGroupBy : 'none'
  let unlistedOnly = false
  let results: ResultRow[] = []
  let deleteError = ''
  let exportError = ''
  let scopeConflicts: ScopeConflict[] = []
  let claimingScope = false
  let claimScopeError = ''
  let showCopyForm = false
  let copyIncludeParticipants = false
  let copying = false
  let copyError = ''

  $: participants = match.participants ?? []
  $: devices = match.devices ?? []
  $: liveScopes = match.liveScopes ?? []
  $: sortedLiveScopes = [...liveScopes].sort((a, b) => a.scope.localeCompare(b.scope))

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
  $: visibleParticipants = unlistedOnly ? participants.filter((participant) => !participant.participantListMemberId) : participants

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

  async function exportCsv() {
    exportError = ''
    try {
      const { blob, filename } = await api.downloadMatchExport(match.id, language)
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

  async function copyMatch() {
    copyError = ''
    copying = true
    try {
      const copyInput: MatchInput = {
        name: labels.copyOfMatchName.replace('{name}', match.name),
        date: match.date,
        shortCode: match.shortCode,
        isOpen: false,
        participantListId: match.participantListId,
        deviceSelectionMode: match.deviceSelectionMode,
        signatureMode: match.signatureMode,
        ends: match.ends,
        arrowsPerEnd: match.arrowsPerEnd,
        groupEnds: match.groupEnds,
        allowFreeParticipants: match.allowFreeParticipants,
        keyboardJson: match.keyboardJson,
        scoringRulesJson: match.scoringRulesJson,
        personalBestClassifier: match.personalBestClassifier
      }
      const copy = await api.createMatch(copyInput)

      const deviceIdMap = new Map<string, string>()
      for (const device of [...devices].sort((a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0))) {
        const newDevice = await api.addDevice(copy.id, { name: device.name })
        deviceIdMap.set(device.id, newDevice.id)
      }

      for (const scope of liveScopes) {
        await api.addLiveScope(copy.id, { scope: scope.scope, groupByCategoryIds: JSON.parse(scope.groupByCategoryIdsJson || '[]'), includeAverage: scope.includeAverage, includeGroupScores: scope.includeGroupScores, includeEqualizers: scope.includeEqualizers, includePersonalBest: scope.includePersonalBest, displayCategoryIds: JSON.parse(scope.displayCategoryIdsJson || '[]') })
      }

      if (copyIncludeParticipants) {
        for (const participant of participants) {
          const newParticipant = await api.addMatchParticipant(copy.id, { participantListMemberId: participant.participantListMemberId, lastName: participant.lastName, fullName: participant.fullName, federationNumber: participant.federationNumber, categories: participant.categories })
          const newDeviceId = participant.deviceId ? deviceIdMap.get(participant.deviceId) : null
          if (newDeviceId) await api.assignParticipantDevice(copy.id, newParticipant.id, newDeviceId)
        }
      }

      showCopyForm = false
      onCopied(copy)
    } catch (error) {
      copyError = labelForError(error, labels, 'matchCopyError')
    } finally {
      copying = false
    }
  }

</script>

<button class="back-link" on:click={onBack}>← {labels.matches}</button>
<div class="page-intro">
  <div><p class="eyebrow">{labels.eyebrowMatch}</p><h1>{match.name}</h1><p class="muted">{formatLocalDate(match.date, language)}</p></div>
  <div class="match-header-actions">
    <button class="highlight-button" class:is-live={match.isOpen} on:click={onToggleOpen}>{match.isOpen ? labels.deactivate : labels.activate}</button>
    <a class="qr-button" aria-label={labels.viewQrCodes} title={labels.viewQrCodes} href={matchQrPath(match.id)} target="_blank" rel="noopener">▦</a>
    <DropdownMenu ariaLabel={labels.matchActions} buttonClass="actions-trigger" align="right">
      <svelte:fragment slot="trigger">⋯</svelte:fragment>
      <a class="menu-item" href={matchEditPath(match.id)} on:click={(event) => navigateOnClick(event, onEditMetadata)}>{labels.editMetadata}</a>
      <a class="menu-item" href={matchDevicesPath(match.id)} on:click={(event) => navigateOnClick(event, onManageDevices)}>{labels.manageDevices}</a>
      <a class="menu-item" href={matchQrPath(match.id)} target="_blank" rel="noopener">{labels.viewQrCodes}</a>
      <a class="menu-item" href={matchPrintPath(match.id)} on:click={(event) => navigateOnClick(event, onPrintScorecards)}>{labels.printScorecards}</a>
      <button class="menu-item" on:click={exportCsv}>{labels.exportCsv}</button>
      <button class="menu-item" on:click={() => (showCopyForm = !showCopyForm)}>{labels.copyMatch}</button>
      <hr class="menu-separator" />
      <button class="menu-item menu-item-danger" on:click={remove}>{labels.deleteMatch}</button>
    </DropdownMenu>
  </div>
</div>
{#if exportError}<p class="error">{exportError}</p>{/if}
{#if deleteError}<p class="error">{deleteError}</p>{/if}
{#if showCopyForm}
  <div class="panel entry-card">
    <label class="checkbox-label"><input type="checkbox" bind:checked={copyIncludeParticipants} /> {labels.copyIncludeParticipantsLabel}</label>
    <div class="editor-row">
      <button class="primary" type="button" disabled={copying} on:click={copyMatch}>{labels.copyMatch}</button>
      <button type="button" class="text-button" on:click={() => (showCopyForm = false)}>{labels.cancel}</button>
    </div>
    {#if copyError}<p class="error">{copyError}</p>{/if}
  </div>
{/if}
{#if sortedLiveScopes.length === 1}
  <div class="results-row">
    <a class="actions-trigger results-trigger" href={matchResultsPath(match.id, sortedLiveScopes[0].scope)} target="_blank" rel="noopener">{labels.resultsLabel}</a>
  </div>
{:else if sortedLiveScopes.length > 1}
  <div class="results-row">
    <DropdownMenu ariaLabel={labels.resultsLabel} buttonClass="actions-trigger results-trigger" align="right">
      <svelte:fragment slot="trigger">{labels.resultsLabel}</svelte:fragment>
      {#each sortedLiveScopes as scope}
        <a class="menu-item" href={matchResultsPath(match.id, scope.scope)} target="_blank" rel="noopener">{scope.scope}</a>
      {/each}
    </DropdownMenu>
  </div>
{/if}
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

  {#if participants.length === 0}<p class="empty-state">{labels.emptyState}</p>{:else if sortedParticipants.length === 0}<p class="empty-state">{labels.noUnlistedParticipants}</p>{/if}
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

    .match-header-actions {
      flex: 0 0 auto;
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

