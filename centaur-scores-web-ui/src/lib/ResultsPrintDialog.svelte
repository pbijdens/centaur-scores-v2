<script lang="ts">
  import PrintDialog from './PrintDialog.svelte'
  import { resultsFontScales, type ResultsFontScale, type ResultsLayoutOptions } from './resultsLayout'

  // The print options of a results report (see ResultsReport.svelte): layout mode, an extra detail line and the font
  // size. Edits a copy of `options`; onPrint receives the chosen options.
  export let open = false
  export let options: ResultsLayoutOptions
  export let labels: Record<string, string>
  export let onPrint: (options: ResultsLayoutOptions) => void

  let mode: ResultsLayoutOptions['mode'] = 'stream'
  let extraLine = false
  let fontScale: ResultsFontScale = 1
  let wasOpen = false

  $: if (open !== wasOpen) {
    wasOpen = open
    if (open) {
      mode = options.mode
      extraLine = options.detailLines === 3
      fontScale = options.fontScale
    }
  }

  function scaleLabel(scale: ResultsFontScale): string {
    const percent = `${Math.round(scale * 100)}%`
    if (scale === 1) return `${percent} (${labels.resultsFontSizeNormal})`
    if (scale === 1.41) return `${percent} (${labels.resultsFontSizeA3})`
    return percent
  }
</script>

<PrintDialog bind:open title={labels.printOptionsTitle} {labels} onPrint={() => onPrint({ mode, detailLines: extraLine ? 3 : 2, fontScale })}>
  <label>
    {labels.resultsLayoutLabel}
    <select bind:value={mode}>
      <option value="stream">{labels.resultsLayoutStream}</option>
      <option value="pages">{labels.resultsLayoutPages}</option>
    </select>
  </label>
  <label>
    {labels.resultsFontSizeLabel}
    <select bind:value={fontScale}>
      {#each resultsFontScales as scale}<option value={scale}>{scaleLabel(scale)}</option>{/each}
    </select>
  </label>
  <label class="checkbox-label"><input type="checkbox" bind:checked={extraLine} /> {labels.resultsExtraDetailLine}</label>
</PrintDialog>
