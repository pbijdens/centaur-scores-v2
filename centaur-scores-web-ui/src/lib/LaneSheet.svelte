<script lang="ts">
  import CategoryTag from './CategoryTag.svelte'
  import { formatLocalDate } from './date'
  import { buildLaneSheet, configurationColor, configurationKey, distinctValues, parseLane, type LaneSheetBlock } from './laneSheet'
  import type { Category, Language, Match, MatchParticipant, ScoreDevice } from './types'

  // The printable lane assignment sheet of MatchDevicesView: hidden on screen, it is the only thing
  // printed from that page (it is the page's .print-output, see app.scss). Six lane blocks per A4 portrait
  // page, grouped by device; each lane header shows its target configuration (the values of
  // `laneCategory` on that lane number) on a background colour per configuration, and each archer gets
  // a presence box plus the selected categories as tags.
  export let match: Match
  export let devices: ScoreDevice[]
  // In print order: per device in device order, then the archers without a device.
  export let participants: MatchParticipant[]
  export let laneCategory: Category | null
  export let deviceCategory: Category | null
  export let language: Language
  export let labels: Record<string, string>

  const blocksPerPage = 6

  $: sheet = buildLaneSheet(devices, participants, blocksPerPage)
  $: tagCategories = [laneCategory, deviceCategory].filter((category): category is Category => !!category)
  // Room for 6 lane headers, device headings and gaps on a 277mm-high page; the rest is split over the
  // rows of the six blocks.
  $: rowHeightMm = Math.min(10, Math.max(5, (277 - 12 - blocksPerPage * 18) / (blocksPerPage * sheet.slotCount)))
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

  function blockTitle(block: LaneSheetBlock): string {
    if (block.kind === 'noLane') return labels.laneSheetNoLane
    if (block.kind === 'noDevice') return labels.laneSheetNoDevice
    return labels.laneSheetLane.replace('{lane}', String(block.laneNumber))
  }

  // Consecutive blocks of the same device on a page share one device heading.
  function groupsOf(blocks: LaneSheetBlock[]): { title: string | null; blocks: LaneSheetBlock[] }[] {
    const groups: { key: string; title: string | null; blocks: LaneSheetBlock[] }[] = []
    for (const block of blocks) {
      const key = block.kind === 'noDevice' ? '#none' : block.deviceId ?? '#gap'
      const last = groups[groups.length - 1]
      if (last && last.key === key) last.blocks.push(block)
      else groups.push({ key, title: block.kind === 'noDevice' ? null : block.deviceName, blocks: [block] })
    }
    return groups
  }
</script>

<div class="lane-sheet print-output" style:--row-height="{rowHeightMm}mm">
  {#each sheet.pages as page, pageIndex}
    <div class="lane-page">
      <div class="sheet-header">
        <strong>{labels.laneSheetTitle} · {match.name}</strong>
        <span>{formatLocalDate(match.date, language)} · {labels.laneSheetPage.replace('{n}', String(pageIndex + 1)).replace('{total}', String(sheet.pages.length))}</span>
      </div>
      {#each groupsOf(page.blocks) as group}
        <section class="device-group">
          {#if group.title}<h2>{group.title}</h2>{/if}
          {#each group.blocks as block}
            {@const values = blockValues(block)}
            <div class="lane-block">
              <div class="lane-header" style:background={blockBackground(block)}>
                <strong>{blockTitle(block)}</strong>
                {#if block.kind === 'empty'}
                  <span class="muted">{labels.laneSheetEmptyLane}</span>
                {:else if laneCategory && values.length > 0}
                  <span class="lane-configuration">
                    {#each values as value}<CategoryTag category={laneCategory} valueId={value ?? undefined} missingLabel={labels.missingCategoryValue} />{/each}
                    {#if values.length > 1}<span class="warning" title={labels.mixedLaneWarning.replace('{lane}', String(block.laneNumber)).replace('{category}', laneCategory.name)}>⚠</span>{/if}
                  </span>
                {/if}
              </div>
              <!-- Fixed column widths (table-layout: fixed) so the columns line up across every block and page. -->
              <table>
                <colgroup>
                  <col class="presence-col" />
                  <col class="slot-col" />
                  <col class="federation-col" />
                  <col />
                  {#each tagCategories as _}<col class="category-col" />{/each}
                </colgroup>
                <thead>
                  <tr>
                    <th></th>
                    <th>{labels.laneLabel}</th>
                    <th>{labels.federationNumberLabel}</th>
                    <th>{labels.fullNameLabel}</th>
                    {#each tagCategories as category}<th>{category.name}</th>{/each}
                  </tr>
                </thead>
                <tbody>
                  {#each block.rows as row}
                    <tr>
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
                </tbody>
              </table>
            </div>
          {/each}
        </section>
      {/each}
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

  .sheet-header {
    display: flex;
    justify-content: space-between;
    gap: 12px;
    padding-bottom: 2mm;
    margin-bottom: 3mm;
    border-bottom: 1px solid #111;
    font-size: 10pt;
  }

  .device-group {
    margin-bottom: 5mm;
  }

  .device-group h2 {
    margin: 0 0 1.5mm;
    font-size: 12pt;
  }

  .lane-block {
    break-inside: avoid;
    margin-bottom: 2.5mm;
    border: 1px solid #555;
  }

  .lane-header {
    display: flex;
    align-items: center;
    gap: 4mm;
    min-height: 7mm;
    padding: 1mm 2mm;
    border-bottom: 1px solid #555;
    background: #f3f3f3;
    print-color-adjust: exact;
    -webkit-print-color-adjust: exact;
  }

  .lane-configuration {
    display: flex;
    flex-wrap: wrap;
    gap: 1.5mm;
    align-items: center;
  }

  .warning {
    font-weight: 700;
  }

  table {
    width: 100%;
    border-collapse: collapse;
    table-layout: fixed;
  }

  .presence-col {
    width: 8mm;
  }

  .slot-col {
    width: 12mm;
  }

  .federation-col {
    width: 26mm;
  }

  .category-col {
    width: 40mm;
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

  tbody tr + tr td {
    border-top: 1px solid #ccc;
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

  .category-cell :global(.category-tag) {
    max-width: 100%;
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
    width: 8mm;
    font-weight: 700;
  }

</style>
