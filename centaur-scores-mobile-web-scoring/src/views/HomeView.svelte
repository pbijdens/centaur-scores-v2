<script lang="ts">
  import Icon from '../components/Icon.svelte';
  import ParticipantTile from '../components/ParticipantTile.svelte';
  import { t, translate } from '../lib/i18n';
  import { toParticipantPayload } from '../lib/matchService';
  import { apiBase, busy, language, matchData, navigate } from '../lib/stores';
  import { fetchMatchInfo } from '../lib/syncService';
  import { ScorekeeperApi } from '../lib/api';
  import { get } from 'svelte/store';
  import type { ScorekeeperMatchParticipant } from '../lib/types';

  function openScoreCard(participant: ScorekeeperMatchParticipant) {
    navigate({ name: 'score-card', matchParticipantId: participant.matchParticipantId });
  }

  function openEdit(participant: ScorekeeperMatchParticipant) {
    navigate({ name: 'edit-participant', matchParticipantId: participant.matchParticipantId });
  }

  function openAdd() {
    navigate({ name: 'add-participant' });
  }

  async function removeParticipant(participant: ScorekeeperMatchParticipant) {
    const match = $matchData;
    const base = get(apiBase);
    if (!match || !base) return;
    const remaining = match.participants.filter((p) => p.matchParticipantId !== participant.matchParticipantId);
    const payload = remaining.map(toParticipantPayload);

    busy.set(true);
    try {
      await new ScorekeeperApi(base).putParticipants(payload);
      await fetchMatchInfo();
    } catch {
      alert(translate('removeFailedBody', get(language)));
    } finally {
      busy.set(false);
    }
  }
</script>

{#if $matchData}
  <div class="view">
    <div class="tiles">
      {#each $matchData.participants as participant, index (participant.matchParticipantId)}
        <ParticipantTile
          match={$matchData}
          {participant}
          {index}
          swipeEnabled={$matchData.allowModifyParticipants}
          canEdit={!participant.tenantParticipantId && $matchData.allowCustomParticipants}
          onOpen={() => openScoreCard(participant)}
          onEdit={() => openEdit(participant)}
          onRemove={() => removeParticipant(participant)}
        />
      {/each}

      {#if $matchData.allowModifyParticipants}
        <button class="add-tile" onclick={openAdd} aria-label={$t('addParticipant')}>
          <Icon name="plus" size={40} />
        </button>
      {/if}
    </div>
  </div>
{/if}

<style lang="scss">
  @use '../styles/variables' as v;

  .tiles {
    display: flex;
    flex-direction: column;
  }

  .add-tile {
    display: flex;
    align-items: center;
    justify-content: center;
    min-height: 5rem;
    border: 2px dashed v.$color-border;
    border-radius: v.$radius;
    background: v.$color-surface;
    color: v.$color-primary;
    margin-bottom: v.$gap;
  }
</style>
