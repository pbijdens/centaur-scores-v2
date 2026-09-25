<script lang="ts">
  import { rankLabel, ruleBlocks, type ResultUnit } from './competitionResultsLayout'

  // One equally high unit of the printed results stream: a group header or a participant row. The number of detail
  // lines under a participant's name comes from the inherited --detail-lines custom property.
  export let unit: ResultUnit
</script>

{#if unit.kind === 'header'}
  <div class="result-unit result-header">{unit.name}</div>
{:else}
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
{/if}

<style>
  .result-unit {
    --name-line: 11pt;
    --detail-line: 8pt;
    --pad: 1.5pt;
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
    padding-top: calc(var(--pad) + 4pt);
    font-size: 10pt;
    font-weight: 700;
    border-bottom: 1px solid #000;
    white-space: nowrap;
    text-overflow: ellipsis;
    break-after: avoid;
  }

  .result-entry {
    display: grid;
    grid-template-columns: 2.2em minmax(0, 1fr) auto;
    font-size: 9.5pt;
    column-gap: 4pt;
    border-bottom: 0.5px solid #ccc;
  }

  .rank,
  .score {
    font-size: 9.5pt;
    line-height: var(--name-line);
    font-variant-numeric: tabular-nums;
  }

  .score {
    font-weight: 600;
    text-align: right;
  }

  .name {
    font-size: 9.5pt;
    font-weight: 600;
    line-height: var(--name-line);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  .details {
    font-size: 6.5pt;
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

  .disqualified .name,
  .disqualified .score {
    color: #666;
  }
</style>
