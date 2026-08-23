<script lang="ts">
  import { onMount } from 'svelte';
  import Header from './components/Header.svelte';
  import ConflictDialog from './components/ConflictDialog.svelte';
  import NoActiveMatchView from './views/NoActiveMatchView.svelte';
  import HomeView from './views/HomeView.svelte';
  import AddParticipantView from './views/AddParticipantView.svelte';
  import EditParticipantView from './views/EditParticipantView.svelte';
  import ScoreCardView from './views/ScoreCardView.svelte';
  import { apiBase, goToParent, screen } from './lib/stores';
  import { initializeFromStartupParams } from './lib/matchService';
  import { startBackgroundSync, stopBackgroundSync } from './lib/syncService';
  import { t } from './lib/i18n';

  onMount(() => {
    initializeFromStartupParams();
    startBackgroundSync();

    const onPopState = () => goToParent();
    window.addEventListener('popstate', onPopState);

    return () => {
      window.removeEventListener('popstate', onPopState);
      stopBackgroundSync();
    };
  });
</script>

{#if !$apiBase}
  <div class="view">
    <div class="empty-state">
      <h1>Centaur Scores</h1>
      <p>Open this application with a valid <code>?api=</code> link for your device.</p>
    </div>
  </div>
{:else}
  <Header />

  {#if $screen.name === 'loading'}
    <div class="view">
      <div class="empty-state">
        <p>{$t('loading')}</p>
      </div>
    </div>
  {:else if $screen.name === 'no-active-match'}
    <NoActiveMatchView />
  {:else if $screen.name === 'home'}
    <HomeView />
  {:else if $screen.name === 'add-participant'}
    <AddParticipantView />
  {:else if $screen.name === 'edit-participant'}
    <EditParticipantView />
  {:else if $screen.name === 'score-card'}
    <ScoreCardView />
  {/if}

  <ConflictDialog />
{/if}
