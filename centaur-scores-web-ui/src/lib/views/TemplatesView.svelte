<script lang="ts">
  import type { ApiClient } from '../api'
  import { labelForError } from '../errors'
  import { navigateOnClick, templatePath } from '../router'
  import { buildEmptyTemplateConfiguration } from '../templateConfig'
  import type { MatchTemplate, ParticipantListSummary } from '../types'

  export let api: ApiClient
  export let templates: MatchTemplate[]
  export let participantLists: ParticipantListSummary[]
  export let labels: Record<string, string>
  export let defaultNarrowcastScope: string
  export let onOpenTemplate: (template: MatchTemplate) => void
  export let onChanged: () => void
  export let onBack: () => void

  let showAddForm = false
  let newTemplateName = ''
  let createError = ''

  $: sortedTemplates = [...templates].sort((a, b) => a.name.localeCompare(b.name))

  function participantListName(id: string | null | undefined): string {
    return participantLists.find((list) => list.id === id)?.name ?? ''
  }

  function modeLabel(mode: string): string {
    if (mode === 'list') return labels.modeList
    if (mode === 'list-and-free') return labels.modeListAndFree
    return labels.modeRestricted
  }

  async function submitAdd() {
    if (!newTemplateName.trim()) return
    createError = ''
    try {
      const template = await api.createTemplate({ name: newTemplateName.trim(), allowFreeParticipants: true, deviceSelectionMode: 'list-and-free', configurationJson: JSON.stringify(buildEmptyTemplateConfiguration(defaultNarrowcastScope)) })
      newTemplateName = ''
      showAddForm = false
      onChanged()
      onOpenTemplate(template)
    } catch (error) {
      createError = labelForError(error, labels, 'templateCreateError')
    }
  }
</script>

<button class="back-link" on:click={onBack}>← {labels.home}</button>
<div class="page-intro">
  <div><p class="eyebrow">{labels.eyebrowTemplates}</p><h1>{labels.templates}</h1><p class="muted">{labels.templatesHint}</p></div>
  <button class="primary" on:click={() => (showAddForm = !showAddForm)}>+ {labels.newTemplate}</button>
</div>
{#if showAddForm}
  <form class="inline-form" on:submit|preventDefault={submitAdd}>
    <label>{labels.templateNameLabel}<input bind:value={newTemplateName} /></label>
    <button class="primary" type="submit" disabled={!newTemplateName.trim()}>{labels.save}</button>
  </form>
  {#if createError}<p class="error">{createError}</p>{/if}
{/if}
<section class="list-panel">
  {#if sortedTemplates.length === 0}<p class="empty-state">{labels.emptyState}</p>{/if}
  {#each sortedTemplates as template}
    <a class="list-row" href={templatePath(template.id)} on:click={(event) => navigateOnClick(event, () => onOpenTemplate(template))}>
      <span class="management-icon">◇</span>
      <span><strong>{template.name}</strong><small>{modeLabel(template.deviceSelectionMode)}{#if participantListName(template.participantListId)} · {participantListName(template.participantListId)}{/if}</small></span>
      <span class="arrow">→</span>
    </a>
  {/each}
</section>
