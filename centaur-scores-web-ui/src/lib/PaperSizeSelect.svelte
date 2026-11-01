<script lang="ts">
  import { paperLabel, paperSizes, type PaperSize } from './paperSize'

  // The paper size field of a print dialog (see paperSize.ts), with a reminder to pick the same paper in the
  // browser's own print dialog.
  export let value: PaperSize
  export let labels: Record<string, string>
</script>

<label>
  {labels.printPaperSizeLabel}
  <select bind:value>
    {#each paperSizes as paper}<option value={paper}>{paperLabel(paper)}</option>{/each}
  </select>
  <span class="muted paper-hint">{labels.printPaperSizeHint}</span>
</label>

<style>
  .paper-hint {
    font-size: 0.85em;
  }
</style>
