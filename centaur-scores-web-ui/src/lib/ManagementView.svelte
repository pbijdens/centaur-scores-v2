<script lang="ts">
  export let eyebrow = 'TENANT DATA'
  export let title = ''
  export let description = ''
  export let action = '+ Add'
  export let items: { id: string; name: string; description: string; status?: string }[] = []
  export let onAdd: () => void = () => undefined
</script>

<div class="page-intro">
  <div><p class="eyebrow">{eyebrow}</p><h1>{title}</h1><p class="muted">{description}</p></div>
  <button class="primary" on:click={onAdd}>{action}</button>
</div>
<section class="list-panel management-list">
  {#if items.length === 0}<p class="empty-state">Nothing has been configured yet.</p>{/if}
  {#each items as item}
    <button class="list-row"><span class="management-icon">◇</span><span><strong>{item.name}</strong><small>{item.description}</small></span><span class="tag">{item.status ?? 'READY'}</span><span class="arrow">→</span></button>
  {/each}
</section>
