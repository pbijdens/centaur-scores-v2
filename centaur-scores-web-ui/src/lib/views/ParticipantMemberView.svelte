<script lang="ts">
  import type { ApiClient } from '../api'
  import DropdownMenu from '../DropdownMenu.svelte'
  import { labelForError } from '../errors'
  import { deriveLastName } from '../participantName'
  import type { Category, ParticipantListMember } from '../types'

  export let api: ApiClient
  export let listId: string
  export let member: ParticipantListMember | null
  export let categories: Category[]
  export let labels: Record<string, string>
  export let onBack: () => void
  export let onSaved: () => void
  export let onDeleted: () => void

  let fullName = member?.fullName ?? ''
  let federationNumber = member?.federationNumber ?? ''
  let isActive = member?.isActive ?? true
  let categoryValues: Record<string, string> = {}
  for (const category of categories) {
    const existing = member?.categories?.[category.id]
    categoryValues[category.id] = existing !== undefined ? String(existing) : ''
  }
  let saveError = ''
  let deleteError = ''

  $: allCategoriesFilled = categories.every((category) => categoryValues[category.id])

  function sortedValues(category: Category) {
    return [...category.values].sort((a, b) => a.valueId - b.valueId)
  }

  async function save() {
    saveError = ''
    const categoriesPayload: Record<string, number> = {}
    for (const category of categories) {
      const value = categoryValues[category.id]
      if (value) categoriesPayload[category.id] = Number(value)
    }
    const body = { lastName: deriveLastName(fullName.trim()), fullName, federationNumber: federationNumber || null, categories: categoriesPayload, isActive }
    try {
      if (member) await api.updateParticipantMember(listId, member.id, body)
      else await api.addParticipantMember(listId, body)
      onSaved()
    } catch (error) {
      saveError = labelForError(error, labels, 'memberSaveError')
    }
  }

  async function remove() {
    if (!member) return
    deleteError = ''
    const message = labels.deleteMemberConfirm.replace('{name}', member.fullName || member.lastName)
    if (!confirm(message)) return
    try {
      await api.deleteParticipantMember(listId, member.id)
      onDeleted()
    } catch (error) {
      deleteError = labelForError(error, labels, 'memberSaveError')
    }
  }
</script>

<button class="back-link" on:click={onBack}>← {labels.membersLabel}</button>
<div class="page-intro">
  <div><p class="eyebrow">{labels.eyebrowParticipant}</p><h1>{member ? (member.fullName || member.lastName) : labels.newMember}</h1></div>
  {#if member}
    <div class="match-header-actions">
      <DropdownMenu ariaLabel={labels.matchActions} buttonClass="actions-trigger" align="right">
        <svelte:fragment slot="trigger">⋯</svelte:fragment>
        <button class="menu-item menu-item-danger" on:click={remove}>{labels.deleteMember}</button>
      </DropdownMenu>
    </div>
  {/if}
</div>
{#if deleteError}<p class="error">{deleteError}</p>{/if}
<section class="panel">
  <form on:submit|preventDefault={save}>
    <label>{labels.fullNameLabel}<input bind:value={fullName} autocomplete="off" /></label>
    <label>{labels.federationNumberLabel}<input bind:value={federationNumber} /></label>
    {#each categories as category}
      <label>{category.name}
        <select bind:value={categoryValues[category.id]}>
          <option value="" disabled>{labels.selectValue}</option>
          {#each sortedValues(category) as value}
            <option value={String(value.valueId)}>{value.name}</option>
          {/each}
        </select>
      </label>
    {/each}
    <label class="checkbox-label"><input type="checkbox" bind:checked={isActive} /> {labels.memberActiveLabel}</label>
    {#if saveError}<p class="error">{saveError}</p>{/if}
    <button class="primary" type="submit" disabled={!fullName.trim() || !allCategoriesFilled}>{labels.save}</button>
  </form>
</section>
