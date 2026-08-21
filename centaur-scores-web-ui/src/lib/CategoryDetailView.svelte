<script lang="ts">
  import type { ApiClient } from './api'
  import { labelForError } from './errors'
  import type { Category } from './types'

  export let api: ApiClient
  export let category: Category
  export let labels: Record<string, string>
  export let onChanged: () => void
  export let onDeleted: () => void
  export let onBack: () => void

  let showAddValueForm = false
  let newValueName = ''
  let valueError = ''
  let deleteError = ''
  let editingValueId: number | null = null
  let editValueName = ''

  $: sortedValues = [...category.values].sort((a, b) => a.name.localeCompare(b.name))

  async function submitAddValue() {
    if (!newValueName.trim()) return
    valueError = ''
    const nextValueId = category.values.reduce((max, value) => Math.max(max, value.valueId), 0) + 1
    try {
      await api.addCategoryValue(category.id, nextValueId, newValueName.trim())
      newValueName = ''
      showAddValueForm = false
      onChanged()
    } catch (error) {
      valueError = labelForError(error, labels, 'valueCreateError')
    }
  }

  function startEditValue(valueId: number, name: string) {
    editingValueId = valueId
    editValueName = name
    valueError = ''
  }

  async function submitEditValue() {
    if (editingValueId === null || !editValueName.trim()) return
    valueError = ''
    try {
      await api.updateCategoryValue(category.id, editingValueId, editValueName.trim())
      editingValueId = null
      onChanged()
    } catch (error) {
      valueError = labelForError(error, labels, 'valueCreateError')
    }
  }

  async function removeValue(valueId: number, name: string) {
    const message = labels.deleteValueConfirm.replace('{name}', name)
    if (!confirm(message)) return
    await api.deleteCategoryValue(category.id, valueId)
    onChanged()
  }

  async function remove() {
    deleteError = ''
    const message = labels.deleteCategoryConfirm.replace('{name}', category.name)
    if (!confirm(message)) return
    try {
      await api.deleteCategory(category.id)
      onDeleted()
    } catch (error) {
      deleteError = labelForError(error, labels, 'categoryCreateError')
    }
  }
</script>

<button class="back-link" on:click={onBack}>← {labels.categories}</button>
<div class="page-intro">
  <div><p class="eyebrow">{labels.eyebrowCategoryDetail}</p><h1>{category.name}</h1></div>
</div>
<section class="panel">
  <h2>{labels.categoryValues}</h2>
  <section class="list-panel">
    {#if sortedValues.length === 0}<p class="empty-state">{labels.emptyState}</p>{/if}
    {#each sortedValues as value}
      <div class="list-row">
        {#if editingValueId === value.valueId}
          <form class="inline-form" on:submit|preventDefault={submitEditValue}>
            <label>{labels.valueNameLabel}<input bind:value={editValueName} /></label>
            <button class="primary" type="submit" disabled={!editValueName.trim()}>{labels.save}</button>
          </form>
        {:else}
          <button class="value-name" on:click={() => startEditValue(value.valueId, value.name)}>
            <span class="management-icon">◇</span>
            <strong>{value.name}</strong>
          </button>
          <button class="icon-button" aria-label={labels.removeValue} on:click={() => removeValue(value.valueId, value.name)}>🗑</button>
        {/if}
      </div>
    {/each}
  </section>
  {#if valueError}<p class="error">{valueError}</p>{/if}
  {#if showAddValueForm}
    <form class="inline-form" on:submit|preventDefault={submitAddValue}>
      <label>{labels.valueNameLabel}<input bind:value={newValueName} /></label>
      <button class="primary" type="submit" disabled={!newValueName.trim()}>{labels.save}</button>
    </form>
  {:else}
    <button class="primary" on:click={() => (showAddValueForm = true)}>+ {labels.addValue}</button>
  {/if}
</section>
{#if category.isUsed}
  <p class="muted">{labels.categoryUsedHint}</p>
{:else}
  <button class="danger-button" on:click={remove}>{labels.deleteCategory}</button>
{/if}
{#if deleteError}<p class="error">{deleteError}</p>{/if}
