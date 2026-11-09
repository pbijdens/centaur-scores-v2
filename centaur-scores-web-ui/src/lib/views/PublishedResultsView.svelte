<script lang="ts">
  import { onMount } from 'svelte'
  import { ApiRequestError, fetchPublishedResults } from '../api'
  import { formatLocalDate } from '../date'
  import PublishedCompetitionTable from '../PublishedCompetitionTable.svelte'
  import PublishedMatchTable from '../PublishedMatchTable.svelte'
  import type { Language, PublishedResults } from '../types'

  // Anonymous, standalone results page (/results/{slug}) - no login, no management chrome. Published results are public
  // but must never be indexed: the robots meta below backs up the X-Robots-Tag header the API and nginx send.
  export let slug: string
  export let language: Language
  export let labels: Record<string, string>
  export let onLanguageChange: (language: string) => void

  let results: PublishedResults | null = null
  let state: 'loading' | 'ready' | 'not-found' | 'rate-limited' | 'error' = 'loading'
  let grouped = true

  $: matchResults = results?.match ?? null
  $: competitionResults = results?.competition ?? null
  $: canGroup = !!(matchResults ? matchResults.groupings.length > 0 : competitionResults?.isGrouped)
  $: showGrouped = grouped && canGroup
  $: dateText = results
    ? results.startDate === results.endDate
      ? formatLocalDate(results.startDate, language)
      : `${formatLocalDate(results.startDate, language)} – ${formatLocalDate(results.endDate, language)}`
    : ''
  $: hasTies = !!matchResults && [...matchResults.ungrouped, ...matchResults.groupings.flatMap((grouping) => grouping.blocks.flatMap((block) => block.entries))].some((entry) => entry.needsTieBreaker)

  async function load() {
    state = 'loading'
    try {
      results = await fetchPublishedResults(slug)
      state = results ? 'ready' : 'not-found'
    } catch (error) {
      // The API rate-limits this anonymous endpoint per caller (429 once a burst has used up its queue).
      state = error instanceof ApiRequestError && error.status === 429 ? 'rate-limited' : 'error'
    }
  }

  onMount(load)
</script>

<svelte:head>
  <meta name="robots" content="noindex, nofollow, noarchive" />
  <title>{results ? `${results.name} - ${results.tenant}` : 'CentaurScores'}</title>
</svelte:head>

<div class="published-shell">
  <div class="published-header">
    {#if results?.logo}<img class="logo" src={results.logo} alt="" />{/if}
    <div class="heading">
      {#if results}<p class="eyebrow">{results.tenant}</p>{/if}
      <h1>{results?.name ?? ''}</h1>
      {#if dateText}<p class="muted date">{dateText}</p>{/if}
    </div>
    <select class="language" aria-label="Language" value={language} on:change={(event) => onLanguageChange(event.currentTarget.value)}>
      <option value="nl">NL</option>
      <option value="en">EN</option>
    </select>
  </div>

  {#if state === 'loading'}
    <p class="muted">…</p>
  {:else if state === 'not-found'}
    <p class="muted">{labels.publishedResultsNotFound}</p>
  {:else if state === 'rate-limited'}
    <p class="error">{labels.publishedResultsRateLimited}</p>
    <button class="primary" on:click={load}>{labels.publishedRetry}</button>
  {:else if state === 'error'}
    <p class="error">{labels.publishedResultsLoadError}</p>
    <button class="primary" on:click={load}>{labels.publishedRetry}</button>
  {:else if results}
    {#if results.kind === 'match' && !results.match}
      <p class="notice">{labels.publishedMatchInProgress}</p>
    {:else if results.activeMatchCount > 0}
      <p class="notice">{(results.includesActiveMatches ? labels.publishedActiveIncluded : labels.publishedActiveExcluded).replace('{count}', String(results.activeMatchCount))}</p>
    {/if}
    {#if canGroup}
      <div class="view-toggle" role="radiogroup" aria-label={labels.publishedViewLabel}>
        <button role="radio" aria-checked={grouped} class:active={grouped} on:click={() => (grouped = true)}>{labels.publishedGrouped}</button>
        <button role="radio" aria-checked={!grouped} class:active={!grouped} on:click={() => (grouped = false)}>{labels.publishedUngrouped}</button>
      </div>
    {/if}

    {#if matchResults}
      {#if matchResults.ungrouped.length === 0}
        <p class="muted">{labels.publishedNoParticipants}</p>
      {:else if showGrouped}
        {#each matchResults.groupings as grouping}
          {#if matchResults.groupings.length > 1}<h2 class="grouping-title">{grouping.categoryNames.join(' · ')}</h2>{/if}
          {#each grouping.blocks as block}
            <section class="block">
              <h3>{block.name}</h3>
              <PublishedMatchTable entries={block.entries} {language} {labels} />
            </section>
          {/each}
        {/each}
      {:else}
        <section class="block"><PublishedMatchTable entries={matchResults.ungrouped} {language} {labels} /></section>
      {/if}
      {#if hasTies}<p class="muted legend">{labels.publishedTieHint}</p>{/if}
    {:else if competitionResults}
      {#if competitionResults.ungrouped.length === 0}
        <p class="muted">{labels.publishedNoParticipants}</p>
      {:else if showGrouped}
        {#each competitionResults.groups as group}
          <section class="block">
            <h3>{group.name}</h3>
            <PublishedCompetitionTable entries={group.entries} rounds={competitionResults.rounds} usesF1={competitionResults.usesF1} {language} {labels} />
          </section>
        {/each}
      {:else}
        <section class="block">
          <PublishedCompetitionTable entries={competitionResults.ungrouped} rounds={competitionResults.rounds} usesF1={competitionResults.usesF1} {language} {labels} />
        </section>
      {/if}
      <p class="muted legend">{labels.publishedNotCountingHint}{competitionResults.usesF1 ? ` ${labels.publishedF1Hint}` : ''}</p>
    {/if}
  {/if}
</div>

<style>
  .published-shell {
    max-width: 1100px;
    margin: 0 auto;
    padding: 32px 24px 48px;
  }

  .published-header {
    display: flex;
    align-items: flex-start;
    gap: 20px;
    margin-bottom: 24px;
  }

  .heading {
    flex: 1;
    min-width: 0;
  }

  .logo {
    width: 64px;
    height: 64px;
    object-fit: contain;
  }

  .date {
    margin: 8px 0 0;
  }

  .language {
    flex: 0 0 auto;
  }

  .notice {
    margin: 0 0 20px;
    padding: 10px 14px;
    border-left: 3px solid var(--accent);
    background: var(--paper);
  }

  .view-toggle {
    display: inline-flex;
    margin-bottom: 20px;
    border: 1px solid var(--line);
    background: var(--paper);
  }

  .view-toggle button {
    border: 0;
    padding: 9px 16px;
    background: transparent;
    color: var(--ink);
  }

  .view-toggle button.active {
    background: var(--green);
    color: #fff;
  }

  .grouping-title {
    margin: 28px 0 4px;
  }

  .block {
    margin-bottom: 24px;
    padding: 16px 20px;
    background: var(--paper);
    border: 1px solid var(--line);
  }

  .block h3 {
    margin: 0 0 10px;
    font-family: 'Space Grotesk', sans-serif;
    font-size: 17px;
  }

  .legend {
    font-size: 14px;
  }

  .published-shell :global(.table-wrap) {
    overflow-x: auto;
  }

  .published-shell :global(.published-table) {
    width: 100%;
    border-collapse: collapse;
  }

  .published-shell :global(.published-table th),
  .published-shell :global(.published-table td) {
    padding: 8px 8px;
    border-bottom: 1px solid var(--line);
    text-align: left;
    vertical-align: top;
  }

  .published-shell :global(.published-table th) {
    font-size: 12px;
    font-weight: 600;
    color: var(--muted);
    text-transform: uppercase;
    letter-spacing: 0.5px;
    vertical-align: bottom;
  }

  .published-shell :global(.published-table tbody tr:last-child td) {
    border-bottom: 0;
  }

  .published-shell :global(.published-table .num) {
    text-align: right;
    white-space: nowrap;
    font-variant-numeric: tabular-nums;
  }

  .published-shell :global(.published-table .pos) {
    width: 3ch;
  }

  .published-shell :global(.published-table .score) {
    font-weight: 700;
  }

  .published-shell :global(.published-table .fed-inline) {
    display: none;
  }

  @media (max-width: 640px) {
    .published-shell {
      padding: 20px 16px 32px;
    }

    .logo {
      width: 44px;
      height: 44px;
    }

    .block {
      padding: 10px 10px;
    }

    .published-shell :global(.published-table th),
    .published-shell :global(.published-table td) {
      padding: 7px 5px;
    }

    .published-shell :global(.published-table .fed) {
      display: none;
    }

    .published-shell :global(.published-table .fed-inline) {
      display: block;
      color: var(--muted);
      font-size: 12px;
    }
  }
</style>
