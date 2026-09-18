<script lang="ts">
  import type { ApiClient } from '../api'
  import { labelForError } from '../errors'
  import { parseMatchKeyboardConfig } from '../matchConfig'
  import { deriveLastName } from '../participantName'
  import ParticipantSelectionTable from '../ParticipantSelectionTable.svelte'
  import type { Category, Match, MatchParticipant, ParticipantList } from '../types'

  export let api: ApiClient
  export let match: Match
  export let participant: MatchParticipant
  export let categories: Category[]
  export let sourceList: ParticipantList | null
  export let labels: Record<string, string>
  export let onBack: () => void
  export let onSaved: () => void

  let showManualCard = false
  let manualFullName = ''
  let manualFederationNumber = ''
  let manualCategoryValues: Record<string, string> = {}
  let replaceError = ''
  let saving = false
  let selectedMemberId: string | null = participant.participantListMemberId ?? null

  $: keyboardConfig = parseMatchKeyboardConfig(match.keyboardJson)
  $: matchCategories = keyboardConfig.categoryOrder.map((id) => categories.find((category) => category.id === id)).filter((category): category is Category => !!category)
  $: assignedMemberIds = new Set(
    (match.participants ?? [])
      .filter((item) => item.id !== participant.id)
      .map((item) => item.participantListMemberId)
      .filter((id): id is string => !!id)
  )
  $: selectedIds = selectedMemberId ? new Set([selectedMemberId]) : new Set<string>()
  $: manualAllCategoriesFilled = matchCategories.every((category) => manualCategoryValues[category.id])
  $: canReplaceManually = manualFullName.trim() !== '' && manualAllCategoriesFilled
  $: heading = participant.participantListMemberId ? labels.changeParticipant : labels.linkParticipant

  function toggleSelected(memberId: string) {
    selectedMemberId = selectedMemberId === memberId ? null : memberId
  }

  async function saveSelection() {
    if (!selectedMemberId || saving) return
    const member = sourceList?.members.find((item) => item.id === selectedMemberId)
    if (!member) return
    saving = true
    try {
      await api.updateMatchParticipant(match.id, participant.id, { participantListMemberId: member.id, lastName: member.lastName, fullName: member.fullName, federationNumber: member.federationNumber, categories: member.categories })
      onSaved()
    } catch (error) {
      alert(labelForError(error, labels, 'participantSaveError'))
      saving = false
    }
  }

  async function submitManualReplace() {
    if (!canReplaceManually || saving) return
    replaceError = ''
    saving = true
    const payloadCategories: Record<string, number> = {}
    for (const category of matchCategories) {
      const value = manualCategoryValues[category.id]
      if (value) payloadCategories[category.id] = Number(value)
    }
    try {
      await api.updateMatchParticipant(match.id, participant.id, { participantListMemberId: null, lastName: deriveLastName(manualFullName.trim()), fullName: manualFullName.trim(), federationNumber: manualFederationNumber || null, categories: payloadCategories })
      onSaved()
    } catch (error) {
      replaceError = labelForError(error, labels, 'participantSaveError')
      saving = false
    }
  }
</script>

<div class="replace-view">
  <button class="back-link" on:click={onBack}>← {participant.fullName || participant.lastName}</button>
  <div class="page-intro">
    <div>
      <p class="eyebrow">{labels.eyebrowReplaceParticipant}</p>
      <h1>{heading}</h1>
      <p class="muted">{labels.replaceParticipantHint}</p>
    </div>
  </div>

  <section class="panel section-gap" class:fill-panel={!!sourceList}>
    {#if sourceList}
      {#if match.allowFreeParticipants}
        <div class="toolbar">
          <button type="button" class="text-button toolbar-button" on:click={() => (showManualCard = !showManualCard)}>+ {labels.addUnlistedParticipant}</button>
        </div>
      {/if}
      <ParticipantSelectionTable members={sourceList.members} categories={matchCategories} {assignedMemberIds} {selectedIds} {labels} onToggle={toggleSelected} fillHeight={true} />
    {/if}
    {#if !sourceList || showManualCard}
      {#if match.allowFreeParticipants}
        <form class="manual-card" on:submit|preventDefault={submitManualReplace}>
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
          <button class="primary large-submit" type="submit" disabled={!canReplaceManually || saving}>{labels.save}</button>
        </form>
      {:else}
        <p class="muted">{labels.participantListLockedHint}</p>
      {/if}
    {/if}
    {#if replaceError}<p class="error">{replaceError}</p>{/if}
  </section>

  {#if sourceList}
    <div class="sticky-actions">
      <button type="button" class="text-button" on:click={onBack}>{labels.cancel}</button>
      <button class="primary" type="button" disabled={!selectedMemberId || saving} on:click={saveSelection}>{labels.save}</button>
    </div>
  {/if}
</div>

<style>
  .replace-view {
    display: flex;
    flex-direction: column;
    height: calc(100dvh - 196px);
  }

  .replace-view .back-link {
    align-self: flex-start;
    text-align: left;
  }

  .fill-panel {
    display: flex;
    flex-direction: column;
    flex: 1 1 0;
    min-height: 400px;
  }

  .toolbar {
    display: flex;
    justify-content: flex-end;
    margin-bottom: 12px;
  }

  .toolbar-button {
    font-size: 16px;
  }

  .manual-card {
    margin-top: 16px;
  }

  .manual-card:not(:first-child) {
    border-top: 1px solid var(--line);
    padding-top: 16px;
  }
</style>
