<script lang="ts">
  import Icon from './Icon.svelte';
  import { t } from '../lib/i18n';
  import type { ScorekeeperKey } from '../lib/types';

  interface Props {
    keys: ScorekeeperKey[];
    onKey: (keyId: string) => void;
    onDelete: () => void;
    onHide: () => void;
  }

  let { keys, onKey, onDelete, onHide }: Props = $props();

  function colorClass(color: string): string {
    return `key-${color.toLowerCase()}`;
  }
</script>

<div class="keyboard">
  {#each keys as key (key.id)}
    <button class="key {colorClass(key.color)}" onclick={() => onKey(key.id)}>
      {key.label}
    </button>
  {/each}
  <button class="key control" onclick={onDelete} aria-label={$t('deleteKey')}>
    <Icon name="backspace" size={26} />
  </button>
  <button class="key control" onclick={onHide} aria-label={$t('hideKeyboard')}>
    <Icon name="hide-keyboard" size={26} />
  </button>
</div>

<style lang="scss">
  @use '../styles/variables' as v;

  .keyboard {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(3.4rem, 1fr));
    gap: 0.5rem;
    padding: 0.75rem;
    background: v.$color-bg;
    border: 2px solid v.$color-primary;
    border-top: none;
    border-radius: 0 0 v.$radius v.$radius;
  }

  .key {
    min-height: 3.4rem;
    border-radius: v.$radius;
    border: 2px solid rgba(0, 0, 0, 0.15);
    font-size: v.$font-size-large;
    font-weight: 700;
    display: flex;
    align-items: center;
    justify-content: center;

    &.key-yellow {
      background: v.$key-yellow-bg;
      color: v.$key-yellow-fg;
    }
    &.key-red {
      background: v.$key-red-bg;
      color: v.$key-red-fg;
    }
    &.key-blue {
      background: v.$key-blue-bg;
      color: v.$key-blue-fg;
    }
    &.key-black {
      background: v.$key-black-bg;
      color: v.$key-black-fg;
    }
    &.key-white {
      background: v.$key-white-bg;
      color: v.$key-white-fg;
    }

    &.control {
      background: v.$color-primary;
      color: #fff;
      border-color: v.$color-primary-dark;
    }
  }
</style>
