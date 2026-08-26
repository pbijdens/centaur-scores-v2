<script lang="ts">
  import QRCode from 'qrcode'
  import { scorekeeperUrl } from '../api'
  import type { Language, Match } from '../types'

  export let match: Match
  export let tenantId: string
  export let language: Language
  export let labels: Record<string, string>

  let codes: Record<string, string> = {}
  let generation = 0

  $: devices = [...(match.devices ?? [])].sort((a, b) => (a.sortOrder ?? Number.MAX_SAFE_INTEGER) - (b.sortOrder ?? Number.MAX_SAFE_INTEGER) || a.name.localeCompare(b.name))
  $: {
    tenantId
    language
    match.id
    devices
    void generateCodes()
  }

  async function generateCodes() {
    const currentGeneration = ++generation
    if (devices.length === 0) {
      codes = {}
      return
    }

    const entries = await Promise.all(
      devices.map(async (device) => {
        const url = scorekeeperUrl(tenantId, match.id, device.id, language)
        const dataUrl = await QRCode.toDataURL(url, { width: 480, margin: 1 })
        return [device.id, dataUrl] as const
      })
    )
    if (currentGeneration !== generation) return
    codes = Object.fromEntries(entries)
  }
</script>

<svelte:head>
  <title>{labels.qrPageTitle} - {match.name}</title>
</svelte:head>

<div class="qr-toolbar">
  <button class="primary" on:click={() => window.print()}>{labels.printButton}</button>
</div>
<div class="qr-page">
  {#each devices as device}
    <div class="qr-block">
      <p class="qr-match-name">{match.name}</p>
      {#if codes[device.id]}
        <img src={codes[device.id]} alt={device.name} />
      {:else}
        <div class="qr-placeholder" aria-label="Loading QR code"></div>
      {/if}
      <p class="qr-device-name"><a href={scorekeeperUrl(tenantId, match.id, device.id, language)}>{device.name}</a></p>
    </div>
  {/each}
</div>

<style>
  :global(body) {
    background: #fff;
  }

  .qr-toolbar {
    padding: 16px;
  }

  .qr-page {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 256px;
    padding: 24px;
    max-width: 900px;
    margin: 0 auto;
  }

  .qr-block {
    border: 2px solid #000;
    padding: 24px;
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 12px;
    break-inside: avoid;
  }

  .qr-block:nth-child(4n) {
    break-after: page;
  }

  .qr-match-name {
    font: 700 20px 'Space Grotesk', sans-serif;
    margin: 0;
    text-align: center;
  }

  .qr-device-name {
    font-size: 16px;
    margin: 0;
    text-align: center;
  }

  .qr-block img {
    width: 100%;
    max-width: 320px;
    height: auto;
  }

  .qr-placeholder {
    width: 100%;
    max-width: 320px;
    aspect-ratio: 1;
    border: 1px solid #bbb;
    background: repeating-linear-gradient(
      45deg,
      #f2f2f2,
      #f2f2f2 12px,
      #e8e8e8 12px,
      #e8e8e8 24px
    );
  }

  @media print {
    .qr-toolbar {
      display: none;
    }

    .qr-page {
      gap: 56px;
    }
  }
</style>
