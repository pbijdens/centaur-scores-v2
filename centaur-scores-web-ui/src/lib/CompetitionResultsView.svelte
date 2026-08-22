<script lang="ts">
  import { onMount } from 'svelte'
  import type { ApiClient } from './api'
  import { formatLocalDate } from './date'
  import type { CompetitionResultsDocument, Language } from './types'

  export let api: ApiClient
  export let competitionId: string
  export let language: Language
  export let labels: Record<string, string>

  let document_: CompetitionResultsDocument | null = null
  let loadError = ''

  onMount(async () => {
    try {
      document_ = await api.fetchCompetitionResults(competitionId)
    } catch {
      loadError = 'Unable to load competition results.'
    }
  })

  function roundScoreDisplay(entry: CompetitionResultsDocument['groups'][number]['entries'][number], roundId: string): string {
    const score = entry.roundScores[roundId]
    return score ? String(score.value) : '–'
  }

  function roundScoreUsed(entry: CompetitionResultsDocument['groups'][number]['entries'][number], roundId: string): boolean {
    return entry.roundScores[roundId]?.used ?? true
  }
</script>

<svelte:head>
  <title>{labels.resultsPageTitle}{document_ ? ` - ${document_.competitionName}` : ''}</title>
</svelte:head>

<div class="results-toolbar">
  <button class="primary" on:click={() => window.print()}>{labels.printButton}</button>
</div>

{#if loadError}
  <p class="error">{loadError}</p>
{:else if !document_}
  <p class="muted">…</p>
{:else}
  <div class="results-page">
    <header class="results-header">
      <h1>{document_.competitionName}</h1>
      <p>{formatLocalDate(document_.startDate, language)} – {formatLocalDate(document_.endDate, language)} · {new Date().toLocaleDateString()}</p>
    </header>
    {#each document_.groups as group}
      <section class="results-group">
        <h2>{group.name}</h2>
        <table>
          <thead>
            <tr>
              <th>{labels.positionLabel}</th>
              <th>{labels.nameLabel ?? 'Name'}</th>
              {#each document_.rounds as round}<th>{round.shortName}</th>{/each}
              <th>{labels.totalLabel}</th>
            </tr>
          </thead>
          <tbody>
            {#each group.entries as entry}
              <tr class:disqualified={entry.disqualified}>
                <td>{entry.position}{entry.needsTieBreaker ? '*' : ''}</td>
                <td>{entry.name}</td>
                {#each document_.rounds as round}
                  <td class:unused-score={!roundScoreUsed(entry, round.id)}>{roundScoreDisplay(entry, round.id)}</td>
                {/each}
                <td>{entry.disqualified ? 'n/a' : entry.total}</td>
              </tr>
            {/each}
          </tbody>
        </table>
      </section>
    {/each}
  </div>
{/if}

<style>
  :global(body) {
    background: #fff;
  }

  .results-toolbar {
    padding: 16px;
  }

  @media print {
    .results-toolbar {
      display: none;
    }
  }

  .results-page {
    max-width: 1000px;
    margin: 0 auto;
    padding: 0 24px 40px;
    color: #000;
  }

  .results-header {
    display: flex;
    justify-content: space-between;
    align-items: baseline;
    border-bottom: 2px solid #000;
    padding-bottom: 8px;
    margin-bottom: 16px;
  }

  .results-group {
    break-inside: avoid;
    margin-bottom: 32px;
  }

  table {
    width: 100%;
    border-collapse: collapse;
    font-size: 14px;
  }

  th,
  td {
    border-bottom: 1px solid #ccc;
    padding: 6px 8px;
    text-align: left;
  }

  tr.disqualified {
    opacity: .6;
  }

  .unused-score {
    text-decoration: line-through;
    color: #888;
  }
</style>
