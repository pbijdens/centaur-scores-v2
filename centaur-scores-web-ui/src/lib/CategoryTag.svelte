<script lang="ts">
  import { tagColor, valueName } from './laneSheet'
  import type { Category } from './types'

  // A category value as a coloured rounded tag (colour fixed per value, see laneSheet.tagColor); a
  // missing value renders as a dashed placeholder so it stands out rather than disappearing.
  export let category: Category
  export let valueId: number | undefined
  export let offset = 0
  export let missingLabel: string

  $: name = valueName(category, valueId)
  $: color = valueId !== undefined ? tagColor(category, valueId, offset) : null
</script>

{#if name && color}
  <span class="category-tag" style:background={color.background} style:color={color.foreground} title={category.name}>{name}</span>
{:else}
  <span class="category-tag missing" title={category.name}>{missingLabel}</span>
{/if}

<style>
  .category-tag {
    display: inline-block;
    padding: 2px 8px;
    border-radius: 999px;
    font-size: 12px;
    font-weight: 600;
    line-height: 1.4;
    white-space: nowrap;
    print-color-adjust: exact;
    -webkit-print-color-adjust: exact;
  }

  .category-tag.missing {
    border: 1px dashed var(--muted, #777);
    color: var(--muted, #777);
    font-weight: 400;
  }
</style>
