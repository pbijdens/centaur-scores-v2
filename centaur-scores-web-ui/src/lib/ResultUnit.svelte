<script lang="ts">
  import { rankLabel, ruleBlocks, type ResultUnit } from './resultsLayout'

  // One equally high unit of the printed results stream: a group header or a participant row - of a competition
  // (scores per scoring rule as details) or of a match (the scope's second line and the average as details). The
  // number of detail lines under a participant's name comes from the inherited --detail-lines custom property, every
  // size is multiplied by the inherited --font-scale.
  export let unit: ResultUnit
  export let averageLabel: string
</script>

{#if unit.kind === 'header'}
  <div class="result-unit result-header">{unit.name}</div>
{:else if unit.kind === 'entry'}
  <div class="result-unit result-entry" class:disqualified={unit.entry.disqualified}>
    <span class="rank">{rankLabel(unit.entry)}</span>
    <div class="middle">
      <div class="name">{unit.entry.name}</div>
      <div class="details">
        {#each ruleBlocks(unit.entry) as block}
          <!-- The label never wraps away from the rule's first score. -->
          <span class="rule"><span class="rule-head"><span class="rule-label">{block.label} ({block.total}):</span>{#each block.scores.slice(0, 1) as score}{' '}<span class="round-score" class:struck={score.struck} class:missing={score.missing}>{score.text}</span>{/each}</span>{#each block.scores.slice(1) as score}{' '}<span class="round-score" class:struck={score.struck} class:missing={score.missing}>{score.text}</span>{/each}</span>{' '}
        {/each}
      </div>
    </div>
    <span class="score">{unit.entry.total ?? '–'}</span>
  </div>
{:else}
  <div class="result-unit result-entry">
    <span class="rank">{unit.entry.position}{unit.entry.needsTieBreaker ? '*' : ''}</span>
    <div class="middle">
      <div class="name">{#if unit.entry.aboveTarget}<span class="pb-star" aria-hidden="true">★</span>{/if}{unit.entry.line1}</div>
      <div class="details">
        {#if unit.entry.line2}<span class="rule">{unit.entry.line2}</span>{' '}{/if}
        {#if unit.entry.average != null}<span class="rule-head"><span class="rule-label">{averageLabel}:</span> {unit.entry.average.toFixed(2)}</span>{/if}
      </div>
    </div>
    <span class="score">{unit.entry.score}</span>
  </div>
{/if}

<style>
  .result-unit {
    --name-line: calc(11pt * var(--font-scale, 1));
    --detail-line: calc(8pt * var(--font-scale, 1));
    --pad: calc(1.5pt * var(--font-scale, 1));
    box-sizing: border-box;
    height: calc(var(--name-line) + var(--detail-lines, 2) * var(--detail-line) + 2 * var(--pad));
    padding: var(--pad) 0;
    overflow: hidden;
    break-inside: avoid;
    color: #000;
  }

  .result-header {
    display: flex;
    align-items: flex-end;
    padding-top: calc(var(--pad) + calc(4pt * var(--font-scale, 1)));
    font-size: calc(10pt * var(--font-scale, 1));
    font-weight: 700;
    border-bottom: 1px solid #000;
    white-space: nowrap;
    text-overflow: ellipsis;
    break-after: avoid;
  }

  .result-entry {
    display: grid;
    grid-template-columns: 2.2em minmax(0, 1fr) auto;
    font-size: calc(9.5pt * var(--font-scale, 1));
    column-gap: calc(4pt * var(--font-scale, 1));
    border-bottom: 0.5px solid #ccc;
  }

  .rank,
  .score {
    font-size: calc(9.5pt * var(--font-scale, 1));
    line-height: var(--name-line);
    font-variant-numeric: tabular-nums;
  }

  .score {
    font-weight: 600;
    text-align: right;
  }

  .name {
    font-size: calc(9.5pt * var(--font-scale, 1));
    font-weight: 600;
    line-height: var(--name-line);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  .details {
    font-size: calc(6.5pt * var(--font-scale, 1));
    line-height: var(--detail-line);
    color: #333;
    display: -webkit-box;
    -webkit-box-orient: vertical;
    -webkit-line-clamp: var(--detail-lines, 2);
    line-clamp: var(--detail-lines, 2);
    overflow: hidden;
    font-variant-numeric: tabular-nums;
  }

  .rule {
    margin-right: 0.6em;
  }

  .rule-head {
    white-space: nowrap;
  }

  .rule-label {
    font-weight: 600;
  }

  .round-score {
    white-space: nowrap;
  }

  .round-score.struck {
    text-decoration: line-through;
    color: #777;
  }

  .round-score.missing {
    color: #777;
  }

  .pb-star {
    margin-right: 0.3em;
  }

  .disqualified .name,
  .disqualified .score {
    color: #666;
  }
</style>
