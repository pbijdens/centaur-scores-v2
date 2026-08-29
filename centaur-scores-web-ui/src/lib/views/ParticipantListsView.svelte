<script lang="ts">
  import type { ApiClient } from '../api'
  import { labelForError } from '../errors'
  import { navigateOnClick, participantListPath } from '../router'
  import type { ParticipantListSummary } from '../types'

  export let api: ApiClient
  export let lists: ParticipantListSummary[]
  export let labels: Record<string, string>
  export let onOpenList: (list: ParticipantListSummary) => void
  export let onChanged: () => void
  export let onBack: () => void

  let showAddForm = false
  let newListName = ''
  let createError = ''

  $: sortedLists = [...lists].sort((a, b) => (a.isActive === b.isActive ? a.name.localeCompare(b.name) : a.isActive ? -1 : 1))

  async function submitAdd() {
    if (!newListName.trim()) return
    createError = ''
    try {
      await api.createParticipantList({ name: newListName.trim(), isActive: true })
      newListName = ''
      showAddForm = false
      onChanged()
    } catch (error) {
      createError = labelForError(error, labels, 'listCreateError')
    }
  }
</script>

<button class="back-link" on:click={onBack}>← {labels.home}</button>
<div class="page-intro">
  <div><p class="eyebrow">{labels.eyebrowParticipantLists}</p><h1>{labels.participants}</h1><p class="muted">{labels.participantsHint}</p></div>
  <button class="primary" on:click={() => (showAddForm = !showAddForm)}>+ {labels.newParticipantList}</button>
</div>
{#if showAddForm}
  <form class="inline-form" on:submit|preventDefault={submitAdd}>
    <label>{labels.listNameLabel}<input bind:value={newListName} /></label>
    <button class="primary" type="submit" disabled={!newListName.trim()}>{labels.save}</button>
  </form>
  {#if createError}<p class="error">{createError}</p>{/if}
{/if}
<section class="list-panel">
  {#if sortedLists.length === 0}<p class="empty-state">{labels.emptyState}</p>{/if}
  {#each sortedLists as list}
    <a class="list-row" href={participantListPath(list.id)} on:click={(event) => navigateOnClick(event, () => onOpenList(list))}>
      <span class:live={list.isActive} class="list-indicator"></span>
      <span><strong>{list.name}</strong><small>{list.memberCount} {labels.membersLabel.toLowerCase()}</small></span>
      {#if !list.isActive}<span class="tag">{labels.statusInactive}</span>{/if}
      <span class="arrow">→</span>
    </a>
  {/each}
</section>
