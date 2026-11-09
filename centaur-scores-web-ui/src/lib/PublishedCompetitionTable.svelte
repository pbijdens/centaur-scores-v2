<script lang="ts">
  import { formatAverage, formatCompetitionTotal, formatRoundScore } from './publishedResults'
  import type { PublishedCompetitionEntry, PublishedCompetitionRound } from './types'

  export let entries: PublishedCompetitionEntry[]
  export let rounds: PublishedCompetitionRound[]
  export let usesF1: boolean
  export let language: string
  export let labels: Record<string, string>

  // Every match score sits in a fixed-width track of one inner grid per row, so on desktop the scores of all rows line up
  // in neat columns; on mobile the grid collapses to one score per line, labelled with the round's short name. The track
  // is in rem, not ch: the header row uses a smaller font, and a ch-based width would differ between header and body.
  $: trackWidth = usesF1 ? '4.6rem' : '2.8rem'
</script>

<div class="table-wrap">
  <table class="published-table" style={`--rounds: ${rounds.length}; --track: ${trackWidth}`}>
    <thead>
      <tr>
        <th class="num pos">{labels.publishedPosition}</th>
        <th class="fed">{labels.publishedFederationNumber}</th>
        <th>{labels.publishedName}</th>
        <th class="num avg">{labels.publishedAverage}</th>
        <th class="scores-head">
          <span class="scores-title">{labels.publishedMatchScores}</span>
          <span class="scores-title-short">{labels.publishedScore}</span>
          <span class="score-grid round-names" aria-hidden="true">
            {#each rounds as round}<span class="round-name" title={round.longName}><span class="round-name-text">{round.shortName}</span></span>{/each}
          </span>
        </th>
        <th class="num total">
          <span class="total-title">{usesF1 ? labels.publishedTotalF1 : labels.publishedTotal}</span>
          <span class="total-title-short">{labels.publishedTotal}</span>
        </th>
      </tr>
    </thead>
    <tbody>
      {#each entries as entry}
        <tr>
          <td class="num pos">{entry.position}</td>
          <td class="fed">{entry.federationNumber ?? ''}</td>
          <td class="name">
            {entry.name}
            {#if entry.federationNumber}<small class="fed-inline">{entry.federationNumber}</small>{/if}
            <small class="avg-inline">{labels.publishedAverage} {formatAverage(entry.average, language)}</small>
          </td>
          <td class="num avg">{formatAverage(entry.average, language)}</td>
          <td class="scores">
            <span class="score-grid">
              {#each rounds as round, index}
                {@const score = entry.rounds[index]}
                <span class="match-score" class:absent={!score || score.score === null}>
                  <span class="round-label round-name" title={round.longName}><span class="round-name-text">{round.shortName}</span></span>
                  {#if score && score.score !== null && !score.counts}
                    <s>{formatRoundScore(score)}</s>
                  {:else}
                    {formatRoundScore(score)}
                  {/if}
                </span>
              {/each}
            </span>
          </td>
          <td class="num score total">{formatCompetitionTotal(entry, usesF1)}</td>
        </tr>
      {/each}
    </tbody>
  </table>
</div>

<style>
  .score-grid {
    display: grid;
    grid-template-columns: repeat(var(--rounds), var(--track));
    column-gap: 8px;
    justify-content: end;
    font-variant-numeric: tabular-nums;
  }

  .score-grid > span {
    text-align: right;
    white-space: nowrap;
  }

  .scores-head {
    text-align: right;
  }

  .scores-title {
    display: block;
    margin-bottom: 4px;
  }

  .round-names {
    font-size: 0.9em;
  }

  /* Round labels that do not fit are cut at the front ("…de 12" rather than "Ronde…"): the distinguishing part of a
     label like "Ronde 12" is at its end. The rtl container moves the overflow and the ellipsis to the left; the inner
     ltr span keeps the text itself (digits, punctuation) in normal reading order. */
  .round-name {
    display: block;
    overflow: hidden;
    white-space: nowrap;
    text-overflow: ellipsis;
    direction: rtl;
  }

  .round-name-text {
    direction: ltr;
    unicode-bidi: embed;
  }

  .round-label,
  .scores-title-short,
  .total-title-short,
  .avg-inline {
    display: none;
  }

  .absent {
    color: var(--muted);
  }

  s {
    color: var(--muted);
  }

  @media (max-width: 760px) {
    .round-names,
    .scores-title,
    .avg {
      display: none;
    }

    .scores-title-short,
    .total-title-short {
      display: inline;
    }

    .total-title {
      display: none;
    }

    /* "1078 (42)" may break before the f1 points so the table fits a phone without scrolling. */
    td.total {
      white-space: normal;
    }

    .avg-inline {
      display: block;
      color: var(--muted);
      font-size: 12px;
    }

    .score-grid > span {
      display: flex;
      justify-content: space-between;
      gap: 6px;
    }

    .scores-title {
      margin: 0;
    }

    .score-grid {
      grid-template-columns: auto;
      row-gap: 2px;
    }

    .round-label {
      display: block;
      flex: 0 1 auto;
      min-width: 0;
      max-width: 5.5em;
      margin-right: 6px;
      color: var(--muted);
      font-size: 0.8em;
      align-self: center;
    }
  }
</style>
