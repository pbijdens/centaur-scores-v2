<script lang="ts">
  export let eyebrow = ''
  export let title = ''
  export let description = ''
  export let action = ''
  export let items: { id: string; name: string; description: string; status?: string }[] = []
  export let labels: Record<string, string>
  export let onAdd: () => void = () => undefined
</script>

<div class="page-intro">
  <div><p class="eyebrow">{eyebrow}</p><h1>{title}</h1><p class="muted">{description}</p></div>
  <button class="primary" on:click={onAdd}>{action}</button>
</div>
<section class="list-panel management-list">
  {#if items.length === 0}<p class="empty-state">{labels.emptyState}</p>{/if}
  {#each items as item}
    <button class="list-row"><span class="management-icon">◇</span><span><strong>{item.name}</strong><small>{item.description}</small></span><span class="tag">{item.status ?? labels.statusReady}</span><span class="arrow">→</span></button>
  {/each}
</section>
