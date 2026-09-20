<script lang="ts">
  import { formatLocalDate } from '../date'
  import { parseMatchKeyboardConfig } from '../matchConfig'
  import { arrowsShotCount, endTotal, groupRunningTotal, hasGroups, runningTotal, scoreFor } from '../scorecard'
  import type { Category, Language, Match, MatchParticipant } from '../types'

  export let match: Match
  export let categories: Category[]
  export let tenantLogoUrl: string | null | undefined
  export let language: Language
  export let labels: Record<string, string>

  type Step = 'select' | 'sheet'
  let step: Step = 'select'

  $: participants = match.participants ?? []
  $: keyboardConfig = parseMatchKeyboardConfig(match.keyboardJson)
  $: matchCategories = keyboardConfig.categoryOrder.map((id) => categories.find((category) => category.id === id)).filter((category): category is Category => !!category)
  $: tenKey = keyboardConfig.keyboard.find((key) => key.label === '10')
  $: nineKey = keyboardConfig.keyboard.find((key) => key.label === '9')
  $: showTensNines = !!tenKey && !!nineKey
  $: groups = hasGroups(match)

  // Both selections default to "everything" - the organizer narrows down from there. Seeded via the
  // reactive blocks below (not here) - plain top-level `let` initializers run before any `$:` block on
  // first execution in Svelte's legacy reactivity, so `matchCategories`/`participants` would still be
  // undefined if read directly in this initializer.
  //
  // The two seedings are deliberately independent one-shot guards rather than a single combined one:
  // on a direct/deep-link navigation to this view, `participants` (embedded in the `match` prop) and
  // `matchCategories` (which also depends on the separately-loaded `categories` store) can become ready
  // on different reactive ticks. A combined "either is non-empty" guard would latch as soon as just one
  // of them arrived, permanently freezing the other's selection at empty.
  let selectedCategoryIds = new Set<string>()
  let selectedParticipantIds = new Set<string>()
  let freeText = ''
  let categorySelectionInitialized = false
  let participantSelectionInitialized = false
  $: if (!categorySelectionInitialized && matchCategories.length > 0) {
    selectedCategoryIds = new Set(matchCategories.map((category) => category.id))
    categorySelectionInitialized = true
  }
  $: if (!participantSelectionInitialized && participants.length > 0) {
    selectedParticipantIds = new Set(participants.map((participant) => participant.id))
    participantSelectionInitialized = true
  }

  $: allParticipantsSelected = participants.length > 0 && selectedParticipantIds.size === participants.length
  $: noParticipantsSelected = selectedParticipantIds.size === 0
  $: participantsIndeterminate = !allParticipantsSelected && !noParticipantsSelected

  function toggleCategory(categoryId: string) {
    const next = new Set(selectedCategoryIds)
    if (next.has(categoryId)) next.delete(categoryId)
    else next.add(categoryId)
    selectedCategoryIds = next
  }

  function toggleParticipant(participantId: string) {
    const next = new Set(selectedParticipantIds)
    if (next.has(participantId)) next.delete(participantId)
    else next.add(participantId)
    selectedParticipantIds = next
  }

  function toggleAllParticipants() {
    selectedParticipantIds = allParticipantsSelected ? new Set() : new Set(participants.map((participant) => participant.id))
  }

  $: printParticipants = [...participants]
    .filter((participant) => selectedParticipantIds.has(participant.id))
    .sort((a, b) => (a.fullName || a.lastName).localeCompare(b.fullName || b.lastName))

  $: printCategories = matchCategories.filter((category) => selectedCategoryIds.has(category.id)).sort((a, b) => a.name.localeCompare(b.name))

  function categoryValueFor(participant: MatchParticipant, category: Category): string | null {
    const valueId = participant.categories[category.id]
    if (valueId === undefined) return null
    return category.values.find((value) => value.valueId === valueId)?.name ?? null
  }

  function participantCategoryLines(participant: MatchParticipant): { name: string; value: string }[] {
    return printCategories
      .map((category) => ({ name: category.name, value: categoryValueFor(participant, category) }))
      .filter((line): line is { name: string; value: string } => !!line.value)
  }

  function endHasAnyScore(scores: MatchParticipant['scores'], end: number): boolean {
    return (scores ?? []).some((score) => score.end === end)
  }

  function keyLabelFor(scores: MatchParticipant['scores'], end: number, arrow: number): string {
    const entry = scoreFor(scores, end, arrow)
    if (!entry) return '-'
    return keyboardConfig.keyboard.find((key) => key.keyId === entry.keyId)?.label ?? entry.keyId
  }

  function countKeyInEnd(scores: MatchParticipant['scores'], end: number, keyId: string): number {
    return (scores ?? []).filter((score) => score.end === end && score.keyId === keyId).length
  }

  function formatSignedAt(signedAtUtc: string | null | undefined): string {
    if (!signedAtUtc) return ''
    try {
      return new Intl.DateTimeFormat(language === 'nl' ? 'nl-NL' : 'en-GB', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(signedAtUtc))
    } catch {
      return signedAtUtc
    }
  }
</script>

<svelte:head>
  <title>{labels.printScorecards} - {match.name}</title>
</svelte:head>

{#if step === 'select'}
  <button class="back-link" type="button" on:click={() => history.back()}>← {match.name}</button>
  <div class="page-intro">
    <div><p class="eyebrow">{labels.eyebrowPrintScorecards}</p><h1>{labels.printSelectionTitle}</h1></div>
  </div>

  <section class="panel">
    <h2>{labels.printCategoriesLabel}</h2>
    <div class="checkbox-grid">
      {#each matchCategories as category}
        <label class="checkbox-label">
          <input type="checkbox" checked={selectedCategoryIds.has(category.id)} on:change={() => toggleCategory(category.id)} />
          {category.name}
        </label>
      {/each}
      {#if matchCategories.length === 0}<p class="muted">-</p>{/if}
    </div>
  </section>

  <section class="panel section-gap">
    <label>{labels.printFreeTextLabel}<input type="text" placeholder={labels.printFreeTextPlaceholder} bind:value={freeText} /></label>
  </section>

  <section class="panel section-gap">
    <div class="participants-header">
      <h2>{labels.printParticipantsLabel}</h2>
      <label class="checkbox-label">
        <input type="checkbox" checked={allParticipantsSelected} indeterminate={participantsIndeterminate} on:change={toggleAllParticipants} />
        {labels.printSelectAllLabel}
      </label>
    </div>
    <div class="list-panel">
      {#each participants as participant (participant.id)}
        <label class="list-row checkbox-row">
          <input type="checkbox" checked={selectedParticipantIds.has(participant.id)} on:change={() => toggleParticipant(participant.id)} />
          <span class="participant-name">{participant.fullName || participant.lastName}</span>
          {#if participant.federationNumber}<span class="muted">{participant.federationNumber}</span>{/if}
        </label>
      {/each}
      {#if participants.length === 0}<p class="empty-state">{labels.emptyState}</p>{/if}
    </div>
  </section>

  {#if noParticipantsSelected}<p class="error section-gap">{labels.printNoParticipantsSelected}</p>{/if}

  <div class="sticky-actions">
    <button class="primary" type="button" disabled={noParticipantsSelected} on:click={() => (step = 'sheet')}>{labels.printPreviewButton}</button>
  </div>
{:else}
  <div class="print-toolbar">
    <button type="button" class="text-button" on:click={() => (step = 'select')}>← {labels.printBackToSelection}</button>
    <button type="button" class="primary" on:click={() => window.print()}>{labels.printButton}</button>
  </div>

  <div class="print-page">
    {#each printParticipants as participant (participant.id)}
      {@const lines = participantCategoryLines(participant)}
      <div class="print-card">
        <div class="card-header">
          {#if tenantLogoUrl}<img class="card-logo" src={tenantLogoUrl} alt="" />{/if}
          <div class="card-heading">
            <p class="card-match-name">{match.name}</p>
            <p class="card-match-date">{formatLocalDate(match.date, language)}</p>
            {#if freeText}<p class="card-free-text">{freeText}</p>{/if}
          </div>
        </div>
        <div class="card-participant-line">
          <span>{participant.fullName || participant.lastName}</span>
          {#if participant.federationNumber}<span>{labels.federationNumberLabel}: {participant.federationNumber}</span>{/if}
        </div>
        {#if lines.length > 0}
          <div class="card-categories">
            {#each lines as line}<span>{line.name}: {line.value}</span>{/each}
          </div>
        {/if}

        <table class="score-table">
          <thead>
            <tr>
              <th class="borderless"></th>
              {#each Array(match.arrowsPerEnd) as _, arrowIndex}<th>{arrowIndex + 1}</th>{/each}
              <th>{labels.printScoreHeader}</th>
              <th>{labels.printSubtotalHeader}</th>
              {#if showTensNines}
                <th>{labels.printTensHeader}</th>
                <th>{labels.printNinesHeader}</th>
              {/if}
            </tr>
          </thead>
          <tbody>
            {#each Array(match.ends) as _, index}
              {@const end = index + 1}
              {@const hasScore = endHasAnyScore(participant.scores, end)}
              <tr>
                <td class="borderless end-number">{end}</td>
                {#each Array(match.arrowsPerEnd) as _, arrowIndex}
                  <td class="score-cell">{hasScore ? keyLabelFor(participant.scores, end, arrowIndex + 1) : ''}</td>
                {/each}
                <td class="score-cell">{hasScore ? endTotal(participant.scores, end) : ''}</td>
                <td class="score-cell">{hasScore ? (groups ? groupRunningTotal(match, participant.scores, end) : runningTotal(participant.scores, end)) : ''}</td>
                {#if showTensNines && tenKey && nineKey}
                  <td class="score-cell">{hasScore ? countKeyInEnd(participant.scores, end, tenKey.keyId) : ''}</td>
                  <td class="score-cell">{hasScore ? countKeyInEnd(participant.scores, end, nineKey.keyId) : ''}</td>
                {/if}
              </tr>
            {/each}
          </tbody>
        </table>

        <div class="card-total-line">
          <span>{labels.printTotalLabel}:</span>
          {#if arrowsShotCount(participant.scores) > 1}
            <strong>{runningTotal(participant.scores, match.ends)}</strong>
          {:else}
            <span class="fill-line"></span>
          {/if}
        </div>

        {#if participant.signed}
          <div class="card-signatures signed">
            {#if participant.archerSignatureDataUrl || participant.markerSignatureDataUrl}
              {#if participant.archerSignatureDataUrl}<img class="signature-image" src={participant.archerSignatureDataUrl} alt={labels.archerSignatureLabel} />{/if}
              {#if participant.markerSignatureDataUrl}<img class="signature-image" src={participant.markerSignatureDataUrl} alt={labels.markerSignatureLabel} />{/if}
            {:else}
              <p class="signed-note">{labels.scorecardSignedBanner}</p>
            {/if}
            {#if participant.signedAtUtc}<p class="signed-at">{labels.printSignedOnLabel.replace('{date}', formatSignedAt(participant.signedAtUtc))}</p>{/if}
          </div>
        {:else}
          <div class="card-signatures">
            <div class="sign-line"><span class="sign-line-rule"></span><span class="sign-line-label">{labels.printSignHereArcher}</span></div>
            <div class="sign-line"><span class="sign-line-rule"></span><span class="sign-line-label">{labels.printSignHereMarker}</span></div>
          </div>
        {/if}
      </div>
    {/each}
  </div>
{/if}

<style>
  :global(body) {
    background: #fff;
  }

  .section-gap {
    margin-top: 24px;
  }

  .checkbox-grid {
    display: flex;
    flex-wrap: wrap;
    gap: 8px 20px;
  }

  .participants-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 16px;
  }

  .checkbox-row {
    display: flex;
    align-items: center;
    gap: 12px;
  }

  .participant-name {
    flex: 1;
    font-weight: 600;
  }

  .print-toolbar {
    display: flex;
    justify-content: space-between;
    gap: 12px;
    padding: 16px;
  }

  .print-page {
    display: flex;
    flex-direction: column;
    gap: 24px;
    padding: 16px;
    max-width: 900px;
    margin: 0 auto;
  }

  .print-card {
    border: 2px solid #000;
    padding: 16px;
    break-inside: avoid;
  }

  .card-header {
    display: flex;
    align-items: center;
    gap: 12px;
    border-bottom: 1px solid #000;
    padding-bottom: 8px;
    margin-bottom: 8px;
  }

  .card-logo {
    width: 48px;
    height: 48px;
    object-fit: contain;
  }

  .card-match-name {
    font: 700 18px 'Space Grotesk', sans-serif;
    margin: 0;
  }

  .card-match-date,
  .card-free-text {
    margin: 2px 0 0;
    font-size: 13px;
  }

  .card-participant-line {
    display: flex;
    justify-content: space-between;
    font-weight: 700;
    margin-bottom: 4px;
  }

  .card-categories {
    display: flex;
    flex-wrap: wrap;
    gap: 4px 16px;
    font-size: 13px;
    color: #333;
    margin-bottom: 8px;
  }

  .score-table {
    width: 100%;
    border-collapse: collapse;
    margin-top: 8px;
  }

  .score-table th {
    font-size: 11px;
    font-weight: 700;
    padding: 2px 4px;
    text-align: center;
  }

  .score-table .borderless {
    border: none;
  }

  .end-number {
    text-align: center;
    font-size: 12px;
    color: #555;
    width: 20px;
  }

  .score-cell {
    border: 1px solid rgba(0, 0, 0, 0.5);
    text-align: center;
    min-width: 26px;
    height: 24px;
    font-size: 13px;
  }

  .card-total-line {
    display: flex;
    align-items: center;
    gap: 8px;
    margin-top: 10px;
    font-weight: 700;
  }

  .fill-line {
    flex: 0 0 120px;
    border-bottom: 1px solid #000;
    height: 1em;
  }

  .card-signatures {
    display: flex;
    flex-wrap: wrap;
    gap: 16px;
    margin-top: 14px;
  }

  .card-signatures.signed {
    align-items: flex-start;
  }

  .signature-image {
    max-width: 260px;
    aspect-ratio: 5 / 1;
    object-fit: contain;
    border: 1px solid #000;
    background: #fff;
  }

  .signed-note {
    font-style: italic;
    margin: 0;
  }

  .signed-at {
    width: 100%;
    font-size: 11px;
    color: #555;
    margin: 4px 0 0;
  }

  .sign-line {
    flex: 1 1 220px;
    display: flex;
    flex-direction: column;
    gap: 2px;
  }

  .sign-line-rule {
    border-bottom: 1px solid #000;
    height: 28px;
  }

  .sign-line-label {
    font-size: 11px;
    color: #555;
  }

  @media print {
    .print-toolbar {
      display: none;
    }

    .print-page {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 24px;
      max-width: none;
      padding: 12px;
    }

    .print-card {
      break-inside: avoid;
    }
  }
</style>
