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

  // Bindable: App.svelte reads this to decide whether to show the normal site chrome (the 'select'
  // configuration screen) or render full-width/standalone (the 'sheet' step, meant for printing).
  type Step = 'select' | 'sheet'
  export let step: Step = 'select'

  type PrintLayout = 'standard' | 'fourColumn'
  let printLayout: PrintLayout = 'standard'

  function chunk<T>(items: T[], size: number): T[][] {
    const result: T[][] = []
    for (let i = 0; i < items.length; i += size) result.push(items.slice(i, i + size))
    return result
  }

  // Remembers the organizer's category selection, print layout and extra line per match, so re-opening
  // the print screen for the same match later starts from where they left off instead of every default
  // resetting to "everything selected, standard layout, no extra line". Deliberately excludes the
  // participant selection (who's present can differ each time) and lives in localStorage (not the API)
  // since it's a per-browser convenience, not match data other organizers or devices need to see.
  type PrintDraft = { categoryIds: string[]; printLayout: PrintLayout; freeText: string }

  function printDraftKey(matchId: string): string {
    return `centaur-print-config-${matchId}`
  }

  function loadPrintDraft(matchId: string): PrintDraft | null {
    try {
      const raw = localStorage.getItem(printDraftKey(matchId))
      if (!raw) return null
      const parsed = JSON.parse(raw)
      if (!parsed || typeof parsed !== 'object' || !Array.isArray(parsed.categoryIds)) return null
      return { categoryIds: parsed.categoryIds, printLayout: parsed.printLayout === 'fourColumn' ? 'fourColumn' : 'standard', freeText: typeof parsed.freeText === 'string' ? parsed.freeText : '' }
    } catch {
      return null
    }
  }

  function savePrintDraft(matchId: string, draft: PrintDraft) {
    try {
      localStorage.setItem(printDraftKey(matchId), JSON.stringify(draft))
    } catch {
      // localStorage unavailable (private browsing, quota) - the draft just won't persist.
    }
  }

  let loadedDraftForMatchId: string | null = null
  let pendingDraftCategoryIds: string[] | null = null
  $: if (loadedDraftForMatchId !== match.id) {
    loadedDraftForMatchId = match.id
    const draft = loadPrintDraft(match.id)
    printLayout = draft?.printLayout ?? 'standard'
    freeText = draft?.freeText ?? ''
    pendingDraftCategoryIds = draft?.categoryIds ?? null
    categorySelectionInitialized = false
  }

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
  // Ready once either the match's keyboard config references no categories at all (nothing to wait
  // for - matchCategories will stay empty regardless of whether `categories` has loaded) or the
  // tenant's category list has actually arrived. Using `matchCategories.length > 0` here instead would
  // mean a match with zero configured categories never initializes, since matchCategories can never
  // become non-empty for it - which also silently prevented the draft-saving reactive block below from
  // ever running for such matches.
  $: categoriesReady = keyboardConfig.categoryOrder.length === 0 || categories.length > 0
  $: if (!categorySelectionInitialized && categoriesReady) {
    const validIds = new Set(matchCategories.map((category) => category.id))
    const draftIds = (pendingDraftCategoryIds ?? []).filter((id) => validIds.has(id))
    selectedCategoryIds = new Set(draftIds.length > 0 ? draftIds : matchCategories.map((category) => category.id))
    categorySelectionInitialized = true
  }
  $: if (!participantSelectionInitialized && participants.length > 0) {
    selectedParticipantIds = new Set(participants.map((participant) => participant.id))
    participantSelectionInitialized = true
  }

  // Persist the draft once initial seeding (from a saved draft or the "everything selected" default) has
  // happened - guarding on categorySelectionInitialized avoids overwriting a saved draft with the
  // transient empty Set that selectedCategoryIds starts as before seeding runs.
  $: if (categorySelectionInitialized) {
    savePrintDraft(match.id, { categoryIds: Array.from(selectedCategoryIds), printLayout, freeText })
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

  $: selectedParticipants = participants.filter((participant) => selectedParticipantIds.has(participant.id))

  // Scorecards are grouped by device (in the order devices are defined on the match), members within a
  // device ordered by their deviceOrder; participants without a device are grouped last, alphabetically.
  // Between groups, null placeholders pad the preceding group up to a multiple of 4 so a device's cards
  // always start on a fresh page in both the standard (nth-of-type(4n) page-break) and four-column
  // (chunk-of-4-per-sheet) layouts - see printItems/printPages below.
  function buildPrintOrder(participantsToOrder: MatchParticipant[], devices: NonNullable<Match['devices']>): (MatchParticipant | null)[] {
    const byDevice = new Map<string, MatchParticipant[]>()
    const unassigned: MatchParticipant[] = []
    for (const participant of participantsToOrder) {
      if (participant.deviceId) {
        const list = byDevice.get(participant.deviceId) ?? []
        list.push(participant)
        byDevice.set(participant.deviceId, list)
      } else {
        unassigned.push(participant)
      }
    }

    const byDeviceOrder = (a: MatchParticipant, b: MatchParticipant) => (a.deviceOrder ?? 0) - (b.deviceOrder ?? 0)
    const groups: MatchParticipant[][] = []
    const knownDeviceIds = new Set(devices.map((device) => device.id))
    // match.devices isn't guaranteed to arrive in configured order - sort by sortOrder the same way
    // MatchDevicesView does, so scorecards follow the device order as set up for the match, not API order.
    const orderedDevices = [...devices].sort((a, b) => (a.sortOrder ?? Number.MAX_SAFE_INTEGER) - (b.sortOrder ?? Number.MAX_SAFE_INTEGER) || a.name.localeCompare(b.name))
    for (const device of orderedDevices) {
      const list = byDevice.get(device.id)
      if (list && list.length > 0) groups.push([...list].sort(byDeviceOrder))
    }
    // Defensive: a participant could reference a device id that's no longer in match.devices.
    for (const [deviceId, list] of byDevice) {
      if (!knownDeviceIds.has(deviceId)) groups.push([...list].sort(byDeviceOrder))
    }
    if (unassigned.length > 0) {
      groups.push([...unassigned].sort((a, b) => (a.fullName || a.lastName).localeCompare(b.fullName || b.lastName)))
    }

    const result: (MatchParticipant | null)[] = []
    groups.forEach((group, index) => {
      result.push(...group)
      const isLastGroup = index === groups.length - 1
      const remainder = result.length % 4
      if (!isLastGroup && remainder !== 0) {
        for (let i = 0; i < 4 - remainder; i++) result.push(null)
      }
    })
    return result
  }

  $: printItems = buildPrintOrder(selectedParticipants, match.devices ?? [])

  function deviceNameFor(participant: MatchParticipant): string {
    if (!participant.deviceId) return '-'
    const name = (match.devices ?? []).find((device) => device.id === participant.deviceId)?.name ?? '-'
    return participant.deviceLane ? `${name} ${participant.deviceLane}` : name
  }

  $: printCategories = matchCategories.filter((category) => selectedCategoryIds.has(category.id)).sort((a, b) => a.name.localeCompare(b.name))

  // In the four-column layout the score table is a flex-grow child that stretches to fill the card's
  // full page height (see .four-column-layout .score-table), but the browser's table row auto-stretch
  // algorithm ignores `max-height` set on individual cells/rows - only a max-height on the <table> itself
  // is respected as a clamp. This caps end-rows at ~1cm each regardless of how much space is available.
  $: scoreTableMaxHeightMm = match.ends * 10 + 6

  // A portrait four-column card is only ~140.5mm tall (see the orientation:portrait CSS below) and the
  // chrome around the table (header, participant line, categories, total line, two stacked signature
  // boxes) empirically takes up ~78mm of that regardless of how many ends the match has. Without this,
  // a match with enough ends makes the table's natural minimum height overflow the card, which pushes the
  // whole grid row to the next page (and, worse, can corrupt the page's layout entirely rather than
  // gracefully paginating - CSS Grid fragmentation in Chrome's print engine does not degrade gently).
  // Sizing each row down to fit guarantees 4-up always; scoreTableMaxHeightMm above still lets rows
  // stretch back up to 1cm when there's extra room (landscape, or a low end count).
  //
  // A `height` on a table cell is only a suggestion the browser can grow past to fit its content's line
  // box - it cannot shrink a row below the line-height its font-size needs. So for high end counts the
  // requested row height must also shrink the font-size (with line-height:1 on .score-cell, a row's floor
  // is exactly its font-size), or rows silently clamp back to their normal ~13px minimum and overflow.
  // No lower clamp on either value: forcing a legibility floor would silently reintroduce the overflow
  // this guards against for very high end counts.
  const FOUR_COLUMN_PORTRAIT_CARD_HEIGHT_MM = 140.5
  const FOUR_COLUMN_CHROME_HEIGHT_MM = 78
  const FOUR_COLUMN_TABLE_HEADER_ROW_MM = 2
  const MM_TO_PX = 96 / 25.4
  $: fourColumnRowHeightMm = Math.min(10, (FOUR_COLUMN_PORTRAIT_CARD_HEIGHT_MM - FOUR_COLUMN_CHROME_HEIGHT_MM - FOUR_COLUMN_TABLE_HEADER_ROW_MM) / match.ends)
  $: fourColumnFontSizePx = Math.min(11, fourColumnRowHeightMm * MM_TO_PX * 0.85)
  $: scoreCellStyle = printLayout === 'fourColumn' ? `height: ${fourColumnRowHeightMm}mm; font-size: ${fourColumnFontSizePx}px` : undefined
  // The end-number cell (row 1, 2, 3...) isn't a .score-cell, but it's a sibling in the same <tr> and
  // table rows share one height - left at its normal 8px font it silently became the tallest cell in the
  // row (with the browser's default <td> padding compounding it further), overriding every other cell's
  // shrunk height and reintroducing the exact overflow fourColumnRowHeightMm exists to prevent.
  $: endNumberStyle = printLayout === 'fourColumn' ? `height: ${fourColumnRowHeightMm}mm; font-size: ${Math.min(8, fourColumnFontSizePx)}px` : undefined

  $: printPages = chunk(printItems, 4)

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
  {#if printLayout === 'standard'}
    <style>
      @page {
        size: A4 landscape;
      }
    </style>
  {/if}
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
    <h2>{labels.printLayoutLabel}</h2>
    <div class="layout-options">
      <label class="checkbox-label">
        <input type="radio" name="printLayout" value="standard" checked={printLayout === 'standard'} on:change={() => (printLayout = 'standard')} />
        {labels.printLayoutStandardOption}
      </label>
      <label class="checkbox-label">
        <input type="radio" name="printLayout" value="fourColumn" checked={printLayout === 'fourColumn'} on:change={() => (printLayout = 'fourColumn')} />
        {labels.printLayoutFourColumnOption}
      </label>
    </div>
    {#if printLayout === 'fourColumn'}<p class="muted layout-hint">{labels.printLayoutFourColumnHint}</p>{/if}
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

  {#snippet printCard(participant: MatchParticipant)}
    {@const lines = participantCategoryLines(participant)}
    <div class="print-card">
      <div class="card-header">
        {#if tenantLogoUrl}<img class="card-logo" src={tenantLogoUrl} alt="" />{/if}
        <div class="card-heading">
          <p class="card-match-name">{match.name}</p>
          <div class="card-date-line">
            <span class="card-match-date">{formatLocalDate(match.date, language)}</span>
            <span class="card-device-name">{deviceNameFor(participant)}</span>
          </div>
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

      <table class="score-table" style="max-height: {scoreTableMaxHeightMm}mm">
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
              <td class="borderless end-number" style={endNumberStyle}>{end}</td>
              {#each Array(match.arrowsPerEnd) as _, arrowIndex}
                <td class="score-cell" style={scoreCellStyle}>{hasScore ? keyLabelFor(participant.scores, end, arrowIndex + 1) : ''}</td>
              {/each}
              <td class="score-cell" style={scoreCellStyle}>{hasScore ? endTotal(participant.scores, end) : ''}</td>
              <td class="score-cell" style={scoreCellStyle}>{hasScore ? (groups ? groupRunningTotal(match, participant.scores, end) : runningTotal(participant.scores, end)) : ''}</td>
              {#if showTensNines && tenKey && nineKey}
                <td class="score-cell" style={scoreCellStyle}>{hasScore ? countKeyInEnd(participant.scores, end, tenKey.keyId) : ''}</td>
                <td class="score-cell" style={scoreCellStyle}>{hasScore ? countKeyInEnd(participant.scores, end, nineKey.keyId) : ''}</td>
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

      <div class="card-signatures">
        <div class="signature-block">
          {#if participant.archerSignatureDataUrl}
            <img class="signature-image" src={participant.archerSignatureDataUrl} alt={labels.archerSignatureLabel} />
          {:else if participant.signed}
            <div class="signature-placeholder"><span>{labels.scorecardSignedBanner}</span></div>
          {:else if match.signatureMode === 'none'}
            <div class="signature-placeholder muted-placeholder"><span>{labels.printSignatureNotNeeded}</span></div>
          {:else}
            <div class="signature-placeholder"></div>
          {/if}
          <p class="signature-caption">{labels.archerSignatureLabel}</p>
        </div>
        <div class="signature-block">
          {#if participant.markerSignatureDataUrl}
            <img class="signature-image" src={participant.markerSignatureDataUrl} alt={labels.markerSignatureLabel} />
          {:else if participant.signed}
            <div class="signature-placeholder"><span>{labels.scorecardSignedBanner}</span></div>
          {:else if match.signatureMode === 'none'}
            <div class="signature-placeholder muted-placeholder"><span>{labels.printSignatureNotNeeded}</span></div>
          {:else}
            <div class="signature-placeholder"></div>
          {/if}
          <p class="signature-caption">{labels.markerSignatureLabel}</p>
        </div>
      </div>
      {#if participant.signedAtUtc}<p class="signed-at">{labels.printSignedOnLabel.replace('{date}', formatSignedAt(participant.signedAtUtc))}</p>{/if}
    </div>
  {/snippet}

  <div class="print-page" class:standard-layout={printLayout === 'standard'} class:four-column-layout={printLayout === 'fourColumn'}>
    {#if printLayout === 'standard'}
      {#each printItems as item, index (item ? item.id : `placeholder-${index}`)}
        {#if item}
          {@render printCard(item)}
        {:else}
          <div class="print-card print-card-placeholder"></div>
        {/if}
      {/each}
    {:else}
      {#each printPages as pageItems, pageIndex (pageIndex)}
        <div class="print-sheet">
          {#each chunk(pageItems, 2) as halfRowItems, halfIndex (halfIndex)}
            <div class="print-half-row">
              {#each halfRowItems as item, itemIndex (item ? item.id : `placeholder-${pageIndex}-${halfIndex}-${itemIndex}`)}
                {#if item}
                  {@render printCard(item)}
                {:else}
                  <div class="print-card print-card-placeholder"></div>
                {/if}
              {/each}
            </div>
          {/each}
        </div>
      {/each}
    {/if}
  </div>
{/if}

<style>
  :global(body) {
    background: #fff;
  }

  @page {
    margin: 6mm;
  }

  .section-gap {
    margin-top: 24px;
  }

  .checkbox-grid {
    display: flex;
    flex-wrap: wrap;
    gap: 8px 20px;
  }

  .layout-options {
    display: flex;
    flex-wrap: wrap;
    gap: 8px 20px;
  }

  .layout-hint {
    margin-top: 8px;
    font-size: 13px;
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
    border: 1.5px solid #000;
    padding: 8px;
    break-inside: avoid;
  }

  .print-card-placeholder {
    visibility: hidden;
  }

  .card-header {
    display: flex;
    align-items: center;
    gap: 6px;
    border-bottom: 1px solid #000;
    padding-bottom: 3px;
    margin-bottom: 3px;
  }

  .card-logo {
    width: 20px;
    height: 20px;
    object-fit: contain;
  }

  .card-match-name {
    font: 700 11px 'Space Grotesk', sans-serif;
    line-height: 1.1;
    margin: 0;
  }

  .card-date-line {
    display: flex;
    justify-content: space-between;
    align-items: baseline;
    gap: 6px;
    margin: 1px 0 0;
  }

  .card-match-date,
  .card-device-name,
  .card-free-text {
    font-size: 7px;
    line-height: 1.1;
  }

  .card-device-name {
    color: #555;
    text-align: right;
    white-space: nowrap;
  }

  .card-free-text {
    margin: 1px 0 0;
  }

  .card-participant-line {
    display: flex;
    justify-content: space-between;
    font-weight: 700;
    font-size: 10px;
    line-height: 1.1;
    margin-bottom: 1px;
  }

  .card-categories {
    display: flex;
    flex-wrap: wrap;
    gap: 1px 10px;
    font-size: 8px;
    line-height: 1.1;
    color: #333;
    margin-bottom: 2px;
  }

  .score-table {
    width: 100%;
    border-collapse: collapse;
    table-layout: fixed;
    margin-top: 2px;
  }

  .score-table th {
    font-size: 8px;
    font-weight: 700;
    padding: 1px 2px;
    text-align: center;
    overflow: hidden;
    white-space: nowrap;
    text-overflow: ellipsis;
  }

  .score-table .borderless {
    border: none;
  }

  .end-number {
    text-align: center;
    font-size: 8px;
    line-height: 1;
    padding: 0;
    color: #555;
  }

  .score-cell {
    border: 1px solid rgba(0, 0, 0, 0.5);
    text-align: center;
    height: 18px;
    padding: 0;
    font-size: 11px;
    line-height: 1;
    overflow: hidden;
  }

  .card-total-line {
    display: flex;
    align-items: center;
    gap: 6px;
    margin: 6px 0;
    font-size: 10px;
    font-weight: 700;
  }

  .fill-line {
    flex: 0 0 80px;
    border-bottom: 1px solid #000;
    height: 1em;
  }

  .card-signatures {
    display: flex;
    flex-wrap: wrap;
    align-items: flex-start;
    gap: 8px;
    margin-top: 8px;
  }

  .signature-block {
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    gap: 1px;
    flex: 1 1 100px;
    max-width: 150px;
  }

  .four-column-layout .card-signatures {
    flex-direction: column;
  }

  .four-column-layout .signature-block {
    flex: 0 0 auto;
    max-width: none;
    width: 100%;
  }

  .four-column-layout .signature-image,
  .four-column-layout .signature-placeholder {
    aspect-ratio: auto;
    height: 15mm;
  }

  .signature-image,
  .signature-placeholder {
    width: 100%;
    aspect-ratio: 5 / 1;
    box-sizing: border-box;
    border: 1px solid #000;
    background: #fff;
  }

  .signature-image {
    object-fit: contain;
  }

  .signature-placeholder {
    display: flex;
    align-items: center;
    justify-content: center;
    text-align: center;
    overflow: hidden;
    padding: 2px;
    font-size: 8px;
    color: #333;
  }

  .signature-placeholder.muted-placeholder {
    color: #777;
    font-style: italic;
  }

  .signature-caption {
    font-size: 8px;
    color: #555;
    margin: 0;
  }

  .signed-at {
    width: 100%;
    font-size: 9px;
    color: #555;
    margin: 2px 0 0;
  }

  @media print {
    .print-toolbar {
      display: none;
    }

    .print-page {
      display: block;
      max-width: none;
      padding: 0;
    }

    .standard-layout {
      font-size: 0;
    }

    .standard-layout .print-card {
      display: inline-block;
      vertical-align: top;
      width: calc(50% - 2mm);
      box-sizing: border-box;
      font-size: 10px;
      margin: 0 4mm 4mm 0;
      break-inside: avoid;
    }

    .standard-layout .print-card:nth-of-type(2n) {
      margin-right: 0;
    }

    .standard-layout .print-card:nth-of-type(4n) {
      break-after: page;
    }

    .four-column-layout .print-sheet {
      gap: 4mm;
    }

    .four-column-layout .print-sheet:not(:last-child) {
      break-after: page;
    }

    .four-column-layout .print-card {
      display: flex;
      flex-direction: column;
      height: 100%;
      box-sizing: border-box;
      margin: 0;
      /* Explicitly overrides the base .print-card's break-inside:avoid (unlike standard-layout, which
         keeps it). Each card here is already sized to fit exactly within its grid cell (see
         fourColumnRowHeightMm/scoreTableMaxHeightMm), so this should never need to break. But if that
         sizing is ever slightly off, break-inside:avoid on an overflowing grid item makes Chrome's print
         engine corrupt the whole page (rows disappearing/overlapping) instead of just letting the small
         excess spill - a far safer failure mode. */
      break-inside: auto;
    }

    .four-column-layout .score-table {
      flex: 1 1 auto;
      min-height: 0;
    }
  }

  /* Each .print-sheet holds up to 4 cards, split into two .print-half-row groups of 2. A single CSS Grid
     row is reliable in Chrome's print engine; a single grid with two 1fr rows is not - empirically, one
     row ends up claiming a disproportionate share of the grid's height (unrelated to its actual content),
     starving the other row until its cards overflow and corrupt the page (missing/overlapping content).
     So landscape's "4 across" is one single-row grid (.print-half-row is inert via display:contents,
     letting its cards join that grid directly), while portrait's "2x2" is built from two independent
     single-row grids stacked in normal flow instead of one two-row grid. */
  @media print and (orientation: landscape) {
    .four-column-layout .print-sheet {
      display: grid;
      grid-template-columns: repeat(4, 1fr);
      grid-template-rows: 1fr;
      height: 198mm;
    }

    .four-column-layout .print-half-row {
      display: contents;
    }
  }

  @media print and (orientation: portrait) {
    .four-column-layout .print-sheet {
      display: block;
      height: 285mm;
    }

    .four-column-layout .print-half-row {
      display: grid;
      grid-template-columns: repeat(2, 1fr);
      grid-template-rows: 1fr;
      gap: 4mm;
      height: 140.5mm;
    }

    .four-column-layout .print-half-row:first-child {
      margin-bottom: 4mm;
    }
  }
</style>
