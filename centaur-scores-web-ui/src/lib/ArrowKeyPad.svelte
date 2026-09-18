<script lang="ts">
  import type { KeyboardKey } from './types'

  export let keys: KeyboardKey[]
  export let onKey: (keyId: string) => void
  export let onClose: (() => void) | null = null
  export let closeLabel = 'Close'

  function colorClass(color: string): string {
    return `key-${color.toLowerCase()}`
  }
</script>

<div class="arrow-key-pad">
  {#each keys as key (key.keyId)}
    <button type="button" class="key {colorClass(key.color)}" on:click={() => onKey(key.keyId)}>{key.label}</button>
  {/each}
  {#if onClose}
    <button type="button" class="key key-close" on:click={onClose} aria-label={closeLabel}>✕</button>
  {/if}
</div>

<style>
  .arrow-key-pad {
    display: flex;
    flex-wrap: wrap;
    gap: 8px;
    padding: 10px 0 4px;
  }

  .key {
    min-width: 44px;
    height: 44px;
    padding: 0 8px;
    border-radius: 8px;
    border: 2px solid rgba(0, 0, 0, 0.15);
    font-weight: 700;
    font-size: 16px;
    cursor: pointer;
  }

  .key-yellow {
    background: #fdc621;
    color: #4a3b00;
  }

  .key-red {
    background: #e8755b;
    color: #4a1207;
  }

  .key-blue {
    background: #9fc3e8;
    color: #0f2d4a;
  }

  .key-black {
    background: #2b2b2b;
    color: #ffffff;
  }

  .key-white {
    background: #f4f4f4;
    color: #333333;
    border-color: rgba(0, 0, 0, 0.25);
  }

  .key-close {
    background: var(--paper);
    color: var(--muted);
    border-color: var(--line);
  }
</style>
