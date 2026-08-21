<script lang="ts">
  import type { ApiClient } from './api'
  import { labelForError } from './errors'
  import type { Category, ParticipantList, ParticipantListMember } from './types'

  export let api: ApiClient
  export let list: ParticipantList
  export let categories: Category[]
  export let labels: Record<string, string>
  export let onOpenMember: (memberId: string) => void
  export let onAddMember: () => void
  export let onChanged: () => void
  export let onBack: () => void

  let name = list.name
  let isActive = list.isActive
  let saveMessage = ''
  let saveError = ''
  let filter = ''

  function categoryLabel(member: ParticipantListMember): string {
    return categories
      .map((category) => category.values.find((value) => value.valueId === member.categories[category.id])?.name)
      .filter((value): value is string => !!value)
      .join(' / ')
  }

  function displayLabel(member: ParticipantListMember): string {
    const name = member.fullName || member.lastName
    const values = categoryLabel(member)
    return values ? `${name} (${values})` : name
  }

  $: sortedMembers = [...list.members]
    .filter((member) => displayLabel(member).toLowerCase().includes(filter.toLowerCase()))
    .sort((a, b) => {
      const nameCompare = (a.fullName || a.lastName).localeCompare(b.fullName || b.lastName)
      return nameCompare !== 0 ? nameCompare : categoryLabel(a).localeCompare(categoryLabel(b))
    })

  async function save() {
    saveMessage = ''
    saveError = ''
    try {
      await api.updateParticipantList(list.id, { name, isActive })
      onChanged()
      saveMessage = labels.listSaved
    } catch (error) {
      saveError = labelForError(error, labels, 'listSaveError')
    }
  }
</script>

<button class="back-link" on:click={onBack}>← {labels.participants}</button>
<div class="page-intro">
  <div><p class="eyebrow">{labels.eyebrowParticipantListDetail}</p><h1>{list.name}</h1></div>
</div>
<section class="panel">
  <form on:submit|preventDefault={save}>
    <label>{labels.listNameLabel}<input bind:value={name} /></label>
    <label class="checkbox-label"><input type="checkbox" bind:checked={isActive} /> {labels.listActiveLabel}</label>
    {#if saveError}<p class="error">{saveError}</p>{/if}
    {#if saveMessage}<p class="success">{saveMessage}</p>{/if}
    <button class="primary" type="submit">{labels.save}</button>
  </form>
</section>
<div class="page-intro members-intro">
  <div><h2>{labels.membersLabel}</h2></div>
  <button class="primary" on:click={onAddMember}>+ {labels.newMember}</button>
</div>
<div class="toolbar">
  <input placeholder={labels.filterParticipantsPlaceholder} bind:value={filter} />
</div>
<section class="list-panel">
  {#if sortedMembers.length === 0}<p class="empty-state">{labels.emptyState}</p>{/if}
  {#each sortedMembers as member}
    <button class="list-row" on:click={() => onOpenMember(member.id)}>
      <span class="management-icon">◇</span>
      <span><strong>{member.fullName || member.lastName}</strong>{#if categoryLabel(member)}<span class="member-categories"> ({categoryLabel(member)})</span>{/if}</span>
      {#if !member.isActive}<span class="tag">{labels.statusInactive}</span>{/if}
      <span class="arrow">→</span>
    </button>
  {/each}
</section>

<style>
  .members-intro {
    margin-top: 32px;
  }

  .member-categories {
    color: var(--muted);
    font-weight: 400;
  }
</style>

