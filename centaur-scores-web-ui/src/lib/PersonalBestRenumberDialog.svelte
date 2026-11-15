<script lang="ts">
  // Shown when a participant list member's federation number is changed on save while the personal best
  // lists (of the owning, possibly super, tenant) still hold entries under the old number: lists a sample of
  // the most recent entries and lets the admin save with or without moving them to the new number. A native
  // modal <dialog>, like PrintDialog; Escape/cancel aborts the save entirely.
  import { formatLocalDate } from './date'
  import type { Language, MemberPersonalBestEntries } from './types'

  export let open = false
  export let entries: MemberPersonalBestEntries | null
  export let newNumber: string
  export let labels: Record<string, string>
  export let language: Language
  export let onAccept: () => void
  export let onDecline: () => void

  let dialogEl: HTMLDialogElement

  $: if (dialogEl) {
    if (open && !dialogEl.open) dialogEl.showModal()
    else if (!open && dialogEl.open) dialogEl.close()
  }

  $: hasNewNumber = newNumber.trim() !== ''
  $: intro = (hasNewNumber ? labels.personalBestRenumberIntro : labels.personalBestRenumberNoNewNumber)
    .replace('{count}', String(entries?.totalCount ?? 0))
    .replace('{old}', entries?.federationNumber ?? '')
    .replace('{new}', newNumber.trim())
</script>

<dialog class="renumber-dialog" bind:this={dialogEl} on:close={() => (open = false)}>
  <div class="renumber-dialog-content">
    <h2>{labels.personalBestRenumberTitle}</h2>
    <div class="renumber-dialog-body">
      <p>{intro}</p>
      {#if entries && entries.recent.length > 0}
        <p>{labels.personalBestRenumberSample.replace('{shown}', String(entries.recent.length))}</p>
        <div class="renumber-entries">
          <table>
            <thead>
              <tr>
                <th>{labels.dateLabel}</th>
                <th>{labels.scoreLabel}</th>
                <th>{labels.disciplineLabel}</th>
                <th>{labels.matchClassifierLabel}</th>
              </tr>
            </thead>
            <tbody>
              {#each entries.recent as entry}
                <tr>
                  <td>{formatLocalDate(entry.date, language)}</td>
                  <td>{entry.score}</td>
                  <td>{entry.discipline}</td>
                  <td>{entry.matchClassifier}</td>
                </tr>
              {/each}
            </tbody>
          </table>
        </div>
      {/if}
    </div>
    <div class="renumber-dialog-actions">
      <button type="button" class="text-button" on:click={() => (open = false)}>{labels.cancel}</button>
      <button type="button" on:click={onDecline}>{labels.personalBestRenumberDecline}</button>
      {#if hasNewNumber}
        <button type="button" class="primary" on:click={onAccept}>{labels.personalBestRenumberAccept}</button>
      {/if}
    </div>
  </div>
</dialog>

<style>
  .renumber-dialog {
    width: min(640px, calc(100% - 24px));
    max-height: calc(100svh - 24px);
    padding: 0;
    border: 1px solid var(--line);
    border-radius: 12px;
    background: var(--paper);
    color: var(--ink);
  }

  .renumber-dialog::backdrop {
    background: rgba(20, 33, 15, 0.45);
  }

  .renumber-dialog-content {
    display: flex;
    flex-direction: column;
    max-height: calc(100svh - 26px);
  }

  h2 {
    margin: 0;
    padding: 20px 24px 12px;
  }

  .renumber-dialog-body {
    display: flex;
    flex-direction: column;
    gap: 8px;
    min-height: 0;
    padding: 4px 24px 20px;
  }

  .renumber-dialog-body p {
    margin: 0;
  }

  /* The sample list scrolls on its own so the question and buttons stay in view. */
  .renumber-entries {
    max-height: 40svh;
    overflow-y: auto;
    border: 1px solid var(--line);
    border-radius: 8px;
  }

  table {
    width: 100%;
    border-collapse: collapse;
  }

  th,
  td {
    padding: 6px 10px;
    text-align: left;
    border-bottom: 1px solid var(--line);
  }

  th {
    position: sticky;
    top: 0;
    background: var(--paper);
  }

  .renumber-dialog-actions {
    display: flex;
    flex-wrap: wrap;
    justify-content: flex-end;
    align-items: center;
    gap: 12px;
    padding: 16px 24px;
    border-top: 1px solid var(--line);
  }
</style>
