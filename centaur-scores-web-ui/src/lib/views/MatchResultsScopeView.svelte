<script lang="ts">
  import { onMount } from 'svelte'
  import type { ApiClient } from '../api'
  import { formatLocalDate } from '../date'
  import MatchProgress from '../MatchProgress.svelte'
  import type { Language, LiveScoringPage } from '../types'

  export let api: ApiClient
  export let matchId: string
  export let scope: string
  export let language: Language
  export let labels: Record<string, string>

  let page: LiveScoringPage | null = null
  let loadError = ''

  onMount(() => {
    document.body.classList.add('match-results-page')
    void (async () => {
      try {
        page = await api.fetchMatchLiveScoringPage(matchId, scope)
      } catch {
        loadError = labels.matchResultsLoadError ?? 'Unable to load these results.'
      }
    })()
    return () => document.body.classList.remove('match-results-page')
  })
</script>

<svelte:head>
  <title>{page ? `${page.matchName} - ${page.tenant}` : labels.resultsLabel}</title>
</svelte:head>

{#if page}
  <div class="results-toolbar">
    <button class="primary" on:click={() => window.print()}>{labels.printButton}</button>
  </div>
{/if}
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
    <main class="live-results">
      {#each page.blocks as block}
        <section class="result-block">
          {#if block.entries.length > 0}
            <h2>{block.name}</h2>
            {#each block.entries as entry}
              <div class="result-entry">
                <span class="position">{entry.position}{entry.needsTieBreaker ? '*' : ''}</span>
                <span class="entry-lines">
                  <strong>{#if entry.aboveTarget}<span class="pb-star" aria-hidden="true">★</span>{/if}{entry.line1}</strong>
                  {#if entry.line2}<small>{entry.line2}</small>{/if}
                </span>
                {#if entry.average != null}<span class="average">{entry.average.toFixed(2)}</span>{/if}
                <strong class="score">{entry.score}</strong>
              </div>
            {/each}
          {/if}
        </section>
      {/each}
    </main>
  {:else if loadError}
    <main class="no-live-matches">{loadError}</main>
  {:else}
    <main class="no-live-matches">…</main>
  {/if}
</div>

<style>
  :global(body.match-results-page) {
    background: #fff;
  }

  .results-toolbar {
    padding: 16px;
  }

  .results-toolbar .primary {
    width: 100%;
    padding: 16px;
    font-size: 16px;
  }

  .live-scoring {
    background: #fff;
    color: #14210f;
  }

  .live-header {
    position: static;
    height: auto;
    z-index: auto;
    display: grid;
    grid-template-columns: 1fr minmax(0, 2fr) 1fr;
    align-items: center;
    gap: 24px;
    padding: 16px 24px;
    border-bottom: 1px solid #dfe4db;
  }

  .live-header h1 {
    overflow: hidden;
    font-size: 22px;
    line-height: 1.2;
    text-align: center;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .date-progress {
    display: flex;
    flex-direction: column;
    justify-content: center;
    align-items: flex-end;
    gap: 6px;
  }

  .date-progress time {
    font-size: 14px;
    font-weight: 600;
  }

  .tenant-identity {
    min-width: 0;
    display: flex;
    align-items: center;
    gap: 10px;
    font-size: 16px;
  }

  .tenant-identity img {
    width: 36px;
    height: 36px;
    object-fit: contain;
    flex: 0 0 auto;
  }

  .tenant-identity strong {
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .live-results {
    padding: 16px 24px 32px;
    column-count: 3;
    column-gap: 24px;
    column-rule: 1px solid #e7eae4;
  }

  .result-block h2 {
    break-after: avoid;
    break-inside: avoid;
    margin: 12px 0 6px;
    padding: 6px 8px;
    background: #eef2eb;
    color: #164a13;
    font-size: 14px;
    line-height: 1.2;
  }

  .result-entry {
    display: grid;
    grid-template-columns: 3ch minmax(0, 1fr) 5ch 5ch;
    align-items: center;
    gap: 6px;
    break-inside: avoid;
    padding: 3px 8px;
    border-bottom: 1px solid #edf0ea;
    font-size: 13px;
    line-height: 1.3;
  }

  .position,
  .average,
  .score {
    font-variant-numeric: tabular-nums;
  }

  .pb-star {
    color: #e0a300;
    font-size: .85em;
    margin-right: .3em;
    text-shadow: 0 0 2px rgba(224, 163, 0, .5);
  }

  .average,
  .score {
    text-align: right;
  }

  .average {
    font-size: .82em;
  }

  .score {
    font-size: 1.12em;
  }

  .entry-lines {
    min-width: 0;
    display: grid;
    gap: 1px;
  }

  .entry-lines strong,
  .entry-lines small {
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .entry-lines small {
    color: #536050;
    font-size: .78em;
  }

  .no-live-matches {
    min-height: 50vh;
    display: grid;
    place-items: center;
    padding: 24px;
    font-size: 24px;
    text-align: center;
  }

  @media (max-width: 900px) {
    .live-results {
      column-count: 2;
    }
  }

  @media (max-width: 550px) {
    .live-results {
      column-count: 1;
    }
  }

  @media print {
    .results-toolbar {
      display: none;
    }

    .result-block h2 {
      -webkit-print-color-adjust: exact;
      print-color-adjust: exact;
    }
  }
</style>
