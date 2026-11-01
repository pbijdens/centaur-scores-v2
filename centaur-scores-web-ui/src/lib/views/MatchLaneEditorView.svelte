<script lang="ts">
  import { tick } from 'svelte'
  import type { ApiClient } from '../api'
  import CategoryTag from '../CategoryTag.svelte'
  import { labelForError } from '../errors'
  import { laneCheckCategories, loadLaneCheck, saveLaneCheck } from '../laneCheck'
  import { distinctValues } from '../laneSheet'
  import { buildLaneEditorState, deviceOfLane, deviceParticipantOrder, LANE_LETTERS, laneParticipantIds, slotKey, slotOf, type LaneEditorState, type LaneLetter } from '../laneEditor'
  import PageHeader from '../PageHeader.svelte'
  import { matchDevicesPath } from '../router'
  import type { Category, Match, MatchParticipant, ScoreDevice } from '../types'

  // The quick lane editor, a full-width page opened from the devices page (mouse only: HTML5 drag and drop).
  // Left: the archers not on a lane slot. Middle: numbered lanes of four slots (A-D) that are not on a device.
  // Right: the devices, each with the lanes dropped on it. Archers are dragged onto empty slots, lanes onto
  // devices (or back to the middle), devices onto each other to reorder them. Every change is written to the
  // server straight away through the regular endpoints (laneEditor.ts explains how lanes follow from the
  // archers' own device and lane), so there is no save button; requests run one after the other. Archers show
  // the values of the two lane-check categories (laneCheck.ts, picked here or on the devices page), lanes their
  // "same per lane" values.
  export let api: ApiClient
  export let match: Match
  export let categories: Category[]
  export let labels: Record<string, string>
  export let onClose: () => void
  // Refetches the match; used to rebuild the editor after a change failed to save.
  export let onChanged: () => Promise<void>

  type Drag = { kind: 'archer'; id: string } | { kind: 'lane'; lane: number } | { kind: 'device'; id: string }

  let archers: MatchParticipant[] = []
  let devices: ScoreDevice[] = []
  let state: LaneEditorState = { laneCount: 0, laneDevice: {}, slots: {} }
  // Until the first change the editor follows the match it is given (the route reloads it on entry);
  // after that its own state is the truth.
  let touched = false
  let pending = 0
  let error = ''
  let queue: Promise<void> = Promise.resolve()
  let drag: Drag | null = null
  let dropKey: string | null = null

  let { laneCategoryId, deviceCategoryId } = loadLaneCheck(match.id)
  $: saveLaneCheck(match.id, { laneCategoryId, deviceCategoryId })
  $: matchCategories = laneCheckCategories(match, categories)
  $: laneCategory = matchCategories.find((category) => category.id === laneCategoryId) ?? null
  $: deviceCategory = matchCategories.find((category) => category.id === deviceCategoryId) ?? null

  $: if (!touched) load(match)
  $: archerById = new Map(archers.map((archer) => [archer.id, archer]))
  $: placedIds = new Set(Object.values(state.slots))
  $: unplaced = archers.filter((archer) => !placedIds.has(archer.id)).sort((a, b) => archerName(a).localeCompare(archerName(b)))
  $: freeLanes = Array.from({ length: state.laneCount }, (_, index) => index + 1).filter((lane) => deviceOfLane(state, lane) === null)

  function load(current: Match) {
    devices = [...(current.devices ?? [])].sort((a, b) => (a.sortOrder ?? Number.MAX_SAFE_INTEGER) - (b.sortOrder ?? Number.MAX_SAFE_INTEGER) || a.name.localeCompare(b.name))
    archers = (current.participants ?? []).map((participant) => ({ ...participant }))
    state = buildLaneEditorState(archers, devices)
  }

  function lanesOfDevice(current: LaneEditorState, deviceId: string): number[] {
    return Array.from({ length: current.laneCount }, (_, index) => index + 1).filter((lane) => deviceOfLane(current, lane) === deviceId)
  }

  function archerName(archer: MatchParticipant): string {
    return archer.fullName || archer.lastName
  }

  function categoryLabel(archer: MatchParticipant): string {
    return categories
      .map((category) => category.values.find((value) => value.valueId === archer.categories[category.id])?.name)
      .filter((value): value is string => !!value)
      .join(' / ')
  }

  // Helpers used by the markup take the state they read as arguments, so the markup re-renders when it changes.

  // The "same per lane" values of the archers on a lane, in valueId order (null = an archer without a value).
  function laneValues(current: LaneEditorState, byId: Map<string, MatchParticipant>, category: Category | null, lane: number): (number | null)[] {
    if (!category) return []
    const onLane = laneParticipantIds(current, lane).map((id) => byId.get(id)).filter((archer): archer is MatchParticipant => !!archer)
    return distinctValues(onLane, category.id)
  }

  function deviceName(all: ScoreDevice[], deviceId: string | null | undefined): string {
    return all.find((device) => device.id === deviceId)?.name ?? ''
  }

  function laneName(lane: number): string {
    return labels.laneSheetLane.replace('{lane}', String(lane))
  }

  function setArcher(id: string, patch: Partial<MatchParticipant>) {
    archers = archers.map((archer) => (archer.id === id ? { ...archer, ...patch } : archer))
  }

  // Applies a change locally right away and queues its requests behind earlier ones. When a request fails,
  // the editor is rebuilt from the server so it never shows something that wasn't saved.
  function persist(task: () => Promise<unknown>) {
    touched = true
    error = ''
    pending++
    queue = queue
      .then(task)
      .catch(async (failure) => {
        error = labelForError(failure, labels, 'laneEditorSaveError')
        await onChanged()
        await tick()
        load(match)
      })
      .finally(() => pending--)
      .then(() => undefined)
  }

  // The device's archers in lane order; read when the request runs, so it includes all earlier changes.
  function saveDeviceOrder(deviceId: string) {
    return api.reorderDeviceParticipants(match.id, deviceId, deviceParticipantOrder(state, archers, deviceId))
  }

  function placeArcher(id: string, lane: number, letter: LaneLetter) {
    const key = slotKey(lane, letter)
    const archer = archerById.get(id)
    if (!archer || state.slots[key]) return
    const from = slotOf(state, id)
    const slots = { ...state.slots }
    if (from) delete slots[slotKey(from.lane, from.letter)]
    slots[key] = id
    state = { ...state, slots }
    const deviceId = deviceOfLane(state, lane)
    const previousDevice = archer.deviceId ?? null
    setArcher(id, { deviceId, deviceLane: key })
    persist(async () => {
      if (deviceId) {
        if (previousDevice !== deviceId) await api.assignParticipantDevice(match.id, id, deviceId)
        await api.updateParticipantLane(match.id, id, key)
        await saveDeviceOrder(deviceId)
      } else {
        // A lane that is not on a device: the archer leaves any device (which also clears the lane).
        if (previousDevice) await api.assignParticipantDevice(match.id, id, null)
        await api.updateParticipantLane(match.id, id, key)
      }
    })
  }

  function unplaceArcher(id: string) {
    const from = slotOf(state, id)
    const archer = archerById.get(id)
    if (!from || !archer) return
    const slots = { ...state.slots }
    delete slots[slotKey(from.lane, from.letter)]
    state = { ...state, slots }
    const previousDevice = archer.deviceId ?? null
    setArcher(id, { deviceId: null, deviceLane: null })
    persist(() => (previousDevice ? api.assignParticipantDevice(match.id, id, null) : api.updateParticipantLane(match.id, id, null)))
  }

  function moveLane(lane: number, deviceId: string | null) {
    if (deviceOfLane(state, lane) === deviceId) return
    state = { ...state, laneDevice: { ...state.laneDevice, [lane]: deviceId } }
    const ids = laneParticipantIds(state, lane)
    const lanes = new Map(ids.map((id) => [id, slotOf(state, id)!]))
    for (const id of ids) setArcher(id, { deviceId })
    if (ids.length === 0) return
    persist(async () => {
      for (const id of ids) {
        await api.assignParticipantDevice(match.id, id, deviceId)
        // Taking an archer off a device clears their lane on the server; put it back.
        if (!deviceId) await api.updateParticipantLane(match.id, id, slotKey(lanes.get(id)!.lane, lanes.get(id)!.letter))
      }
      if (deviceId) await saveDeviceOrder(deviceId)
    })
  }

  function addLane() {
    touched = true
    state = { ...state, laneCount: state.laneCount + 1 }
  }

  function addDevice() {
    const name = labels.laneEditorDeviceName.replace('{n}', String(devices.length + 1))
    persist(async () => {
      const device = await api.addDevice(match.id, { name })
      devices = [...devices, device]
    })
  }

  function renameDevice(deviceId: string, input: HTMLInputElement) {
    const device = devices.find((item) => item.id === deviceId)
    const name = input.value.trim()
    if (!device) return
    if (!name || name === device.name) {
      input.value = device.name
      return
    }
    devices = devices.map((item) => (item.id === deviceId ? { ...item, name } : item))
    persist(() => api.renameDevice(match.id, deviceId, name))
  }

  // The dragged device takes the target's place; the devices in between shift up or down.
  function moveDevice(deviceId: string, targetId: string) {
    const from = devices.findIndex((device) => device.id === deviceId)
    const to = devices.findIndex((device) => device.id === targetId)
    if (from < 0 || to < 0 || from === to) return
    const reordered = [...devices]
    const [moved] = reordered.splice(from, 1)
    reordered.splice(to, 0, moved)
    devices = reordered
    persist(() => api.reorderDevices(match.id, reordered.map((device) => device.id)))
  }

  function startDrag(event: DragEvent, value: Drag, image?: Element | null) {
    event.stopPropagation()
    drag = value
    if (!event.dataTransfer) return
    event.dataTransfer.effectAllowed = 'move'
    event.dataTransfer.setData('text/plain', value.kind) // Firefox only starts a drag with data set
    if (image) event.dataTransfer.setDragImage(image, 12, 12)
  }

  function endDrag() {
    drag = null
    dropKey = null
  }

  // Marks a valid drop target under the pointer. Handlers on nested targets run first; the editor's own
  // handler (last in the bubble) clears the highlight when nothing accepted the drag.
  function accept(event: DragEvent, key: string, ok: boolean) {
    if (!ok || event.defaultPrevented) return
    event.preventDefault()
    if (event.dataTransfer) event.dataTransfer.dropEffect = 'move'
    dropKey = key
  }

  function editorDragOver(event: DragEvent) {
    if (!event.defaultPrevented) dropKey = null
  }

  function slotDragOver(event: DragEvent, lane: number, letter: LaneLetter) {
    accept(event, `slot-${lane}${letter}`, drag?.kind === 'archer' && !state.slots[slotKey(lane, letter)])
  }

  function slotDrop(event: DragEvent, lane: number, letter: LaneLetter) {
    if (drag?.kind !== 'archer') return
    event.preventDefault()
    event.stopPropagation()
    const id = drag.id
    endDrag()
    placeArcher(id, lane, letter)
  }

  function unplacedDragOver(event: DragEvent) {
    accept(event, 'unplaced', drag?.kind === 'archer' && placedIds.has(drag.id))
  }

  function unplacedDrop(event: DragEvent) {
    if (drag?.kind !== 'archer') return
    event.preventDefault()
    const id = drag.id
    endDrag()
    unplaceArcher(id)
  }

  function lanesDragOver(event: DragEvent) {
    accept(event, 'lanes', drag?.kind === 'lane' && deviceOfLane(state, drag.lane) !== null)
  }

  function lanesDrop(event: DragEvent) {
    if (drag?.kind !== 'lane') return
    event.preventDefault()
    const lane = drag.lane
    endDrag()
    moveLane(lane, null)
  }

  function deviceDragOver(event: DragEvent, deviceId: string) {
    const ok = (drag?.kind === 'lane' && deviceOfLane(state, drag.lane) !== deviceId) || (drag?.kind === 'device' && drag.id !== deviceId)
    accept(event, `device-${deviceId}`, ok)
  }

  function deviceDrop(event: DragEvent, deviceId: string) {
    const current = drag
    if (current?.kind !== 'lane' && current?.kind !== 'device') return
    event.preventDefault()
    endDrag()
    if (current.kind === 'lane') moveLane(current.lane, deviceId)
    else moveDevice(current.id, deviceId)
  }
</script>

<svelte:head>
  <title>{labels.laneEditorTitle} - {match.name}</title>
</svelte:head>

<PageHeader eyebrow={labels.manageDevices} title={labels.laneEditorTitle} subtitle={match.name} actions={[{ label: labels.laneEditorClose, header: 'primary', href: matchDevicesPath(match.id), onClick: onClose }]} />

<div class="editor-toolbar">
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
  <p class="editor-status">
    <span class="muted">{labels.laneEditorHint}</span>
    {#if pending > 0}<span class="saving">{labels.laneEditorSaving}</span>{/if}
    {#if error}<span class="error">{error}</span>{/if}
  </p>
</div>

{#snippet archerTags(archer: MatchParticipant)}
  {#if laneCategory || deviceCategory}
    <span class="archer-tags">
      {#if laneCategory}<CategoryTag category={laneCategory} valueId={archer.categories[laneCategory.id]} missingLabel={labels.missingCategoryValue} />{/if}
      {#if deviceCategory}<CategoryTag category={deviceCategory} valueId={archer.categories[deviceCategory.id]} offset={6} missingLabel={labels.missingCategoryValue} />{/if}
    </span>
  {/if}
{/snippet}

{#snippet laneCard(lane: number)}
  {@const values = laneValues(state, archerById, laneCategory, lane)}
  <div class="lane-card">
    <div class="lane-header" draggable="true" role="button" tabindex="-1" on:dragstart={(event) => startDrag(event, { kind: 'lane', lane }, event.currentTarget.parentElement)} on:dragend={endDrag}>
      <span class="drag-handle" aria-hidden="true">⠿</span>
      <span class="lane-name">{laneName(lane)}</span>
      {#if laneCategory && values.length > 0}
        <span class="archer-tags">
          {#each values as value}<CategoryTag category={laneCategory} valueId={value ?? undefined} missingLabel={labels.missingCategoryValue} />{/each}
          {#if values.length > 1}<span class="warning" title={labels.mixedLaneWarning.replace('{lane}', String(lane)).replace('{category}', laneCategory.name)}>⚠</span>{/if}
        </span>
      {/if}
    </div>
    {#each LANE_LETTERS as letter}
      {@const id = state.slots[slotKey(lane, letter)]}
      {@const archer = id ? archerById.get(id) : undefined}
      <div class="slot" class:is-target={dropKey === `slot-${lane}${letter}`} role="listitem" on:dragover={(event) => slotDragOver(event, lane, letter)} on:drop={(event) => slotDrop(event, lane, letter)}>
        <span class="slot-letter">{letter}</span>
        {#if archer}
          <span class="slot-archer" draggable="true" role="button" tabindex="-1" title={categoryLabel(archer)} on:dragstart={(event) => startDrag(event, { kind: 'archer', id: archer.id })} on:dragend={endDrag}>
            <span class="archer-name">{archerName(archer)}</span>
            {@render archerTags(archer)}
          </span>
        {/if}
      </div>
    {/each}
  </div>
{/snippet}

<div class="lane-editor" class:is-dragging={!!drag} role="application" on:dragover={editorDragOver}>
  <section class="editor-column" class:is-target={dropKey === 'unplaced'} aria-label={labels.laneEditorUnassigned} on:dragover={unplacedDragOver} on:drop={unplacedDrop}>
    <h2>{labels.laneEditorUnassigned} <span class="count">{unplaced.length}</span></h2>
    <ul class="archer-list">
      {#each unplaced as archer (archer.id)}
        <li class="archer-card" draggable="true" title={categoryLabel(archer)} on:dragstart={(event) => startDrag(event, { kind: 'archer', id: archer.id })} on:dragend={endDrag}>
          <span class="archer-line">
            <span class="archer-name">{archerName(archer)}</span>
            {@render archerTags(archer)}
          </span>
          {#if archer.deviceId || archer.deviceLane}
            <span class="archer-badges">
              {#if archer.deviceId}<span class="badge">{deviceName(devices, archer.deviceId)}</span>{/if}
              {#if archer.deviceLane}<span class="badge is-muted">{labels.laneEditorWasLane.replace('{lane}', archer.deviceLane)}</span>{/if}
            </span>
          {/if}
        </li>
      {/each}
    </ul>
  </section>

  <section class="editor-column" class:is-target={dropKey === 'lanes'} aria-label={labels.laneEditorLanes} on:dragover={lanesDragOver} on:drop={lanesDrop}>
    <h2>{labels.laneEditorLanes}</h2>
    <div class="card-grid" role="list">
      {#each freeLanes as lane (lane)}{@render laneCard(lane)}{/each}
      <button type="button" class="add-card" on:click={addLane}>+ {labels.laneEditorAddLane}</button>
    </div>
  </section>

  <section class="editor-column" aria-label={labels.laneEditorDevices}>
    <h2>{labels.laneEditorDevices}</h2>
    <div class="card-grid">
      {#each devices as device (device.id)}
        {@const lanes = lanesOfDevice(state, device.id)}
        <div class="device-card" class:is-target={dropKey === `device-${device.id}`} role="list" on:dragover={(event) => deviceDragOver(event, device.id)} on:drop={(event) => deviceDrop(event, device.id)}>
          <div class="device-header">
            <span class="drag-handle" draggable="true" role="button" tabindex="-1" aria-label={labels.dragToReorder} on:dragstart={(event) => startDrag(event, { kind: 'device', id: device.id }, event.currentTarget.closest('.device-card'))} on:dragend={endDrag}>⠿</span>
            <input type="text" value={device.name} aria-label={labels.deviceNameLabel} on:change={(event) => renameDevice(device.id, event.currentTarget)} on:keydown={(event) => event.key === 'Enter' && event.currentTarget.blur()} />
          </div>
          <div class="device-lanes" role="list">
            {#each lanes as lane (lane)}{@render laneCard(lane)}{/each}
          </div>
          {#if lanes.length === 0}<p class="drop-hint">{labels.laneEditorDropLanesHere}</p>{/if}
        </div>
      {/each}
      <button type="button" class="add-card" disabled={pending > 0} on:click={addDevice}>+ {labels.laneEditorAddDevice}</button>
    </div>
  </section>
</div>

<style>
  /* The two lane-check category pickers and the status line, in one compact row above the columns. */
  .editor-toolbar {
    display: flex;
    flex-wrap: wrap;
    align-items: end;
    gap: 8px 16px;
    margin: -20px 0 14px;
  }

  .editor-toolbar label {
    display: grid;
    gap: 2px;
    font-size: 0.8rem;
    font-weight: 600;
  }

  .editor-toolbar select {
    min-width: 180px;
    padding: 4px 6px;
  }

  .editor-status {
    display: flex;
    flex-wrap: wrap;
    gap: 16px;
    margin: 0 0 4px;
    font-size: 0.9rem;
  }

  .saving {
    color: var(--green);
    font-weight: 600;
  }

  .editor-status .error {
    margin: 0;
  }

  /* Three columns that each scroll on their own, so the whole editor fits on one screen. */
  .lane-editor {
    display: grid;
    grid-template-columns: minmax(220px, 3fr) minmax(0, 4fr) minmax(0, 5fr);
    gap: 16px;
    height: calc(100svh - 250px);
    min-height: 420px;
  }

  .editor-column {
    display: flex;
    flex-direction: column;
    min-height: 0;
    overflow-y: auto;
    padding: 12px;
    border: 1px solid var(--line);
    border-radius: 8px;
    background: var(--paper);
  }

  .editor-column h2 {
    margin: 0 0 10px;
    font-size: 1rem;
  }

  .count {
    color: var(--muted);
    font-weight: 400;
  }

  .is-target {
    outline: 2px dashed var(--green);
    outline-offset: -2px;
  }

  /* As many columns of archer cards as fit. */
  .archer-list {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(170px, 1fr));
    align-content: start;
    gap: 6px;
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .archer-card {
    display: grid;
    gap: 2px;
    padding: 3px 6px;
    border: 1px solid var(--line);
    border-radius: 6px;
    background: #fff;
    cursor: grab;
    font-size: 0.85rem;
  }

  /* An archer on one line: the name, cut off with an ellipsis, then the lane-check category tags. */
  .archer-line,
  .slot-archer {
    display: flex;
    align-items: center;
    gap: 4px;
    min-width: 0;
  }

  .archer-name {
    flex: 1 1 auto;
    min-width: 0;
    overflow: hidden;
    white-space: nowrap;
    text-overflow: ellipsis;
    font-weight: 600;
  }

  .archer-tags {
    display: inline-flex;
    flex: 0 1 auto;
    align-items: center;
    gap: 2px;
    min-width: 0;
    overflow: hidden;
  }

  .archer-tags :global(.category-tag) {
    padding: 0 5px;
    font-size: 10px;
    line-height: 1.5;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  .warning {
    font-weight: 700;
  }

  .archer-badges {
    display: flex;
    flex-wrap: wrap;
    gap: 4px;
  }

  .badge {
    padding: 0 6px;
    border-radius: 999px;
    background: #e8f0e4;
    font-size: 0.75rem;
  }

  .badge.is-muted {
    background: #f0f0f0;
    color: var(--muted);
  }

  .card-grid {
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
    gap: 8px;
    align-items: start;
  }

  .lane-card {
    border: 1px solid #b8c4b2;
    border-radius: 6px;
    background: #fff;
    font-size: 0.8rem;
    overflow: hidden;
  }

  .lane-header {
    display: flex;
    align-items: center;
    gap: 6px;
    min-width: 0;
    padding: 3px 8px;
    background: #eef3eb;
    font-weight: 700;
    cursor: grab;
  }

  .slot {
    display: flex;
    align-items: center;
    gap: 6px;
    min-height: 24px;
    padding: 0 8px;
    border-top: 1px solid #eee;
  }

  .slot-letter {
    flex: 0 0 auto;
    width: 1em;
    color: var(--muted);
    font-weight: 700;
  }

  .slot-archer {
    flex: 1 1 auto;
    cursor: grab;
  }

  .slot-archer .archer-name {
    font-weight: 400;
  }

  .lane-name {
    flex: 0 0 auto;
  }

  .drag-handle {
    color: var(--muted);
    cursor: grab;
  }

  .device-card {
    display: grid;
    gap: 6px;
    padding: 8px;
    border: 1px solid var(--line);
    border-radius: 8px;
    background: #fafbf9;
  }

  .device-header {
    display: flex;
    align-items: center;
    gap: 6px;
  }

  .device-header input {
    flex: 1 1 auto;
    min-width: 0;
    padding: 4px 6px;
    font-weight: 700;
  }

  .device-lanes {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(150px, 1fr));
    gap: 6px;
  }

  .drop-hint {
    margin: 0;
    padding: 10px;
    border: 1px dashed var(--line);
    border-radius: 6px;
    color: var(--muted);
    font-size: 0.8rem;
    text-align: center;
  }

  .add-card {
    min-height: 40px;
    border: 1px dashed var(--line);
    border-radius: 6px;
    background: none;
    color: var(--muted);
    font-weight: 600;
    cursor: pointer;
  }

  .add-card:hover:not(:disabled) {
    border-color: var(--green);
    color: var(--green);
  }
</style>
