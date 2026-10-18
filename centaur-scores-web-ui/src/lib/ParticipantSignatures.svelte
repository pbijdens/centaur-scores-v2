<script lang="ts">
  import type { ApiClient } from './api'
  import type { MatchParticipantSignature } from './types'

  // Recorded archer/marker signature images for one signed scorecard. They aren't part of the
  // participant payloads (they're large), so this fetches them itself, only while a card is signed.
  export let api: ApiClient
  export let matchId: string
  export let participantId: string
  export let signed: boolean
  export let labels: Record<string, string>

  let signature: MatchParticipantSignature | null = null

  // Keyed on primitives so a new `participant` object (e.g. after every score entry) doesn't refetch.
  $: load(matchId, participantId, signed)

  async function load(forMatchId: string, forParticipantId: string, isSigned: boolean) {
    signature = null
    if (!isSigned) return
    try {
      const fetched = await api.fetchParticipantSignature(forMatchId, forParticipantId)
      if (forParticipantId === participantId && signed) signature = fetched
    } catch {
      // A missing image is cosmetic - the signed status banner already shows the card is final.
    }
  }
</script>

{#if signature && (signature.archerSignatureDataUrl || signature.markerSignatureDataUrl)}
  <div class="signature-images">
    {#if signature.archerSignatureDataUrl}
      <div class="signature-block"><span class="muted">{labels.archerSignatureLabel}</span><img src={signature.archerSignatureDataUrl} alt={labels.archerSignatureLabel} /></div>
    {/if}
    {#if signature.markerSignatureDataUrl}
      <div class="signature-block"><span class="muted">{labels.markerSignatureLabel}</span><img src={signature.markerSignatureDataUrl} alt={labels.markerSignatureLabel} /></div>
    {/if}
  </div>
{/if}
