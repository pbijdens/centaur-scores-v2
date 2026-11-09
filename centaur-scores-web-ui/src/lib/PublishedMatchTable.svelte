<script lang="ts">
  import { formatAverage } from './publishedResults'
  import type { PublishedMatchEntry } from './types'

  export let entries: PublishedMatchEntry[]
  export let language: string
  export let labels: Record<string, string>
</script>

<div class="table-wrap">
  <table class="published-table">
    <thead>
      <tr>
        <th class="num pos">{labels.publishedPosition}</th>
        <th class="fed">{labels.publishedFederationNumber}</th>
        <th>{labels.publishedName}</th>
        <th class="num arrows">{labels.publishedArrows}</th>
        <th class="num">{labels.publishedAverage}</th>
        <th class="num">{labels.publishedScore}</th>
      </tr>
    </thead>
    <tbody>
      {#each entries as entry}
        <tr>
          <td class="num pos">{entry.position}{entry.needsTieBreaker ? '*' : ''}</td>
          <td class="fed">{entry.federationNumber ?? ''}</td>
          <td class="name">{entry.name}{#if entry.federationNumber}<small class="fed-inline">{entry.federationNumber}</small>{/if}</td>
          <td class="num arrows">{entry.arrows}</td>
          <td class="num">{formatAverage(entry.average, language)}</td>
          <td class="num score">{entry.score}</td>
        </tr>
      {/each}
    </tbody>
  </table>
</div>

<style>
  @media (max-width: 640px) {
    .arrows {
      display: none;
    }
  }
</style>
