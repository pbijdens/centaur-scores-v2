<script lang="ts">
  import PageHeader from '../PageHeader.svelte'
  import PersonalBestRenumberDialog from '../PersonalBestRenumberDialog.svelte'
  import type { ApiClient } from '../api'
  import { labelForError } from '../errors'
  import { deriveLastName } from '../participantName'
  import type { Category, Language, MemberPersonalBestEntries, ParticipantListMember } from '../types'

  export let api: ApiClient
  export let listId: string
  export let member: ParticipantListMember | null
  export let prefill: { fullName: string; federationNumber: string | null; categories: Record<string, number> } | null = null
  export let categories: Category[]
  export let labels: Record<string, string>
  export let language: Language
  export let onBack: () => void
  export let onSaved: (member: ParticipantListMember) => void
  export let onDeleted: () => void

  let fullName = ''
  let federationNumber = ''
  let isActive = true
  let categoryValues: Record<string, string> = {}
  let saveError = ''
  let deleteError = ''
  let generating = false
  // The personal-best renumber question (see save()): the request body waits here while the dialog is open.
  let renumberOpen = false
  let renumberEntries: MemberPersonalBestEntries | null = null
  let pendingBody: MemberBody | null = null

  type MemberBody = { lastName: string; fullName: string; federationNumber: string | null; categories: Record<string, number>; isActive: boolean }

  // `member` can still be loading when this view first mounts (e.g. navigating here from a
  // screen that never had the source list loaded) - a plain `let ... = member?.x ?? ...` only
  // reads the prop once at mount and would then show a permanently empty form once `member`
  // resolves. Re-prefill whenever the target we're editing actually changes, but only then, so
  // an unrelated background refresh of `member`/`categories` never clobbers in-progress edits.
  let prefilledFor: string | null = null
  $: {
    const targetKey = member?.id ?? 'new'
    if (targetKey !== prefilledFor) {
      prefilledFor = targetKey
      fullName = member?.fullName ?? prefill?.fullName ?? ''
      federationNumber = member?.federationNumber ?? prefill?.federationNumber ?? ''
      isActive = member?.isActive ?? true
      const values: Record<string, string> = {}
      for (const category of categories) {
        const existing = member?.categories?.[category.id] ?? prefill?.categories?.[category.id]
        values[category.id] = existing !== undefined ? String(existing) : ''
      }
      categoryValues = values
    }
  }

  $: allCategoriesFilled = categories.every((category) => categoryValues[category.id])

  function sortedValues(category: Category) {
    return [...category.values].sort((a, b) => a.valueId - b.valueId)
  }

  async function generateNumber() {
    saveError = ''
    generating = true
    try {
      federationNumber = await api.generateFederationNumber()
    } catch (error) {
      saveError = labelForError(error, labels, 'generateFederationNumberError')
    } finally {
      generating = false
    }
  }

  async function save() {
    saveError = ''
    const categoriesPayload: Record<string, number> = {}
    for (const category of categories) {
      const value = categoryValues[category.id]
      if (value) categoriesPayload[category.id] = Number(value)
    }
    const body: MemberBody = { lastName: deriveLastName(fullName.trim()), fullName, federationNumber: federationNumber.trim() || null, categories: categoriesPayload, isActive }

    // Changing an existing member's (non-empty) number: if the personal best lists still hold entries under the
    // old number, ask whether to move them along before saving. (API callers never get this - it is opt-in.)
    const oldNumber = member?.federationNumber?.trim() ?? ''
    if (member && oldNumber && oldNumber !== (body.federationNumber ?? '')) {
      try {
        const entries = await api.fetchMemberPersonalBests(listId, member.id)
        if (entries.totalCount > 0) {
          renumberEntries = entries
          pendingBody = body
          renumberOpen = true
          return
        }
      } catch (error) {
        saveError = labelForError(error, labels, 'personalBestRenumberCheckError')
        return
      }
    }
    await persist(body, false)
  }

  async function persist(body: MemberBody, updatePersonalBests: boolean) {
    try {
      const saved: ParticipantListMember = member ? await api.updateParticipantMember(listId, member.id, body, updatePersonalBests) : await api.addParticipantMember(listId, body)
      onSaved(saved)
    } catch (error) {
      saveError = labelForError(error, labels, 'memberSaveError')
    }
  }

  function answerRenumber(updatePersonalBests: boolean) {
    const body = pendingBody
    renumberOpen = false
    pendingBody = null
    if (body) persist(body, updatePersonalBests)
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

<PageHeader eyebrow={labels.participants} title={member ? (member.fullName || member.lastName) : labels.newMember} backLabel={labels.membersLabel} {onBack} actions={member ? [{ label: labels.deleteMember, danger: true, onClick: remove }] : []} menuLabel={labels.matchActions} />
{#if deleteError}<p class="error">{deleteError}</p>{/if}
<section class="panel">
  <form on:submit|preventDefault={save}>
    <label>{labels.fullNameLabel}<input bind:value={fullName} autocomplete="off" /></label>
    <label>{labels.federationNumberLabel}
      <span class="input-with-button">
        <input bind:value={federationNumber} />
        <button type="button" title={labels.generateFederationNumberHint} disabled={generating} on:click={generateNumber}>{labels.generateFederationNumber}</button>
      </span>
    </label>
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

<PersonalBestRenumberDialog bind:open={renumberOpen} entries={renumberEntries} newNumber={federationNumber} {labels} {language} onAccept={() => answerRenumber(true)} onDecline={() => answerRenumber(false)} />

<style>
  /* The generate button sits flush against the right edge of the federation number input. */
  .input-with-button {
    display: flex;
  }

  .input-with-button input {
    flex: 1;
  }

  .input-with-button button {
    border: 1px solid var(--line);
    border-left: none;
    background: var(--paper);
    color: var(--ink);
    padding: 0 14px;
    font-weight: 600;
  }

  .input-with-button button:disabled {
    cursor: progress;
    opacity: 0.6;
  }
</style>
