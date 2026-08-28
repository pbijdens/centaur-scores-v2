<script lang="ts">
  import type { ApiClient } from '../api'
  import DropdownMenu from '../DropdownMenu.svelte'
  import { labelForError } from '../errors'
  import RowActions from '../RowActions.svelte'
  import { keyboardColors, deviceSelectionModes, parseTemplateConfiguration } from '../templateConfig'
  import type { Category, MatchTemplate, ParticipantListSummary } from '../types'

  export let api: ApiClient
  export let template: MatchTemplate
  export let categories: Category[]
  export let participantLists: ParticipantListSummary[]
  export let labels: Record<string, string>
  export let onBack: () => void
  export let onSaved: () => void
  export let onDeleted: () => void

  let name = template.name
  let participantListId = template.participantListId ?? ''
  let allowFreeParticipants = template.allowFreeParticipants
  let deviceSelectionMode = template.deviceSelectionMode
  let config = parseTemplateConfiguration(template.configurationJson)
  let saveMessage = ''
  let saveError = ''
  let deleteError = ''
  let personalBestClassifier = template.personalBestClassifier ?? ''
  let personalBestClassifiers: string[] = []

  async function loadPersonalBestClassifiers() {
    try {
      personalBestClassifiers = await api.fetchPersonalBestClassifiers()
    } catch {
      personalBestClassifiers = []
    }
  }
  loadPersonalBestClassifiers()

  $: orderedCategories = [
    ...config.categoryOrder.map((id) => categories.find((category) => category.id === id)).filter((category): category is Category => !!category),
    ...categories.filter((category) => !config.categoryOrder.includes(category.id))
  ]

  function modeLabel(mode: string): string {
    if (mode === 'list') return labels.modeList
    if (mode === 'list-and-free') return labels.modeListAndFree
    return labels.modeRestricted
  }

  function toggleCategory(categoryId: string) {
    config.categoryOrder = config.categoryOrder.includes(categoryId)
      ? config.categoryOrder.filter((id) => id !== categoryId)
      : [...config.categoryOrder, categoryId]
  }

  function moveCategory(index: number, direction: -1 | 1) {
    const order = [...config.categoryOrder]
    const target = index + direction
    if (target < 0 || target >= order.length) return
    ;[order[index], order[target]] = [order[target], order[index]]
    config.categoryOrder = order
  }

  function addDevice() {
    config.deviceNames = [...config.deviceNames, '']
  }

  function renameDevice(index: number, name: string) {
    config.deviceNames = config.deviceNames.map((currentName, currentIndex) => currentIndex === index ? name : currentName)
  }

  function removeDevice(index: number) {
    config.deviceNames = config.deviceNames.filter((_, currentIndex) => currentIndex !== index)
  }

  function moveDevice(index: number, direction: -1 | 1) {
    const deviceNames = [...config.deviceNames]
    const target = index + direction
    if (target < 0 || target >= deviceNames.length) return
    ;[deviceNames[index], deviceNames[target]] = [deviceNames[target], deviceNames[index]]
    config.deviceNames = deviceNames
  }

  function addKey() {
    config.keyboard = [...config.keyboard, { keyId: '', label: '', color: 'Yellow', value: 0 }]
  }

  function removeKey(index: number) {
    config.keyboard = config.keyboard.filter((_, i) => i !== index)
  }

  function moveKey(index: number, direction: -1 | 1) {
    const keyboard = [...config.keyboard]
    const target = index + direction
    if (target < 0 || target >= keyboard.length) return
    ;[keyboard[index], keyboard[target]] = [keyboard[target], keyboard[index]]
    config.keyboard = keyboard
  }

  function addDisabledKeyRule() {
    const firstCategory = categories[0]
    if (!firstCategory) return
    config.disabledKeyRules = [...config.disabledKeyRules, { categoryId: firstCategory.id, valueId: firstCategory.values[0]?.valueId ?? 0, disabledKeyIds: [] }]
  }

  function removeDisabledKeyRule(index: number) {
    config.disabledKeyRules = config.disabledKeyRules.filter((_, i) => i !== index)
  }

  function toggleRuleDisabledKey(ruleIndex: number, keyId: string) {
    config.disabledKeyRules = config.disabledKeyRules.map((rule, index) => {
      if (index !== ruleIndex) return rule
      const disabledKeyIds = rule.disabledKeyIds.includes(keyId) ? rule.disabledKeyIds.filter((id) => id !== keyId) : [...rule.disabledKeyIds, keyId]
      return { ...rule, disabledKeyIds }
    })
  }

  function addScoringRule() {
    config.scoringRules = [...config.scoringRules, { type: 'total' }]
  }

  function removeScoringRule(index: number) {
    if (config.scoringRules.length <= 1) return
    config.scoringRules = config.scoringRules.filter((_, i) => i !== index)
  }

  function moveScoringRule(index: number, direction: -1 | 1) {
    const rules = [...config.scoringRules]
    const target = index + direction
    if (target < 0 || target >= rules.length) return
    ;[rules[index], rules[target]] = [rules[target], rules[index]]
    config.scoringRules = rules
  }

  function addLiveScope() {
    config.liveScopes = [...config.liveScopes, { scope: '', groupByCategoryIds: [], includeAverage: false, includeGroupScores: false, includeEqualizers: false, includePersonalBest: false }]
  }

  function removeLiveScope(index: number) {
    config.liveScopes = config.liveScopes.filter((_, i) => i !== index)
  }

  function toggleScopeCategory(scopeIndex: number, categoryId: string) {
    config.liveScopes = config.liveScopes.map((scope, index) => {
      if (index !== scopeIndex) return scope
      const groupByCategoryIds = scope.groupByCategoryIds.includes(categoryId)
        ? scope.groupByCategoryIds.filter((id) => id !== categoryId)
        : [...scope.groupByCategoryIds, categoryId]
      return { ...scope, groupByCategoryIds }
    })
  }

  async function save() {
    saveMessage = ''
    saveError = ''
    try {
      await api.updateTemplate(template.id, {
        name,
        participantListId: participantListId || null,
        allowFreeParticipants,
        deviceSelectionMode,
        configurationJson: JSON.stringify(config),
        personalBestClassifier: personalBestClassifier || null
      })
      saveMessage = labels.templateSaved
      onSaved()
    } catch (error) {
      saveError = labelForError(error, labels, 'templateSaveError')
    }
  }

  async function remove() {
    deleteError = ''
    const message = labels.deleteTemplateConfirm.replace('{name}', name)
    if (!confirm(message)) return
    try {
      await api.deleteTemplate(template.id)
      onDeleted()
    } catch (error) {
      deleteError = labelForError(error, labels, 'templateDeleteError')
    }
  }
</script>

<button class="back-link" on:click={onBack}>← {labels.templates}</button>
<div class="page-intro">
  <div><p class="eyebrow">{labels.eyebrowTemplateDetail}</p><h1>{template.name}</h1></div>
  <div class="match-header-actions">
    <DropdownMenu ariaLabel={labels.matchActions} buttonClass="actions-trigger" align="right">
      <svelte:fragment slot="trigger">⋯</svelte:fragment>
      <button class="menu-item menu-item-danger" on:click={remove}>{labels.deleteTemplate}</button>
    </DropdownMenu>
  </div>
</div>
{#if deleteError}<p class="error">{deleteError}</p>{/if}

<section class="panel">
  <label>{labels.templateNameLabel}<input bind:value={name} /></label>
  <label>{labels.endsLabel}<input type="number" min="1" bind:value={config.ends} /></label>
  <label>{labels.arrowsPerEndLabel}<input type="number" min="1" bind:value={config.arrowsPerEnd} /></label>
  <label>{labels.groupEndsLabel}<input type="number" min="1" bind:value={config.groupEnds} /></label>
  <label>{labels.participantListLabel}
    <select bind:value={participantListId}>
      <option value="">{labels.noParticipantList}</option>
      {#each participantLists as list}<option value={list.id}>{list.name}</option>{/each}
    </select>
  </label>
  <div class="editor-row">
  <label class="checkbox-label"><input type="checkbox" bind:checked={allowFreeParticipants} /> {labels.allowFreeParticipantsLabel}</label>
  </div>
  <label>{labels.deviceModeLabel}
    <select bind:value={deviceSelectionMode}>
      {#each deviceSelectionModes as mode}<option value={mode}>{modeLabel(mode)}</option>{/each}
    </select>
  </label>
  {#if personalBestClassifiers.length > 0}
    <label>{labels.personalBestClassifierLabel}
      <select bind:value={personalBestClassifier}>
        <option value="">{labels.noPersonalBestClassifier}</option>
        {#each personalBestClassifiers as classifier}<option value={classifier}>{classifier}</option>{/each}
      </select>
    </label>
  {/if}
</section>

<section class="panel section-gap">
  <h2>{labels.categoryOrderLabel}</h2>
  <p class="muted">{labels.categoryOrderHint}</p>
  <div class="list-panel">
    {#each orderedCategories as category, index (category.id)}
      <div class="editor-row">
        <label class="checkbox-label">
          <input type="checkbox" checked={config.categoryOrder.includes(category.id)} on:change={() => toggleCategory(category.id)} />
          {category.name}
        </label>
        {#if config.categoryOrder.includes(category.id)}
          <RowActions
            {labels}
            canMoveUp={index > 0}
            canMoveDown={index < config.categoryOrder.length - 1}
            onMoveUp={() => moveCategory(index, -1)}
            onMoveDown={() => moveCategory(index, 1)}
          />
        {/if}
      </div>
    {/each}
  </div>
</section>

<section class="panel section-gap">
  <h2>{labels.templateDevicesLabel}</h2>
  <p class="muted">{labels.templateDevicesHint}</p>
  {#each config.deviceNames as deviceName, index}
    <div class="editor-row device-row">
      <span class="muted device-position">{index + 1}.</span>
      <label>{labels.deviceNameLabel}<input value={deviceName} on:input={(event) => renameDevice(index, event.currentTarget.value)} /></label>
      <RowActions
        {labels}
        canMoveUp={index > 0}
        canMoveDown={index < config.deviceNames.length - 1}
        onMoveUp={() => moveDevice(index, -1)}
        onMoveDown={() => moveDevice(index, 1)}
        onDelete={() => removeDevice(index)}
      />
    </div>
  {/each}
  <button class="primary" on:click={addDevice}>+ {labels.addTemplateDevice}</button>
</section>

<section class="panel section-gap">
  <h2>{labels.keyboardLabel}</h2>
  <p class="muted">{labels.keyboardHint}</p>
  {#each config.keyboard as key, index}
    <div class="editor-row keyboard-row">
      <label>{labels.keyLabelLabel}<input bind:value={key.label} /></label>
      <label>{labels.keyIdLabel}<input bind:value={key.keyId} /></label>
      <label>{labels.keyColorLabel}
        <select bind:value={key.color}>
          {#each keyboardColors as color}<option value={color}>{labels[`color${color}`]}</option>{/each}
        </select>
      </label>
      <label>{labels.keyValueLabel}<input type="number" bind:value={key.value} /></label>
      <RowActions
        {labels}
        canMoveUp={index > 0}
        canMoveDown={index < config.keyboard.length - 1}
        onMoveUp={() => moveKey(index, -1)}
        onMoveDown={() => moveKey(index, 1)}
        onDelete={() => removeKey(index)}
      />
    </div>
  {/each}
  <div class="editor-actions">
    <button class="primary" on:click={addKey}>+ {labels.addKey}</button>
  </div>
</section>

<section class="panel section-gap">
  <h2>{labels.disabledKeysLabel}</h2>
  <p class="muted">{labels.disabledKeysHint}</p>
  {#each config.disabledKeyRules as rule, index}
    <div class="editor-row">
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
        {#each config.keyboard as key}
          <label class="checkbox-label">
            <input type="checkbox" checked={rule.disabledKeyIds.includes(key.keyId)} on:change={() => toggleRuleDisabledKey(index, key.keyId)} />
            {key.label}
          </label>
        {/each}
      </div>
      <button class="icon-button danger-icon-button row-delete-only" aria-label={labels.removeValue} on:click={() => removeDisabledKeyRule(index)}>🗑</button>
    </div>
  {/each}
  <button class="primary" on:click={addDisabledKeyRule} disabled={categories.length === 0}>+ {labels.addDisabledKeyRule}</button>
</section>

<section class="panel section-gap">
  <h2>{labels.scoringRulesLabel}</h2>
  <p class="muted">{labels.scoringRulesHint}</p>
  {#each config.scoringRules as rule, index}
    <div class="rule-block">
      <div class="rule-row">
        <span class="muted">{index + 1}.</span>
        <label>{labels.ruleTypeLabel}
          <select bind:value={rule.type}>
            <option value="total">{labels.ruleTypeTotal}</option>
            <option value="countKey">{labels.ruleTypeCountKey}</option>
          </select>
        </label>
        <RowActions
          {labels}
          canMoveUp={index > 0}
          canMoveDown={index < config.scoringRules.length - 1}
          onMoveUp={() => moveScoringRule(index, -1)}
          onMoveDown={() => moveScoringRule(index, 1)}
          onDelete={config.scoringRules.length > 1 ? () => removeScoringRule(index) : null}
        />
      </div>
      {#if rule.type === 'countKey'}
        <div class="rule-params-row">
          <label>{labels.ruleCountKeyLabel}
            <select bind:value={rule.keyId}>
              {#each config.keyboard as key}<option value={key.keyId}>{key.label}</option>{/each}
            </select>
          </label>
        </div>
      {/if}
    </div>
  {/each}
  <button class="primary" on:click={addScoringRule}>+ {labels.addScoringRule}</button>
</section>

<section class="panel section-gap">
  <h2>{labels.liveScopesLabel}</h2>
  <p class="muted">{labels.liveScopesHint}</p>
  {#each config.liveScopes as scope, index}
    <div class="editor-row">
      <label>{labels.scopeNameLabel}<input bind:value={scope.scope} /></label>
      <div class="checkbox-grid">
        <span class="muted">{labels.scopeGroupByLabel}:</span>
        {#each categories as category}
          <label class="checkbox-label">
            <input type="checkbox" checked={scope.groupByCategoryIds.includes(category.id)} on:change={() => toggleScopeCategory(index, category.id)} />
            {category.name}
          </label>
        {/each}
      </div>
      <div class="checkbox-grid">
        <label class="checkbox-label"><input type="checkbox" bind:checked={scope.includeAverage} /> {labels.scopeIncludeAverage}</label>
        <label class="checkbox-label"><input type="checkbox" bind:checked={scope.includeGroupScores} /> {labels.scopeIncludeGroupScores}</label>
        <label class="checkbox-label"><input type="checkbox" bind:checked={scope.includeEqualizers} /> {labels.scopeIncludeEqualizers}</label>
        <label class="checkbox-label"><input type="checkbox" bind:checked={scope.includePersonalBest} /> {labels.scopeIncludePersonalBest}</label>
      </div>
      <button class="icon-button danger-icon-button row-delete-only" aria-label={labels.removeValue} on:click={() => removeLiveScope(index)}>🗑</button>
    </div>
  {/each}
  <button class="primary" on:click={addLiveScope}>+ {labels.addLiveScope}</button>
</section>

<section class="panel section-gap">
  {#if saveError}<p class="error">{saveError}</p>{/if}
  {#if saveMessage}<p class="success">{saveMessage}</p>{/if}
  <button class="primary" on:click={save}>{labels.save}</button>
</section>

<style>
  .section-gap {
    margin-top: 32px;
  }

  .panel > label {
    min-width: 0;
  }

  .panel > label input,
  .panel > label select {
    width: 100%;
    max-width: 100%;
  }

  .editor-row {
    display: flex;
    align-items: end;
    flex-wrap: wrap;
    gap: 16px;
    padding: 14px 0;
    border-top: 1px solid var(--line);
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

  .device-row label {
    flex: 1;
  }

  .device-position {
    align-self: center;
    min-width: 2ch;
  }

  .keyboard-row {
    display: grid;
    grid-template-columns: minmax(0, 1.3fr) minmax(0, 1fr) minmax(0, 1.1fr) minmax(0, 0.8fr) auto;
  }

  .keyboard-row input,
  .keyboard-row select {
    width: 100%;
    max-width: 100%;
  }

  .rule-block {
    padding: 14px 0;
    border-top: 1px solid var(--line);
  }

  .rule-row {
    display: flex;
    align-items: end;
    flex-wrap: wrap;
    gap: 16px;
  }

  .rule-params-row {
    display: flex;
    margin-top: 12px;
  }

  .rule-row label,
  .rule-params-row label {
    flex: 1 1 220px;
    min-width: 0;
  }

  .rule-row select,
  .rule-params-row select {
    width: 100%;
    max-width: 100%;
  }

  .row-delete-only {
    display: grid;
    place-items: center;
    flex: 0 0 44px;
    width: 44px;
    height: 44px;
    margin-left: auto;
    padding: 0;
    border: 1px solid var(--line);
    background: var(--paper);
  }

  .row-delete-only:hover,
  .row-delete-only:focus-visible {
    color: #b84232;
    border-color: #e8755b;
    background: #fdeeea;
  }

  @media (max-width: 720px) {
    .editor-row {
      flex-direction: column;
      align-items: stretch;
    }

    .device-row label {
      flex-basis: auto;
    }

    .keyboard-row {
      grid-template-columns: 1fr;
    }

    .row-delete-only {
      align-self: flex-end;
    }
  }
</style>
