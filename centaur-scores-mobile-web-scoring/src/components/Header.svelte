<script lang="ts">
  import Icon from './Icon.svelte';
  import { language, matchData, navigate, pendingUpdates, screen, switchToAdjacentParticipant, syncStatus } from '../lib/stores';
  import { forceSync } from '../lib/syncService';
  import { t } from '../lib/i18n';
  import { firstParticipantNeedingScore, totalScore } from '../lib/scoring';
  import type { Language } from '../lib/types';

  let langMenuOpen = $state(false);

  const hasPending = $derived(Object.keys($pendingUpdates).length > 0);

  const syncVisual = $derived.by(() => {
    if ($syncStatus === 'syncing') return { icon: 'ellipsis' as const, className: 'syncing' };
    if ($syncStatus === 'error') return { icon: 'error' as const, className: 'error' };
    if (hasPending) return { icon: 'wifi' as const, className: 'pending' };
    return { icon: 'wifi' as const, className: 'ok' };
  });

  const scoreCardParticipant = $derived.by(() => {
    if ($screen.name !== 'score-card' || !$matchData) return null;
    return $matchData.participants.find((p) => p.matchParticipantId === $screen.matchParticipantId) ?? null;
  });

  const showParticipantNav = $derived(
    $screen.name === 'score-card' && !!scoreCardParticipant && ($matchData?.participants.length ?? 0) > 1,
  );

  const showEnterScoresNow = $derived($screen.name === 'home' && ($matchData?.participants.length ?? 0) > 0);

  function goHome() {
    navigate({ name: 'home' }, { resetStack: true });
  }

  function onSyncTap() {
    if (hasPending) {
      forceSync();
    }
  }

  function selectLanguage(lang: Language) {
    language.set(lang);
    langMenuOpen = false;
  }

  function enterScoresNow() {
    const match = $matchData;
    if (!match) return;
    const target = firstParticipantNeedingScore(match);
    if (!target) return;
    navigate({ name: 'score-card', matchParticipantId: target.matchParticipantId });
  }
</script>

<header class="app-header">
  <div class="row main-row">
    <button class="home-button" onclick={goHome} aria-label={$t('home')}>
      <Icon name="home" size={28} />
      <span class="match-name">{$matchData?.match ?? ''}</span>
    </button>

    <div class="lang-wrap">
      <button class="icon-button lang-button" onclick={() => (langMenuOpen = !langMenuOpen)} aria-label="Language">
        <Icon name={$language === 'NL' ? 'flag-nl' : 'flag-en'} size={28} />
      </button>
      {#if langMenuOpen}
        <div class="lang-menu">
          <button class:active={$language === 'NL'} onclick={() => selectLanguage('NL')}>
            <Icon name="flag-nl" size={22} /> Nederlands
          </button>
          <button class:active={$language === 'EN'} onclick={() => selectLanguage('EN')}>
            <Icon name="flag-en" size={22} /> English
          </button>
        </div>
      {/if}
    </div>

    <button class="sync-icon {syncVisual.className}" onclick={onSyncTap} aria-label="Sync status">
      <Icon name={syncVisual.icon} size={22} />
    </button>
  </div>

  {#if showParticipantNav}
    <div class="row nav-row">
      <button class="link-button" onclick={() => switchToAdjacentParticipant(-1)}>
        <Icon name="chevron-left" size={16} /> {$t('previous')}
      </button>
      <button class="link-button" onclick={() => switchToAdjacentParticipant(1)}>
        {$t('next')} <Icon name="chevron-right" size={16} />
      </button>
    </div>
  {:else if showEnterScoresNow}
    <div class="row nav-row single">
      <button class="button enter-scores" onclick={enterScoresNow}>{$t('enterScoresNow')}</button>
    </div>
  {/if}

  {#if scoreCardParticipant && $matchData}
    <div class="row participant-row">
      <span class="participant-name">{scoreCardParticipant.name}</span>
      <span class="participant-score">{$t('score')}: {totalScore($matchData, scoreCardParticipant)}</span>
    </div>
  {/if}
</header>

<style lang="scss">
  @use '../styles/variables' as v;

  .app-header {
    position: sticky;
    top: 0;
    z-index: 20;
    background: v.$color-surface;
    border-bottom: 2px solid v.$color-border;
    box-shadow: 0 1px 4px rgba(0, 0, 0, 0.08);
  }

  .row {
    display: flex;
    align-items: center;
  }

  .main-row {
    min-height: v.$header-height;
    padding: 0 0.4rem;
    gap: 0.4rem;
  }

  .home-button {
    flex: 1;
    display: flex;
    align-items: center;
    gap: 0.6rem;
    min-width: 0;
    background: none;
    border: none;
    color: v.$color-text;
    padding: 0.5rem;
    text-align: left;
  }

  .match-name {
    font-weight: 700;
    font-size: v.$font-size-large;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  .lang-wrap {
    position: relative;
    flex-shrink: 0;
  }

  .lang-button {
    width: v.$touch-target;
    height: v.$touch-target;
  }

  .lang-menu {
    position: absolute;
    right: 0;
    top: calc(100% + 0.3rem);
    background: v.$color-surface;
    border: 2px solid v.$color-border;
    border-radius: v.$radius;
    box-shadow: 0 4px 14px rgba(0, 0, 0, 0.18);
    display: flex;
    flex-direction: column;
    overflow: hidden;
    min-width: 10rem;

    button {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      border: none;
      background: none;
      padding: 0.7rem 0.9rem;
      font-size: v.$font-size-base;
      color: v.$color-text;

      &.active {
        background: v.$color-bg;
        font-weight: 700;
      }
    }
  }

  .sync-icon {
    flex-shrink: 0;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    line-height: 0;
    padding: 0;
    border: none;
    background: none;

    &.ok {
      color: v.$color-sync-ok;
    }
    &.pending {
      color: v.$color-sync-pending;
    }
    &.error {
      color: v.$color-sync-error;
    }
    &.syncing {
      color: v.$color-sync-syncing;
    }
  }

  .nav-row {
    justify-content: space-between;
    padding: 0.25rem 1rem;
    gap: 1rem;

    &.single {
      justify-content: center;
      padding: 0.5rem 1rem 0.75rem;
    }
  }

  .enter-scores {
    min-height: 2.6rem;
    padding: 0.4rem 1.4rem;
  }

  .link-button {
    display: inline-flex;
    align-items: center;
    gap: 0.25rem;
    background: none;
    border: none;
    padding: 0.2rem;
    color: v.$color-primary;
    font-weight: 600;
    font-size: v.$font-size-small;
    text-decoration: underline;
  }

  .participant-row {
    justify-content: space-between;
    padding: 0.4rem 1rem 0.6rem;
    font-size: v.$font-size-base;
    font-weight: 600;
    gap: 1rem;
  }

  .participant-name {
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .participant-score {
    flex-shrink: 0;
    color: v.$color-primary;
  }
</style>
