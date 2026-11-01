<script lang="ts">
  import CategoryTag from './CategoryTag.svelte'
  import { formatLocalDate } from './date'
  import { printableArea, type PaperSize } from './paperSize'
  import { buildLaneSheet, configurationColor, configurationKey, distinctValues, paginateLaneSheet, parseLane, type LaneSheetBlock, type LaneSheetSegment } from './laneSheet'
  import type { Category, Language, Match, MatchParticipant, ScoreDevice } from './types'

  // The printable lane assignment sheet of MatchDevicesView: hidden on screen, it is the only thing
  // printed from that page (it is the page's .print-output, see app.scss). One table per portrait page: a narrow
  // device column, spanning its lanes; then a narrow lane column, spanning its archers, with two lines: the lane
  // name and its target configuration (the values of `laneCategory` on that lane number) on a background colour
  // per configuration - both columns' text rotated 90° counter-clockwise; then one row per archer with a presence
  // box and the selected categories as tags. Every block has at least four rows (MIN_LANE_ROWS) and every page
  // exactly ROWS_PER_PAGE rows of equal height, sized to fill the paper: six lanes of four archers per page. Lanes
  // with more archers simply fit fewer per page. A device whose lanes run over a page break is repeated on the
  // next page.
  export let match: Match
  export let devices: ScoreDevice[]
  // In print order: per device in device order, then the archers without a device.
  export let participants: MatchParticipant[]
  export let laneCategory: Category | null
  export let deviceCategory: Category | null
  export let paper: PaperSize
  export let language: Language
  export let labels: Record<string, string>

  // The @page margin MatchDevicesView sets; the fixed heights of the page header (including its gap) and of the
  // column headers; a safety margin for rounding. The rest of the printable height is divided over the rows:
  // on A4 (277 - 8 - 5 - 3) / 24 = 10.9mm per row.
  const PAGE_MARGIN_MM = 10
  const ROWS_PER_PAGE = 24
  const SHEET_HEADER_MM = 8
  const COLUMN_HEADER_MM = 5
  const SAFETY_MM = 3

  $: pageHeightMm = printableArea(paper, 'portrait', PAGE_MARGIN_MM).height
  $: rowHeightMm = (pageHeightMm - SHEET_HEADER_MM - COLUMN_HEADER_MM - SAFETY_MM) / ROWS_PER_PAGE
  $: sheet = buildLaneSheet(devices, participants)
  $: pages = paginateLaneSheet(sheet.blocks, ROWS_PER_PAGE)
  $: tagCategories = [laneCategory, deviceCategory].filter((category): category is Category => !!category)
  // A lane split over two devices is still one lane: its configuration comes from all its archers.
  $: laneValues = laneCategory ? lanesValues(participants, laneCategory.id) : new Map<number, (number | null)[]>()
  $: configurationIndex = indexConfigurations(laneValues)

  function lanesValues(all: MatchParticipant[], categoryId: string): Map<number, (number | null)[]> {
    const byLane = new Map<number, MatchParticipant[]>()
    for (const participant of all) {
      const { number } = parseLane(participant.deviceLane)
      if (number !== null && participant.deviceId) byLane.set(number, [...(byLane.get(number) ?? []), participant])
    }
    return new Map([...byLane].map(([number, laneParticipants]) => [number, distinctValues(laneParticipants, categoryId)]))
  }

  function indexConfigurations(values: Map<number, (number | null)[]>): Map<string, number> {
    const index = new Map<string, number>()
    for (const number of [...values.keys()].sort((a, b) => a - b)) {
      const key = configurationKey(values.get(number)!)
      if (!index.has(key)) index.set(key, index.size)
    }
    return index
  }

  function blockValues(block: LaneSheetBlock): (number | null)[] {
    return block.laneNumber !== null && block.kind === 'lane' ? laneValues.get(block.laneNumber) ?? [] : []
  }

  function blockBackground(block: LaneSheetBlock): string | undefined {
    const values = blockValues(block)
    if (values.length === 0) return undefined
    return configurationColor(configurationIndex.get(configurationKey(values)) ?? 0)
  }

  function laneTitle(block: LaneSheetBlock): string {
    if (block.kind === 'noLane') return labels.laneSheetNoLane
    if (block.kind === 'noDevice') return ''
    return labels.laneSheetLane.replace('{lane}', String(block.laneNumber))
  }

  function deviceTitle(block: LaneSheetBlock): string {
    return block.kind === 'noDevice' ? labels.laneSheetNoDevice : block.deviceName ?? ''
  }

  // The length available to a rotated label in a cell spanning `rows` rows: minus the cell's padding and some
  // slack, so a label never stretches the rows it spans.
  function spanMm(rows: number): string {
    return `${rows * rowHeightMm - 4}mm`
  }

  // Consecutive segments of the same device on a page share one device cell spanning all their rows.
  function deviceGroupsOf(segments: LaneSheetSegment[]): { title: string; rowCount: number; segments: LaneSheetSegment[] }[] {
    const groups: { key: string; title: string; rowCount: number; segments: LaneSheetSegment[] }[] = []
    for (const segment of segments) {
      const key = segment.block.kind === 'noDevice' ? '#none' : segment.block.deviceId ?? '#gap'
      const last = groups[groups.length - 1]
      if (last && last.key === key) {
        last.segments.push(segment)
        last.rowCount += segment.rows.length
      } else groups.push({ key, title: deviceTitle(segment.block), rowCount: segment.rows.length, segments: [segment] })
    }
    return groups
  }
</script>

<div class="lane-sheet print-output" style:--row-height="{rowHeightMm}mm" style:--sheet-header-height="{SHEET_HEADER_MM}mm" style:--column-header-height="{COLUMN_HEADER_MM}mm">
  {#each pages as page, pageIndex}
    <div class="lane-page">
      <div class="sheet-header">
        <strong>{labels.laneSheetTitle} · {match.name}</strong>
        <span>{formatLocalDate(match.date, language)} · {labels.laneSheetPage.replace('{n}', String(pageIndex + 1)).replace('{total}', String(pages.length))}</span>
      </div>
      <!-- Fixed column widths (table-layout: fixed) so the columns line up on every page. -->
      <table>
        <colgroup>
          <col class="device-col" />
          <col class="lane-col" />
          <col class="presence-col" />
          <col class="slot-col" />
          <col class="federation-col" />
          <col />
          {#each tagCategories as _}<col class="category-col" />{/each}
        </colgroup>
        <thead>
          <tr>
            <!-- Too narrow for a caption; the rotated device names speak for themselves. -->
            <th></th>
            <th>{labels.laneLabel}</th>
            <th></th>
            <th></th>
            <th>{labels.federationNumberLabel}</th>
            <th>{labels.fullNameLabel}</th>
            {#each tagCategories as category}<th>{category.name}</th>{/each}
          </tr>
        </thead>
        {#each deviceGroupsOf(page.segments) as group}
          <tbody>
            {#each group.segments as segment, segmentIndex}
              {@const block = segment.block}
              {@const values = blockValues(block)}
              {#each segment.rows as row, rowIndex}
                <tr class:lane-start={rowIndex === 0}>
                  {#if segmentIndex === 0 && rowIndex === 0}
                    <td class="device" rowspan={group.rowCount}><div class="rotated" style:max-height={spanMm(group.rowCount)}><span class="device-name">{group.title}</span></div></td>
                  {/if}
                  {#if rowIndex === 0}
                    <td class="lane" rowspan={segment.rows.length} style:background={blockBackground(block)}>
                      <div class="rotated" style:max-height={spanMm(segment.rows.length)}>
                      <span class="lane-title">{laneTitle(block)}</span>
                      <span class="lane-configuration">
                        {#if block.kind === 'empty'}
                          <span class="muted">{labels.laneSheetEmptyLane}</span>
                        {:else if laneCategory && values.length > 0}
                          {#each values as value}<CategoryTag category={laneCategory} valueId={value ?? undefined} missingLabel={labels.missingCategoryValue} />{/each}
                          {#if values.length > 1}<span class="warning" title={labels.mixedLaneWarning.replace('{lane}', String(block.laneNumber)).replace('{category}', laneCategory.name)}>⚠</span>{/if}
                        {/if}
                      </span>
                      </div>
                    </td>
                  {/if}
                  <td><span class="presence-box"></span></td>
                  <td class="slot">{row.slot}</td>
                  <td class="federation">{row.participant?.federationNumber ?? ''}</td>
                  <td class="name">{row.participant ? row.participant.fullName || row.participant.lastName : ''}</td>
                  {#each tagCategories as category, categoryIndex}
                    <td class="category-cell">
                      {#if row.participant}<CategoryTag {category} valueId={row.participant.categories[category.id]} offset={categoryIndex * 6} missingLabel={labels.missingCategoryValue} />{/if}
                    </td>
                  {/each}
                </tr>
              {/each}
            {/each}
          </tbody>
        {/each}
      </table>
    </div>
  {/each}
</div>

<style>
  .lane-sheet {
    display: none;
    color: #111;
    font-size: 11pt;
  }

  @media print {
    .lane-sheet {
      display: block;
    }
  }

  .lane-page {
    break-after: page;
  }

  .lane-page:last-child {
    break-after: auto;
  }

  /* Fixed heights (SHEET_HEADER_MM, COLUMN_HEADER_MM): the row height is what is left of the page. */
  .sheet-header {
    display: flex;
    justify-content: space-between;
    align-items: flex-end;
    gap: 12px;
    box-sizing: border-box;
    height: calc(var(--sheet-header-height) - 2mm);
    margin-bottom: 2mm;
    padding-bottom: 1mm;
    border-bottom: 1px solid #111;
    font-size: 10pt;
    white-space: nowrap;
  }

  thead tr {
    height: var(--column-header-height);
  }

  table {
    width: 100%;
    border-collapse: collapse;
    table-layout: fixed;
  }

  .device-col {
    width: 9mm;
  }

  .lane-col {
    width: 13mm;
  }

  .presence-col {
    width: 7mm;
  }

  .slot-col {
    width: 10mm;
  }

  .federation-col {
    width: 20mm;
  }

  .category-col {
    width: 28mm;
  }

  th {
    padding: 0.5mm 2mm;
    border-bottom: 1px solid #999;
    color: #555;
    font-size: 7.5pt;
    font-weight: 600;
    text-align: left;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  tbody tr {
    height: var(--row-height);
  }

  tbody td {
    border-top: 1px solid #ccc;
  }

  /* A new lane and a new device start with a dark line, the last row of a device (and so of the page) ends
     with one; the device and lane cells are framed. */
  tbody tr.lane-start td,
  tbody tr:first-child td,
  td.device,
  td.lane {
    border-top: 1px solid #555;
  }

  tbody tr:last-child td {
    border-bottom: 1px solid #555;
  }

  td.device,
  td.lane {
    border-right: 1px solid #555;
    border-bottom: 1px solid #555;
    white-space: normal;
    print-color-adjust: exact;
    -webkit-print-color-adjust: exact;
  }

  td.device,
  td.lane {
    padding: 1mm 0;
    text-align: center;
  }

  td.device {
    border-left: 1px solid #555;
    font-size: 10pt;
    font-weight: 700;
  }

  /* Rotated 90° counter-clockwise: read bottom to top, the first line on the left. The max-height (the cell's
     spanned length) makes long names end in an ellipsis instead of stretching the rows. */
  .rotated {
    display: inline-block;
    writing-mode: vertical-rl;
    transform: rotate(180deg);
    overflow: hidden;
    text-align: center;
    vertical-align: middle;
  }

  .device-name {
    display: block;
    overflow: hidden;
    white-space: nowrap;
    text-overflow: ellipsis;
  }

  /* Two lines: the lane's name, then its target configuration. */
  td.lane {
    background: #f3f3f3;
  }

  .lane-configuration {
    display: block;
    overflow: hidden;
    white-space: nowrap;
    text-overflow: ellipsis;
  }

  .lane-title {
    display: block;
    overflow: hidden;
    white-space: nowrap;
    text-overflow: ellipsis;
    font-size: 10pt;
    font-weight: 700;
    line-height: 1.3;
  }

  .lane-configuration {
    font-size: 8pt;
    line-height: 1.5;
  }

  .lane-configuration :global(.category-tag) {
    margin-bottom: 1mm;
    padding: 1.5mm 0;
    font-size: 7.5pt;
  }

  .warning {
    font-weight: 700;
  }

  td {
    padding: 0 2mm;
    vertical-align: middle;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  .federation {
    font-variant-numeric: tabular-nums;
  }

  td.category-cell {
    padding: 0 1mm;
  }

  .category-cell :global(.category-tag) {
    max-width: 100%;
    font-size: 8.5pt;
    overflow: hidden;
    text-overflow: ellipsis;
    vertical-align: middle;
  }

  .presence-box {
    display: block;
    width: 4mm;
    height: 4mm;
    border: 1px solid #111;
  }

  .slot {
    font-weight: 700;
  }

</style>
