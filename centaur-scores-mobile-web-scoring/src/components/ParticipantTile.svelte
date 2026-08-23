<script lang="ts">
  import Icon from './Icon.svelte';
  import { t } from '../lib/i18n';
  import { arrowsShot, splitScores, totalScore } from '../lib/scoring';
  import type { ScorekeeperMatch, ScorekeeperMatchParticipant } from '../lib/types';

  interface Props {
    match: ScorekeeperMatch;
    participant: ScorekeeperMatchParticipant;
    index: number;
    swipeEnabled: boolean;
    canEdit: boolean;
    onOpen: () => void;
    onEdit: () => void;
    onRemove: () => void;
  }

  let { match, participant, index, swipeEnabled, canEdit, onOpen, onEdit, onRemove }: Props = $props();

  const ACTION_WIDTH = 70;
  const openOffset = $derived(-ACTION_WIDTH * (canEdit ? 2 : 1));

  let dragging = false;
  let startX = 0;
  let startOffset = 0;
  let offset = $state(0);
  let moved = false;

  const total = $derived(totalScore(match, participant));
  const shot = $derived(arrowsShot(participant));
  const splits = $derived(splitScores(match, participant));

  function clamp(value: number): number {
    return Math.min(0, Math.max(openOffset, value));
  }

  function onPointerDown(event: PointerEvent) {
    if (!swipeEnabled) return;
    dragging = true;
    moved = false;
    startX = event.clientX;
    startOffset = offset;
    (event.currentTarget as HTMLElement).setPointerCapture(event.pointerId);
  }

  function onPointerMove(event: PointerEvent) {
    if (!dragging) return;
    const delta = event.clientX - startX;
    if (Math.abs(delta) > 4) moved = true;
    offset = clamp(startOffset + delta);
  }

  function onPointerUp() {
    if (!dragging) return;
    dragging = false;
    offset = offset < openOffset / 2 ? openOffset : 0;
  }

  function onTileClick() {
    if (moved) {
      moved = false;
      return;
    }
    if (offset !== 0) {
      offset = 0;
      return;
    }
    onOpen();
  }
</script>

<div class="tile-wrap">
  {#if swipeEnabled}
    <div class="actions">
      {#if canEdit}
        <button class="action edit" onclick={onEdit} aria-label={$t('edit')}>
          <Icon name="edit" size={26} />
        </button>
      {/if}
      <button class="action remove" onclick={onRemove} aria-label={$t('remove')}>
        <Icon name="trash" size={26} />
      </button>
    </div>
  {/if}
  <button
    class="tile"
    style="transform: translateX({offset}px)"
    onpointerdown={onPointerDown}
    onpointermove={onPointerMove}
    onpointerup={onPointerUp}
    onpointercancel={onPointerUp}
    onclick={onTileClick}
  >
    <div class="tile-main">
      <span class="bib">{index + 1}</span>
      <span class="name">{participant.name}</span>
    </div>
    {#if participant.info}
      <div class="info">{participant.info}</div>
    {/if}
    <div class="tile-stats">
      <div class="stat">
        <span class="stat-label">{$t('score')}</span>
        <span class="stat-value">{total}</span>
      </div>
      <div class="stat">
        <span class="stat-label">{$t('arrowsShot')}</span>
        <span class="stat-value">{shot}</span>
      </div>
      {#each splits as split, i (i)}
        <div class="stat">
          <span class="stat-label">{$t('split')} {i + 1}</span>
          <span class="stat-value">{split}</span>
        </div>
      {/each}
    </div>
  </button>
</div>

<style lang="scss">
  @use '../styles/variables' as v;

  .tile-wrap {
    position: relative;
    overflow: hidden;
    border-radius: v.$radius;
    margin-bottom: v.$gap;
  }

  .actions {
    position: absolute;
    inset: 0;
    display: flex;
    justify-content: flex-end;
  }

  .action {
    width: 70px;
    border: none;
    color: #fff;
    display: flex;
    align-items: center;
    justify-content: center;

    &.edit {
      background: v.$color-primary;
    }
    &.remove {
      background: v.$color-danger;
    }
  }

  .tile {
    position: relative;
    width: 100%;
    display: block;
    text-align: left;
    background: v.$color-surface;
    border: 2px solid v.$color-border;
    border-radius: v.$radius;
    padding: 0.9rem 1.1rem;
    touch-action: pan-y;
    transition: transform 0.15s ease-out;
  }

  .tile-main {
    display: flex;
    align-items: baseline;
    gap: 0.6rem;
    margin-bottom: 0.5rem;
  }

  .bib {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    min-width: 2.2rem;
    height: 2.2rem;
    border-radius: 999px;
    background: v.$color-primary;
    color: #fff;
    font-weight: 700;
    font-size: v.$font-size-small;
  }

  .name {
    font-size: v.$font-size-large;
    font-weight: 700;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .info {
    font-size: v.$font-size-small;
    color: v.$color-text-muted;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    margin-bottom: 0.6rem;
  }

  .tile-stats {
    display: flex;
    flex-wrap: wrap;
    gap: 1.2rem;
  }

  .stat {
    display: flex;
    flex-direction: column;
  }

  .stat-label {
    font-size: v.$font-size-small;
    color: v.$color-text-muted;
  }

  .stat-value {
    font-size: v.$font-size-large;
    font-weight: 700;
  }
</style>
