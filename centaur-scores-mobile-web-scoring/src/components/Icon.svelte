<script lang="ts">
  interface Props {
    name:
      | 'home'
      | 'flag-nl'
      | 'flag-en'
      | 'thumbs-up'
      | 'wifi'
      | 'error'
      | 'ellipsis'
      | 'plus'
      | 'edit'
      | 'trash'
      | 'backspace'
      | 'hide-keyboard'
      | 'search'
      | 'chevron-left'
      | 'chevron-right'
      | 'close';
    size?: number;
  }
  let { name, size = 24 }: Props = $props();
</script>

<svg width={size} height={size} viewBox="0 0 24 24" fill="none" aria-hidden="true" focusable="false">
  {#if name === 'home'}
    <path d="M3 11.5 12 4l9 7.5" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" />
    <path d="M5 10v9a1 1 0 0 0 1 1h4v-6h4v6h4a1 1 0 0 0 1-1v-9" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" />
  {:else if name === 'flag-nl'}
    <rect x="2" y="4" width="20" height="16" rx="2" fill="#fff" />
    <rect x="2" y="4" width="20" height="16" rx="2" fill="none" stroke="#999" stroke-width="1" />
    <rect x="2" y="4" width="20" height="5.33" fill="#AE1C28" />
    <rect x="2" y="14.66" width="20" height="5.34" fill="#21468B" />
  {:else if name === 'flag-en'}
    <rect x="2" y="4" width="20" height="16" rx="2" fill="#00247d" />
    <path d="M2 4 22 20M22 4 2 20" stroke="#fff" stroke-width="2" />
    <path d="M12 4v16M2 12h20" stroke="#fff" stroke-width="3" />
    <path d="M12 4v16M2 12h20" stroke="#cf142b" stroke-width="1.4" />
  {:else if name === 'thumbs-up'}
    <path d="M7 11v9H4a1 1 0 0 1-1-1v-7a1 1 0 0 1 1-1h3Zm0 0 4.5-7a2 2 0 0 1 3.6 1.4L14 9h4.2a2 2 0 0 1 1.94 2.5l-1.6 6.5A2 2 0 0 1 16.6 20H10a3 3 0 0 1-3-3v-6Z" stroke="currentColor" stroke-width="1.6" stroke-linejoin="round" fill="currentColor" fill-opacity="0.08" />
  {:else if name === 'wifi'}
    <path d="M2 8.5a15 15 0 0 1 20 0M5.2 12a10.5 10.5 0 0 1 13.6 0M8.6 15.5a6 6 0 0 1 6.8 0" stroke="currentColor" stroke-width="2" stroke-linecap="round" />
    <circle cx="12" cy="19" r="1.4" fill="currentColor" />
  {:else if name === 'error'}
    <circle cx="12" cy="12" r="9.5" stroke="currentColor" stroke-width="2" />
    <path d="M12 7v6" stroke="currentColor" stroke-width="2" stroke-linecap="round" />
    <circle cx="12" cy="16.5" r="1.2" fill="currentColor" />
  {:else if name === 'ellipsis'}
    <circle cx="6" cy="12" r="1.8" fill="currentColor" />
    <circle cx="12" cy="12" r="1.8" fill="currentColor" />
    <circle cx="18" cy="12" r="1.8" fill="currentColor" />
  {:else if name === 'plus'}
    <path d="M12 5v14M5 12h14" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" />
  {:else if name === 'edit'}
    <path d="M4 20h4L18.5 9.5a2.1 2.1 0 0 0-3-3L5 17v3Z" stroke="currentColor" stroke-width="1.8" stroke-linejoin="round" />
  {:else if name === 'trash'}
    <path d="M5 7h14M9 7V5a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1v2m-8 0 1 13a1 1 0 0 0 1 1h6a1 1 0 0 0 1-1l1-13" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" />
  {:else if name === 'backspace'}
    <path d="M9 6h10a1 1 0 0 1 1 1v10a1 1 0 0 1-1 1H9l-6-6a1 1 0 0 1 0-2Z" stroke="currentColor" stroke-width="1.8" stroke-linejoin="round" />
    <path d="M13 10.5 17 14.5M17 10.5 13 14.5" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" />
  {:else if name === 'hide-keyboard'}
    <rect x="2.5" y="5" width="19" height="12" rx="2" stroke="currentColor" stroke-width="1.8" />
    <path d="M6 9h.01M10 9h.01M14 9h.01M18 9h.01M8 13h8" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" />
    <path d="M9 20.5 12 18l3 2.5" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" />
  {:else if name === 'search'}
    <circle cx="10.5" cy="10.5" r="6.5" stroke="currentColor" stroke-width="2" />
    <path d="M20 20 15.5 15.5" stroke="currentColor" stroke-width="2" stroke-linecap="round" />
  {:else if name === 'chevron-left'}
    <path d="M15 5 8 12l7 7" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" />
  {:else if name === 'chevron-right'}
    <path d="M9 5l7 7-7 7" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" />
  {:else if name === 'close'}
    <path d="M6 6l12 12M18 6 6 18" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" />
  {/if}
</svg>
