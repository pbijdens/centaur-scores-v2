<script lang="ts">
  export let labels: Record<string, string>
  export let canMoveUp = true
  export let canMoveDown = true
  export let onMoveUp: () => void
  export let onMoveDown: () => void
  export let onDelete: (() => void) | null = null
  export let deleteLabel: string | null = null
  export let pushRight = true
</script>

<div class="row-actions" class:push-right={pushRight}>
  <button class="icon-button" aria-label={labels.moveUp} disabled={!canMoveUp} on:click={onMoveUp}>↑</button>
  <button class="icon-button" aria-label={labels.moveDown} disabled={!canMoveDown} on:click={onMoveDown}>↓</button>
  {#if onDelete}
    <button class="icon-button danger-icon-button" aria-label={deleteLabel ?? labels.removeValue} on:click={onDelete}>🗑</button>
  {/if}
</div>

<style>
  .row-actions {
    display: flex;
    flex-wrap: wrap;
    justify-content: flex-end;
    gap: 10px;
    align-items: center;
    flex: 0 0 auto;
  }

  .row-actions.push-right {
    margin-left: auto;
  }

  .row-actions .icon-button {
    display: grid;
    place-items: center;
    flex: 0 0 44px;
    width: 44px;
    height: 44px;
    margin-left: 0;
    padding: 0;
    border: 1px solid var(--line);
    background: var(--paper);
  }

  .row-actions .icon-button:hover,
  .row-actions .icon-button:focus-visible {
    color: var(--green);
    border-color: var(--green);
    background: var(--neutral);
  }

  .row-actions .danger-icon-button:hover,
  .row-actions .danger-icon-button:focus-visible {
    color: #b84232;
    border-color: #e8755b;
    background: #fdeeea;
  }

  @media (max-width: 720px) {
    .row-actions.push-right {
      width: 100%;
      justify-content: flex-end;
      margin-left: 0;
    }
  }
</style>
