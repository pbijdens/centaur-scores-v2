<script lang="ts">
  import { normalizeSlug, publishedResultsUrl, PUBLISHED_SLUG_MAX_LENGTH } from './publishedResults'

  // "Publish the results at slug ____" for a match or competition. The parent saves it (setMatchPublication /
  // setCompetitionPublication); this only edits the values and previews the normalized public link.
  export let published: boolean
  export let slug: string
  // Whether the public page already shows results of matches that are still open (active).
  export let includeActiveMatches: boolean
  export let labels: Record<string, string>
  // A save error for this setting (e.g. the slug is taken), shown right below the field.
  export let error = ''

  $: normalized = normalizeSlug(slug)
  $: url = normalized ? publishedResultsUrl(normalized) : ''
</script>

<div class="publication">
  <div class="publication-row">
    <label class="checkbox-label">
      <input type="checkbox" bind:checked={published} on:change={() => (error = '')} />
      {labels.publishResultsAtSlug}
    </label>
    <input
      class="slug-input"
      aria-label={labels.publishResultsAtSlug}
      placeholder={labels.publicationSlugPlaceholder}
      maxlength={PUBLISHED_SLUG_MAX_LENGTH * 2}
      bind:value={slug}
      disabled={!published}
      on:input={() => (error = '')}
    />
  </div>
  {#if published}
    <label class="checkbox-label include-active">
      <input type="checkbox" bind:checked={includeActiveMatches} />
      {labels.publishIncludeActiveMatches}
    </label>
    {#if url}
      <p class="publication-link">{labels.publicationLinkLabel}: <a href={url} target="_blank" rel="noopener">{url}</a></p>
    {:else}
      <p class="error">{labels.publicationSlugEmpty}</p>
    {/if}
  {/if}
  {#if error}<p class="error">{error}</p>{/if}
</div>

<style>
  .publication-row {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 8px 16px;
  }

  .checkbox-label {
    display: flex;
    align-items: center;
    gap: 8px;
  }

  .slug-input {
    flex: 1 1 260px;
    min-width: 0;
  }

  .include-active {
    margin-top: 10px;
  }

  .publication-link {
    margin: 10px 0 0;
    overflow-wrap: anywhere;
  }
</style>
