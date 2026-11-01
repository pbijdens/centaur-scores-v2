<script lang="ts">
  import { tick } from 'svelte'
  import { formatLocalDate } from '../date'
  import { parseMatchKeyboardConfig } from '../matchConfig'
  import { endTotal, groupEndRanges, groupRunningTotal, hasGroups, runningTotal, scoreFor } from '../scorecard'
  import type { ApiClient } from '../api'
  import MatchPageHeader from '../MatchPageHeader.svelte'
  import { printAction, type MatchHeaderHandlers } from '../pageActions'
  import PaperSizeSelect from '../PaperSizeSelect.svelte'
  import { loadPaperSize, printableArea, savePaperSize, type PaperSize, type PrintOrientation } from '../paperSize'
  import PrintDialog from '../PrintDialog.svelte'
  import { loadPrintOptions, printOptionsKey, savePrintOptions } from '../printOptions'
  import type { Category, Language, Match, MatchParticipant, MatchParticipantSignature } from '../types'

  export let api: ApiClient
  export let match: Match
  export let categories: Category[]
  export let tenantLogoUrl: string | null | undefined
  export let language: Language
  export let labels: Record<string, string>
  export let headerHandlers: MatchHeaderHandlers
  export let onBack: () => void

  function chunk<T>(items: T[], size: number): T[][] {
    const result: T[][] = []
    for (let i = 0; i < items.length; i += size) result.push(items.slice(i, i + size))
    return result
  }

  // The print dialog's answers, remembered per match (see printOptions.ts). Categories and archers are
  // stored as what was *left out*, so an archer or category added to the match later is included by
  // default, and the selection never depends on the order in which the match and the tenant's categories
  // finish loading.
  // blackOnly prints all text and lines in pure black (only the logo keeps its colours), for low-quality printers.
  type ScorecardPrintOptions = { excludedCategoryIds: string[]; excludedParticipantIds: string[]; freeText: string; splitByGroup: boolean; blackOnly: boolean }

  function parseOptions(stored: Record<string, unknown> | null): ScorecardPrintOptions {
    const ids = (value: unknown) => (Array.isArray(value) ? value.filter((id): id is string => typeof id === 'string') : [])
    return {
      excludedCategoryIds: ids(stored?.excludedCategoryIds),
      excludedParticipantIds: ids(stored?.excludedParticipantIds),
      freeText: typeof stored?.freeText === 'string' ? stored.freeText : '',
      splitByGroup: stored?.splitByGroup === true,
      blackOnly: stored?.blackOnly === true
    }
  }

  $: optionsKey = printOptionsKey('scorecards', match.id)
  let options: ScorecardPrintOptions = parseOptions(null)
  let loadedOptionsKey = ''
  $: if (loadedOptionsKey !== optionsKey) {
    loadedOptionsKey = optionsKey
    options = loadPrintOptions(optionsKey, parseOptions)
  }

  $: participants = match.participants ?? []
  $: keyboardConfig = parseMatchKeyboardConfig(match.keyboardJson)
  $: matchCategories = keyboardConfig.categoryOrder.map((id) => categories.find((category) => category.id === id)).filter((category): category is Category => !!category)
  $: tenKey = keyboardConfig.keyboard.find((key) => key.label === '10')
  $: nineKey = keyboardConfig.keyboard.find((key) => key.label === '9')
  $: groups = hasGroups(match)

  $: selectedParticipants = participants.filter((participant) => !options.excludedParticipantIds.includes(participant.id))
  $: printCategories = matchCategories.filter((category) => !options.excludedCategoryIds.includes(category.id)).sort((a, b) => a.name.localeCompare(b.name))

  // The dialog edits a copy of the options; only printing commits (and remembers) them.
  // The paper is remembered once for every printable page (see paperSize.ts).
  let paper: PaperSize = loadPaperSize()
  let dialogOpen = false
  let dialogPaper: PaperSize = paper
  let dialogExcludedCategoryIds = new Set<string>()
  let dialogExcludedParticipantIds = new Set<string>()
  let dialogFreeText = ''
  let dialogSplitByGroup = false
  let dialogBlackOnly = false
  // The archer list starts collapsed every time the dialog opens; its summary shows how many are selected.
  let participantsExpanded = false

  $: dialogSelectedCount = participants.filter((participant) => !dialogExcludedParticipantIds.has(participant.id)).length
  $: allParticipantsSelected = participants.length > 0 && dialogSelectedCount === participants.length
  $: noParticipantsSelected = dialogSelectedCount === 0
  $: participantsIndeterminate = !allParticipantsSelected && !noParticipantsSelected

  function openDialog() {
    dialogExcludedCategoryIds = new Set(options.excludedCategoryIds)
    dialogExcludedParticipantIds = new Set(options.excludedParticipantIds)
    dialogFreeText = options.freeText
    dialogSplitByGroup = options.splitByGroup
    dialogBlackOnly = options.blackOnly
    dialogPaper = paper
    participantsExpanded = false
    dialogOpen = true
  }

  function toggled(set: Set<string>, id: string): Set<string> {
    const next = new Set(set)
    if (next.has(id)) next.delete(id)
    else next.add(id)
    return next
  }

  function toggleAllParticipants() {
    dialogExcludedParticipantIds = allParticipantsSelected ? new Set(participants.map((participant) => participant.id)) : new Set()
  }

  async function print() {
    // Only ids that still exist are remembered, so the stored lists don't grow with every removed archer.
    const participantIds = new Set(participants.map((participant) => participant.id))
    const categoryIds = new Set(matchCategories.map((category) => category.id))
    options = {
      excludedCategoryIds: [...dialogExcludedCategoryIds].filter((id) => categoryIds.has(id)),
      excludedParticipantIds: [...dialogExcludedParticipantIds].filter((id) => participantIds.has(id)),
      freeText: dialogFreeText,
      splitByGroup: dialogSplitByGroup,
      blackOnly: dialogBlackOnly
    }
    savePrintOptions(optionsKey, options)
    paper = dialogPaper
    savePaperSize(paper)
    dialogOpen = false
    await tick()
    window.print()
  }

  // A single printed card: one participant over a range of ends. Normally that's the whole match; with
  // splitByGroup each participant gets one card per group of ends instead.
  type PrintCard = { participant: MatchParticipant; firstEnd: number; lastEnd: number }

  // Scorecards are grouped by device (in the order devices are defined on the match), members within a
  // device ordered by their deviceOrder; participants without a device are grouped last, alphabetically.
  // With splitByGroup, each device group is repeated once per group of ends (all of a device's cards for
  // ends 1-10, then all of them for 11-20, ...). Between blocks, null placeholders pad the preceding block
  // up to a multiple of 4 so each block always starts on a fresh printed page - see printPages below.
  function buildPrintOrder(participantsToOrder: MatchParticipant[], devices: NonNullable<Match['devices']>, endRanges: [number, number][]): (PrintCard | null)[] {
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

    const blocks: PrintCard[][] = groups.flatMap((group) => endRanges.map(([firstEnd, lastEnd]) => group.map((participant) => ({ participant, firstEnd, lastEnd }))))
    const result: (PrintCard | null)[] = []
    blocks.forEach((block, index) => {
      result.push(...block)
      const isLastBlock = index === blocks.length - 1
      const remainder = result.length % 4
      if (!isLastBlock && remainder !== 0) {
        for (let i = 0; i < 4 - remainder; i++) result.push(null)
      }
    })
    return result
  }

  $: splitCards = groups && options.splitByGroup
  $: endRanges = splitCards ? groupEndRanges(match) : ([[1, match.ends]] as [number, number][])
  $: printItems = buildPrintOrder(selectedParticipants, match.devices ?? [], endRanges)
  $: printPages = chunk(printItems, 4)
  // Rows a single card's table needs: its ends, plus the "running total" carry-in row on later split cards.
  $: rowsPerCard = Math.max(...endRanges.map(([firstEnd, lastEnd]) => lastEnd - firstEnd + 1 + (firstEnd > 1 ? 1 : 0)))

  // Signature images aren't part of the participant payloads, so they're fetched in bulk for the match's
  // signed participants. Ids go in the query string, so they're chunked to keep each URL well under
  // typical proxy header limits. Printing waits until they've loaded.
  const signatureChunkSize = 50
  let signatures = new Map<string, MatchParticipantSignature>()
  let loadingSignatures = false
  let signatureGeneration = 0

  $: signedIdsKey = participants.filter((participant) => participant.signed).map((participant) => participant.id).sort().join(',')
  $: void loadSignatures(match.id, signedIdsKey)

  async function loadSignatures(matchId: string, idsKey: string) {
    const generation = ++signatureGeneration
    const ids = idsKey ? idsKey.split(',') : []
    if (ids.length === 0) {
      signatures = new Map()
      loadingSignatures = false
      return
    }
    loadingSignatures = true
    try {
      const chunks = await Promise.all(chunk(ids, signatureChunkSize).map((idChunk) => api.fetchMatchSignatures(matchId, idChunk)))
      if (generation === signatureGeneration) signatures = new Map(chunks.flat().map((signature) => [signature.matchParticipantId, signature]))
    } catch {
      // Without images the cards still print, showing the "signed and final" placeholder instead.
    } finally {
      if (generation === signatureGeneration) loadingSignatures = false
    }
  }

  function deviceNameFor(participant: MatchParticipant): string {
    if (!participant.deviceId) return '-'
    return (match.devices ?? []).find((device) => device.id === participant.deviceId)?.name ?? '-'
  }

  // Printed sizes, only applied on paper (as CSS variables, see the print rules below), computed separately
  // for each orientation so the score grid fills the card's height in both. The card size follows from the
  // chosen paper (cardSize): on A4 a card is 97 × 140.5mm in portrait (2×2) and 68 × 198mm in landscape
  // (4 across), see the orientation CSS below. Around the grid the card
  // needs ~57mm regardless of the number of ends (header with lane box, participant line, two stacked
  // signature boxes - measured in Chrome's print layout), plus the two-line categories block and the taller
  // header of the optional free-text line when present. Every one of those lines is fixed in height (single
  // lines with an ellipsis, the categories block clamped to two lines), so the same chrome holds for every
  // card. CHROME_SAFETY_MM covers font-metric differences between browsers. The column headers and the totals
  // rows take another TABLE_HEADER_ROW_MM + TOTALS_ROWS_MM. The rest is divided over the rows, up to
  // MAX_ROW_HEIGHT_MM each.
  //
  // Without shrinking, a match with enough ends makes the table's natural minimum height overflow the card,
  // which pushes the whole grid row to the next page (and, worse, can corrupt the page's layout entirely
  // rather than gracefully paginating - CSS Grid fragmentation in Chrome's print engine does not degrade
  // gently). The table is also a flex-grow child of the card, so any safety margin left over still goes to
  // the rows; the browser's table row auto-stretch algorithm ignores `max-height` on cells/rows - only a
  // max-height on the <table> itself clamps it, which keeps rows at MAX_ROW_HEIGHT_MM for short matches.
  //
  // A `height` on a table cell is only a suggestion the browser can grow past to fit its content's line
  // box - it cannot shrink a row below the line-height its font-size needs. So for high end counts the
  // requested row height must also shrink the font-size (with line-height:1 on .score-cell, a row's floor
  // is exactly its font-size), or rows silently clamp back to their normal ~13px minimum and overflow.
  // The end-number cell must shrink along with them: table rows share one height, so a larger font there
  // would make it the tallest cell and override every other cell's shrunk height.
  // No lower clamp on either value: forcing a legibility floor would silently reintroduce the overflow
  // this guards against for very high end counts.
  // The @page margin and the gap between cards. Two cards across in portrait (two rows of two), four in landscape.
  const PAGE_MARGIN_MM = 6
  const CARD_GAP_MM = 4
  // A card's border and padding: the width inside it bounds the digit size too. The table is laid out with
  // equal-width columns (end number, arrows, score, subtotal, 10s, 9s), so a three-digit subtotal must fit its
  // column on one line (6 arrows in landscape on A4: ~5.8mm).
  const CARD_FRAME_MM = 5
  const DIGITS_PER_CELL = 3
  const DIGIT_WIDTH_EM = 0.62
  const CARD_CHROME_HEIGHT_MM = 57
  const CATEGORIES_BLOCK_MM = 5.2
  const FREE_TEXT_LINE_MM = 1.5
  const CHROME_SAFETY_MM = 2.5
  const TABLE_HEADER_ROW_MM = 3.5
  const TOTALS_ROWS_MM = 7.5
  const MAX_ROW_HEIGHT_MM = 15
  const MAX_CELL_FONT_PX = 13
  const MM_TO_PX = 96 / 25.4

  function cardSize(paper: PaperSize, orientation: PrintOrientation): { sheetHeight: number; width: number; height: number } {
    const area = printableArea(paper, orientation, PAGE_MARGIN_MM)
    return orientation === 'portrait'
      ? { sheetHeight: area.height, width: (area.width - CARD_GAP_MM) / 2, height: (area.height - CARD_GAP_MM) / 2 }
      : { sheetHeight: area.height, width: (area.width - 3 * CARD_GAP_MM) / 4, height: area.height }
  }

  function orientationSizes(paper: PaperSize, orientation: PrintOrientation, rows: number, columns: number, chromeMm: number): string {
    const card = cardSize(paper, orientation)
    const rowHeightMm = Math.min(MAX_ROW_HEIGHT_MM, (card.height - chromeMm - TABLE_HEADER_ROW_MM - TOTALS_ROWS_MM) / rows)
    const columnWidthPx = ((card.width - CARD_FRAME_MM) / columns) * MM_TO_PX
    const cellFontPx = Math.min(MAX_CELL_FONT_PX, rowHeightMm * MM_TO_PX * 0.8, columnWidthPx / (DIGITS_PER_CELL * DIGIT_WIDTH_EM + 0.3))
    return `--sheet-height-${orientation}: ${card.sheetHeight}mm; --card-height-${orientation}: ${card.height}mm; --row-height-${orientation}: ${rowHeightMm}mm; --cell-font-${orientation}: ${cellFontPx}px; --end-font-${orientation}: ${Math.min(9, cellFontPx)}px`
  }

  $: chromeMm = CARD_CHROME_HEIGHT_MM + (printCategories.length > 0 ? CATEGORIES_BLOCK_MM : 0) + (options.freeText ? FREE_TEXT_LINE_MM : 0) + CHROME_SAFETY_MM
  // End number, arrows, score and subtotal, plus the 10s and 9s columns when the keyboard has those keys.
  $: tableColumns = match.arrowsPerEnd + (tenKey && nineKey ? 5 : 3)
  $: printSizes = `${orientationSizes(paper, 'portrait', rowsPerCard, tableColumns, chromeMm)}; ${orientationSizes(paper, 'landscape', rowsPerCard, tableColumns, chromeMm)}; --table-max-height: ${rowsPerCard * MAX_ROW_HEIGHT_MM + TABLE_HEADER_ROW_MM + TOTALS_ROWS_MM}mm`

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

  function hasScoreBetween(scores: MatchParticipant['scores'], firstEnd: number, lastEnd: number): boolean {
    return (scores ?? []).some((score) => score.end >= firstEnd && score.end <= lastEnd)
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

  function countKeyThrough(scores: MatchParticipant['scores'], lastEnd: number, keyId: string): number {
    return (scores ?? []).filter((score) => score.end <= lastEnd && score.keyId === keyId).length
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
  <!-- No page size: the cards follow the orientation picked in the browser's print dialog. The margin is
       PAGE_MARGIN_MM. -->
  <style>
    @page {
      margin: 6mm;
    }
  </style>
</svelte:head>

<MatchPageHeader {api} {match} {language} {labels} handlers={headerHandlers} current="print" title={labels.printScorecards} subtitle={match.name} backLabel={match.name} {onBack} pageActions={[printAction(labels, openDialog)]} />

<PrintDialog bind:open={dialogOpen} title={labels.printOptionsTitle} {labels} printDisabled={noParticipantsSelected || loadingSignatures} onPrint={print}>
  <section>
    <h3>{labels.printCategoriesLabel}</h3>
    <div class="checkbox-grid">
      {#each matchCategories as category}
        <label class="checkbox-label">
          <input type="checkbox" checked={!dialogExcludedCategoryIds.has(category.id)} on:change={() => (dialogExcludedCategoryIds = toggled(dialogExcludedCategoryIds, category.id))} />
          {category.name}
        </label>
      {/each}
      {#if matchCategories.length === 0}<p class="muted">-</p>{/if}
    </div>
  </section>

  <label>{labels.printFreeTextLabel}<input type="text" placeholder={labels.printFreeTextPlaceholder} bind:value={dialogFreeText} /></label>

  {#if groups}
    <label class="checkbox-label">
      <input type="checkbox" bind:checked={dialogSplitByGroup} />
      {labels.printSplitByGroupLabel.replace('{groupEnds}', String(match.groupEnds))}
    </label>
  {/if}

  <label class="checkbox-label">
    <input type="checkbox" bind:checked={dialogBlackOnly} />
    {labels.printBlackOnlyLabel}
  </label>

  <PaperSizeSelect bind:value={dialogPaper} {labels} />

  <p class="muted layout-hint">{labels.printLayoutFourColumnHint}</p>

  <details class="participants-details" bind:open={participantsExpanded}>
    <summary>
      <h3>{labels.printParticipantsLabel}</h3>
      <span class="muted">{labels.printParticipantsSelectedSummary.replace('{selected}', String(dialogSelectedCount)).replace('{total}', String(participants.length))}</span>
    </summary>
    <div class="participants-header">
      <label class="checkbox-label">
        <input type="checkbox" checked={allParticipantsSelected} indeterminate={participantsIndeterminate} on:change={toggleAllParticipants} />
        {labels.printSelectAllLabel}
      </label>
    </div>
    <div class="list-panel">
      {#each participants as participant (participant.id)}
        <label class="list-row checkbox-row">
          <input type="checkbox" checked={!dialogExcludedParticipantIds.has(participant.id)} on:change={() => (dialogExcludedParticipantIds = toggled(dialogExcludedParticipantIds, participant.id))} />
          <span class="participant-name">{participant.fullName || participant.lastName}</span>
          {#if participant.federationNumber}<span class="muted">{participant.federationNumber}</span>{/if}
        </label>
      {/each}
      {#if participants.length === 0}<p class="empty-state">{labels.emptyState}</p>{/if}
    </div>
  </details>
  {#if noParticipantsSelected}<p class="error">{labels.printNoParticipantsSelected}</p>{/if}
</PrintDialog>

{#snippet printCard(card: PrintCard)}
  {@const participant = card.participant}
  {@const lines = participantCategoryLines(participant)}
  {@const scores = participant.scores}
  {@const totalsKnown = hasScoreBetween(scores, card.firstEnd, card.lastEnd)}
  <div class="print-card">
    <div class="card-header">
      {#if tenantLogoUrl}<img class="card-logo" src={tenantLogoUrl} alt="" />{/if}
      <div class="card-heading">
        <p class="card-match-name">{match.name}</p>
        <div class="card-date-line">
          <span class="card-match-date">{formatLocalDate(match.date, language)}</span>
          <span class="card-device-name">{deviceNameFor(participant)}</span>
        </div>
        {#if options.freeText}<p class="card-free-text">{options.freeText}</p>{/if}
      </div>
      <!-- Always present: empty when the lane assignment doesn't give this archer a lane yet. -->
      <div class="card-lane">
        <span class="card-lane-caption">{labels.laneLabel}</span>
        <strong class="card-lane-value">{participant.deviceLane ?? ''}</strong>
      </div>
    </div>
    <div class="card-participant-line">
      <span class="card-participant-name">{participant.fullName || participant.lastName}</span>
      {#if participant.federationNumber}<span class="card-federation-number">{labels.federationNumberLabel}: {participant.federationNumber}</span>{/if}
    </div>
    <!-- Present on every card as soon as any category is printed, always exactly two lines high (see
         CATEGORIES_BLOCK_MM), so long values can never push the grid down and every card's grid lines up. -->
    {#if printCategories.length > 0}
      <div class="card-categories">
        {#each lines as line}<span class="card-category">{line.name}: {line.value}</span>{' '}{/each}
      </div>
    {/if}

    <!-- Thick lines frame the arrow columns, the two total columns and the right edge of the grid. -->
    <table class="score-table">
      <thead>
        <tr>
          <th class="borderless"></th>
          {#each Array(match.arrowsPerEnd) as _, arrowIndex}<th>{arrowIndex + 1}</th>{/each}
          <th>{labels.printScoreHeader}</th>
          <th>{labels.printSubtotalHeader}</th>
          {#if tenKey && nineKey}
            <th>{labels.printTensHeader}</th>
            <th>{labels.printNinesHeader}</th>
          {/if}
        </tr>
      </thead>
      <tbody>
        {#if card.firstEnd > 1}
          {@const carryKnown = hasScoreBetween(scores, 1, card.firstEnd - 1)}
          <tr>
            <td class="borderless end-number"></td>
            <td class="borderless running-total-label" colspan={match.arrowsPerEnd + 1}>{labels.printRunningTotalLabel}</td>
            <td class="score-cell thick-right">{carryKnown ? runningTotal(scores, card.firstEnd - 1) : ''}</td>
            {#if tenKey && nineKey}
              <td class="score-cell">{carryKnown ? countKeyThrough(scores, card.firstEnd - 1, tenKey.keyId) : ''}</td>
              <td class="score-cell thick-right">{carryKnown ? countKeyThrough(scores, card.firstEnd - 1, nineKey.keyId) : ''}</td>
            {/if}
          </tr>
        {/if}
        {#each Array(card.lastEnd - card.firstEnd + 1) as _, index}
          {@const end = card.firstEnd + index}
          {@const hasScore = endHasAnyScore(scores, end)}
          <tr>
            <td class="borderless end-number">{end}</td>
            {#each Array(match.arrowsPerEnd) as _, arrowIndex}
              <td class="score-cell" class:thick-left={arrowIndex === 0} class:thick-right={arrowIndex === match.arrowsPerEnd - 1}>{hasScore ? keyLabelFor(scores, end, arrowIndex + 1) : ''}</td>
            {/each}
            <td class="score-cell">{hasScore ? endTotal(scores, end) : ''}</td>
            <td class="score-cell thick-right">{hasScore ? (groups && !splitCards ? groupRunningTotal(match, scores, end) : runningTotal(scores, end)) : ''}</td>
            {#if tenKey && nineKey}
              <td class="score-cell">{hasScore ? countKeyInEnd(scores, end, tenKey.keyId) : ''}</td>
              <td class="score-cell thick-right">{hasScore ? countKeyInEnd(scores, end, nineKey.keyId) : ''}</td>
            {/if}
          </tr>
        {/each}
      </tbody>
      <!-- The totals the archers add up themselves, a little apart from the grid; filled in when the
           match already has scores for this card's ends. -->
      <tfoot>
        <tr class="totals-gap"><td class="borderless" colspan={match.arrowsPerEnd + 3 + (tenKey && nineKey ? 2 : 0)}></td></tr>
        <tr class="totals-row">
          <td class="borderless"></td>
          <td class="borderless totals-label" colspan={match.arrowsPerEnd}>{labels.printTotalLabel}</td>
          <td class="total-box" colspan="2">{totalsKnown ? runningTotal(scores, card.lastEnd) : ''}</td>
          {#if tenKey && nineKey}
            <td class="total-box">{totalsKnown ? countKeyThrough(scores, card.lastEnd, tenKey.keyId) : ''}</td>
            <td class="total-box">{totalsKnown ? countKeyThrough(scores, card.lastEnd, nineKey.keyId) : ''}</td>
          {/if}
        </tr>
      </tfoot>
    </table>

    <div class="card-signatures">
      <div class="signature-block">
        {#if signatures.get(participant.id)?.archerSignatureDataUrl}
          <img class="signature-image" src={signatures.get(participant.id)?.archerSignatureDataUrl} alt={labels.archerSignatureLabel} />
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
        {#if signatures.get(participant.id)?.markerSignatureDataUrl}
          <img class="signature-image" src={signatures.get(participant.id)?.markerSignatureDataUrl} alt={labels.markerSignatureLabel} />
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

<!-- On screen the cards form a responsive grid; on paper there are four per page, in a 2×2 grid in
     portrait or side by side in landscape (see the print rules below). -->
<div class="print-page print-output" class:black-only={options.blackOnly} style={printSizes}>
  {#each printPages as pageItems, pageIndex (pageIndex)}
    <div class="print-sheet">
      {#each chunk(pageItems, 2) as halfRowItems, halfIndex (halfIndex)}
        <div class="print-half-row">
          {#each halfRowItems as item, itemIndex (item ? `${item.participant.id}-${item.firstEnd}` : `placeholder-${pageIndex}-${halfIndex}-${itemIndex}`)}
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
  {#if selectedParticipants.length === 0}<p class="empty-state">{participants.length === 0 ? labels.emptyState : labels.printNoParticipantsSelected}</p>{/if}
</div>

<style>
  .checkbox-grid {
    display: flex;
    flex-wrap: wrap;
    gap: 8px 20px;
  }

  h3 {
    margin: 0 0 10px;
    font-size: 16px;
  }

  .layout-hint {
    margin: 0;
    font-size: 13px;
  }

  .participants-details summary {
    display: flex;
    align-items: baseline;
    flex-wrap: wrap;
    gap: 4px 12px;
    cursor: pointer;
  }

  .participants-details summary::before {
    content: '›';
    font-weight: 700;
    transition: transform 0.15s;
  }

  .participants-details[open] summary::before {
    transform: rotate(90deg);
  }

  .participants-details summary::-webkit-details-marker {
    display: none;
  }

  .participants-details summary h3 {
    margin: 0;
  }

  .participants-header {
    display: flex;
    justify-content: flex-end;
    margin: 10px 0;
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

  .running-total-label {
    text-align: right;
    font-size: 8px;
    font-weight: 700;
    padding: 0 4px 0 0;
    white-space: nowrap;
  }

  /* On screen: one responsive grid of cards, enlarged from their paper size. The page and half-row
     wrappers only matter on paper. */
  @media screen {
    .print-page {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(min(100%, 340px), 1fr));
      gap: 20px;
      align-items: start;
    }

    .print-sheet,
    .print-half-row {
      display: contents;
    }

    .print-card {
      zoom: 1.3;
      background: #fff;
    }

    .print-card-placeholder {
      display: none;
    }
  }

  /* The "black only" print option: every text and line in pure black - no greys, no semi-transparent
     borders - for printers that render grey poorly. Images (logo, signatures) keep their colours. */
  .black-only .print-card,
  .black-only .print-card :not(img) {
    color: #000 !important;
    border-color: #000 !important;
  }

  .print-card {
    border: 1.5px solid #000;
    padding: 8px;
    break-inside: avoid;
    color: #000;
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

  .card-heading {
    flex: 1;
    min-width: 0;
  }

  /* Every line of the card's header is a single line (cut off with an ellipsis): a wrapped line would make
     the card taller than the space its grid was sized for (see CARD_CHROME_HEIGHT_MM). */
  .card-match-name,
  .card-match-date,
  .card-free-text,
  .card-participant-name {
    overflow: hidden;
    white-space: nowrap;
    text-overflow: ellipsis;
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

  .card-lane {
    flex: 0 0 auto;
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    min-width: 34px;
    height: 25px;
    padding: 1px 3px;
    box-sizing: border-box;
    border: 2px solid #000;
  }

  .card-lane-caption {
    font-size: 5.5px;
    line-height: 1;
    text-transform: uppercase;
    color: #555;
  }

  .card-lane-value {
    font: 700 13px 'Space Grotesk', sans-serif;
    line-height: 1;
    white-space: nowrap;
  }

  .card-participant-line {
    display: flex;
    justify-content: space-between;
    gap: 6px;
    font-weight: 700;
    font-size: 10px;
    line-height: 1.1;
    margin-bottom: 1px;
  }

  .card-participant-name,
  .card-match-date {
    min-width: 0;
  }

  .card-federation-number {
    flex: 0 0 auto;
    white-space: nowrap;
  }

  /* Exactly two lines, whatever the values: longer text is cut off after the second line. */
  .card-categories {
    display: -webkit-box;
    -webkit-box-orient: vertical;
    -webkit-line-clamp: 2;
    line-clamp: 2;
    overflow: hidden;
    height: 2.2em;
    font-size: 8px;
    line-height: 1.1;
    color: #333;
    margin-bottom: 2px;
  }

  .card-category {
    margin-right: 6px;
    white-space: nowrap;
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
    white-space: nowrap;
    border: 1px solid rgba(0, 0, 0, 0.5);
    text-align: center;
    height: 18px;
    padding: 0;
    font-size: 11px;
    line-height: 1;
    overflow: hidden;
  }

  /* In a collapsed table the wider border wins, so these override the thin cell borders they meet. */
  .score-cell.thick-left {
    border-left: 2px solid #000;
  }

  .score-cell.thick-right {
    border-right: 2px solid #000;
  }

  .totals-gap td {
    height: 1.5mm;
    padding: 0;
  }

  .totals-label {
    text-align: right;
    font-size: 9px;
    font-weight: 700;
    padding: 0 4px 0 0;
  }

  .total-box {
    border: 2px solid #000;
    height: 6mm;
    padding: 0;
    text-align: center;
    font-size: 11px;
    font-weight: 700;
    line-height: 1;
  }

  .card-signatures {
    display: flex;
    flex-direction: column;
    gap: 8px;
    margin-top: 8px;
  }

  .signature-block {
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    gap: 1px;
    width: 100%;
  }

  .signature-image,
  .signature-placeholder {
    width: 100%;
    height: 15mm;
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
    .print-page {
      display: block;
    }

    .print-sheet:not(:last-child) {
      break-after: page;
    }

    .print-card {
      display: flex;
      flex-direction: column;
      height: 100%;
      box-sizing: border-box;
      margin: 0;
      /* Overrides the base .print-card's break-inside:avoid. Each card is already sized to fit exactly
         within its grid cell (see orientationSizes), so this should never need to break. But if that sizing is
         ever slightly off, break-inside:avoid on an overflowing grid item makes Chrome's print engine
         corrupt the whole page (rows disappearing/overlapping) instead of just letting the small excess
         spill - a far safer failure mode. */
      break-inside: auto;
    }

    .score-table {
      flex: 1 1 auto;
      min-height: 0;
      max-height: var(--table-max-height);
    }

    tbody .score-cell {
      height: var(--row-height);
      font-size: var(--cell-font);
    }

    .end-number {
      height: var(--row-height);
      font-size: var(--end-font);
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
    .print-page {
      --row-height: var(--row-height-landscape);
      --cell-font: var(--cell-font-landscape);
      --end-font: var(--end-font-landscape);
    }

    .print-sheet {
      display: grid;
      grid-template-columns: repeat(4, 1fr);
      grid-template-rows: 1fr;
      gap: 4mm;
      height: var(--sheet-height-landscape);
    }

    .print-half-row {
      display: contents;
    }
  }

  @media print and (orientation: portrait) {
    .print-page {
      --row-height: var(--row-height-portrait);
      --cell-font: var(--cell-font-portrait);
      --end-font: var(--end-font-portrait);
    }

    .print-sheet {
      display: block;
      height: var(--sheet-height-portrait);
    }

    .print-half-row {
      display: grid;
      grid-template-columns: repeat(2, 1fr);
      grid-template-rows: 1fr;
      gap: 4mm;
      height: var(--card-height-portrait);
    }

    .print-half-row:first-child {
      margin-bottom: 4mm;
    }
  }
</style>
