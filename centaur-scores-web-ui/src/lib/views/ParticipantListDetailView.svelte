<script lang="ts">
  import type { ApiClient } from '../api'
  import DropdownMenu from '../DropdownMenu.svelte'
  import { labelForError } from '../errors'
  import { memberCategoryLabel, memberDetailLabel, memberDisplayLabel } from '../participantName'
  import type { Category, Language, ParticipantList, ParticipantListMember } from '../types'

  export let api: ApiClient
  export let list: ParticipantList
  export let categories: Category[]
  export let language: Language
  export let canManage: boolean
  export let labels: Record<string, string>
  export let onOpenMember: (memberId: string) => void
  export let onAddMember: () => void
  export let onChanged: () => void
  export let onDeleted: () => void
  export let onBack: () => void

  let name = list.name
  let isActive = list.isActive
  let showSettings = false
  let saveMessage = ''
  let saveError = ''
  let deleteError = ''
  let filter = ''
  let exportError = ''
  let importError = ''
  let importMessage = ''
  let importWarnings: string[] = []
  let importing = false
  let fileInput: HTMLInputElement

  function displayLabel(member: ParticipantListMember): string {
    return memberDisplayLabel(categories, member)
  }

  $: sortedMembers = [...list.members]
    .filter((member) => displayLabel(member).toLowerCase().includes(filter.toLowerCase()))
    .sort((a, b) => {
      const nameCompare = (a.fullName || a.lastName).localeCompare(b.fullName || b.lastName)
      return nameCompare !== 0 ? nameCompare : memberCategoryLabel(categories, a).localeCompare(memberCategoryLabel(categories, b))
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

  async function exportList() {
    exportError = ''
    try {
      const { blob, filename } = await api.downloadParticipantListExport(list.id, language)
      const url = URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.download = filename
      link.click()
      URL.revokeObjectURL(url)
    } catch (error) {
      exportError = labelForError(error, labels, 'exportParticipantListError')
    }
  }

  async function importFile(event: Event) {
    const input = event.target as HTMLInputElement
    const file = input.files?.[0]
    input.value = ''
    if (!file) return
    importError = ''
    importMessage = ''
    importWarnings = []
    importing = true
    try {
      const result = await api.importParticipantList(list.id, file)
      importMessage = labels.importSummary.replace('{created}', String(result.created)).replace('{updated}', String(result.updated))
      importWarnings = result.warnings
      onChanged()
    } catch (error) {
      importError = labelForError(error, labels, 'importError')
    } finally {
      importing = false
    }
  }

  async function remove() {
    deleteError = ''
    if (!confirm(labels.deleteParticipantListConfirm.replace('{name}', list.name))) return
    try {
      await api.deleteParticipantList(list.id)
      onDeleted()
    } catch (error) {
      deleteError = labelForError(error, labels, 'listDeleteError')
    }
  }
</script>

<button class="back-link" on:click={onBack}>← {labels.participants}</button>
<div class="page-intro">
  <div><p class="eyebrow">{labels.eyebrowParticipantListDetail}</p><h1>{list.name}</h1></div>
  <div class="match-header-actions">
    <button class="primary" on:click={onAddMember}>+ {labels.newMember}</button>
    <DropdownMenu ariaLabel={labels.listActions} buttonClass="actions-trigger" align="right">
      <svelte:fragment slot="trigger">⋯</svelte:fragment>
      <button class="menu-item" class:active={showSettings} on:click={() => (showSettings = !showSettings)}>{labels.editListSettings}</button>
      {#if canManage}
        <button class="menu-item" on:click={exportList}>{labels.exportParticipantList}</button>
        <button class="menu-item" disabled={importing} on:click={() => fileInput.click()}>{labels.importParticipantList}</button>
        <hr class="menu-separator" />
        <button class="menu-item menu-item-danger" on:click={remove}>{labels.deleteParticipantList}</button>
      {/if}
    </DropdownMenu>
  </div>
</div>
<input type="file" accept=".xlsx" bind:this={fileInput} on:change={importFile} hidden />
{#if deleteError}<p class="error">{deleteError}</p>{/if}
{#if showSettings}
  <section class="panel">
    <form on:submit|preventDefault={save}>
      <label>{labels.listNameLabel}<input bind:value={name} /></label>
      <label class="checkbox-label"><input type="checkbox" bind:checked={isActive} /> {labels.listActiveLabel}</label>
      {#if saveError}<p class="error">{saveError}</p>{/if}
      {#if saveMessage}<p class="success">{saveMessage}</p>{/if}
      <button class="primary" type="submit">{labels.save}</button>
    </form>
  </section>
{/if}
<div class="page-intro members-intro">
  <div><h2>{labels.membersLabel}</h2></div>
</div>
{#if exportError}<p class="error">{exportError}</p>{/if}
{#if importError}<p class="error">{importError}</p>{/if}
{#if importMessage}
  <p class="success">{importMessage}</p>
  {#if importWarnings.length > 0}
    <ul class="import-warnings">
      {#each importWarnings as warning}<li>{warning}</li>{/each}
    </ul>
  {/if}
{/if}
<div class="toolbar">
  <input placeholder={labels.filterParticipantsPlaceholder} bind:value={filter} />
</div>
<section class="list-panel">
  {#if sortedMembers.length === 0}<p class="empty-state">{labels.emptyState}</p>{/if}
  {#each sortedMembers as member}
    <button class="list-row" on:click={() => onOpenMember(member.id)}>
      <span class="management-icon">◇</span>
      <span><strong>{member.fullName || member.lastName}</strong>{#if memberDetailLabel(categories, member)}<span class="member-categories"> ({memberDetailLabel(categories, member)})</span>{/if}</span>
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

  .import-warnings {
    color: var(--muted);
    font-size: 0.9em;
    margin: -8px 0 16px;
  }
</style>

