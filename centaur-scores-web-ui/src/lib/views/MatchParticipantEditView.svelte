<script lang="ts">
  import type { ApiClient } from '../api'
  import MatchPageHeader from '../MatchPageHeader.svelte'
  import type { MatchHeaderHandlers } from '../pageActions'
  import { labelForError } from '../errors'
  import { parseMatchKeyboardConfig } from '../matchConfig'
  import { deriveLastName } from '../participantName'
  import type { Category, Language, Match, MatchParticipant } from '../types'

  export let api: ApiClient
  export let match: Match
  export let participant: MatchParticipant
  export let categories: Category[]
  export let language: Language
  export let labels: Record<string, string>
  export let headerHandlers: MatchHeaderHandlers
  export let onBack: () => void
  export let onSaved: () => void

  let fullName = participant.fullName
  let federationNumber = participant.federationNumber ?? ''
  let categoryValues: Record<string, string> = {}
  let saveError = ''

  $: matchCategories = matchCategoriesFor(match, categories)

  function matchCategoriesFor(currentMatch: Match, allCategories: Category[]): Category[] {
    return parseMatchKeyboardConfig(currentMatch.keyboardJson).categoryOrder
      .map((id) => allCategories.find((category) => category.id === id))
      .filter((category): category is Category => !!category)
  }

  // Seeds the form from the participant. This runs during component init, before `$:` statements have
  // computed anything, so it must not read the reactive `matchCategories` (it is still undefined here).
  for (const category of matchCategoriesFor(match, categories)) {
    const value = participant.categories[category.id]
    if (value !== undefined) categoryValues[category.id] = String(value)
  }

  async function save() {
    if (!fullName.trim()) return
    saveError = ''
    const payloadCategories: Record<string, number> = {}
    for (const category of matchCategories) {
      const value = categoryValues[category.id]
      if (value) payloadCategories[category.id] = Number(value)
    }
    try {
      await api.updateMatchParticipant(match.id, participant.id, { participantListMemberId: null, lastName: deriveLastName(fullName.trim()), fullName: fullName.trim(), federationNumber: federationNumber || null, categories: payloadCategories })
      onSaved()
    } catch (error) {
      saveError = labelForError(error, labels, 'participantSaveError')
    }
  }
</script>

<MatchPageHeader {api} {match} {language} {labels} handlers={headerHandlers} matchActions={false} title={labels.editParticipantDetails} subtitle={participant.fullName || participant.lastName} backLabel={participant.fullName || participant.lastName} {onBack} />
<section class="panel">
  <form class="inline-form" on:submit|preventDefault={save}>
    <label>{labels.fullNameLabel}<input bind:value={fullName} autocomplete="off" /></label>
    <label>{labels.federationNumberLabel}<input bind:value={federationNumber} /></label>
    {#each matchCategories as category}
      <label>{category.name}
        <select bind:value={categoryValues[category.id]}>
          <option value="">{labels.selectValue}</option>
          {#each [...category.values].sort((a, b) => a.valueId - b.valueId) as value}<option value={String(value.valueId)}>{value.name}</option>{/each}
        </select>
      </label>
    {/each}
    <button class="primary" type="submit" disabled={!fullName.trim()}>{labels.save}</button>
    <button class="secondary" type="button" on:click={onBack}>{labels.cancel}</button>
  </form>
  {#if saveError}<p class="error">{saveError}</p>{/if}
</section>
