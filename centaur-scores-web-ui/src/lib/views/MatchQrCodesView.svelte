<script lang="ts">
  import QRCode from 'qrcode'
  import { scorekeeperUrl, type ApiClient } from '../api'
  import MatchPageHeader from '../MatchPageHeader.svelte'
  import { printAction, type MatchHeaderHandlers } from '../pageActions'
  import type { Language, Match } from '../types'

  export let api: ApiClient
  export let match: Match
  export let tenantId: string
  export let scorekeeperBaseUrl: string | null | undefined = undefined
  export let language: Language
  export let labels: Record<string, string>
  export let headerHandlers: MatchHeaderHandlers
  export let onBack: () => void

  let codes: Record<string, string> = {}
  let generation = 0

  $: devices = [...(match.devices ?? [])].sort((a, b) => (a.sortOrder ?? Number.MAX_SAFE_INTEGER) - (b.sortOrder ?? Number.MAX_SAFE_INTEGER) || a.name.localeCompare(b.name))
  $: {
    tenantId
    scorekeeperBaseUrl
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
        const url = scorekeeperUrl(tenantId, match.id, device.id, language, scorekeeperBaseUrl)
        const dataUrl = await QRCode.toDataURL(url, { width: 480, margin: 1 })
        return [device.id, dataUrl] as const
      })
    )
    if (currentGeneration !== generation) return
    codes = Object.fromEntries(entries)
  }

  function chunk<T>(items: T[], size: number): T[][] {
    const result: T[][] = []
    for (let i = 0; i < items.length; i += size) result.push(items.slice(i, i + size))
    return result
  }
</script>

<svelte:head>
  <title>{labels.qrPageTitle} - {match.name}</title>
</svelte:head>

<MatchPageHeader {api} {match} {language} {labels} handlers={headerHandlers} current="qr" title={labels.qrPageTitle} subtitle={match.name} backLabel={match.name} {onBack} pageActions={[printAction(labels, () => window.print())]} />

<!-- Four codes per printed page: a 2×2 grid in portrait, four side by side in landscape. -->
<div class="qr-page print-output">
  {#each chunk(devices, 4) as sheetDevices}
    <div class="qr-sheet">
      {#each sheetDevices as device (device.id)}
        <div class="qr-block">
          <p class="qr-match-name">{match.name}</p>
          {#if codes[device.id]}
            <img src={codes[device.id]} alt={device.name} />
          {:else}
            <div class="qr-placeholder" aria-label="Loading QR code"></div>
          {/if}
          <p class="qr-device-name"><a href={scorekeeperUrl(tenantId, match.id, device.id, language, scorekeeperBaseUrl)}>{device.name}</a></p>
        </div>
      {/each}
    </div>
  {/each}
  {#if devices.length === 0}<p class="empty-state">{labels.emptyState}</p>{/if}
</div>

<style>
  /* On screen the sheets dissolve into one responsive grid of codes. */
  .qr-page {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(min(100%, 240px), 1fr));
    gap: 20px;
  }

  .qr-sheet {
    display: contents;
  }

  .qr-block {
    border: 2px solid #000;
    border-radius: 4px;
    background: #fff;
    padding: 20px;
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 12px;
    break-inside: avoid;
  }

  .qr-match-name {
    font: 700 18px 'Space Grotesk', sans-serif;
    margin: 0;
    text-align: center;
  }

  .qr-device-name {
    font-size: 16px;
    margin: 0;
    text-align: center;
  }

  .qr-block img,
  .qr-placeholder {
    width: 100%;
    max-width: 320px;
  }

  .qr-block img {
    height: auto;
  }

  .qr-placeholder {
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
    .qr-page {
      display: block;
    }

    .qr-sheet {
      display: grid;
      grid-template-columns: repeat(2, 1fr);
      gap: 10mm;
      padding: 6mm;
    }

    .qr-sheet:not(:last-child) {
      break-after: page;
    }

    .qr-block {
      border-radius: 0;
      padding: 6mm;
    }

    .qr-block img,
    .qr-placeholder {
      max-width: 75mm;
    }
  }

  @media print and (orientation: landscape) {
    .qr-sheet {
      grid-template-columns: repeat(4, 1fr);
      gap: 6mm;
    }

    .qr-block img,
    .qr-placeholder {
      max-width: 55mm;
    }
  }
</style>
