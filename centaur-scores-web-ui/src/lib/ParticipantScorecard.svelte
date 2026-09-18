<script lang="ts">
  import ArrowKeyPad from './ArrowKeyPad.svelte'
  import { endTotal, groupRunningTotal, hasGroups, isGroupDivider, runningTotal, scoreFor } from './scorecard'
  import type { ArrowScore, KeyboardKey, Match } from './types'

  export let match: Match
  export let scores: ArrowScore[] | undefined
  // All keys the match's keyboard defines - used to look up the color/label of an already-entered score,
  // even one whose key has since been disabled for this participant's category.
  export let keyboard: KeyboardKey[]
  // The keys actually offered when entering a new value (already filtered by disabledKeyRules); defaults
  // to the full keyboard when not given, which is fine for read-only mode where it's never used.
  export let entryKeys: KeyboardKey[] = keyboard
  export let interactive = false
  export let onScore: ((end: number, arrow: number, keyId: string) => void) | null = null
  export let closeLabel = 'Close'

  let openEnd: number | null = null
  let focusedArrow: number | null = null

  $: groups = hasGroups(match)

  function keyFor(keyId: string | undefined): KeyboardKey | undefined {
    return keyId ? keyboard.find((key) => key.keyId === keyId) : undefined
  }

  function colorClass(color: string): string {
    return `key-${color.toLowerCase()}`
  }

  function firstEmptyArrowInEnd(end: number): number | null {
    for (let arrow = 1; arrow <= match.arrowsPerEnd; arrow++) {
      if (!scoreFor(scores, end, arrow)) return arrow
    }
    return null
  }

  function onArrowClick(end: number, arrow: number) {
    if (!interactive) return
    if (openEnd === end) {
      focusedArrow = arrow
      return
    }
    openEnd = end
    const isEmpty = !scoreFor(scores, end, arrow)
    focusedArrow = isEmpty ? (firstEmptyArrowInEnd(end) ?? arrow) : arrow
  }

  function closePad() {
    openEnd = null
    focusedArrow = null
  }

  function onKey(keyId: string) {
    if (openEnd === null || focusedArrow === null || !onScore) return
    onScore(openEnd, focusedArrow, keyId)
    if (focusedArrow === match.arrowsPerEnd) closePad()
    else focusedArrow += 1
  }
</script>

<div class="scorecard">
  {#each Array(match.ends) as _, index}
    {@const end = index + 1}
    <div class="end-block" class:active={interactive && openEnd === end} class:group-divider={isGroupDivider(match, end)}>
      <div class="end-row">
        <span class="end-number">{end}</span>
        <div class="arrows">
          {#each Array(match.arrowsPerEnd) as _, arrowIndex}
            {@const arrow = arrowIndex + 1}
            {@const entry = scoreFor(scores, end, arrow)}
            {@const key = keyFor(entry?.keyId)}
            <button
              type="button"
              class="arrow {key ? colorClass(key.color) : 'empty'}"
              class:focused={interactive && openEnd === end && focusedArrow === arrow}
              class:readonly={!interactive}
              disabled={!interactive}
              on:click={() => onArrowClick(end, arrow)}
            >{key ? key.label : '–'}</button>
          {/each}
        </div>
        <span class="end-total">{endTotal(scores, end)}</span>
        <div class="totals-cell">
          {#if groups}
            <span class="running-total">{groupRunningTotal(match, scores, end)}</span>
            <span class="group-total">{runningTotal(scores, end)}</span>
          {:else}
            <span class="running-total">{runningTotal(scores, end)}</span>
          {/if}
        </div>
      </div>
      {#if interactive && openEnd === end}
        <ArrowKeyPad keys={entryKeys} {onKey} onClose={closePad} {closeLabel} />
      {/if}
    </div>
  {/each}
</div>

<style>
  .scorecard {
    display: flex;
    flex-direction: column;
  }

  .end-block {
    border-bottom: 1px solid var(--line);
    padding: 8px 0;
  }

  .end-block.active {
    background: var(--tint, rgba(22, 74, 19, 0.05));
    border-radius: 6px;
  }

  .end-block.group-divider {
    border-bottom: 3px solid var(--green);
  }

  .end-row {
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    gap: 10px;
  }

  .end-number {
    min-width: 28px;
    font-weight: 700;
    color: var(--muted);
    text-align: center;
  }

  .arrows {
    display: flex;
    gap: 6px;
    flex-wrap: wrap;
  }

  .arrow {
    min-width: 38px;
    height: 38px;
    border-radius: 8px;
    border: 2px solid rgba(0, 0, 0, 0.15);
    font-weight: 700;
    font-size: 14px;
    cursor: pointer;
  }

  .arrow.readonly {
    cursor: default;
  }

  .arrow:disabled {
    opacity: 1;
  }

  .arrow.empty {
    background: #e6e6e6;
    color: #888888;
  }

  .arrow.key-yellow {
    background: #fdc621;
    color: #4a3b00;
  }

  .arrow.key-red {
    background: #e8755b;
    color: #4a1207;
  }

  .arrow.key-blue {
    background: #9fc3e8;
    color: #0f2d4a;
  }

  .arrow.key-black {
    background: #2b2b2b;
    color: #ffffff;
  }

  .arrow.key-white {
    background: #f4f4f4;
    color: #333333;
    border-color: rgba(0, 0, 0, 0.25);
  }

  .arrow.focused {
    outline: 3px solid var(--green);
    outline-offset: 2px;
  }

  .end-total {
    margin-left: auto;
    font-weight: 700;
    min-width: 36px;
    text-align: right;
  }

  .totals-cell {
    display: flex;
    flex-direction: column;
    align-items: flex-end;
    min-width: 44px;
    text-align: right;
    line-height: 1.15;
  }

  .running-total {
    font-weight: 700;
  }

  .group-total {
    font-size: 0.75em;
    color: var(--muted);
  }
</style>
