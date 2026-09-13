<script lang="ts">
  import type { ApiClient } from '../api'
  import { labelForError } from '../errors'
  import { parseMatchKeyboardConfig } from '../matchConfig'
  import { deriveLastName } from '../participantName'
  import ParticipantSelectionTable from '../ParticipantSelectionTable.svelte'
  import type { Category, Match, ParticipantList } from '../types'

  export let api: ApiClient
  export let match: Match
  export let categories: Category[]
  export let sourceList: ParticipantList | null
  export let labels: Record<string, string>
  export let onBack: () => void
  export let onChanged: () => void

  let showManualCard = false
  let manualFullName = ''
  let manualFederationNumber = ''
  let manualCategoryValues: Record<string, string> = {}
  let addError = ''
  let selectedIds = new Set<string>()
  let applying = false
  let applyError = ''

  $: participants = match.participants ?? []
  $: keyboardConfig = parseMatchKeyboardConfig(match.keyboardJson)
  $: matchCategories = keyboardConfig.categoryOrder.map((id) => categories.find((category) => category.id === id)).filter((category): category is Category => !!category)
  $: assignedMemberIds = new Set(participants.map((participant) => participant.participantListMemberId).filter((id): id is string => !!id))
  $: manualAllCategoriesFilled = matchCategories.every((category) => manualCategoryValues[category.id])
  $: canAddManually = manualFullName.trim() !== '' && manualAllCategoriesFilled

  function toggleSelected(memberId: string) {
    const next = new Set(selectedIds)
    if (next.has(memberId)) next.delete(memberId)
    else next.add(memberId)
    selectedIds = next
  }

  async function apply() {
    if (selectedIds.size === 0) return
    applyError = ''
    applying = true
    try {
      for (const memberId of selectedIds) {
        const member = sourceList?.members.find((item) => item.id === memberId)
        if (!member) continue
        await api.addMatchParticipant(match.id, { participantListMemberId: member.id, lastName: member.lastName, fullName: member.fullName, federationNumber: member.federationNumber, categories: member.categories })
      }
      selectedIds = new Set()
      onChanged()
    } catch (error) {
      applyError = labelForError(error, labels, 'addParticipantError')
    } finally {
      applying = false
    }
  }

  function resetManualForm() {
    manualFullName = ''
    manualFederationNumber = ''
    manualCategoryValues = {}
    showManualCard = false
  }

  async function submitAddManually() {
    if (!canAddManually) return
    addError = ''
    const categoryValues: Record<string, number> = {}
    for (const category of matchCategories) {
      const value = manualCategoryValues[category.id]
      if (value) categoryValues[category.id] = Number(value)
    }
    try {
      await api.addMatchParticipant(match.id, { participantListMemberId: null, lastName: deriveLastName(manualFullName.trim()), fullName: manualFullName.trim(), federationNumber: manualFederationNumber || null, categories: categoryValues })
      resetManualForm()
      onChanged()
    } catch (error) {
      addError = labelForError(error, labels, 'addParticipantError')
    }
  }
</script>

<button class="back-link" on:click={onBack}>← {match.name}</button>
<div class="page-intro">
  <div>
    <p class="eyebrow">{labels.eyebrowAddParticipants}</p>
    <h1>{labels.addParticipant}</h1>
    {#if sourceList}<p class="muted">{sourceList.name}</p>{/if}
  </div>
</div>

<section class="panel section-gap">
  {#if sourceList}
    <ParticipantSelectionTable members={sourceList.members} categories={matchCategories} {assignedMemberIds} {selectedIds} {labels} onToggle={toggleSelected} />
    {#if match.allowFreeParticipants}
      <button type="button" class="text-button add-unlisted-button" on:click={() => (showManualCard = !showManualCard)}>+ {labels.addUnlistedParticipant}</button>
    {/if}
  {/if}
  {#if !sourceList || showManualCard}
    {#if match.allowFreeParticipants}
      <form class="manual-card" on:submit|preventDefault={submitAddManually}>
        <label>{labels.fullNameLabel}<input bind:value={manualFullName} autocomplete="off" /></label>
        <label>{labels.federationNumberLabel}<input bind:value={manualFederationNumber} /></label>
        {#each matchCategories as category}
          <label>{category.name}
            <select bind:value={manualCategoryValues[category.id]}>
              <option value="">{labels.selectValue}</option>
              {#each [...category.values].sort((a, b) => a.valueId - b.valueId) as value}<option value={String(value.valueId)}>{value.name}</option>{/each}
            </select>
          </label>
        {/each}
        <button class="primary large-submit" type="submit" disabled={!canAddManually}>+ {labels.addThisParticipant}</button>
      </form>
    {:else}
      <p class="muted">{labels.participantListLockedHint}</p>
    {/if}
  {/if}
  {#if addError}<p class="error">{addError}</p>{/if}
  {#if applyError}<p class="error">{applyError}</p>{/if}
</section>

<div class="sticky-actions">
  <button type="button" class="text-button" on:click={onBack}>{labels.cancel}</button>
  {#if sourceList}
    <button class="primary" type="button" disabled={selectedIds.size === 0 || applying} on:click={apply}>{labels.applyLabel}</button>
  {/if}
</div>

<style>
  .add-unlisted-button {
    margin-top: 12px;
  }

  .manual-card {
    margin-top: 16px;
  }

  .manual-card:not(:first-child) {
    border-top: 1px solid var(--line);
    padding-top: 16px;
  }
</style>
