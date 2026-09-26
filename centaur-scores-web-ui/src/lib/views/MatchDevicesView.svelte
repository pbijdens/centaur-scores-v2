<script lang="ts">
  import type { ApiClient } from '../api'
  import DropdownMenu from '../DropdownMenu.svelte'
  import { labelForError } from '../errors'
  import RowActions from '../RowActions.svelte'
  import type { Category, Language, Match, MatchParticipant, ScoreDevice } from '../types'

  export let api: ApiClient
  export let match: Match
  export let categories: Category[]
  export let language: Language
  export let labels: Record<string, string>
  export let onBack: () => void
  export let onChanged: () => void | Promise<void>

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
  let exportError = ''

  $: devices = [...(match.devices ?? [])].sort((a, b) => (a.sortOrder ?? Number.MAX_SAFE_INTEGER) - (b.sortOrder ?? Number.MAX_SAFE_INTEGER) || a.name.localeCompare(b.name))
  $: participants = match.participants ?? []
  $: unassignedParticipants = participants
    .filter((participant) => !participant.deviceId)
    .sort((a, b) => participantName(a).localeCompare(participantName(b)))

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
    return participants
      .filter((participant) => participant.deviceId === deviceId)
      .sort((a, b) => (a.deviceOrder ?? Number.MAX_SAFE_INTEGER) - (b.deviceOrder ?? Number.MAX_SAFE_INTEGER) || participantName(a).localeCompare(participantName(b)))
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

  async function exportLaneAssignments() {
    exportError = ''
    try {
      const { blob, filename } = await api.downloadLaneAssignmentExport(match.id, language)
      const url = URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.download = filename
      link.click()
      URL.revokeObjectURL(url)
    } catch (error) {
      exportError = labelForError(error, labels, 'exportLaneAssignmentsError')
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

<button class="back-link" on:click={onBack}>← {match.name}</button>
<div class="page-intro">
  <div><p class="eyebrow">{labels.eyebrowDevices}</p><h1>{labels.manageDevices}</h1><p class="muted">{labels.devicesHint}</p></div>
  <div class="match-header-actions">
    <button class="primary" on:click={() => (showAddForm = !showAddForm)}>+ {labels.newDevice}</button>
    <DropdownMenu ariaLabel={labels.deviceActions} buttonClass="actions-trigger" align="right">
      <svelte:fragment slot="trigger">⋯</svelte:fragment>
      <button class="menu-item" on:click={exportLaneAssignments}>{labels.exportLaneAssignments}</button>
    </DropdownMenu>
  </div>
</div>
{#if exportError}<p class="error">{exportError}</p>{/if}
{#if showAddForm}
  <form class="inline-form" on:submit|preventDefault={submitAdd}>
    <label>{labels.deviceNameLabel}<input bind:value={newDeviceName} /></label>
    <button class="primary" type="submit" disabled={!newDeviceName.trim()}>{labels.save}</button>
  </form>
  {#if createError}<p class="error">{createError}</p>{/if}
{/if}
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
          {#each assignedParticipants(device.id) as participant, participantIndex}
            <div class="list-row participant-row">
              <input
                class="lane-input"
                type="text"
                maxlength="8"
                value={participant.deviceLane ?? ''}
                aria-label={labels.laneLabel}
                placeholder={labels.laneLabel}
                on:blur={(event) => updateLane(participant.id, event.currentTarget.value)}
              />
              <span>
                <strong>{participantName(participant)}</strong>
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
