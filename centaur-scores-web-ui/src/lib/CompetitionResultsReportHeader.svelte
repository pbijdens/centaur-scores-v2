<script lang="ts">
  import { formatLocalDate } from './date'
  import type { Language } from './types'

  export let competitionName: string
  export let startDate: string
  export let endDate: string
  export let logoUrl: string | null | undefined = undefined
  export let language: Language
  export let labels: Record<string, string>

  const now = new Date()
  const today = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`
</script>

<!-- A div, not <header>: the global header rule in app.scss (the app bar) would add padding, height and stickiness. -->
<div class="report-header">
  {#if logoUrl}<img class="report-logo" src={logoUrl} alt="" />{/if}
  <div class="report-title">
    <h1>{competitionName}</h1>
    <p>{formatLocalDate(startDate, language)} – {formatLocalDate(endDate, language)}</p>
  </div>
  <p class="report-date">{labels.resultsPrintedOnLabel} {formatLocalDate(today, language)}</p>
</div>

<style>
  .report-header {
    display: flex;
    align-items: center;
    gap: 3mm;
    border-bottom: 2px solid #000;
    padding-bottom: 2mm;
    margin-bottom: 3mm;
    color: #000;
  }

  .report-logo {
    height: 14mm;
    width: auto;
    flex: 0 0 auto;
  }

  .report-title {
    flex: 1;
    min-width: 0;
  }

  h1 {
    font-size: 15pt;
    line-height: 1.15;
    margin: 0;
  }

  p {
    margin: 0;
    font-size: 9pt;
  }

  .report-date {
    align-self: flex-end;
    white-space: nowrap;
    color: #333;
  }
</style>
