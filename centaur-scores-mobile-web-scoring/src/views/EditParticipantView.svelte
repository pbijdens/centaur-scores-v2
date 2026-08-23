<script lang="ts">
  import ParticipantForm from '../components/ParticipantForm.svelte';
  import { t, translate } from '../lib/i18n';
  import { customParticipantPayload, toParticipantPayload } from '../lib/matchService';
  import { apiBase, goToParent, language, matchData, screen } from '../lib/stores';
  import { fetchMatchInfo } from '../lib/syncService';
  import { ScorekeeperApi } from '../lib/api';
  import { get } from 'svelte/store';
  import type { ParticipantCategoryValue } from '../lib/types';

  const currentScreen = $derived($screen);
  const participant = $derived.by(() => {
    if (currentScreen.name !== 'edit-participant' || !$matchData) return null;
    return $matchData.participants.find((p) => p.matchParticipantId === currentScreen.matchParticipantId) ?? null;
  });

  let saving = $state(false);

  async function handleSave(fields: { federationNumber: string | null; name: string; categories: ParticipantCategoryValue[] }) {
    const match = $matchData;
    const base = get(apiBase);
    const current = participant;
    if (!match || !base || !current) return;

    const payload = match.participants.map((p) =>
      p.matchParticipantId === current.matchParticipantId
        ? customParticipantPayload(fields, current.matchParticipantId)
        : toParticipantPayload(p),
    );

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

  function handleCancel() {
    goToParent();
  }
</script>

<div class="view">
  <h1>{$t('editParticipant')}</h1>
  {#if $matchData && participant}
    <ParticipantForm
      categories={$matchData.categories}
      initialFederationNumber={participant.federationNumber}
      initialName={participant.name}
      initialCategoryValues={participant.categories}
      {saving}
      onSave={handleSave}
      onCancel={handleCancel}
    />
  {/if}
</div>
