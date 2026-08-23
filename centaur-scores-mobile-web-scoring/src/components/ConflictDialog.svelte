<script lang="ts">
  import { conflicts, matchData } from '../lib/stores';
  import { discardParticipantConflict, resolveScoreConflict } from '../lib/syncService';
  import { t } from '../lib/i18n';

  function participantName(matchParticipantId: string): string {
    return $matchData?.participants.find((p) => p.matchParticipantId === matchParticipantId)?.name ?? $t('unknownParticipant');
  }

  function keyLabel(keyId: string | null): string {
    if (keyId === null) return '-';
    return $matchData?.keyboard.find((k) => k.id === keyId)?.label ?? keyId;
  }
</script>

{#if $conflicts && $conflicts.length > 0}
  <div class="overlay" role="alertdialog" aria-modal="true">
    <div class="dialog">
      <h2>{$t('conflictTitle')}</h2>
      <p>{$t('conflictBody')}</p>
      <div class="entries">
        {#each $conflicts as entry (entry.matchParticipantId)}
          <div class="entry">
            <h3>{participantName(entry.matchParticipantId)}</h3>

            {#if entry.error === 'PARTICIPANT_CONFLICT'}
              <p class="participant-conflict-body">{$t('participantConflictBody')}</p>
              <div class="choices">
                <button class="button danger" onclick={() => discardParticipantConflict(entry.matchParticipantId)}>
                  {$t('discardChange')}
                </button>
              </div>
            {:else}
              {#each entry.conflicts as c (c.index)}
                <div class="conflict-row">
                  <span class="idx">#{c.index + 1}</span>
                  <span class="values">{$t('myValue')}: <strong>{keyLabel(c.new)}</strong> &middot; {$t('serverValue')}: <strong>{keyLabel(c.current)}</strong></span>
                  <div class="choices">
                    <button class="button secondary" onclick={() => resolveScoreConflict(entry.matchParticipantId, c.index, 'mine', c.current)}>
                      {$t('useMine')}
                    </button>
                    <button class="button secondary" onclick={() => resolveScoreConflict(entry.matchParticipantId, c.index, 'theirs', c.current)}>
                      {$t('useTheirs')}
                    </button>
                  </div>
                </div>
              {/each}
            {/if}
          </div>
        {/each}
      </div>
    </div>
  </div>
{/if}

<style lang="scss">
  @use '../styles/variables' as v;

  .overlay {
    position: fixed;
    inset: 0;
    background: rgba(0, 0, 0, 0.5);
    display: flex;
    align-items: flex-end;
    justify-content: center;
    z-index: 100;

    @media (min-width: v.$breakpoint-tablet) {
      align-items: center;
    }
  }

  .dialog {
    width: 100%;
    max-width: 32rem;
    max-height: 85vh;
    overflow-y: auto;
    background: v.$color-surface;
    border-radius: v.$radius v.$radius 0 0;
    padding: 1.25rem;

    @media (min-width: v.$breakpoint-tablet) {
      border-radius: v.$radius;
    }
  }

  .entry {
    border-top: 1px solid v.$color-border;
    padding-top: 0.75rem;
    margin-top: 0.75rem;
  }

  .participant-conflict-body {
    color: v.$color-text-muted;
    margin: 0 0 0.75rem;
  }

  .conflict-row {
    display: flex;
    flex-direction: column;
    gap: 0.4rem;
    padding: 0.5rem 0;
  }

  .idx {
    font-weight: 700;
    color: v.$color-text-muted;
  }

  .choices {
    display: flex;
    gap: 0.6rem;
  }

  .choices .button {
    flex: 1;
    padding: 0.5rem;
    font-size: v.$font-size-small;
  }
</style>
