<script lang="ts">
  import type { ApiClient } from './api'
  import { labelForError } from './errors'
  import { parseMatchKeyboardConfig } from './matchConfig'
  import type { Category, KeyboardKey, Match, MatchParticipant } from './types'

  export let api: ApiClient
  export let match: Match
  export let participant: MatchParticipant
  export let categories: Category[]
  export let labels: Record<string, string>
  export let onBack: () => void
  export let onChanged: () => void
  export let onRemoved: () => void

  let removeError = ''
  let quickTotal = 0
  let quickSetError = ''
  let quickSetMessage = ''

  $: keyboardConfig = parseMatchKeyboardConfig(match.keyboardJson)
  $: matchCategories = keyboardConfig.categoryOrder.map((id) => categories.find((category) => category.id === id)).filter((category): category is Category => !!category)
  $: disabledKeyIds = new Set(
    keyboardConfig.disabledKeyRules
      .filter((rule) => participant.categories[rule.categoryId] === rule.valueId)
      .flatMap((rule) => rule.disabledKeyIds)
  )
  $: availableKeys = keyboardConfig.keyboard.filter((key) => !disabledKeyIds.has(key.keyId))
  $: devices = match.devices ?? []
  $: totalScore = (participant.scores ?? []).reduce((sum, score) => sum + score.value, 0)

  function categoryLabel(): string {
    return matchCategories
      .map((category) => category.values.find((value) => value.valueId === participant.categories[category.id])?.name)
      .filter((value): value is string => !!value)
      .join(' / ')
  }

  function scoreFor(end: number, arrow: number) {
    return (participant.scores ?? []).find((score) => score.end === end && score.arrow === arrow)
  }

  async function setScore(end: number, arrow: number, keyId: string) {
    const key = keyboardConfig.keyboard.find((item) => item.keyId === keyId)
    if (!key) return
    await api.enterScore(match.id, participant.id, { end, arrow, keyId: key.keyId, value: key.value })
    onChanged()
  }

  async function assignDevice(deviceId: string) {
    await api.assignParticipantDevice(match.id, participant.id, deviceId || null)
    onChanged()
  }

  function bestFillAssignments(total: number): KeyboardKey[] {
    const totalArrows = match.ends * match.arrowsPerEnd
    const scoringKeys = availableKeys
    if (scoringKeys.length === 0 || totalArrows === 0) return []
    const sortedDesc = [...scoringKeys].sort((a, b) => b.value - a.value)
    const lowestKey = [...scoringKeys].sort((a, b) => a.value - b.value)[0]
    const maxValue = sortedDesc[0].value
    const assignments: KeyboardKey[] = []
    let remaining = total
    for (let i = 0; i < totalArrows; i++) {
      const arrowsLeft = totalArrows - i
      const key = sortedDesc.find((candidate) => candidate.value <= remaining && remaining - candidate.value <= (arrowsLeft - 1) * maxValue) ?? lowestKey
      assignments.push(key)
      remaining -= key.value
    }
    return assignments
  }

  async function applyQuickSet() {
    quickSetError = ''
    quickSetMessage = ''
    const assignments = bestFillAssignments(quickTotal)
    if (assignments.length === 0) { quickSetError = labels.templateSaveError; return }
    try {
      let index = 0
      for (let end = 1; end <= match.ends; end++) {
        for (let arrow = 1; arrow <= match.arrowsPerEnd; arrow++) {
          const key = assignments[index]
          index++
          await api.enterScore(match.id, participant.id, { end, arrow, keyId: key.keyId, value: key.value })
        }
      }
      quickSetMessage = labels.templateSaved
      onChanged()
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
</div>

<section class="panel">
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
    <div class="editor-row wrap">
      <span class="muted">{labels.endLabel} {endIndex + 1}</span>
      {#each Array(match.arrowsPerEnd) as _, arrowIndex}
        <label>{labels.arrowLabel} {arrowIndex + 1}
          <select value={scoreFor(endIndex + 1, arrowIndex + 1)?.keyId ?? ''} on:change={(event) => setScore(endIndex + 1, arrowIndex + 1, event.currentTarget.value)}>
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

<button class="danger-button" on:click={remove}>{labels.removeParticipant}</button>
{#if removeError}<p class="error">{removeError}</p>{/if}

<style>
  .section-gap {
    margin-top: 32px;
  }

  .editor-row {
    display: flex;
    align-items: end;
    gap: 16px;
    padding: 14px 0;
    border-top: 1px solid var(--line);
  }

  .editor-row.wrap {
    flex-wrap: wrap;
  }
</style>
