<script lang="ts">
  import { onMount } from 'svelte'
  import { fetchLiveScoringMatches, fetchLiveScoringPage } from '../api'
  import { formatLocalDate } from '../date'
  import { translationsFor } from '../i18n'
  import MatchProgress from '../MatchProgress.svelte'
  import type { Language, LiveScoringBlock, LiveScoringEntry, LiveScoringMatch, LiveScoringPage } from '../types'

  export let scope: string

  const CAPACITY_ENTRIES = 48
  const FONT_FILL_RATIO = 0.78
  const GAP_FRACTION = 0.015
  // A category header only needs about half a grid unit of height - it's a single short line,
  // not two lines of content like a result entry. It still counts as a "unit" everywhere in the
  // row-capacity/column-partitioning arithmetic below (so the 48-participant capacity math
  // stays simple), but only consumes this fraction of that unit's actual pixel height on screen.
  const HEADER_UNIT_FRACTION = 0.5
  // A result entry's 1.0 grid unit: most of it goes to the archer's name (line 1), a smaller
  // share to the detail line (line 2), and a sliver to padding.
  const LINE1_FRACTION = 0.63
  const LINE2_FRACTION = 0.35
  const ENTRY_PAD_FRACTION = 0.02
  // Position only ever needs to show up to 2 digits (max 48 participants) plus an optional
  // tie-breaker "*" - 3ch (a full 3rd digit's width) was more room than that ever uses.
  const POSITION_CH = 2.4
  // Position/average/score sit in fixed "ch"-sized grid tracks (POSITION_CH / 5ch / 5ch) at
  // average's .82em and score's 1.12em relative sizing - this is that same POSITION_CH + 5*.82 +
  // 5*1.12 total, times an estimated average glyph width (tabular digits run close to .55em) -
  // used to cap how large that font is allowed to get so the numeric columns can never outgrow
  // the space the name column (line1/line2) needs.
  const NUMERIC_WIDTH_FRACTION = 0.42
  // Measured against the app's actual font (DM Sans) via canvas measureText: a tabular digit
  // - which is what the position/average/score "ch" grid tracks hold - renders at ~0.675em,
  // not the ~0.55em a generic estimate would suggest. Understating this doesn't just mis-size
  // those columns: since the numeric font size is solved for by inverting this same ratio
  // against a fixed px budget, understating it also makes the reserved-numeric-width figure
  // fed into the name-column budget too small, silently overstating how much space is left for
  // the name text. Rounded up slightly from the ~0.675 measurement for margin.
  const CH_ESTIMATE = 0.68
  const NUMERIC_CH_TOTAL = POSITION_CH + 5 * 0.82 + 5 * 1.12
  // Once the numeric font is otherwise sized (by row height or by the width budget above), it's
  // scaled down further still - position/average/score don't need to be as prominent as the
  // archer's name, and shrinking them frees width for the name column too.
  const NUMERIC_FONT_SCALE = 0.7
  // Row height alone can't drive the name/header font size either: a sparse match (few
  // entries) gets a very tall row, and blowing the font up to fill that height overruns the
  // column's fixed width long before it runs out of vertical room, truncating names/headers
  // to a couple of characters. These budgets assume "enough characters to show a typical name
  // or category label" (~20/24 chars) and cap the font so it never needs more width than that -
  // any leftover row height is left as vertical padding instead of forcing an unreadably large,
  // truncated font. TEXT_CH_ESTIMATE is likewise measured against DM Sans (~0.588em average
  // mixed-case glyph width), rounded up slightly for margin.
  const NAME_CHAR_BUDGET = 20
  const HEADER_CHAR_BUDGET = 24
  const TEXT_CH_ESTIMATE = 0.62

  let page: LiveScoringPage | null = null
  let retrySeconds = 30
  let progress = 0
  let generation = 0
  let interval: ReturnType<typeof setInterval> | undefined
  let timeout: ReturnType<typeof setTimeout> | undefined
  const language = (localStorage.getItem('centaur-language') ?? 'nl') as Language
  const labels = translationsFor(language)

  let resultsViewport: HTMLElement | undefined
  let viewportWidth = 0
  let viewportHeight = 0

  function clearTimers() {
    if (interval) clearInterval(interval)
    if (timeout) clearTimeout(timeout)
    interval = undefined
    timeout = undefined
  }

  async function start(preserveCurrentPage = false) {
    const currentGeneration = ++generation
    clearTimers()
    if (!preserveCurrentPage) page = null
    progress = 0
    try {
      const matches = await fetchLiveScoringMatches(scope)
      if (currentGeneration !== generation) return
      if (matches.length === 0) {
        page = null
        startRetryCountdown(currentGeneration)
        return
      }
      await showMatch(matches, 0, currentGeneration)
    } catch {
      if (currentGeneration === generation) {
        page = null
        startRetryCountdown(currentGeneration)
      }
    }
  }

  function startRetryCountdown(currentGeneration: number) {
    retrySeconds = 30
    interval = setInterval(() => {
      retrySeconds -= 1
      if (retrySeconds <= 0 && currentGeneration === generation) void start()
    }, 1000)
  }

  async function showMatch(matches: LiveScoringMatch[], index: number, currentGeneration: number) {
    try {
      page = await fetchLiveScoringPage(scope, matches[index].id)
    } catch {
      if (currentGeneration === generation) void advance(matches, index, currentGeneration)
      return
    }
    if (currentGeneration !== generation || !page) return

    const duration = Math.max(1, Number(page.timeout) || 15) * 1000
    const startedAt = performance.now()
    progress = 0
    interval = setInterval(() => {
      progress = Math.min(100, ((performance.now() - startedAt) / duration) * 100)
    }, 100)
    timeout = setTimeout(() => void advance(matches, index, currentGeneration), duration)
  }

  async function advance(matches: LiveScoringMatch[], index: number, currentGeneration: number) {
    if (currentGeneration !== generation) return
    clearTimers()
    if (index + 1 >= matches.length) {
      await start(true)
      return
    }
    await showMatch(matches, index + 1, currentGeneration)
  }

  onMount(() => {
    document.body.classList.add('live-scoring-page')
    void start()
    return () => {
      generation += 1
      clearTimers()
      document.body.classList.remove('live-scoring-page')
    }
  })

  // Layout: the result output area is a strict grid where a category header and a
  // result entry each occupy exactly 1.0 "grid height unit". The unit height (in px) is
  // derived so that 48 participants, plus the category headers needed to reach the first
  // 48 of them, always fit the available height without vertical scrolling - regardless of
  // viewport size. When the match actually has fewer entries, the same calculation (based
  // on the real counts, capped at the first-48 window) naturally yields more breathing room
  // instead of forcing a fixed 48-row layout.
  //
  // Columns are assigned ourselves (rather than via native CSS multi-column layout): each
  // column is filled to exactly `rowsPerColumn` units before the next one starts, keeping a
  // header attached to at least its first entry where possible. This makes column count and
  // content placement fully deterministic - the browser's own column-breaking heuristics
  // otherwise silently disagree with this arithmetic by a row here and there, which either
  // strands a header alone or (worse) pushes a row into a column beyond what's rendered as
  // scrollable, making it invisible. When more columns result than the 2/3 baseline, the view
  // auto-scrolls horizontally over the page's timeout to reveal them.
  type ColumnItem = { type: 'header'; name: string } | { type: 'entry'; entry: LiveScoringEntry }

  function blocksForFirst48(blocks: LiveScoringBlock[]): LiveScoringBlock[] {
    const result: LiveScoringBlock[] = []
    let seen = 0
    for (const block of blocks) {
      if (block.entries.length === 0) continue
      if (seen >= CAPACITY_ENTRIES) break
      const entries = block.entries.slice(0, CAPACITY_ENTRIES - seen)
      result.push({ ...block, entries })
      seen += entries.length
    }
    return result
  }

  function partitionColumns(blocks: LiveScoringBlock[], rowsPerColumn: number, baselineColumns: number): ColumnItem[][] {
    const capacity = Math.max(1, rowsPerColumn)
    const columns: ColumnItem[][] = Array.from({ length: Math.max(1, baselineColumns) }, () => [])
    let colIndex = 0
    let used = 0

    function advanceIfNeeded(unitsNeeded: number) {
      if (used > 0 && used + unitsNeeded > capacity) {
        colIndex += 1
        used = 0
        if (!columns[colIndex]) columns[colIndex] = []
      }
    }

    for (const block of blocks) {
      if (block.entries.length === 0) continue
      const headerNeed = Math.min(HEADER_UNIT_FRACTION + 1, HEADER_UNIT_FRACTION + block.entries.length, capacity)
      advanceIfNeeded(headerNeed)
      columns[colIndex].push({ type: 'header', name: block.name })
      used += HEADER_UNIT_FRACTION
      for (const entry of block.entries) {
        if (used + 1 > capacity) {
          colIndex += 1
          used = 0
          if (!columns[colIndex]) columns[colIndex] = []
        }
        columns[colIndex].push({ type: 'entry', entry })
        used += 1
      }
    }
    return columns
  }

  // Row sizing always assumes the full 48-participant capacity - not the match's actual entry
  // count - so a small match doesn't get blown up into an oversized, sparse-looking grid. The
  // only thing that varies row count (and so unit height/font size) between matches is how many
  // category headers are in play within that 48-slot window; a match with more categories gets
  // slightly more rows-per-column (smaller rows) to make room for the extra headers, but two
  // matches with the same category count look the same regardless of how many people actually
  // showed up. The naive `ceil(units / columns)` row count assumes perfect packing, but keeping
  // a header attached to its first entry can strand a spare unit at the bottom of a column (e.g.
  // a block that exactly fills the remainder minus one) - grow rowsPerColumn until the first-48
  // window's *real* content (which may be smaller than the assumed 48) actually partitions into
  // no more than the baseline column count, so the "48 always fits without scrolling" guarantee
  // holds against the real partitioning behaviour, not just the arithmetic total.
  function computeRowsPerColumn(blocks: LiveScoringBlock[], baselineColumns: number): number {
    const capBlocks = blocksForFirst48(blocks)
    const capacityUnits = CAPACITY_ENTRIES + capBlocks.length * HEADER_UNIT_FRACTION
    let rows = Math.max(1, Math.ceil(capacityUnits / baselineColumns))
    while (partitionColumns(capBlocks, rows, baselineColumns).length > baselineColumns) {
      rows += 1
    }
    return rows
  }

  $: blocks = page?.blocks ?? []
  $: baselineColumns = viewportWidth >= viewportHeight ? 3 : 2

  $: rowsPerColumn = computeRowsPerColumn(blocks, baselineColumns)
  $: unitHeight = viewportHeight > 0 ? viewportHeight / rowsPerColumn : 0

  $: resultColumns = partitionColumns(blocks, rowsPerColumn, baselineColumns)
  $: columnsUsed = resultColumns.length

  $: gapPx = viewportWidth * GAP_FRACTION
  $: columnWidthPx = baselineColumns > 0 ? Math.max(0, (viewportWidth - gapPx * (baselineColumns - 1)) / baselineColumns) : 0

  $: innerWidth = columnsUsed > 0 ? columnsUsed * columnWidthPx + gapPx * (columnsUsed - 1) : 0
  $: maxScrollPx = Math.max(0, innerWidth - viewportWidth)
  $: if (resultsViewport) resultsViewport.scrollLeft = (progress / 100) * maxScrollPx

  $: line1Height = unitHeight * LINE1_FRACTION
  $: line2Height = unitHeight * LINE2_FRACTION
  $: entryPadV = unitHeight * (ENTRY_PAD_FRACTION / 2)
  $: headerHeight = unitHeight * HEADER_UNIT_FRACTION
  $: headerPad = headerHeight * 0.02
  $: line1HeightFont = line1Height * FONT_FILL_RATIO
  $: line2HeightFont = line2Height * FONT_FILL_RATIO
  $: headerHeightFont = headerHeight * 0.96 * FONT_FILL_RATIO

  $: numericBudgetPx = columnWidthPx * NUMERIC_WIDTH_FRACTION
  $: numberFontByWidth = numericBudgetPx > 0 ? numericBudgetPx / (CH_ESTIMATE * NUMERIC_CH_TOTAL) : line1HeightFont
  $: entryFontSize = Math.min(line1HeightFont, numberFontByWidth) * NUMERIC_FONT_SCALE

  $: actualNumericWidthPx = entryFontSize * CH_ESTIMATE * NUMERIC_CH_TOTAL
  $: nameColumnWidthPx = Math.max(0, columnWidthPx - actualNumericWidthPx - columnWidthPx * 0.08)
  $: nameWidthCapFont = nameColumnWidthPx / (NAME_CHAR_BUDGET * TEXT_CH_ESTIMATE)
  $: nameScaleFactor = line1HeightFont > 0 ? Math.min(1, nameWidthCapFont / line1HeightFont) : 1
  $: line1FontSize = line1HeightFont * nameScaleFactor
  $: line2FontSize = line2HeightFont * nameScaleFactor

  $: headerWidthCapFont = (columnWidthPx * 0.94) / (HEADER_CHAR_BUDGET * TEXT_CH_ESTIMATE)
  $: headerFontSize = Math.min(headerHeightFont, headerWidthCapFont)

  $: innerStyle = [
    `--unit-height: ${unitHeight}px`,
    `--header-height: ${headerHeight}px`,
    `--header-pad: ${headerPad}px`,
    `--header-font: ${headerFontSize}px`,
    `--entry-pad-v: ${entryPadV}px`,
    `--entry-font: ${entryFontSize}px`,
    `--position-ch: ${POSITION_CH}ch`,
    `--line1-height: ${line1Height}px`,
    `--line2-height: ${line2Height}px`,
    `--line1-font: ${line1FontSize}px`,
    `--line2-font: ${line2FontSize}px`
  ].join('; ')
</script>

<svelte:head>
  <title>{page ? `${page.matchName} - ${page.tenant}` : 'Live scoring'}</title>
</svelte:head>

<div class="live-scoring">
  {#if page}
    <header class="live-header">
      <div class="tenant-identity">
        {#if page.logo}<img src={page.logo} alt="" />{/if}
        <strong>{page.tenant}</strong>
      </div>
      <h1>{page.matchName}</h1>
      <div class="date-progress">
        <time datetime={page.matchDate}>{formatLocalDate(page.matchDate, language)}</time>
        <MatchProgress {page} label={labels.matchProgressLabel} />
      </div>
    </header>
    <main class="live-results" bind:this={resultsViewport} bind:clientWidth={viewportWidth} bind:clientHeight={viewportHeight}>
      <div class="live-results-inner" style={innerStyle}>
        {#each resultColumns as column}
          <div class="result-column" style={`width: ${columnWidthPx}px; height: ${viewportHeight}px;`}>
            {#each column as item}
              {#if item.type === 'header'}
                <h2 class="result-header">{item.name}</h2>
              {:else}
                <div class="result-entry">
                  <span class="position">{item.entry.position}{item.entry.needsTieBreaker ? '*' : ''}</span>
                  <span class="entry-lines">
                    <strong>{#if item.entry.aboveTarget}<span class="pb-star" aria-hidden="true">★</span>{/if}{item.entry.line1}</strong>
                    {#if item.entry.line2}<small>{item.entry.line2}</small>{/if}
                  </span>
                  {#if item.entry.average != null}<span class="average">{item.entry.average.toFixed(2)}</span>{/if}
                  <strong class="score">{item.entry.score}</strong>
                </div>
              {/if}
            {/each}
          </div>
        {/each}
      </div>
    </main>
    <div class="live-progress" aria-hidden="true"><span style={`width: ${progress}%`}></span></div>
  {:else}
    <main class="no-live-matches">{labels.liveScoringNoSessions.replace('{seconds}', String(retrySeconds))}</main>
  {/if}
</div>

<style>
  :global(body.live-scoring-page) {
    overflow: hidden;
    background: #fff;
  }

  /* Mobile browsers (iOS Safari, Android Chrome) resolve 100vh to the viewport as it would be
     with their address/navigation bars collapsed, so a 100vh page is taller than what's actually
     visible and the bottom result rows end up hidden behind the toolbar. dvh tracks the currently
     visible viewport instead; --vh falls back to plain vh on browsers without dvh support. */
  .live-scoring {
    --vh: 1vh;
    width: 100vw;
    height: calc(100 * var(--vh));
    display: flex;
    flex-direction: column;
    padding-bottom: 4px;
    box-sizing: border-box;
    overflow: hidden;
    background: #fff;
    color: #14210f;
  }

  @supports (height: 1dvh) {
    .live-scoring {
      --vh: 1dvh;
    }
  }

  .live-header {
    flex: 0 0 auto;
    height: calc(8 * var(--vh));
    min-height: 0;
    display: grid;
    grid-template-columns: 1fr minmax(0, 2fr) 1fr;
    align-items: center;
    gap: 1.5vw;
    padding: 0 1.5vw;
    border-bottom: 1px solid #dfe4db;
  }

  .live-header h1 {
    overflow: hidden;
    font-size: clamp(15px, calc(2.1 * var(--vh)), 28px);
    line-height: 1;
    text-align: center;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .date-progress {
    display: flex;
    flex-direction: column;
    justify-content: center;
    align-items: flex-end;
    gap: calc(.4 * var(--vh));
    height: 100%;
  }

  .date-progress time {
    font-size: clamp(11px, calc(1.5 * var(--vh)), 18px);
    font-weight: 600;
  }

  .tenant-identity {
    min-width: 0;
    display: flex;
    align-items: center;
    gap: .7vw;
    font-size: clamp(11px, calc(1.7 * var(--vh)), 20px);
  }

  .tenant-identity img {
    width: calc(4.5 * var(--vh));
    height: calc(4.5 * var(--vh));
    object-fit: contain;
    flex: 0 0 auto;
  }

  .tenant-identity strong {
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .live-results {
    flex: 1 1 auto;
    min-height: 0;
    overflow: hidden;
  }

  .live-results-inner {
    display: flex;
    flex-direction: row;
    align-items: flex-start;
    gap: 1.5vw;
    height: 100%;
  }

  .result-column {
    flex: 0 0 auto;
    overflow: hidden;
    border-right: 1px solid #e7eae4;
  }

  .result-column:last-child {
    border-right: none;
  }

  .result-header {
    box-sizing: border-box;
    height: var(--header-height);
    margin: 0;
    padding: var(--header-pad) .5vw;
    display: flex;
    align-items: center;
    color: #eef2eb;
    background: #164a13;
    font-size: var(--header-font);
    line-height: 1;
    overflow: hidden;
    white-space: nowrap;
    text-overflow: ellipsis;
  }

  .result-entry {
    box-sizing: border-box;
    height: var(--unit-height);
    display: grid;
    grid-template-columns: var(--position-ch) minmax(0, 1fr) 5ch 5ch;
    align-items: center;
    gap: .4vw;
    padding: var(--entry-pad-v) .5vw;
    border-bottom: 1px solid #edf0ea;
    font-size: var(--entry-font);
    line-height: 1;
  }

  .position,
  .average,
  .score {
    font-variant-numeric: tabular-nums;
  }

  .average,
  .score {
    text-align: right;
  }

  .pb-star {
    color: #e0a300;
    font-size: .85em;
    margin-right: .3em;
    text-shadow: 0 0 2px rgba(224, 163, 0, .5);
  }

  .average {
    font-size: .82em;
  }

  .score {
    font-size: 1.12em;
  }

  .entry-lines {
    min-width: 0;
    height: 100%;
    display: flex;
    flex-direction: column;
    justify-content: center;
  }

  .entry-lines strong,
  .entry-lines small {
    display: flex;
    align-items: center;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .entry-lines strong {
    height: var(--line1-height);
    font-size: var(--line1-font);
  }

  .entry-lines small {
    height: var(--line2-height);
    color: #536050;
    font-size: var(--line2-font);
  }

  .live-progress {
    position: fixed;
    inset: auto 0 0;
    height: 4px;
    background: #e7eae4;
  }

  .live-progress span {
    display: block;
    height: 100%;
    background: #164a13;
  }

  .no-live-matches {
    flex: 1 1 auto;
    display: grid;
    place-items: center;
    padding: 24px;
    font-size: clamp(18px, calc(2.5 * var(--vh)), 30px);
    text-align: center;
  }
</style>
