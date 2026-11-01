<script lang="ts">
  import { formatLocalDate } from './date'
  import type { Language } from './types'

  // The heading of a printed results report (see resultsLayout.ts); every size follows the inherited --font-scale.
  export let title: string
  export let startDate: string
  // Omitted for a single-day report (a match).
  export let endDate: string | null | undefined = undefined
  export let logoUrl: string | null | undefined = undefined
  export let language: Language
  export let labels: Record<string, string>

  const now = new Date()
  const today = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`
</script>

<!-- A div, not <header>: the global header rule in app.scss (the app bar) would add padding, height and stickiness. -->
<div class="report-header">
  {#if logoUrl}<img class="report-logo" src={logoUrl} alt="" />{/if}
  <h1 class="report-title">{title}</h1>
  <div class="report-dates">
    <p>{formatLocalDate(startDate, language)}{endDate ? ` – ${formatLocalDate(endDate, language)}` : ''}</p>
    <p class="report-date">{labels.resultsPrintedOnLabel} {formatLocalDate(today, language)}</p>
  </div>
</div>

<style>
  .report-header {
    display: flex;
    align-items: center;
    gap: calc(3mm * var(--font-scale, 1));
    border-bottom: 2px solid #000;
    padding-bottom: calc(2mm * var(--font-scale, 1));
    margin-bottom: calc(3mm * var(--font-scale, 1));
    color: #000;
  }

  .report-logo {
    height: calc(14mm * var(--font-scale, 1));
    width: auto;
    flex: 0 0 auto;
  }

  .report-title {
    flex: 1;
    min-width: 0;
    font-size: calc(20pt * var(--font-scale, 1));
    line-height: 1.1;
    margin: 0;
    /* At most two lines: PDF mode reserves room for that on every page (resultsLayout.ts). */
    display: -webkit-box;
    -webkit-box-orient: vertical;
    -webkit-line-clamp: 2;
    line-clamp: 2;
    overflow: hidden;
  }

  .report-dates {
    flex: 0 0 auto;
    align-self: flex-end;
    text-align: right;
  }

  p {
    margin: 0;
    font-size: calc(8pt * var(--font-scale, 1));
    white-space: nowrap;
  }

  .report-date {
    color: #333;
  }
</style>
