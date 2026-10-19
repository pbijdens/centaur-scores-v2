<script lang="ts">
  import DropdownMenu from './DropdownMenu.svelte'
  import type { PageAction } from './pageActions'
  import { navigateOnClick } from './router'

  // The shared page header: a back link, an eyebrow naming the active top-level section, the title and
  // an optional subtitle, then the page's actions. Actions with `header` set render as buttons next to
  // the title; every action is also listed in the ⋯ menu, which is only shown when at least one action
  // is menu-only (otherwise it would merely repeat the visible buttons).
  export let eyebrow: string
  export let title: string
  export let subtitle: string | null | undefined = undefined
  export let backLabel: string | null | undefined = undefined
  export let onBack: (() => void) | undefined = undefined
  export let actions: PageAction[] = []
  export let menuLabel: string = ''

  let expandedGroups: Record<string, boolean> = {}

  $: headerActions = actions.filter((action) => action.header)
  $: showMenu = actions.some((action) => !action.header)

  function headerClass(action: PageAction): string {
    if (action.header === 'highlight') return action.active ? 'highlight-button is-live' : 'highlight-button'
    if (action.header === 'icon') return 'icon-action-button'
    if (action.header === 'text') return 'text-button'
    return 'primary'
  }

  function toggleGroup(event: MouseEvent, label: string) {
    // Keeps the ⋯ menu open: DropdownMenu closes on any click that bubbles up to its panel.
    event.stopPropagation()
    expandedGroups = { ...expandedGroups, [label]: !expandedGroups[label] }
  }
</script>

{#if backLabel && onBack}<button class="back-link" on:click={onBack}>← {backLabel}</button>{/if}
<div class="page-intro">
  <div>
    <p class="eyebrow">{eyebrow}</p>
    <h1>{title}</h1>
    {#if subtitle}<p class="muted">{subtitle}</p>{/if}
    <slot />
  </div>
  {#if headerActions.length > 0 || showMenu}
    <div class="match-header-actions">
      {#each headerActions as action}
        {#if action.children}
          <DropdownMenu ariaLabel={action.label} buttonClass={headerClass(action)} align="right">
            <svelte:fragment slot="trigger">{action.headerLabel ?? action.label}</svelte:fragment>
            {#each action.children as child}
              {#if child.href}
                <a class="menu-item" href={child.href} target={child.newTab ? '_blank' : undefined} rel={child.newTab ? 'noopener' : undefined}>{child.label}</a>
              {:else}
                <button class="menu-item" disabled={child.disabled} on:click={child.onClick}>{child.label}</button>
              {/if}
            {/each}
          </DropdownMenu>
        {:else if action.href && action.newTab}
          <a class={headerClass(action)} href={action.href} target="_blank" rel="noopener" aria-label={action.label} title={action.label}>{action.headerLabel ?? action.label}</a>
        {:else if action.href}
          <a class={headerClass(action)} href={action.href} on:click={(event) => action.onClick && navigateOnClick(event, action.onClick)}>{action.headerLabel ?? action.label}</a>
        {:else}
          <button class={headerClass(action)} disabled={action.disabled} aria-label={action.label} title={action.label} on:click={action.onClick}>{action.headerLabel ?? action.label}</button>
        {/if}
      {/each}
      {#if showMenu}
        <DropdownMenu ariaLabel={menuLabel} buttonClass="actions-trigger" align="right">
          <svelte:fragment slot="trigger">⋯</svelte:fragment>
          {#each actions as action, index}
            {#if action.separatorBefore && index > 0}<hr class="menu-separator" />{/if}
            {#if action.children}
              <button class="menu-item menu-group-toggle" aria-expanded={!!expandedGroups[action.label]} on:click={(event) => toggleGroup(event, action.label)}>
                <span>{action.label}</span><span aria-hidden="true">{expandedGroups[action.label] ? '▾' : '›'}</span>
              </button>
              {#if expandedGroups[action.label]}
                {#each action.children as child}
                  {#if child.href}
                    <a class="menu-item menu-subitem" href={child.href} target={child.newTab ? '_blank' : undefined} rel={child.newTab ? 'noopener' : undefined}>{child.label}</a>
                  {:else}
                    <button class="menu-item menu-subitem" disabled={child.disabled} on:click={child.onClick}>{child.label}</button>
                  {/if}
                {/each}
              {/if}
            {:else if action.href && action.newTab}
              <a class="menu-item" href={action.href} target="_blank" rel="noopener">{action.label}</a>
            {:else if action.href}
              <a class="menu-item" class:menu-item-danger={action.danger} href={action.href} on:click={(event) => action.onClick && navigateOnClick(event, action.onClick)}>{action.label}</a>
            {:else}
              <button class="menu-item" class:menu-item-danger={action.danger} disabled={action.disabled} on:click={action.onClick}>{action.label}</button>
            {/if}
          {/each}
        </DropdownMenu>
      {/if}
    </div>
  {/if}
</div>
