<script lang="ts">
  import type { ApiClient } from './api'
  import { labelForError } from './errors'
  import { defaultMatchKeyboardJson, defaultMatchScoringRulesJson } from './matchConfig'
  import { parseTemplateConfiguration } from './templateConfig'
  import type { Language, Match, MatchTemplate } from './types'

  export let api: ApiClient
  export let matches: Match[]
  export let templates: MatchTemplate[]
  export let language: Language
  export let labels: Record<string, string>
  export let onOpenMatch: (match: Match) => void
  export let onChanged: () => void

  let filter = ''
  let filterMode: 'all' | 'future' | 'past' = 'all'
  let showAddForm = false
  let newMatchName = ''
  let newMatchDate = ''
  let newTemplateId = ''
  let createError = ''

  function localToday(): string {
    const now = new Date()
    return new Date(now.getTime() - now.getTimezoneOffset() * 60000).toISOString().slice(0, 10)
  }

  $: today = localToday()
  $: named = matches.filter((match) => match.name.toLowerCase().includes(filter.toLowerCase()))
  $: openMatches = named.filter((match) => match.isOpen)
  $: upcomingMatches = named.filter((match) => !match.isOpen && match.date >= today).sort((a, b) => a.date.localeCompare(b.date))
  $: pastMatches = named.filter((match) => !match.isOpen && match.date < today).sort((a, b) => b.date.localeCompare(a.date))
  $: showUpcoming = filterMode !== 'past'
  $: showPast = filterMode !== 'future'

  function formatDate(value: string): string {
    return new Intl.DateTimeFormat(language === 'nl' ? 'nl-NL' : 'en-GB', { day: 'numeric', month: 'short', year: 'numeric' }).format(new Date(`${value.slice(0, 10)}T12:00:00`))
  }

  async function submitAdd() {
    if (!newMatchName.trim() || !newMatchDate) return
    createError = ''
    const template = templates.find((item) => item.id === newTemplateId)
    let keyboardJson = defaultMatchKeyboardJson
    let scoringRulesJson = defaultMatchScoringRulesJson
    let participantListId: string | null = null
    let allowFreeParticipants = false
    let deviceSelectionMode = 'restricted'
    let ends = 10
    let arrowsPerEnd = 3
    let groupEnds: number | null = null
    if (template) {
      const config = parseTemplateConfiguration(template.configurationJson)
      keyboardJson = JSON.stringify({ categoryOrder: config.categoryOrder, keyboard: config.keyboard, disabledKeyRules: config.disabledKeyRules })
      scoringRulesJson = JSON.stringify(config.scoringRules)
      participantListId = template.participantListId ?? null
      allowFreeParticipants = template.allowFreeParticipants
      deviceSelectionMode = template.deviceSelectionMode
      ends = config.ends
      arrowsPerEnd = config.arrowsPerEnd
      groupEnds = config.groupEnds
    }
    try {
      const match = await api.createMatch({
        name: newMatchName.trim(),
        date: newMatchDate,
        isOpen: false,
        participantListId,
        deviceSelectionMode,
        ends,
        arrowsPerEnd,
        groupEnds,
        allowFreeParticipants,
        keyboardJson,
        scoringRulesJson
      })
      if (template) {
        const config = parseTemplateConfiguration(template.configurationJson)
        for (const scope of config.liveScopes) await api.addLiveScope(match.id, scope)
        for (const deviceName of config.deviceNames.map((name) => name.trim()).filter(Boolean)) {
          await api.addDevice(match.id, { name: deviceName })
        }
      }
      newMatchName = ''
      newMatchDate = ''
      newTemplateId = ''
      showAddForm = false
      onChanged()
      onOpenMatch(match)
    } catch (error) {
      createError = labelForError(error, labels, 'matchCreateError')
    }
  }
</script>

<div class="page-intro">
  <div><p class="eyebrow">{labels.eyebrowTenantData}</p><h1>{labels.matches}</h1><p class="muted">{labels.matchesDescription}</p></div>
  <button class="primary" on:click={() => (showAddForm = !showAddForm)}>+ {labels.newMatch}</button>
</div>
{#if showAddForm}
  <form class="inline-form" on:submit|preventDefault={submitAdd}>
    <label>{labels.matchNameLabel}<input bind:value={newMatchName} /></label>
    <label>{labels.matchDateLabel}<input type="date" bind:value={newMatchDate} /></label>
    <label>{labels.matchTemplateLabel}
      <select bind:value={newTemplateId}>
        <option value="">{labels.noTemplate}</option>
        {#each templates as template}<option value={template.id}>{template.name}</option>{/each}
      </select>
    </label>
    <button class="primary" type="submit" disabled={!newMatchName.trim() || !newMatchDate}>{labels.save}</button>
  </form>
  {#if createError}<p class="error">{createError}</p>{/if}
{/if}
<div class="toolbar">
  <input placeholder={labels.filterMatchesPlaceholder} bind:value={filter} />
  <select bind:value={filterMode}>
    <option value="all">{labels.allMatches}</option>
    <option value="future">{labels.futureMatches}</option>
    <option value="past">{labels.pastMatches}</option>
  </select>
</div>
<section class="list-panel">
  {#if openMatches.length === 0 && upcomingMatches.length === 0 && pastMatches.length === 0}<p class="empty-state">{labels.emptyState}</p>{/if}
  {#each openMatches as match}
    <button class="list-row" on:click={() => onOpenMatch(match)}>
      <span class:live={match.isOpen} class="list-indicator"></span>
      <span><strong>{match.name}</strong><small>{formatDate(match.date)} · {match.participantCount ?? 0} participants</small></span>
      <span class="tag">{labels.statusOpen}</span>
      <span class="arrow">→</span>
    </button>
  {/each}
  {#if showUpcoming}
    {#each upcomingMatches as match}
      <button class="list-row" on:click={() => onOpenMatch(match)}>
        <span class="list-indicator"></span>
        <span><strong>{match.name}</strong><small>{formatDate(match.date)} · {match.participantCount ?? 0} participants</small></span>
        <span class="tag">{labels.statusPlanned}</span>
        <span class="arrow">→</span>
      </button>
    {/each}
  {/if}
  {#if showPast}
    {#each pastMatches as match}
      <button class="list-row past" on:click={() => onOpenMatch(match)}>
        <span class="list-indicator"></span>
        <span><strong>{match.name}</strong><small>{formatDate(match.date)} · {match.participantCount ?? 0} participants</small></span>
        <span class="tag">{labels.statusPast}</span>
        <span class="arrow">→</span>
      </button>
    {/each}
  {/if}
</section>

<style>
  .list-row .tag {
    flex: 0 0 72px;
    text-align: right;
  }

  .list-row .arrow {
    margin-left: 0;
  }

  .list-row.past {
    opacity: .65;
  }
</style>

