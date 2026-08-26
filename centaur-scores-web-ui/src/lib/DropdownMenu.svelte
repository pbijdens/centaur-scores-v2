<script lang="ts">
  export let ariaLabel: string
  export let buttonClass = 'menu-trigger'
  export let align: 'left' | 'right' = 'right'

  let open = false
  let rootEl: HTMLDivElement

  function toggle() {
    open = !open
  }

  function close() {
    open = false
  }

  function handleWindowClick(event: MouseEvent) {
    if (open && rootEl && !rootEl.contains(event.target as Node)) close()
  }

  function handleKeydown(event: KeyboardEvent) {
    if (event.key === 'Escape') close()
  }
</script>

<svelte:window on:click={handleWindowClick} on:keydown={handleKeydown} />

<div class="dropdown-menu" bind:this={rootEl}>
  <button
    type="button"
    class={buttonClass}
    aria-haspopup="true"
    aria-expanded={open}
    aria-label={ariaLabel}
    on:click|stopPropagation={toggle}
  >
    <slot name="trigger" />
  </button>
  {#if open}
    <!-- svelte-ignore a11y_click_events_have_key_events -->
    <!-- svelte-ignore a11y_no_static_element_interactions -->
    <div class="dropdown-panel" class:align-left={align === 'left'} on:click={close}>
      <slot />
    </div>
  {/if}
</div>

<style>
  .dropdown-menu {
    position: relative;
    display: inline-flex;
  }

  .dropdown-panel {
    position: absolute;
    top: calc(100% + 8px);
    right: 0;
    z-index: 100;
    display: flex;
    flex-direction: column;
    min-width: 220px;
    max-width: min(88vw, 320px);
    background: var(--paper);
    border: 1px solid var(--line);
    box-shadow: 0 14px 32px rgba(20, 33, 15, .14);
    padding: 6px;
  }

  .dropdown-panel.align-left {
    right: auto;
    left: 0;
  }
</style>
