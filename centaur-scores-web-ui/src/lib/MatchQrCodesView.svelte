<script lang="ts">
  import QRCode from 'qrcode'
  import { scorekeeperUrl } from './api'
  import type { Match } from './types'

  export let match: Match
  export let tenantId: string
  export let labels: Record<string, string>

  let codes: Record<string, string> = {}

  $: devices = match.devices ?? []

  async function generateCodes() {
    const entries = await Promise.all(
      devices.map(async (device) => {
        const url = scorekeeperUrl(tenantId, match.id, device.id)
        const dataUrl = await QRCode.toDataURL(url, { width: 480, margin: 1 })
        return [device.id, dataUrl] as const
      })
    )
    codes = Object.fromEntries(entries)
  }
  generateCodes()
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
      <img src={codes[device.id]} alt={device.name} />
      <p class="qr-device-name">{device.name}</p>
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
    gap: 24px;
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

  @media print {
    .qr-toolbar {
      display: none;
    }
  }
</style>
