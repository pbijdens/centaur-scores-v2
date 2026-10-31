<script lang="ts">
  import type { ApiClient } from '../api'
  import CategoryTag from '../CategoryTag.svelte'
  import LaneSheet from '../LaneSheet.svelte'
  import MatchPageHeader from '../MatchPageHeader.svelte'
  import type { MatchHeaderHandlers } from '../pageActions'
  import { labelForError } from '../errors'
  import { distinctValues, mixedLaneNumbers, parseLane, uniformDeviceValue } from '../laneSheet'
  import { parseMatchKeyboardConfig } from '../matchConfig'
  import RowActions from '../RowActions.svelte'
  import { matchParticipantPath, navigateOnClick } from '../router'
  import type { Category, Language, Match, MatchParticipant, ScoreDevice } from '../types'

  export let api: ApiClient
  export let match: Match
  export let categories: Category[]
  export let language: Language
  export let labels: Record<string, string>
  export let headerHandlers: MatchHeaderHandlers
  export let onBack: () => void
  export let onChanged: () => void | Promise<void>
  export let onOpenParticipant: (participantId: string) => void

  let showAddForm = false
  let openParticipantEditorDeviceId: string | null = null
  let newDeviceName = ''
  let pendingSelections: Record<string, string> = {}
  let createError = ''
  let deleteError = ''
  let orderError = ''
  let assignmentError = ''
  let laneError = ''
  let renameError = ''
  let dragging: { deviceId: string; participantId: string } | null = null
  let dropTarget: { participantId: string; after: boolean } | null = null

  // The two categories picked for the lane check, remembered per match on this browser.
  const laneCheckStorageKey = `centaur-lane-check-${match.id}`
  let { laneCategoryId, deviceCategoryId } = loadLaneCheck()

  $: devices = [...(match.devices ?? [])].sort((a, b) => (a.sortOrder ?? Number.MAX_SAFE_INTEGER) - (b.sortOrder ?? Number.MAX_SAFE_INTEGER) || a.name.localeCompare(b.name))
  $: participants = match.participants ?? []
  $: unassignedParticipants = participants
    .filter((participant) => !participant.deviceId)
    .sort((a, b) => participantName(a).localeCompare(participantName(b)))
  $: matchCategories = matchCategoriesFor(match, categories)
  $: laneCategory = matchCategories.find((category) => category.id === laneCategoryId) ?? null
  $: deviceCategory = matchCategories.find((category) => category.id === deviceCategoryId) ?? null
  $: tagCategories = [laneCategory, deviceCategory].filter((category): category is Category => !!category)
  $: saveLaneCheck(laneCategoryId, deviceCategoryId)
  $: mixedLanes = laneCategory ? mixedLaneNumbers(participants, laneCategory.id) : new Set<number>()
  $: laneOverview = laneCategory ? laneOverviewFor(participants, laneCategory.id) : []
  $: sheetParticipants = [...devices.flatMap((device) => sortedDeviceParticipants(participants, device.id)), ...unassignedParticipants]

  function matchCategoriesFor(currentMatch: Match, allCategories: Category[]): Category[] {
    const ordered = parseMatchKeyboardConfig(currentMatch.keyboardJson).categoryOrder
      .map((id) => allCategories.find((category) => category.id === id))
      .filter((category): category is Category => !!category)
    return ordered.length > 0 ? ordered : allCategories
  }

  function loadLaneCheck(): { laneCategoryId: string; deviceCategoryId: string } {
    try {
      const stored = JSON.parse(localStorage.getItem(laneCheckStorageKey) ?? '{}')
      return { laneCategoryId: String(stored.laneCategoryId ?? ''), deviceCategoryId: String(stored.deviceCategoryId ?? '') }
    } catch {
      return { laneCategoryId: '', deviceCategoryId: '' }
    }
  }

  function saveLaneCheck(lane: string, device: string) {
    localStorage.setItem(laneCheckStorageKey, JSON.stringify({ laneCategoryId: lane, deviceCategoryId: device }))
  }

  // Every lane number from the lowest to the highest in use, with the values of the lane category on it.
  function laneOverviewFor(all: MatchParticipant[], categoryId: string): { number: number; values: (number | null)[] }[] {
    const byLane = new Map<number, MatchParticipant[]>()
    for (const participant of all) {
      const { number } = parseLane(participant.deviceLane)
      if (number !== null && participant.deviceId) byLane.set(number, [...(byLane.get(number) ?? []), participant])
    }
    if (byLane.size === 0) return []
    const numbers = [...byLane.keys()]
    const overview = []
    for (let number = Math.min(...numbers); number <= Math.max(...numbers); number++) {
      overview.push({ number, values: byLane.has(number) ? distinctValues(byLane.get(number)!, categoryId) : [] })
    }
    return overview
  }

  function sortedDeviceParticipants(all: MatchParticipant[], deviceId: string): MatchParticipant[] {
    return all
      .filter((participant) => participant.deviceId === deviceId)
      .sort((a, b) => (a.deviceOrder ?? Number.MAX_SAFE_INTEGER) - (b.deviceOrder ?? Number.MAX_SAFE_INTEGER) || participantName(a).localeCompare(participantName(b)))
  }

  function isMixedLane(lane: string | null | undefined): boolean {
    const { number } = parseLane(lane)
    return number !== null && mixedLanes.has(number)
  }

  function mixedLaneTitle(lane: string | null | undefined): string {
    if (!laneCategory || !isMixedLane(lane)) return labels.laneLabel
    return labels.mixedLaneWarning.replace('{lane}', String(parseLane(lane).number)).replace('{category}', laneCategory.name)
  }

  function assignedCount(deviceId: string): number {
    return participants.filter((participant) => participant.deviceId === deviceId).length
  }

  function participantName(participant: MatchParticipant): string {
    return participant.fullName || participant.lastName
  }

  function participantCategoryLabel(participant: MatchParticipant): string {
    return categories
      .map((category) => category.values.find((value) => value.valueId === participant.categories[category.id])?.name)
      .filter((value): value is string => !!value)
      .join(' / ')
  }

  function assignedParticipants(deviceId: string): MatchParticipant[] {
    return sortedDeviceParticipants(participants, deviceId)
  }

  function participantLabel(participant: MatchParticipant): string {
    const categoriesLabel = participantCategoryLabel(participant)
    return categoriesLabel ? `${participantName(participant)} (${categoriesLabel})` : participantName(participant)
  }

  async function reorderDevices(deviceIds: string[]) {
    orderError = ''
    try {
      await api.reorderDevices(match.id, deviceIds)
      await onChanged()
    } catch (error) {
      orderError = labelForError(error, labels, 'matchSaveError')
    }
  }

  async function moveDevice(deviceId: string, direction: -1 | 1) {
    const index = devices.findIndex((device) => device.id === deviceId)
    const target = index + direction
    if (index < 0 || target < 0 || target >= devices.length) return
    const reordered = [...devices]
    const [moved] = reordered.splice(index, 1)
    reordered.splice(target, 0, moved)
    await reorderDevices(reordered.map((device) => device.id))
  }

  async function reorderDeviceParticipants(deviceId: string, orderedParticipantIds: string[]) {
    orderError = ''
    try {
      await api.reorderDeviceParticipants(match.id, deviceId, orderedParticipantIds)
      await onChanged()
    } catch (error) {
      orderError = labelForError(error, labels, 'matchSaveError')
    }
  }

  async function moveParticipant(deviceId: string, participantId: string, direction: -1 | 1) {
    const assigned = assignedParticipants(deviceId)
    const index = assigned.findIndex((participant) => participant.id === participantId)
    const target = index + direction
    if (index < 0 || target < 0 || target >= assigned.length) return
    const reordered = [...assigned]
    const [moved] = reordered.splice(index, 1)
    reordered.splice(target, 0, moved)
    await reorderDeviceParticipants(deviceId, reordered.map((participant) => participant.id))
  }

  function startDrag(event: DragEvent & { currentTarget: HTMLElement }, deviceId: string, participantId: string) {
    dragging = { deviceId, participantId }
    if (!event.dataTransfer) return
    event.dataTransfer.effectAllowed = 'move'
    event.dataTransfer.setData('text/plain', participantId) // Firefox only starts a drag with data set
    const row = event.currentTarget.closest('.participant-row')
    if (row) event.dataTransfer.setDragImage(row, 24, 24)
  }

  // Archers can only be reordered within their own device; the row under the pointer shows whether the
  // dragged archer will land above or below it.
  function dragOver(event: DragEvent & { currentTarget: HTMLElement }, deviceId: string, participantId: string) {
    if (!dragging || dragging.deviceId !== deviceId) return
    event.preventDefault()
    if (event.dataTransfer) event.dataTransfer.dropEffect = 'move'
    const rect = event.currentTarget.getBoundingClientRect()
    const after = event.clientY > rect.top + rect.height / 2
    if (dropTarget?.participantId !== participantId || dropTarget.after !== after) dropTarget = { participantId, after }
  }

  function endDrag() {
    dragging = null
    dropTarget = null
  }

  async function drop(event: DragEvent, deviceId: string) {
    event.preventDefault()
    const source = dragging
    const target = dropTarget
    endDrag()
    if (!source || !target || source.deviceId !== deviceId || source.participantId === target.participantId) return
    const assigned = assignedParticipants(deviceId)
    const moved = assigned.find((participant) => participant.id === source.participantId)
    if (!moved) return
    const reordered = assigned.filter((participant) => participant.id !== source.participantId)
    const targetIndex = reordered.findIndex((participant) => participant.id === target.participantId)
    if (targetIndex < 0) return
    reordered.splice(target.after ? targetIndex + 1 : targetIndex, 0, moved)
    const orderedIds = reordered.map((participant) => participant.id)
    if (orderedIds.join() === assigned.map((participant) => participant.id).join()) return
    // Show the new order right away; onChanged() reconciles with the server.
    participants = participants.map((participant) =>
      participant.deviceId === deviceId ? { ...participant, deviceOrder: orderedIds.indexOf(participant.id) } : participant
    )
    await reorderDeviceParticipants(deviceId, orderedIds)
  }

  async function addParticipantToDevice(deviceId: string) {
    const participantId = pendingSelections[deviceId]
    if (!participantId) return
    assignmentError = ''
    try {
      await api.assignParticipantDevice(match.id, participantId, deviceId)
      participants = participants.map((participant) =>
        participant.id === participantId ? { ...participant, deviceId } : participant
      )
      pendingSelections = { ...pendingSelections, [deviceId]: '' }
      await onChanged()
    } catch (error) {
      assignmentError = labelForError(error, labels, 'addParticipantError')
    }
  }

  async function removeParticipantFromDevice(participantId: string) {
    assignmentError = ''
    try {
      await api.assignParticipantDevice(match.id, participantId, null)
      await onChanged()
    } catch (error) {
      assignmentError = labelForError(error, labels, 'addParticipantError')
    }
  }

  async function updateLane(participantId: string, rawValue: string) {
    const lane = rawValue.trim() || null
    const previous = participants.find((participant) => participant.id === participantId)?.deviceLane ?? null
    if (previous === lane) return
    laneError = ''
    participants = participants.map((participant) =>
      participant.id === participantId ? { ...participant, deviceLane: lane } : participant
    )
    try {
      await api.updateParticipantLane(match.id, participantId, lane)
      await onChanged()
    } catch (error) {
      laneError = labelForError(error, labels, 'laneSaveError')
    }
  }

  async function renameDevice(deviceId: string, event: FocusEvent & { currentTarget: HTMLInputElement }) {
    const input = event.currentTarget
    const name = input.value.trim()
    const device = devices.find((item) => item.id === deviceId)
    if (!device) return
    if (!name) {
      input.value = device.name
      return
    }
    if (device.name === name) return
    renameError = ''
    try {
      await api.renameDevice(match.id, deviceId, name)
      await onChanged()
    } catch (error) {
      renameError = labelForError(error, labels, 'deviceCreateError')
      input.value = device.name
    }
  }

  async function submitAdd() {
    if (!newDeviceName.trim()) return
    createError = ''
    try {
      await api.addDevice(match.id, { name: newDeviceName.trim() })
      newDeviceName = ''
      showAddForm = false
      onChanged()
    } catch (error) {
      createError = labelForError(error, labels, 'deviceCreateError')
    }
  }

  async function removeDevice(deviceId: string, name: string) {
    deleteError = ''
    if (!confirm(labels.deleteDeviceConfirm.replace('{name}', name))) return
    try {
      await api.deleteDevice(match.id, deviceId)
      onChanged()
    } catch (error) {
      deleteError = labelForError(error, labels, 'deviceDeleteError')
    }
  }
</script>

<svelte:head>
  <!-- Present only while this page is shown: printing it yields just the lane sheet. -->
  <style>
    @page {
      size: A4 portrait;
      margin: 10mm;
    }

    @media print {
      .app-shell > header {
        display: none !important;
      }

      .app-shell {
        min-height: 0 !important;
        background: none !important;
      }

      main.content {
        width: auto !important;
        margin: 0 !important;
        padding: 0 !important;
      }

      main.content > :not(.lane-sheet) {
        display: none !important;
      }

      main.content > .lane-sheet {
        display: block !important;
      }
    }
  </style>
</svelte:head>

<MatchPageHeader {api} {match} {language} {labels} handlers={headerHandlers} current="devices" title={labels.manageDevices} subtitle={labels.devicesHint} backLabel={match.name} {onBack} pageActions={[
  { label: `+ ${labels.newDevice}`, header: 'primary', onClick: () => (showAddForm = !showAddForm) },
  { label: labels.printLaneSheet, header: 'text', onClick: () => window.print() }
]} />
{#if showAddForm}
  <form class="inline-form" on:submit|preventDefault={submitAdd}>
    <label>{labels.deviceNameLabel}<input bind:value={newDeviceName} /></label>
    <button class="primary" type="submit" disabled={!newDeviceName.trim()}>{labels.save}</button>
  </form>
  {#if createError}<p class="error">{createError}</p>{/if}
{/if}
<section class="panel lane-check">
  <h2>{labels.laneCheckTitle}</h2>
  <p class="muted">{labels.laneCheckHint}</p>
  <div class="inline-form">
    <label>{labels.laneCategoryLabel}
      <select bind:value={laneCategoryId}>
        <option value="">{labels.noCategorySelected}</option>
        {#each matchCategories as category}<option value={category.id}>{category.name}</option>{/each}
      </select>
    </label>
    <label>{labels.deviceCategoryLabel}
      <select bind:value={deviceCategoryId}>
        <option value="">{labels.noCategorySelected}</option>
        {#each matchCategories as category}<option value={category.id}>{category.name}</option>{/each}
      </select>
    </label>
  </div>
  {#if laneCategory && laneOverview.length > 0}
    <div class="lane-overview" role="list" aria-label={labels.laneOverviewLabel}>
      {#each laneOverview as lane}
        <span class="lane-chip" role="listitem" class:is-mixed={lane.values.length > 1} class:is-empty={lane.values.length === 0} title={lane.values.length > 1 ? labels.mixedLaneWarning.replace('{lane}', String(lane.number)).replace('{category}', laneCategory.name) : undefined}>
          <strong>{lane.number}</strong>
          {#each lane.values as value}<CategoryTag category={laneCategory} valueId={value ?? undefined} missingLabel={labels.missingCategoryValue} />{/each}
          {#if lane.values.length > 1}<span aria-hidden="true">⚠</span>{/if}
        </span>
      {/each}
    </div>
  {/if}
</section>

<section class="list-panel">
  {#if devices.length === 0}<p class="empty-state">{labels.emptyState}</p>{/if}
  {#each devices as device, deviceIndex}
    <div class="device-block">
      <div class="list-row">
        <span class="management-icon">◇</span>
        <span>
          <input
            class="device-name-input"
            type="text"
            value={device.name}
            aria-label={labels.deviceNameLabel}
            on:blur={(event) => renameDevice(device.id, event)}
          />
          <small>{assignedCount(device.id)} {labels.membersLabel.toLowerCase()}</small>
          {#if deviceCategory}
            {@const sharedValue = uniformDeviceValue(assignedParticipants(device.id), deviceCategory.id)}
            {#if sharedValue !== undefined}
              <span class="device-warning">⚠ {labels.uniformDeviceWarning}: <CategoryTag category={deviceCategory} valueId={sharedValue} offset={6} missingLabel={labels.missingCategoryValue} /></span>
            {/if}
          {/if}
        </span>
        <div class="device-actions">
          <button
            class="icon-button participant-add-button"
            aria-label={labels.addParticipant}
            aria-expanded={openParticipantEditorDeviceId === device.id}
            title={labels.addParticipant}
            on:click={() => (openParticipantEditorDeviceId = openParticipantEditorDeviceId === device.id ? null : device.id)}
          >+</button>
          <RowActions
            {labels}
            pushRight={false}
            canMoveUp={deviceIndex > 0}
            canMoveDown={deviceIndex < devices.length - 1}
            onMoveUp={() => moveDevice(device.id, -1)}
            onMoveDown={() => moveDevice(device.id, 1)}
            onDelete={() => removeDevice(device.id, device.name)}
          />
        </div>
      </div>

      {#if openParticipantEditorDeviceId === device.id}
        <div class="inline-form participant-add-row">
          <label>{labels.selectParticipantLabel}
            <select value={pendingSelections[device.id] ?? ''} on:change={(event) => (pendingSelections = { ...pendingSelections, [device.id]: event.currentTarget.value })}>
              <option value="">{labels.selectValue}</option>
              {#each unassignedParticipants as participant (participant.id)}<option value={participant.id}>{participantLabel(participant)}</option>{/each}
            </select>
          </label>
          <button class="primary" on:click={() => addParticipantToDevice(device.id)} disabled={!(pendingSelections[device.id] ?? '').trim()}>+ {labels.addParticipant}</button>
        </div>
      {/if}

      <div class="participants-list">
        {#if assignedParticipants(device.id).length === 0}
          <p class="muted">{labels.emptyState}</p>
        {:else}
          {#each assignedParticipants(device.id) as participant, participantIndex (participant.id)}
            <div
              class="list-row participant-row"
              class:is-dragging={dragging?.participantId === participant.id}
              class:drop-before={dropTarget?.participantId === participant.id && !dropTarget.after}
              class:drop-after={dropTarget?.participantId === participant.id && dropTarget.after}
              role="listitem"
              on:dragover={(event) => dragOver(event, device.id, participant.id)}
              on:drop={(event) => drop(event, device.id)}
            >
              <span
                class="drag-handle"
                draggable="true"
                role="button"
                tabindex="-1"
                aria-label={labels.dragToReorder}
                title={labels.dragToReorder}
                on:dragstart={(event) => startDrag(event, device.id, participant.id)}
                on:dragend={endDrag}
              >⋮⋮</span>
              <input
                class="lane-input"
                class:is-mixed={isMixedLane(participant.deviceLane)}
                type="text"
                maxlength="8"
                value={participant.deviceLane ?? ''}
                aria-label={labels.laneLabel}
                title={mixedLaneTitle(participant.deviceLane)}
                placeholder={labels.laneLabel}
                on:blur={(event) => updateLane(participant.id, event.currentTarget.value)}
              />
              <span class="participant-text">
                <strong>{participantName(participant)}</strong>
                {#if tagCategories.length > 0}
                  <span class="participant-tags">
                    {#each tagCategories as category, categoryIndex}<CategoryTag {category} valueId={participant.categories[category.id]} offset={categoryIndex * 6} missingLabel={labels.missingCategoryValue} />{/each}
                  </span>
                {/if}
                {#if participantCategoryLabel(participant)}<small>{participantCategoryLabel(participant)}</small>{/if}
              </span>
              <RowActions
                {labels}
                canMoveUp={participantIndex > 0}
                canMoveDown={participantIndex < assignedParticipants(device.id).length - 1}
                onMoveUp={() => moveParticipant(device.id, participant.id, -1)}
                onMoveDown={() => moveParticipant(device.id, participant.id, 1)}
                onDelete={() => removeParticipantFromDevice(participant.id)}
              />
              <a
                class="open-participant"
                href={matchParticipantPath(match.id, participant.id)}
                aria-label={labels.openParticipant}
                title={labels.openParticipant}
                on:click={(event) => navigateOnClick(event, () => onOpenParticipant(participant.id))}
              >→</a>
            </div>
          {/each}
        {/if}
      </div>
    </div>
  {/each}
</section>
{#if renameError}<p class="error">{renameError}</p>{/if}
{#if deleteError}<p class="error">{deleteError}</p>{/if}
{#if assignmentError}<p class="error">{assignmentError}</p>{/if}
{#if laneError}<p class="error">{laneError}</p>{/if}
{#if orderError}<p class="error">{orderError}</p>{/if}

<LaneSheet {match} {devices} participants={sheetParticipants} {laneCategory} {deviceCategory} {language} {labels} />

<style>
  .list-panel {
    display: grid;
    gap: 16px;
    padding: 0;
    background: transparent;
    border: 0;
  }

  .device-block {
    padding: 20px;
    background: var(--paper);
    border: 1px solid var(--line);
  }

  .device-block > .list-row {
    padding-top: 0;
    border-top: 0;
  }

  .device-actions {
    display: flex;
    flex-wrap: wrap;
    gap: 10px;
    align-items: center;
    justify-content: flex-end;
    margin-left: auto;
  }

  .device-actions .icon-button {
    display: grid;
    place-items: center;
    flex: 0 0 44px;
    width: 44px;
    height: 44px;
    margin-left: 0;
    padding: 0;
    border: 1px solid var(--line);
    background: var(--paper);
  }

  .device-actions .icon-button:hover,
  .device-actions .icon-button:focus-visible,
  .participant-add-button:hover {
    color: var(--green);
    border-color: var(--green);
    background: var(--neutral);
  }

  .participant-add-row {
    align-items: end;
    margin-top: 12px;
  }

  .participants-list {
    margin-top: 12px;
  }

  .participant-row {
    border-top: 1px solid var(--line);
    margin-top: 8px;
    padding-top: 8px;
  }

  .device-name-input {
    display: block;
    width: 100%;
    max-width: 260px;
    padding: 4px 6px;
    border: 1px solid transparent;
    background: transparent;
    color: var(--ink);
    font-size: 14px;
    font-weight: 600;
  }

  .device-name-input:hover,
  .device-name-input:focus {
    border-color: var(--line);
    background: var(--paper);
  }

  .lane-check {
    margin-bottom: 16px;
  }

  .lane-overview {
    display: flex;
    flex-wrap: wrap;
    gap: 8px;
    margin-top: 16px;
  }

  .lane-chip {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    padding: 4px 8px;
    border: 1px solid var(--line);
    background: var(--paper);
  }

  .lane-chip.is-mixed {
    border-color: #e8755b;
    background: #fdeeea;
    color: #b84232;
  }

  .lane-chip.is-empty {
    border-style: dashed;
    color: var(--muted);
  }

  .device-warning {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 6px;
    margin-top: 4px;
    color: #b84232;
    font-size: 13px;
  }

  .participant-text {
    min-width: 0;
  }

  .participant-tags {
    display: flex;
    flex-wrap: wrap;
    gap: 4px;
    margin: 4px 0 2px;
  }

  .drag-handle {
    flex: 0 0 auto;
    padding: 8px 4px;
    color: var(--muted);
    cursor: grab;
    letter-spacing: -3px;
    user-select: none;
  }

  .participant-row.is-dragging {
    opacity: .5;
  }

  .participant-row.drop-before {
    box-shadow: inset 0 3px 0 var(--green);
  }

  .participant-row.drop-after {
    box-shadow: inset 0 -3px 0 var(--green);
  }

  .open-participant {
    display: grid;
    place-items: center;
    flex: 0 0 44px;
    width: 44px;
    height: 44px;
    border: 1px solid var(--line);
    background: var(--paper);
    color: var(--ink);
    text-decoration: none;
  }

  .open-participant:hover,
  .open-participant:focus-visible {
    color: var(--green);
    border-color: var(--green);
    background: var(--neutral);
  }

  .lane-input.is-mixed {
    border-color: #e8755b;
    background: #fdeeea;
  }

  .lane-input {
    flex: 0 0 64px;
    width: 64px;
    padding: 6px 8px;
    border: 1px solid var(--line);
    background: var(--paper);
    color: var(--ink);
    font-size: 13px;
    text-align: center;
  }

  @media (max-width: 720px) {
    .device-block {
      padding: 14px;
    }

    .device-block .list-row {
      flex-wrap: wrap;
      align-items: flex-start;
    }

    .device-actions {
      width: 100%;
      justify-content: flex-end;
      margin-left: 0;
    }
  }
</style>
