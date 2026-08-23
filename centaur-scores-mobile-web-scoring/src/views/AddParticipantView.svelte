<script lang="ts">
  import { onMount } from 'svelte';
  import ParticipantForm from '../components/ParticipantForm.svelte';
  import Icon from '../components/Icon.svelte';
  import { t, translate } from '../lib/i18n';
  import { customParticipantPayload, optionToParticipantPayload, toParticipantPayload } from '../lib/matchService';
  import { apiBase, goToParent, language, matchData } from '../lib/stores';
  import { fetchMatchInfo } from '../lib/syncService';
  import { ScorekeeperApi } from '../lib/api';
  import { get } from 'svelte/store';
  import type { ParticipantCategoryValue, ParticipantOptionsResponse, ScoreKeeperParticipantInfo } from '../lib/types';

  let mode = $state<'list' | 'form'>('list');
  let query = $state('');
  let options = $state<ParticipantOptionsResponse | null>(null);
  let loadingOptions = $state(true);
  let saving = $state(false);

  onMount(async () => {
    const base = get(apiBase);
    if (!base) return;
    try {
      options = await new ScorekeeperApi(base).getParticipantOptions();
    } finally {
      loadingOptions = false;
    }
  });

  function matches(item: ScoreKeeperParticipantInfo, needle: string): boolean {
    if (!needle) return true;
    const q = needle.toLowerCase();
    return (
      item.name.toLowerCase().includes(q) ||
      (item.info ?? '').toLowerCase().includes(q) ||
      (item.federationNumber ?? '').toLowerCase().includes(q)
    );
  }

  const unassignedList = $derived((options?.unassigned ?? []).filter((i) => matches(i, query)));
  const availableList = $derived((options?.potential ?? []).filter((i) => matches(i, query)));
  const assignedList = $derived((options?.assigned ?? []).filter((i) => matches(i, query)));

  async function selectOption(option: ScoreKeeperParticipantInfo) {
    const match = $matchData;
    const base = get(apiBase);
    if (!match || !base) return;

    const payload = [...match.participants.map(toParticipantPayload), optionToParticipantPayload(option)];

    saving = true;
    try {
      await new ScorekeeperApi(base).putParticipants(payload);
      await fetchMatchInfo();
      goToParent();
    } catch {
      alert(translate('saveFailedBody', get(language)));
    } finally {
      saving = false;
    }
  }

  async function saveUnlisted(fields: { federationNumber: string | null; name: string; categories: ParticipantCategoryValue[] }) {
    const match = $matchData;
    const base = get(apiBase);
    if (!match || !base) return;

    const payload = [...match.participants.map(toParticipantPayload), customParticipantPayload(fields)];

    saving = true;
    try {
      await new ScorekeeperApi(base).putParticipants(payload);
      await fetchMatchInfo();
      goToParent();
    } catch {
      alert(translate('saveFailedBody', get(language)));
    } finally {
      saving = false;
    }
  }
</script>

<div class="view">
  {#if mode === 'list'}
    <h1>{$t('addParticipant')}</h1>
    <div class="search">
      <Icon name="search" size={22} />
      <input type="text" placeholder={$t('search')} bind:value={query} disabled={saving} />
    </div>

    {#if loadingOptions}
      <p>{$t('loading')}</p>
    {:else}
      {#if unassignedList.length > 0}
        <h2 class="section">{$t('unassigned')}</h2>
        {#each unassignedList as item (item.matchParticipantId ?? item.name + item.federationNumber)}
          <button class="option" onclick={() => selectOption(item)} disabled={saving}>
            <div class="option-line1">{item.name}</div>
            <div class="option-line2">
              <span class="info">{item.info ?? ''}</span>
              <span class="fed">{item.federationNumber ?? ''}</span>
            </div>
          </button>
        {/each}
      {/if}

      {#if availableList.length > 0}
        <h2 class="section">{$t('available')}</h2>
        {#each availableList as item (item.tenantParticipantId ?? item.name + item.federationNumber)}
          <button class="option" onclick={() => selectOption(item)} disabled={saving}>
            <div class="option-line1">{item.name}</div>
            <div class="option-line2">
              <span class="info">{item.info ?? ''}</span>
              <span class="fed">{item.federationNumber ?? ''}</span>
            </div>
          </button>
        {/each}
      {/if}

      {#if assignedList.length > 0}
        <h2 class="section">{$t('alreadyAssigned')}</h2>
        {#each assignedList as item (item.matchParticipantId ?? item.name + item.federationNumber)}
          <button class="option" onclick={() => selectOption(item)} disabled={saving}>
            <div class="option-line1">{item.name}</div>
            <div class="option-line2">
              <span class="info">{item.info ?? ''}</span>
              <span class="fed">{item.federationNumber ?? ''}</span>
            </div>
          </button>
        {/each}
      {/if}
    {/if}

    {#if $matchData?.allowCustomParticipants}
      <button class="button secondary add-unlisted" onclick={() => (mode = 'form')} disabled={saving}>
        {$t('addUnlistedParticipant')}
      </button>
    {/if}
  {:else if $matchData}
    <h1>{$t('addUnlistedParticipant')}</h1>
    <ParticipantForm categories={$matchData.categories} {saving} onSave={saveUnlisted} onCancel={() => (mode = 'list')} />
  {/if}
</div>

<style lang="scss">
  @use '../styles/variables' as v;

  .search {
    display: flex;
    align-items: center;
    gap: 0.5rem;
    background: v.$color-surface;
    border: 2px solid v.$color-border;
    border-radius: v.$radius;
    padding: 0.3rem 0.75rem;
    margin-bottom: 1rem;
    color: v.$color-text-muted;

    input {
      flex: 1;
      border: none;
      min-height: v.$touch-target;
      background: transparent;
      color: v.$color-text;

      &:focus {
        outline: none;
      }
    }
  }

  .section {
    font-size: v.$font-size-small;
    text-transform: uppercase;
    letter-spacing: 0.04em;
    color: v.$color-text-muted;
    border-bottom: 1px solid v.$color-border;
    padding-bottom: 0.3rem;
    margin: 1.2rem 0 0.5rem;
  }

  .option {
    display: block;
    width: 100%;
    text-align: left;
    background: v.$color-surface;
    border: 2px solid v.$color-border;
    border-radius: v.$radius;
    padding: 0.7rem 0.9rem;
    margin-bottom: 0.5rem;
  }

  .option-line1 {
    font-size: v.$font-size-base;
    font-weight: 700;
  }

  .option-line2 {
    display: flex;
    justify-content: space-between;
    gap: 1rem;
    font-size: v.$font-size-small;
    color: v.$color-text-muted;
  }

  .add-unlisted {
    width: 100%;
    margin-top: 1.5rem;
  }
</style>
