<script lang="ts">
  import Keyboard from '../components/Keyboard.svelte';
  import { t } from '../lib/i18n';
  import {
    availableKeys,
    endArrows,
    endTotal,
    firstNullIndexInEnd,
    groupRunningTotal,
    runningTotalThroughEnd,
  } from '../lib/scoring';
  import { matchData, navigate, screen } from '../lib/stores';
  import { recordScoreEdit } from '../lib/syncService';
  import type { ScorekeeperKey } from '../lib/types';

  const currentScreen = $derived($screen);
  const participant = $derived.by(() => {
    if (currentScreen.name !== 'score-card' || !$matchData) return null;
    return $matchData.participants.find((p) => p.matchParticipantId === currentScreen.matchParticipantId) ?? null;
  });

  $effect(() => {
    if (currentScreen.name === 'score-card' && $matchData && !participant) {
      // Participant no longer belongs to this device (e.g. removed elsewhere).
      navigate({ name: 'home' }, { resetStack: true });
    }
  });

  let openEndIndex = $state<number | null>(null);
  let focusedIndex = $state<number | null>(null);
  let keyboardEl = $state<HTMLElement | null>(null);

  $effect(() => {
    if (openEndIndex !== null && keyboardEl) {
      keyboardEl.scrollIntoView({ block: 'end', behavior: 'smooth' });
    }
  });

  function updateLocalArrow(matchParticipantId: string, index: number, value: string | null) {
    matchData.update((m) => {
      if (!m) return m;
      return {
        ...m,
        participants: m.participants.map((p) => {
          if (p.matchParticipantId !== matchParticipantId) return p;
          const arrowScores = [...p.arrowScores];
          arrowScores[index] = value;
          return { ...p, arrowScores };
        }),
      };
    });
  }

  function onArrowTap(endIndex: number, globalIndex: number) {
    if (!$matchData || !participant) return;
    const isNull = participant.arrowScores[globalIndex] === null;
    if (openEndIndex === endIndex) {
      focusedIndex = globalIndex;
      return;
    }
    openEndIndex = endIndex;
    if (isNull) {
      focusedIndex = firstNullIndexInEnd($matchData, participant, endIndex) ?? globalIndex;
    } else {
      focusedIndex = globalIndex;
    }
  }

  function onKey(keyId: string) {
    if (!$matchData || !participant || focusedIndex === null) return;
    const match = $matchData;
    const idx = focusedIndex;
    const endIndex = Math.floor(idx / match.arrowsPerEnd);
    const endStart = endIndex * match.arrowsPerEnd;
    const previous = participant.arrowScores[idx];

    recordScoreEdit(participant.matchParticipantId, idx, previous, keyId);
    updateLocalArrow(participant.matchParticipantId, idx, keyId);

    const isLastArrowOfEnd = idx === endStart + match.arrowsPerEnd - 1;
    if (isLastArrowOfEnd) {
      focusedIndex = idx;
      openEndIndex = null;
    } else {
      focusedIndex = idx + 1;
    }
  }

  function onDelete() {
    if (!$matchData || !participant || focusedIndex === null) return;
    const idx = focusedIndex;
    const previous = participant.arrowScores[idx];
    recordScoreEdit(participant.matchParticipantId, idx, previous, null);
    updateLocalArrow(participant.matchParticipantId, idx, null);
  }

  function onHideKeyboard() {
    openEndIndex = null;
  }

  function keyForValue(keys: ScorekeeperKey[], value: string | null): ScorekeeperKey | null {
    if (value === null) return null;
    return keys.find((k) => k.id === value) ?? null;
  }

  function colorClass(color: string): string {
    return `key-${color.toLowerCase()}`;
  }

  const SWIPE_THRESHOLD = 60;
  const SWIPE_LOCK_THRESHOLD = 10;

  let touchStartX = 0;
  let touchStartY = 0;
  let touchActive = false;
  let swipeDirection: 'horizontal' | 'vertical' | null = null;

  function onPointerDown(event: PointerEvent) {
    touchStartX = event.clientX;
    touchStartY = event.clientY;
    touchActive = true;
    swipeDirection = null;
  }

  function onPointerMove(event: PointerEvent) {
    if (!touchActive) return;
    const dx = event.clientX - touchStartX;
    const dy = event.clientY - touchStartY;

    if (swipeDirection === null && (Math.abs(dx) > SWIPE_LOCK_THRESHOLD || Math.abs(dy) > SWIPE_LOCK_THRESHOLD)) {
      swipeDirection = Math.abs(dx) > Math.abs(dy) ? 'horizontal' : 'vertical';
    }

    if (swipeDirection === 'horizontal') {
      // Claim the gesture before the browser turns it into a page-back
      // navigation or a scroll attempt.
      event.preventDefault();
    }
  }

  function onPointerUp(event: PointerEvent) {
    if (!touchActive) return;
    touchActive = false;
    const dx = event.clientX - touchStartX;
    if (swipeDirection === 'horizontal' && Math.abs(dx) > SWIPE_THRESHOLD) {
      switchParticipant(dx < 0 ? 1 : -1);
    }
    swipeDirection = null;
  }

  function switchParticipant(direction: number) {
    const match = $matchData;
    if (!match || !participant || match.participants.length < 2) return;
    const idx = match.participants.findIndex((p) => p.matchParticipantId === participant!.matchParticipantId);
    const count = match.participants.length;
    const nextIdx = (idx + direction + count) % count;
    openEndIndex = null;
    focusedIndex = null;
    navigate({ name: 'score-card', matchParticipantId: match.participants[nextIdx].matchParticipantId }, { replace: true });
  }
</script>

{#if $matchData && participant}
  {@const match = $matchData}
  {@const keys = availableKeys(match, participant)}
  <div
    class="view score-card"
    role="presentation"
    onpointerdown={onPointerDown}
    onpointermove={onPointerMove}
    onpointerup={onPointerUp}
    onpointercancel={() => {
      touchActive = false;
      swipeDirection = null;
    }}
  >
    {#each Array(match.ends) as _, endIndex (endIndex)}
      {@const arrows = endArrows(match, participant, endIndex)}
      <div class="end-block" class:active={openEndIndex === endIndex}>
        <div class="end-row">
          <span class="end-number">{endIndex + 1}</span>
          <div class="arrows">
            {#each arrows as arrowValue, i (i)}
              {@const globalIndex = endIndex * match.arrowsPerEnd + i}
              {@const key = keyForValue(match.keyboard, arrowValue)}
              <button
                class="arrow {key ? colorClass(key.color) : 'empty'}"
                class:focused={openEndIndex === endIndex && focusedIndex === globalIndex}
                onclick={() => onArrowTap(endIndex, globalIndex)}
              >
                {key ? key.label : '-'}
              </button>
            {/each}
          </div>
          <span class="end-total">{endTotal(match, participant, endIndex)}</span>
          <div class="totals-cell">
            <span class="running-total">{runningTotalThroughEnd(match, participant, endIndex)}</span>
            {#if match.groupEnds}
              <span class="group-total">{groupRunningTotal(match, participant, endIndex)}</span>
            {/if}
          </div>
        </div>

        {#if openEndIndex === endIndex}
          <div bind:this={keyboardEl}>
            <Keyboard {keys} onKey={onKey} onDelete={onDelete} onHide={onHideKeyboard} />
          </div>
        {/if}
      </div>
    {/each}
  </div>
{/if}

<style lang="scss">
  @use '../styles/variables' as v;

  .score-card {
    touch-action: pan-y;
  }

  .end-block {
    border-bottom: 1px solid v.$color-border;
    padding: 0.6rem 0;

    &.active {
      background: rgba(28, 59, 87, 0.04);
      border-radius: v.$radius;
    }
  }

  .end-row {
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    gap: 0.6rem;
  }

  .end-number {
    min-width: 1.8rem;
    font-weight: 700;
    color: v.$color-text-muted;
    text-align: center;
  }

  .arrows {
    display: flex;
    gap: 0.4rem;
    flex-wrap: wrap;
  }

  .arrow {
    min-width: 2.6rem;
    height: 2.6rem;
    border-radius: v.$radius;
    border: 2px solid rgba(0, 0, 0, 0.15);
    font-weight: 700;
    font-size: v.$font-size-base;

    &.empty {
      background: #e6e6e6;
      color: #888;
    }
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

    &.focused {
      outline: 3px solid v.$color-primary;
      outline-offset: 2px;
    }
  }

  .end-total {
    margin-left: auto;
    font-weight: 700;
    min-width: 2.4rem;
    text-align: right;
  }

  .totals-cell {
    display: flex;
    flex-direction: column;
    align-items: flex-end;
    min-width: 2.4rem;
    text-align: right;
    line-height: 1.15;
  }

  .running-total {
    flex: 0 0 60%;
    font-weight: 700;
    font-size: v.$font-size-base;
  }

  .group-total {
    flex: 0 0 40%;
    font-weight: 400;
    font-size: 0.75em;
    color: v.$color-text-muted;
  }
</style>
