<script lang="ts">
  import PageHeader from '../PageHeader.svelte'
  import type { ApiClient } from '../api'
  import { labelForError } from '../errors'
  import { categoryPath, navigateOnClick } from '../router'
  import type { Category } from '../types'

  export let api: ApiClient
  export let categories: Category[]
  export let labels: Record<string, string>
  export let onOpenCategory: (category: Category) => void
  export let onChanged: () => void
  export let onBack: () => void

  let showAddForm = false
  let newCategoryName = ''
  let createError = ''

  async function submitAdd() {
    if (!newCategoryName.trim()) return
    createError = ''
    try {
      await api.createCategory(newCategoryName.trim())
      newCategoryName = ''
      showAddForm = false
      onChanged()
    } catch (error) {
      createError = labelForError(error, labels, 'categoryCreateError')
    }
  }
</script>

<PageHeader eyebrow={labels.categories} title={labels.categories} subtitle={labels.categoriesHint} backLabel={labels.home} {onBack} actions={[{ label: `+ ${labels.newCategory}`, header: 'primary', onClick: () => (showAddForm = !showAddForm) }]} />
{#if showAddForm}
  <form class="inline-form" on:submit|preventDefault={submitAdd}>
    <label>{labels.categoryNameLabel}<input bind:value={newCategoryName} /></label>
    <button class="primary" type="submit" disabled={!newCategoryName.trim()}>{labels.save}</button>
  </form>
  {#if createError}<p class="error">{createError}</p>{/if}
{/if}
<section class="list-panel">
  {#if categories.length === 0}<p class="empty-state">{labels.emptyState}</p>{/if}
  {#each categories as category}
    <a class="list-row" href={categoryPath(category.id)} on:click={(event) => navigateOnClick(event, () => onOpenCategory(category))}>
      <span class="management-icon">◇</span>
      <span><strong>{category.name}</strong><small>{category.values.length} {labels.categoryValues.toLowerCase()}</small></span>
      <span class="arrow">→</span>
    </a>
  {/each}
</section>
