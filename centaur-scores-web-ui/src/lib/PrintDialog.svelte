<script lang="ts">
  // The options dialog behind a printable page's "Print this" button (printAction in pageActions.ts):
  // the page puts its print-only options in the slot and prints from onPrint. A native modal <dialog>,
  // so focus handling and Escape-to-cancel are the browser's own.
  export let open = false
  export let title: string
  export let labels: Record<string, string>
  export let printDisabled = false
  export let onPrint: () => void

  let dialogEl: HTMLDialogElement

  $: if (dialogEl) {
    if (open && !dialogEl.open) dialogEl.showModal()
    else if (!open && dialogEl.open) dialogEl.close()
  }
</script>

<dialog class="print-dialog" bind:this={dialogEl} on:close={() => (open = false)}>
  <form method="dialog" on:submit|preventDefault={onPrint}>
    <h2>{title}</h2>
    <div class="print-dialog-body"><slot /></div>
    <div class="print-dialog-actions">
      <button type="button" class="text-button" on:click={() => (open = false)}>{labels.cancel}</button>
      <button type="submit" class="primary" disabled={printDisabled}>🖨 {labels.printButton}</button>
    </div>
  </form>
</dialog>

<style>
  .print-dialog {
    width: min(640px, calc(100% - 24px));
    max-height: calc(100svh - 24px);
    padding: 0;
    border: 1px solid var(--line);
    border-radius: 12px;
    background: var(--paper);
    color: var(--ink);
  }

  .print-dialog::backdrop {
    background: rgba(20, 33, 15, 0.45);
  }

  form {
    display: flex;
    flex-direction: column;
    max-height: calc(100svh - 26px);
  }

  h2 {
    margin: 0;
    padding: 20px 24px 12px;
  }

  .print-dialog-body {
    display: grid;
    gap: 20px;
    padding: 4px 24px 20px;
    overflow-y: auto;
  }

  .print-dialog-actions {
    display: flex;
    justify-content: flex-end;
    align-items: center;
    gap: 16px;
    padding: 16px 24px;
    border-top: 1px solid var(--line);
  }
</style>
