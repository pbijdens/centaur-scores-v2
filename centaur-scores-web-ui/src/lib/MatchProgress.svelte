<script lang="ts">
  import { groupBoundaryPercents, typicalEndsCompleted } from './matchProgress'
  import type { LiveScoringPage } from './types'

  export let page: LiveScoringPage
  export let label: string

  $: completed = typicalEndsCompleted(page)
  $: boundaries = groupBoundaryPercents(page)
  $: fillPercent = page.ends > 0 ? Math.min(100, (completed / page.ends) * 100) : 0
</script>

{#if page.ends > 0}
  <div class="match-progress" role="progressbar" aria-label={label} aria-valuemin="0" aria-valuemax={page.ends} aria-valuenow={completed}>
    <div class="progress-track">
      <div class="progress-fill" style="width: {fillPercent}%"></div>
      {#each boundaries as percent}<span class="progress-tick" style="left: {percent}%"></span>{/each}
    </div>
    <span class="progress-text">{completed} / {page.ends}</span>
  </div>
{/if}

<style>
  .match-progress {
    display: flex;
    align-items: center;
    gap: .6vw;
    width: 100%;
  }

  .progress-track {
    position: relative;
    flex: 1;
    min-width: 0;
    height: .9vh;
    min-height: 4px;
    border-radius: 999px;
    background: #dfe4db;
    overflow: hidden;
  }

  .progress-fill {
    height: 100%;
    background: #164a13;
    border-radius: 999px;
    transition: width .3s ease;
  }

  .progress-tick {
    position: absolute;
    top: 0;
    bottom: 0;
    width: 1px;
    background: rgba(255, 255, 255, .8);
  }

  .progress-text {
    flex: 0 0 auto;
    font-variant-numeric: tabular-nums;
    font-weight: 600;
    font-size: clamp(10px, 1.5vh, 18px);
  }
</style>
