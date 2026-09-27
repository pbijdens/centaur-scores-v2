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
  <h1 class="report-title">{competitionName}</h1>
  <div class="report-dates">
    <p>{formatLocalDate(startDate, language)} – {formatLocalDate(endDate, language)}</p>
    <p class="report-date">{labels.resultsPrintedOnLabel} {formatLocalDate(today, language)}</p>
  </div>
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
    font-size: 20pt;
    line-height: 1.1;
    margin: 0;
  }

  .report-dates {
    flex: 0 0 auto;
    align-self: flex-end;
    text-align: right;
  }

  p {
    margin: 0;
    font-size: 8pt;
    white-space: nowrap;
  }

  .report-date {
    color: #333;
  }
</style>
