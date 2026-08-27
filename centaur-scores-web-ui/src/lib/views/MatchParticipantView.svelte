<script lang="ts">
  import type { ApiClient } from '../api'
  import DropdownMenu from '../DropdownMenu.svelte'
  import { labelForError } from '../errors'
  import { parseMatchKeyboardConfig } from '../matchConfig'
  import { deriveLastName, memberDisplayLabel } from '../participantName'
  import type { ArrowScore, Category, KeyboardKey, Match, MatchParticipant, ParticipantList } from '../types'

  export let api: ApiClient
  export let match: Match
  export let participant: MatchParticipant
  export let categories: Category[]
  export let sourceList: ParticipantList | null
  export let labels: Record<string, string>
  export let onBack: () => void
  export let onChanged: () => void | Promise<void>
  export let onRemoved: () => void

  let removeError = ''
  let quickTotal = 0
  let quickSetError = ''
  let quickSetMessage = ''

  let showMetadataEditor = false
  let editMode: 'manual' | 'list' = 'manual'
  let editFullName = ''
  let editFederationNumber = ''
  let editCategoryValues: Record<string, string> = {}
  let editSourceMemberId = ''
  let metadataError = ''
  let metadataMessage = ''

  $: keyboardConfig = parseMatchKeyboardConfig(match.keyboardJson)
  $: matchCategories = keyboardConfig.categoryOrder.map((id) => categories.find((category) => category.id === id)).filter((category): category is Category => !!category)
  $: disabledKeyIds = new Set(
    keyboardConfig.disabledKeyRules
      .filter((rule) => participant.categories[rule.categoryId] === rule.valueId)
      .flatMap((rule) => rule.disabledKeyIds)
  )
  $: availableKeys = keyboardConfig.keyboard.filter((key) => !disabledKeyIds.has(key.keyId))
  $: devices = [...(match.devices ?? [])].sort((a, b) => (a.sortOrder ?? Number.MAX_SAFE_INTEGER) - (b.sortOrder ?? Number.MAX_SAFE_INTEGER) || a.name.localeCompare(b.name))
  $: totalScore = (participant.scores ?? []).reduce((sum, score) => sum + score.value, 0)
  $: assignedMemberIds = new Set(
    (match.participants ?? [])
      .filter((item) => item.id !== participant.id)
      .map((item) => item.participantListMemberId)
      .filter((id): id is string => !!id)
  )
  $: availableMembers = sourceList ? sourceList.members.filter((member) => member.isActive && !assignedMemberIds.has(member.id)) : []
  $: if (!sourceList) editMode = 'manual'
  $: showGroupRunningTotal = !!match.groupEnds && match.groupEnds > 0 && match.groupEnds < match.ends

  function categoryLabel(): string {
    return matchCategories
      .map((category) => category.values.find((value) => value.valueId === participant.categories[category.id])?.name)
      .filter((value): value is string => !!value)
      .join(' / ')
  }

  function openMetadataEditor() {
    editFullName = participant.fullName
    editFederationNumber = participant.federationNumber ?? ''
    editCategoryValues = {}
    for (const category of matchCategories) {
      const value = participant.categories[category.id]
      if (value !== undefined) editCategoryValues[category.id] = String(value)
    }
    editSourceMemberId = participant.participantListMemberId ?? ''
    editMode = sourceList ? 'list' : 'manual'
    metadataError = ''
    metadataMessage = ''
    showMetadataEditor = true
  }

  function applyUpdatedMetadata(updated: MatchParticipant) {
    // the update endpoint does not return entered scores, so keep the ones we already have locally until the next full refresh
    participant = { ...participant, ...updated, scores: participant.scores }
  }

  async function submitManualEdit() {
    if (!editFullName.trim()) return
    metadataError = ''
    const categoryValues: Record<string, number> = {}
    for (const category of matchCategories) {
      const value = editCategoryValues[category.id]
      if (value) categoryValues[category.id] = Number(value)
    }
    try {
      const updated = await api.updateMatchParticipant(match.id, participant.id, { participantListMemberId: null, lastName: deriveLastName(editFullName.trim()), fullName: editFullName.trim(), federationNumber: editFederationNumber || null, categories: categoryValues })
      applyUpdatedMetadata(updated)
      showMetadataEditor = false
      metadataMessage = labels.participantSaved
      await onChanged()
    } catch (error) {
      metadataError = labelForError(error, labels, 'participantSaveError')
    }
  }

  async function submitReplaceFromList() {
    const member = sourceList?.members.find((item) => item.id === editSourceMemberId)
    if (!member) return
    metadataError = ''
    try {
      const updated = await api.updateMatchParticipant(match.id, participant.id, { participantListMemberId: member.id, lastName: member.lastName, fullName: member.fullName, federationNumber: member.federationNumber, categories: member.categories })
      applyUpdatedMetadata(updated)
      showMetadataEditor = false
      metadataMessage = labels.participantSaved
      await onChanged()
    } catch (error) {
      metadataError = labelForError(error, labels, 'participantSaveError')
    }
  }

  function scoreFor(end: number, arrow: number) {
    return (participant.scores ?? []).find((score) => score.end === end && score.arrow === arrow)
  }

  function totalForEnd(end: number): number {
    return totalBetweenEnds(end, end)
  }

  function totalBetweenEnds(firstEnd: number, lastEnd: number): number {
    return (participant.scores ?? [])
      .filter((score) => score.end >= firstEnd && score.end <= lastEnd)
      .reduce((sum, score) => sum + score.value, 0)
  }

  function runningTotal(end: number): number {
    return totalBetweenEnds(1, end)
  }

  function groupRunningTotal(end: number): number {
    const groupEnds = match.groupEnds ?? match.ends
    const firstEnd = Math.floor((end - 1) / groupEnds) * groupEnds + 1
    return totalBetweenEnds(firstEnd, end)
  }

  async function setScore(end: number, arrow: number, keyId: string) {
    const key = keyboardConfig.keyboard.find((item) => item.keyId === keyId)
    if (!key) return
    // update totals immediately instead of waiting for the round-trip to finish
    const otherScores = (participant.scores ?? []).filter((score) => !(score.end === end && score.arrow === arrow))
    participant = { ...participant, scores: [...otherScores, { id: `${participant.id}-${end}-${arrow}`, matchParticipantId: participant.id, end, arrow, keyId: key.keyId, value: key.value }] }
    await api.enterScore(match.id, participant.id, { end, arrow, keyId: key.keyId, value: key.value })
    await onChanged()
  }

  async function assignDevice(deviceId: string) {
    participant = { ...participant, deviceId: deviceId || null }
    await api.assignParticipantDevice(match.id, participant.id, deviceId || null)
    await onChanged()
  }

  function bestFillAssignments(total: number): KeyboardKey[] {
    const totalArrows = match.ends * match.arrowsPerEnd
    if (totalArrows === 0 || total <= 0) return []

    // prefer keys with a numeric label over keys with labels such as X, M, etc. when the value is the same
    const orderedKeys = [...availableKeys]
      .sort((a, b) => {
        if (a.value !== b.value) return a.value - b.value

        const aIsNumeric = /^[0-9]+$/.test(a.label.trim())
        const bIsNumeric = /^[0-9]+$/.test(b.label.trim())

        if (aIsNumeric !== bIsNumeric) return Number(bIsNumeric) - Number(aIsNumeric)
        return 0
      })

    const uniqueScoreKeys: KeyboardKey[] = []
    const seenScores = new Set<number>()
    for (const key of orderedKeys) {
      if (seenScores.has(key.value)) continue
      seenScores.add(key.value)
      uniqueScoreKeys.push(key)
    }

    if (uniqueScoreKeys.length === 0) return []

    const scoringKeys = [...uniqueScoreKeys].sort((a, b) => a.value - b.value) // ascending order
    const assignments: KeyboardKey[] = []
    let arrowsToSet = totalArrows
    let pointsLeftToSet = total

    while (arrowsToSet > 0) {
      const averageTarget = pointsLeftToSet / arrowsToSet
      const nextHigher = scoringKeys
        .filter((key) => key.value >= averageTarget)[0]

      // console.log('averageTarget', averageTarget, 'nextHigher', nextHigher)

      const selected = nextHigher ?? scoringKeys[scoringKeys.length - 1] // if no higher, pick the highest available
      assignments.push(selected)

      pointsLeftToSet -= selected.value
      arrowsToSet -= 1
    }

    return assignments
  }

  async function applyQuickSet() {
    quickSetError = ''
    quickSetMessage = ''
    const assignments = bestFillAssignments(quickTotal)
    if (assignments.length === 0) { quickSetError = labels.templateSaveError; return }
    try {
      const newScores: ArrowScore[] = []
      let index = 0
      for (let end = 1; end <= match.ends; end++) {
        for (let arrow = 1; arrow <= match.arrowsPerEnd; arrow++) {
          const key = assignments[index]
          index++
          newScores.push({ id: `${participant.id}-${end}-${arrow}`, matchParticipantId: participant.id, end, arrow, keyId: key.keyId, value: key.value })
        }
      }
      // reflect the new totals and dropdown selections immediately, then reconcile with the server
      participant = { ...participant, scores: newScores }
      for (const score of newScores) {
        await api.enterScore(match.id, participant.id, { end: score.end, arrow: score.arrow, keyId: score.keyId, value: score.value })
      }
      window.location.reload()
    } catch (error) {
      quickSetError = labelForError(error, labels, 'templateSaveError')
    }
  }

  async function remove() {
    removeError = ''
    const message = labels.removeParticipantConfirm.replace('{name}', participant.fullName || participant.lastName)
    if (!confirm(message)) return
    try {
      await api.removeMatchParticipant(match.id, participant.id)
      onRemoved()
    } catch (error) {
      removeError = labelForError(error, labels, 'templateSaveError')
    }
  }
</script>

<button class="back-link" on:click={onBack}>← {labels.matches}</button>
<div class="page-intro">
  <div><p class="eyebrow">{labels.eyebrowParticipantDetail}</p><h1>{participant.fullName || participant.lastName}</h1>{#if categoryLabel()}<p class="muted">{categoryLabel()}</p>{/if}</div>
  <div class="match-header-actions">
    <DropdownMenu ariaLabel={labels.matchActions} buttonClass="actions-trigger" align="right">
      <svelte:fragment slot="trigger">⋯</svelte:fragment>
      <button class="menu-item menu-item-danger" on:click={remove}>{labels.removeParticipant}</button>
    </DropdownMenu>
  </div>
</div>
{#if removeError}<p class="error">{removeError}</p>{/if}

<section class="panel">
  <div>
    <h2>{labels.participantDetailsLabel}</h2>
    <p class="muted">{participant.federationNumber} / {participant.fullName || participant.lastName}{#if categoryLabel()}<span class="muted">/ {categoryLabel()}</span>{/if}</p>
    <button class="primary" on:click={() => (showMetadataEditor ? (showMetadataEditor = false) : openMetadataEditor())}>{labels.editParticipantDetails}</button>
  </div>
  {#if showMetadataEditor}
    {#if sourceList}
      <form class="inline-form">
        <label>{labels.addModeLabel}
          <select bind:value={editMode}>
            <option value="manual">{labels.editModeManual}</option>
            <option value="list">{labels.editModeReplace}</option>
          </select>
        </label>
      </form>
    {/if}
    {#if editMode === 'list' && sourceList}
      <form class="inline-form" on:submit|preventDefault={submitReplaceFromList}>
        <label>{labels.selectParticipantLabel}
          <select bind:value={editSourceMemberId}>
            <option value="">{labels.selectValue}</option>
            {#each availableMembers as member}<option value={member.id}>{memberDisplayLabel(categories, member)}</option>{/each}
          </select>
        </label>
        <button class="primary" type="submit" disabled={!editSourceMemberId}>{labels.save}</button>
        <button type="button" on:click={() => (showMetadataEditor = false)}>{labels.cancel}</button>
      </form>
    {:else}
      <form class="inline-form" on:submit|preventDefault={submitManualEdit}>
        <label>{labels.fullNameLabel}<input bind:value={editFullName} autocomplete="off" /></label>
        <label>{labels.federationNumberLabel}<input bind:value={editFederationNumber} /></label>
        {#each matchCategories as category}
          <label>{category.name}
            <select bind:value={editCategoryValues[category.id]}>
              <option value="">{labels.selectValue}</option>
              {#each [...category.values].sort((a, b) => a.valueId - b.valueId) as value}<option value={String(value.valueId)}>{value.name}</option>{/each}
            </select>
          </label>
        {/each}
        <button class="primary" type="submit" disabled={!editFullName.trim()}>{labels.save}</button>
        <button class="secondary" type="button" on:click={() => (showMetadataEditor = false)}>{labels.cancel}</button>
      </form>
    {/if}
  {/if}
  {#if metadataError}<p class="error">{metadataError}</p>{/if}
  {#if metadataMessage}<p class="success">{metadataMessage}</p>{/if}
</section>

<section class="panel section-gap">
  <label>{labels.assignedDeviceLabel}
    <select value={participant.deviceId ?? ''} on:change={(event) => assignDevice(event.currentTarget.value)}>
      <option value="">{labels.noDevice}</option>
      {#each devices as device}<option value={device.id}>{device.name}</option>{/each}
    </select>
  </label>
</section>

<section class="panel section-gap">
  <h2>{labels.scoresLabel}</h2>
  <p class="muted">{labels.totalScoreLabel}: {totalScore}</p>
  {#each Array(match.ends) as _, endIndex}
    <div class="editor-row">
      <div class="score-summary">
        <span><span class="muted">{labels.endLabel}</span><strong>{endIndex + 1}</strong></span>
        <span><span class="muted">{labels.endScoreLabel}</span><strong>{totalForEnd(endIndex + 1)}</strong></span>
        <span><span class="muted">{labels.runningTotalLabel}</span><strong>{runningTotal(endIndex + 1)}{#if showGroupRunningTotal}<span>&nbsp;({groupRunningTotal(endIndex + 1)})</span>{/if}</strong></span>
      </div>
      {#each Array(match.arrowsPerEnd) as _, arrowIndex}
        <label class="arrow-score-label">{labels.arrowLabel} {arrowIndex + 1}
          <select class="arrow-score-select" value={scoreFor(endIndex + 1, arrowIndex + 1)?.keyId ?? ''} on:change={(event) => setScore(endIndex + 1, arrowIndex + 1, event.currentTarget.value)}>
            <option value="">{labels.selectValue}</option>
            {#each availableKeys as key}<option value={key.keyId}>{key.label}</option>{/each}
          </select>
        </label>
      {/each}
    </div>
  {/each}
</section>

<section class="panel section-gap">
  <h2>{labels.quickSetTotal}</h2>
  <form class="inline-form" on:submit|preventDefault={applyQuickSet}>
    <label>{labels.totalScoreLabel}<input type="number" bind:value={quickTotal} /></label>
    <button class="primary" type="submit">{labels.applyLabel}</button>
  </form>
  {#if quickSetError}<p class="error">{quickSetError}</p>{/if}
  {#if quickSetMessage}<p class="success">{quickSetMessage}</p>{/if}
</section>

<style>
  .section-gap {
    margin-top: 32px;
  }

  .editor-row {
    display: flex;
    align-items: end;
    flex-wrap: wrap;
    gap: 16px;
    padding: 14px 0;
    border-top: 1px solid var(--line);
  }

  .score-summary {
    display: flex;
    flex-wrap: wrap;
    gap: 16px;
  }

  .score-summary > span {
    display: grid;
    gap: 4px;
    min-width: 96px;
  }

  .arrow-score-label {
    width: 90px;
  }

  .arrow-score-select {
    width: 100%;
  }

  @media (max-width: 720px) {
    .editor-row {
      flex-direction: column;
      align-items: stretch;
    }

    .arrow-score-label {
      width: 100%;
    }
  }
</style>
