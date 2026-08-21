<script lang="ts">
  import type { ApiClient } from './api'
  import { labelForError } from './errors'
  import { parseMatchKeyboardConfig, parseMatchScoringRules } from './matchConfig'
  import { deviceSelectionModes, keyboardColors } from './templateConfig'
  import type { Category, Match, ParticipantList } from './types'

  export let api: ApiClient
  export let match: Match
  export let categories: Category[]
  export let participantLists: ParticipantList[]
  export let labels: Record<string, string>
  export let onBack: () => void
  export let onSaved: () => void
  export let onDeleted: () => void

  let name = match.name
  let date = match.date.slice(0, 10)
  let shortCode = match.shortCode ?? ''
  let participantListId = match.participantListId ?? ''
  let allowFreeParticipants = match.allowFreeParticipants
  let deviceSelectionMode = match.deviceSelectionMode
  let ends = match.ends
  let arrowsPerEnd = match.arrowsPerEnd
  let groupEnds = match.groupEnds ?? null
  let keyboardConfig = parseMatchKeyboardConfig(match.keyboardJson)
  let scoringRules = parseMatchScoringRules(match.scoringRulesJson)
  let saveMessage = ''
  let saveError = ''
  let deleteError = ''

  $: hasParticipants = (match.participants ?? []).length > 0
  $: orderedCategories = [
    ...keyboardConfig.categoryOrder.map((id) => categories.find((category) => category.id === id)).filter((category): category is Category => !!category),
    ...categories.filter((category) => !keyboardConfig.categoryOrder.includes(category.id))
  ]

  function modeLabel(mode: string): string {
    if (mode === 'list') return labels.modeList
    if (mode === 'list-and-free') return labels.modeListAndFree
    return labels.modeRestricted
  }

  function toggleCategory(categoryId: string) {
    keyboardConfig.categoryOrder = keyboardConfig.categoryOrder.includes(categoryId)
      ? keyboardConfig.categoryOrder.filter((id) => id !== categoryId)
      : [...keyboardConfig.categoryOrder, categoryId]
  }

  function moveCategory(index: number, direction: -1 | 1) {
    const order = [...keyboardConfig.categoryOrder]
    const target = index + direction
    if (target < 0 || target >= order.length) return
    ;[order[index], order[target]] = [order[target], order[index]]
    keyboardConfig.categoryOrder = order
  }

  function addKey() {
    keyboardConfig.keyboard = [...keyboardConfig.keyboard, { keyId: '', label: '', color: 'Yellow', value: 0 }]
  }

  function removeKey(index: number) {
    keyboardConfig.keyboard = keyboardConfig.keyboard.filter((_, i) => i !== index)
  }

  function moveKey(index: number, direction: -1 | 1) {
    const keyboard = [...keyboardConfig.keyboard]
    const target = index + direction
    if (target < 0 || target >= keyboard.length) return
    ;[keyboard[index], keyboard[target]] = [keyboard[target], keyboard[index]]
    keyboardConfig.keyboard = keyboard
  }

  function addDisabledKeyRule() {
    const firstCategory = categories[0]
    if (!firstCategory) return
    keyboardConfig.disabledKeyRules = [...keyboardConfig.disabledKeyRules, { categoryId: firstCategory.id, valueId: firstCategory.values[0]?.valueId ?? 0, disabledKeyIds: [] }]
  }

  function removeDisabledKeyRule(index: number) {
    keyboardConfig.disabledKeyRules = keyboardConfig.disabledKeyRules.filter((_, i) => i !== index)
  }

  function toggleRuleDisabledKey(ruleIndex: number, keyId: string) {
    keyboardConfig.disabledKeyRules = keyboardConfig.disabledKeyRules.map((rule, index) => {
      if (index !== ruleIndex) return rule
      const disabledKeyIds = rule.disabledKeyIds.includes(keyId) ? rule.disabledKeyIds.filter((id) => id !== keyId) : [...rule.disabledKeyIds, keyId]
      return { ...rule, disabledKeyIds }
    })
  }

  function addScoringRule() {
    scoringRules = [...scoringRules, { type: 'total' }]
  }

  function removeScoringRule(index: number) {
    if (scoringRules.length <= 1) return
    scoringRules = scoringRules.filter((_, i) => i !== index)
  }

  function moveScoringRule(index: number, direction: -1 | 1) {
    const rules = [...scoringRules]
    const target = index + direction
    if (target < 0 || target >= rules.length) return
    ;[rules[index], rules[target]] = [rules[target], rules[index]]
    scoringRules = rules
  }

  async function save() {
    saveMessage = ''
    saveError = ''
    try {
      await api.updateMatch(match.id, {
        name,
        date,
        shortCode: shortCode || null,
        isOpen: match.isOpen,
        participantListId: participantListId || null,
        deviceSelectionMode,
        ends,
        arrowsPerEnd,
        groupEnds,
        allowFreeParticipants,
        keyboardJson: JSON.stringify(keyboardConfig),
        scoringRulesJson: JSON.stringify(scoringRules)
      })
      saveMessage = labels.matchSaved
      onSaved()
    } catch (error) {
      saveError = labelForError(error, labels, 'matchSaveError')
    }
  }

  async function remove() {
    deleteError = ''
    const message = labels.deleteMatchConfirm.replace('{name}', name)
    if (!confirm(message)) return
    try {
      await api.deleteMatch(match.id)
      onDeleted()
    } catch (error) {
      deleteError = labelForError(error, labels, 'matchDeleteError')
    }
  }
</script>

<button class="back-link" on:click={onBack}>← {match.name}</button>
<div class="page-intro">
  <div><p class="eyebrow">{labels.eyebrowMatchMetadata}</p><h1>{match.name}</h1></div>
</div>

<section class="panel">
  <label>{labels.matchNameLabel}<input bind:value={name} /></label>
  <label>{labels.matchDateLabel}<input type="date" bind:value={date} /></label>
  <label>{labels.shortCodeLabel}<input bind:value={shortCode} /></label>
  <label>{labels.endsLabel}<input type="number" min="1" bind:value={ends} /></label>
  <label>{labels.arrowsPerEndLabel}<input type="number" min="1" bind:value={arrowsPerEnd} /></label>
  <label>{labels.groupEndsLabel}<input type="number" min="1" bind:value={groupEnds} /></label>
</section>

<section class="panel section-gap">
  <h2>{labels.participantsBlockLabel}</h2>
  <label>{labels.participantListLabel}
    <select bind:value={participantListId} disabled={hasParticipants}>
      <option value="">{labels.noParticipantList}</option>
      {#each participantLists as list}<option value={list.id}>{list.name}</option>{/each}
    </select>
  </label>
  {#if hasParticipants}<p class="muted">{labels.participantListLockedHint}</p>{/if}
  <label class="checkbox-label"><input type="checkbox" bind:checked={allowFreeParticipants} /> {labels.allowFreeParticipantsLabel}</label>
  <label>{labels.deviceModeLabel}
    <select bind:value={deviceSelectionMode}>
      {#each deviceSelectionModes as mode}<option value={mode}>{modeLabel(mode)}</option>{/each}
    </select>
  </label>
</section>

<section class="panel section-gap">
  <h2>{labels.categoryOrderLabel}</h2>
  <p class="muted">{labels.categoryOrderHint}</p>
  <div class="list-panel">
    {#each orderedCategories as category, index}
      <div class="editor-row">
        <label class="checkbox-label">
          <input type="checkbox" checked={keyboardConfig.categoryOrder.includes(category.id)} on:change={() => toggleCategory(category.id)} />
          {category.name}
        </label>
        {#if keyboardConfig.categoryOrder.includes(category.id)}
          <button class="icon-button move-button" aria-label={labels.moveUp} disabled={index === 0} on:click={() => moveCategory(index, -1)}>▲</button>
          <button class="icon-button move-button" aria-label={labels.moveDown} disabled={index === keyboardConfig.categoryOrder.length - 1} on:click={() => moveCategory(index, 1)}>▼</button>
        {/if}
      </div>
    {/each}
  </div>
</section>

<section class="panel section-gap">
  <h2>{labels.keyboardLabel}</h2>
  <p class="muted">{labels.keyboardHint}</p>
  {#each keyboardConfig.keyboard as key, index}
    <div class="editor-row">
      <label>{labels.keyLabelLabel}<input bind:value={key.label} /></label>
      <label>{labels.keyIdLabel}<input bind:value={key.keyId} /></label>
      <label>{labels.keyColorLabel}
        <select bind:value={key.color}>
          {#each keyboardColors as color}<option value={color}>{labels[`color${color}`]}</option>{/each}
        </select>
      </label>
      <label>{labels.keyValueLabel}<input type="number" bind:value={key.value} /></label>
      <button class="icon-button move-button" aria-label={labels.moveUp} disabled={index === 0} on:click={() => moveKey(index, -1)}>▲</button>
      <button class="icon-button move-button" aria-label={labels.moveDown} disabled={index === keyboardConfig.keyboard.length - 1} on:click={() => moveKey(index, 1)}>▼</button>
      <button class="icon-button" aria-label={labels.removeValue} on:click={() => removeKey(index)}>🗑</button>
    </div>
  {/each}
  <div class="editor-actions">
    <button class="primary" on:click={addKey}>+ {labels.addKey}</button>
  </div>
</section>

<section class="panel section-gap">
  <h2>{labels.disabledKeysLabel}</h2>
  <p class="muted">{labels.disabledKeysHint}</p>
  {#each keyboardConfig.disabledKeyRules as rule, index}
    <div class="editor-row wrap">
      <label>{labels.ruleCategoryLabel}
        <select bind:value={rule.categoryId}>
          {#each categories as category}<option value={category.id}>{category.name}</option>{/each}
        </select>
      </label>
      <label>{labels.ruleValueLabel}
        <select bind:value={rule.valueId}>
          {#each categories.find((category) => category.id === rule.categoryId)?.values ?? [] as value}
            <option value={value.valueId}>{value.name}</option>
          {/each}
        </select>
      </label>
      <div class="checkbox-grid">
        <span class="muted">{labels.ruleDisabledKeysLabel}:</span>
        {#each keyboardConfig.keyboard as key}
          <label class="checkbox-label">
            <input type="checkbox" checked={rule.disabledKeyIds.includes(key.keyId)} on:change={() => toggleRuleDisabledKey(index, key.keyId)} />
            {key.label}
          </label>
        {/each}
      </div>
      <button class="icon-button" aria-label={labels.removeValue} on:click={() => removeDisabledKeyRule(index)}>🗑</button>
    </div>
  {/each}
  <button class="primary" on:click={addDisabledKeyRule} disabled={categories.length === 0}>+ {labels.addDisabledKeyRule}</button>
</section>

<section class="panel section-gap">
  <h2>{labels.scoringRulesLabel}</h2>
  <p class="muted">{labels.scoringRulesHint}</p>
  {#each scoringRules as rule, index}
    <div class="editor-row">
      <span class="muted">{index + 1}.</span>
      <label>{labels.ruleTypeLabel}
        <select bind:value={rule.type}>
          <option value="total">{labels.ruleTypeTotal}</option>
          <option value="countKey">{labels.ruleTypeCountKey}</option>
        </select>
      </label>
      {#if rule.type === 'countKey'}
        <label>{labels.ruleCountKeyLabel}
          <select bind:value={rule.keyId}>
            {#each keyboardConfig.keyboard as key}<option value={key.keyId}>{key.label}</option>{/each}
          </select>
        </label>
      {/if}
      <button class="icon-button move-button" aria-label={labels.moveUp} disabled={index === 0} on:click={() => moveScoringRule(index, -1)}>▲</button>
      <button class="icon-button move-button" aria-label={labels.moveDown} disabled={index === scoringRules.length - 1} on:click={() => moveScoringRule(index, 1)}>▼</button>
      <button class="icon-button" aria-label={labels.removeValue} disabled={scoringRules.length <= 1} on:click={() => removeScoringRule(index)}>🗑</button>
    </div>
  {/each}
  <button class="primary" on:click={addScoringRule}>+ {labels.addScoringRule}</button>
</section>

<section class="panel section-gap">
  {#if saveError}<p class="error">{saveError}</p>{/if}
  {#if saveMessage}<p class="success">{saveMessage}</p>{/if}
  <button class="primary" on:click={save}>{labels.save}</button>
</section>

<button class="danger-button" on:click={remove}>{labels.deleteMatch}</button>
{#if deleteError}<p class="error">{deleteError}</p>{/if}

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

  .editor-actions {
    display: flex;
    gap: 12px;
    margin-top: 16px;
  }

  .checkbox-grid {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 6px 16px;
  }

  .move-button:hover {
    color: var(--green);
  }
</style>
