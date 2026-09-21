<script lang="ts">
  import { recordSignature } from '../lib/syncService';
  import { t } from '../lib/i18n';
  import SignConfirmDialog from './SignConfirmDialog.svelte';
  import SignatureCaptureDialog from './SignatureCaptureDialog.svelte';
  import type { ScorekeeperMatch, ScorekeeperMatchParticipant } from '../lib/types';

  interface Props {
    match: ScorekeeperMatch;
    participant: ScorekeeperMatchParticipant;
  }

  let { match, participant }: Props = $props();

  let showConfirmDialog = $state(false);
  let showCaptureDialog = $state(false);

  function onSignClick() {
    if (match.signatureMode === 'signature') {
      showCaptureDialog = true;
    } else if (match.signatureMode === 'confirm') {
      showConfirmDialog = true;
    }
  }

  function onConfirmSign() {
    showConfirmDialog = false;
    recordSignature(participant.matchParticipantId, { archerSignatureDataUrl: null, markerSignatureDataUrl: null });
  }

  function onCaptureConfirm(archerDataUrl: string, markerDataUrl: string) {
    showCaptureDialog = false;
    recordSignature(participant.matchParticipantId, { archerSignatureDataUrl: archerDataUrl, markerSignatureDataUrl: markerDataUrl });
  }
</script>

{#if match.signatureMode !== 'none'}
  <div class="sign-area">
    {#if participant.signed}
      {#if participant.archerSignatureDataUrl || participant.markerSignatureDataUrl}
        <div class="signature-images">
          {#if participant.archerSignatureDataUrl}
            <div class="signature-block">
              <span class="label">{$t('archerSignature')}</span>
              <img src={participant.archerSignatureDataUrl} alt={$t('archerSignature')} />
            </div>
          {/if}
          {#if participant.markerSignatureDataUrl}
            <div class="signature-block">
              <span class="label">{$t('markerSignature')}</span>
              <img src={participant.markerSignatureDataUrl} alt={$t('markerSignature')} />
            </div>
          {/if}
        </div>
      {:else}
        <p class="signed-readonly">{$t('signedReadOnly')}</p>
      {/if}
    {:else}
      <button type="button" class="button sign-button" onclick={onSignClick}>{$t('sign')}</button>
    {/if}
  </div>
{/if}

{#if showConfirmDialog}
  <SignConfirmDialog onConfirm={onConfirmSign} onCancel={() => (showConfirmDialog = false)} />
{/if}
{#if showCaptureDialog}
  <SignatureCaptureDialog participantName={participant.name} onConfirm={onCaptureConfirm} onCancel={() => (showCaptureDialog = false)} />
{/if}

<style lang="scss">
  @use '../styles/variables' as v;

  .sign-area {
    padding: 1rem 0;
    border-top: 2px solid v.$color-border;
    margin-top: 0.5rem;
  }

  .sign-button {
    width: 100%;
  }

  .signed-readonly {
    color: v.$color-text-muted;
    font-style: italic;
    text-align: center;
    margin: 0;
  }

  .signature-images {
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
  }

  .signature-block .label {
    display: block;
    font-size: v.$font-size-small;
    color: v.$color-text-muted;
    margin-bottom: 0.25rem;
  }

  .signature-block img {
    width: 100%;
    aspect-ratio: 5 / 1;
    object-fit: contain;
    border: 2px solid v.$color-border;
    border-radius: v.$radius;
    background: #fff;
  }
</style>
