<script lang="ts">
  import { tick } from 'svelte'
  import type { ApiClient } from '../api'
  import DropdownMenu from '../DropdownMenu.svelte'
  import { labelForError } from '../errors'
  import { parseMatchKeyboardConfig } from '../matchConfig'
  import ParticipantScorecard from '../ParticipantScorecard.svelte'
  import { arrowsShotCount, groupTotals, hasGroups, totalScore } from '../scorecard'
  import { matchParticipantEditPath, matchParticipantReplacePath, matchParticipantScorePath, navigateOnClick, participantMemberPath } from '../router'
  import type { Category, KeyboardKey, Match, MatchParticipant, ParticipantList } from '../types'

  export let api: ApiClient
  export let match: Match
  export let participant: MatchParticipant
  export let categories: Category[]
  export let sourceList: ParticipantList | null
  export let canManage: boolean
  export let labels: Record<string, string>
  export let onBack: () => void
  export let onChanged: () => void | Promise<void>
  export let onRemoved: () => void
  export let onAddToList: () => void
  export let onEditSharedDetails: () => void
  export let onEditDetails: () => void
  export let onChangeParticipant: () => void
  export let onViewScores: () => void

  let removeError = ''
  let quickTotal = 0
  let quickSetError = ''
  let quickSetMessage = ''
  let showQuickSet = false
  let showScorecard = false
  let quickSetSection: HTMLElement | null = null

  $: keyboardConfig = parseMatchKeyboardConfig(match.keyboardJson)
  $: matchCategories = keyboardConfig.categoryOrder.map((id) => categories.find((category) => category.id === id)).filter((category): category is Category => !!category)
  $: disabledKeyIds = new Set(
    keyboardConfig.disabledKeyRules
      .filter((rule) => participant.categories[rule.categoryId] === rule.valueId)
      .flatMap((rule) => rule.disabledKeyIds)
  )
  $: availableKeys = keyboardConfig.keyboard.filter((key) => !disabledKeyIds.has(key.keyId))
  $: devices = [...(match.devices ?? [])].sort((a, b) => (a.sortOrder ?? Number.MAX_SAFE_INTEGER) - (b.sortOrder ?? Number.MAX_SAFE_INTEGER) || a.name.localeCompare(b.name))
  $: currentTotal = totalScore(participant.scores)
  $: totalArrows = match.ends * match.arrowsPerEnd
  $: shotArrows = arrowsShotCount(participant.scores)
  $: showGroupTotals = hasGroups(match)
  $: currentGroupTotals = showGroupTotals ? groupTotals(match, participant.scores) : []
  $: editSharedDetailsHref = sourceList && participant.participantListMemberId ? participantMemberPath(sourceList.id, participant.participantListMemberId) : ''

  function categoryLabel(): string {
    return matchCategories
      .map((category) => category.values.find((value) => value.valueId === participant.categories[category.id])?.name)
      .filter((value): value is string => !!value)
      .join(' / ')
  }

  function bestFillAssignments(total: number): KeyboardKey[] {
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
      const newScores = []
      let index = 0
      for (let end = 1; end <= match.ends; end++) {
        for (let arrow = 1; arrow <= match.arrowsPerEnd; arrow++) {
          const key = assignments[index]
          index++
          newScores.push({ id: `${participant.id}-${end}-${arrow}`, matchParticipantId: participant.id, end, arrow, keyId: key.keyId, value: key.value })
        }
      }
      // reflect the new totals immediately, then reconcile with the server
      participant = { ...participant, scores: newScores }
      for (const score of newScores) {
        await api.enterScore(match.id, participant.id, { end: score.end, arrow: score.arrow, keyId: score.keyId, value: score.value })
      }
      window.location.reload()
    } catch (error) {
      quickSetError = labelForError(error, labels, 'templateSaveError')
    }
  }

  async function assignDevice(deviceId: string) {
    participant = { ...participant, deviceId: deviceId || null }
    await api.assignParticipantDevice(match.id, participant.id, deviceId || null)
    await onChanged()
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

  // The quick-set form only renders once shown, so wait for that DOM update before scrolling to
  // it - otherwise it isn't there yet to scroll to, and it can land off-screen at the page bottom.
  async function toggleQuickSet() {
    showQuickSet = !showQuickSet
    if (showQuickSet) {
      await tick()
      quickSetSection?.scrollIntoView({ behavior: 'smooth', block: 'start' })
    }
  }
</script>

<button class="back-link" on:click={onBack}>← {labels.matches}</button>
<div class="page-intro">
  <div><p class="eyebrow">{labels.eyebrowParticipantDetail}</p><h1>{participant.fullName || participant.lastName}</h1>{#if categoryLabel()}<p class="muted">{categoryLabel()}</p>{/if}</div>
  <div class="match-header-actions">
    <DropdownMenu ariaLabel={labels.matchActions} buttonClass="actions-trigger" align="right">
      <svelte:fragment slot="trigger">⋯</svelte:fragment>
      {#if participant.participantListMemberId}
        {#if editSharedDetailsHref}
          <a class="menu-item" href={editSharedDetailsHref} on:click={(event) => navigateOnClick(event, onEditSharedDetails)}>{labels.editSharedDetails}</a>
        {/if}
        <a class="menu-item" href={matchParticipantReplacePath(match.id, participant.id)} on:click={(event) => navigateOnClick(event, onChangeParticipant)}>{labels.changeParticipant}</a>
      {:else}
        <a class="menu-item" href={matchParticipantEditPath(match.id, participant.id)} on:click={(event) => navigateOnClick(event, onEditDetails)}>{labels.editUnlistedParticipantDetails}</a>
        {#if sourceList}
          <a class="menu-item" href={matchParticipantReplacePath(match.id, participant.id)} on:click={(event) => navigateOnClick(event, onChangeParticipant)}>{labels.linkParticipant}</a>
        {/if}
      {/if}
      <a class="menu-item" href={matchParticipantScorePath(match.id, participant.id)} on:click={(event) => navigateOnClick(event, onViewScores)}>{labels.viewEditScores}</a>
      <button class="menu-item" on:click={toggleQuickSet}>{labels.replaceScores}</button>
      <button class="menu-item menu-item-danger" on:click={remove}>{labels.removeParticipant}</button>
    </DropdownMenu>
  </div>
</div>
{#if removeError}<p class="error">{removeError}</p>{/if}

<section class="panel">
  <h2>{labels.participantDetailsLabel}</h2>
  <p class="muted">{participant.federationNumber} / {participant.fullName || participant.lastName}{#if categoryLabel()}<span class="muted">/ {categoryLabel()}</span>{/if}</p>
  {#if !participant.participantListMemberId && sourceList && canManage}
    <a class="primary" href={participantMemberPath(sourceList.id, 'new')} on:click={(event) => navigateOnClick(event, onAddToList)}>{labels.addToParticipantList.replace('{name}', sourceList.name)}</a>
  {/if}
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
  <div class="scores-header">
    <h2>{labels.scoresLabel}</h2>
    <a class="edit-scores-pencil" href={matchParticipantScorePath(match.id, participant.id)} aria-label={labels.editScoresAria} title={labels.editScoresAria} on:click={(event) => navigateOnClick(event, onViewScores)}>✎</a>
  </div>
  <div class="stats-row">
    <span><span class="muted">{labels.currentTotalLabel}</span><strong>{currentTotal}</strong></span>
    <span><span class="muted">{labels.arrowsShotLabel}</span><strong>{shotArrows} / {totalArrows}</strong></span>
    {#each currentGroupTotals as groupTotal, index}
      <span><span class="muted">{labels.groupTotalLabel.replace('{n}', String(index + 1))}</span><strong>{groupTotal}</strong></span>
    {/each}
  </div>
  <button type="button" class="text-button" on:click={() => (showScorecard = !showScorecard)}>{showScorecard ? labels.hideScorecard : labels.showScorecard}</button>
  {#if showScorecard}
    <div class="scorecard-preview">
      <ParticipantScorecard {match} scores={participant.scores} keyboard={keyboardConfig.keyboard} interactive={false} />
    </div>
  {/if}
</section>

{#if showQuickSet}
  <section class="panel section-gap" bind:this={quickSetSection}>
    <h2>{labels.quickSetTotal}</h2>
    <form class="inline-form" on:submit|preventDefault={applyQuickSet}>
      <label>{labels.totalScoreLabel}<input type="number" bind:value={quickTotal} /></label>
      <button class="primary" type="submit">{labels.applyLabel}</button>
    </form>
    {#if quickSetError}<p class="error">{quickSetError}</p>{/if}
    {#if quickSetMessage}<p class="success">{quickSetMessage}</p>{/if}
  </section>
{/if}

<style>
  .section-gap {
    margin-top: 32px;
  }

  .scores-header {
    display: flex;
    align-items: center;
    gap: 10px;
  }

  .edit-scores-pencil {
    text-decoration: none;
    font-size: 16px;
    line-height: 1;
    color: var(--muted);
    border: 1px solid var(--line);
    border-radius: 6px;
    padding: 4px 8px;
  }

  .stats-row {
    display: flex;
    flex-wrap: wrap;
    gap: 16px;
    margin: 12px 0;
  }

  .stats-row > span {
    display: grid;
    gap: 4px;
    min-width: 96px;
  }

  .scorecard-preview {
    margin-top: 12px;
    border: 1px solid var(--line);
    border-radius: 8px;
    padding: 4px 12px;
  }
</style>
