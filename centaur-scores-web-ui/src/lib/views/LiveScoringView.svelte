<script lang="ts">
  import { onMount } from 'svelte'
  import { fetchLiveScoringMatches, fetchLiveScoringPage } from '../api'
  import { formatLocalDate } from '../date'
  import { translationsFor } from '../i18n'
  import MatchProgress from '../MatchProgress.svelte'
  import type { Language, LiveScoringMatch, LiveScoringPage } from '../types'

  export let scope: string

  let page: LiveScoringPage | null = null
  let retrySeconds = 30
  let progress = 0
  let generation = 0
  let interval: ReturnType<typeof setInterval> | undefined
  let timeout: ReturnType<typeof setTimeout> | undefined
  const language = (localStorage.getItem('centaur-language') ?? 'en') as Language
  const labels = translationsFor(language)

  function clearTimers() {
    if (interval) clearInterval(interval)
    if (timeout) clearTimeout(timeout)
    interval = undefined
    timeout = undefined
  }

  async function start() {
    const currentGeneration = ++generation
    clearTimers()
    page = null
    progress = 0
    try {
      const matches = await fetchLiveScoringMatches(scope)
      if (currentGeneration !== generation) return
      if (matches.length === 0) {
        startRetryCountdown(currentGeneration)
        return
      }
      await showMatch(matches, 0, currentGeneration)
    } catch {
      if (currentGeneration === generation) startRetryCountdown(currentGeneration)
    }
  }

  function startRetryCountdown(currentGeneration: number) {
    retrySeconds = 30
    interval = setInterval(() => {
      retrySeconds -= 1
      if (retrySeconds <= 0 && currentGeneration === generation) void start()
    }, 1000)
  }

  async function showMatch(matches: LiveScoringMatch[], index: number, currentGeneration: number) {
    try {
      page = await fetchLiveScoringPage(scope, matches[index].id)
    } catch {
      if (currentGeneration === generation) void advance(matches, index, currentGeneration)
      return
    }
    if (currentGeneration !== generation || !page) return

    const duration = Math.max(1, Number(page.timeout) || 15) * 1000
    const startedAt = performance.now()
    progress = 0
    interval = setInterval(() => {
      progress = Math.min(100, ((performance.now() - startedAt) / duration) * 100)
    }, 100)
    timeout = setTimeout(() => void advance(matches, index, currentGeneration), duration)
  }

  async function advance(matches: LiveScoringMatch[], index: number, currentGeneration: number) {
    if (currentGeneration !== generation) return
    clearTimers()
    if (index + 1 >= matches.length) {
      await start()
      return
    }
    await showMatch(matches, index + 1, currentGeneration)
  }

  onMount(() => {
    document.body.classList.add('live-scoring-page')
    void start()
    return () => {
      generation += 1
      clearTimers()
      document.body.classList.remove('live-scoring-page')
    }
  })
</script>

<svelte:head>
  <title>{page ? `${page.matchName} - ${page.tenant}` : 'Live scoring'}</title>
</svelte:head>

<div class="live-scoring">
  {#if page}
    <header class="live-header">
      <div class="tenant-identity">
        {#if page.logo}<img src={page.logo} alt="" />{/if}
        <strong>{page.tenant}</strong>
      </div>
      <h1>{page.matchName}</h1>
      <div class="date-progress">
        <time datetime={page.matchDate}>{formatLocalDate(page.matchDate, language)}</time>
        <MatchProgress {page} label={labels.matchProgressLabel} />
      </div>
    </header>
    <main class="live-results">
      {#each page.blocks as block}
        <section class="result-block">
          {#if block.entries.length > 0}
            <h2>{block.name}</h2>
            {#each block.entries as entry}
              <div class="result-entry">
                <span class="position">{entry.position}{entry.needsTieBreaker ? '*' : ''}</span>
                <span class="entry-lines">
                  <strong>{#if entry.aboveTarget}<span class="pb-star" aria-hidden="true">★</span>{/if}{entry.line1}</strong>
                  {#if entry.line2}<small>{entry.line2}</small>{/if}
                </span>
                {#if entry.average != null}<span class="average">{entry.average.toFixed(2)}</span>{/if}
                <strong class="score">{entry.score}</strong>
              </div>
            {/each}
          {/if}
        </section>
      {/each}
    </main>
    <div class="live-progress" aria-hidden="true"><span style={`width: ${progress}%`}></span></div>
  {:else}
    <main class="no-live-matches">There are currently no active sessions. Will check again in {retrySeconds} seconds.</main>
  {/if}
</div>

<style>
  :global(body.live-scoring-page) {
    overflow: hidden;
    background: #fff;
  }

  .live-scoring {
    width: 100vw;
    height: 100vh;
    overflow: hidden;
    background: #fff;
    color: #14210f;
  }

  .live-header {
    height: 8vh;
    min-height: 0;
    display: grid;
    grid-template-columns: 1fr minmax(0, 2fr) 1fr;
    align-items: center;
    gap: 1.5vw;
    padding: 0 1.5vw;
    border-bottom: 1px solid #dfe4db;
  }

  .live-header h1 {
    overflow: hidden;
    font-size: clamp(15px, 2.1vh, 28px);
    line-height: 1;
    text-align: center;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .date-progress {
    display: flex;
    flex-direction: column;
    justify-content: center;
    align-items: flex-end;
    gap: .4vh;
    height: 100%;
  }

  .date-progress time {
    font-size: clamp(11px, 1.5vh, 18px);
    font-weight: 600;
  }

  .tenant-identity {
    min-width: 0;
    display: flex;
    align-items: center;
    gap: .7vw;
    font-size: clamp(11px, 1.7vh, 20px);
  }

  .tenant-identity img {
    width: 4.5vh;
    height: 4.5vh;
    object-fit: contain;
    flex: 0 0 auto;
  }

  .tenant-identity strong {
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .live-results {
    height: calc(92vh - 4px);
    padding: 1vh 1vw;
    column-count: 3;
    column-fill: auto;
    column-gap: 1.5vw;
    column-rule: 1px solid #e7eae4;
    overflow: hidden;
  }

  .result-block h2 {
    break-after: avoid-column;
    margin: .7vh 0 .3vh;
    padding: .45vh .5vw;
    background: #eef2eb;
    color: #164a13;
    font-size: clamp(11px, 1.45vh, 17px);
    line-height: 1.15;
  }

  .result-entry {
    min-height: 2.45vh;
    display: grid;
    grid-template-columns: 3ch minmax(0, 1fr) 5ch 5ch;
    align-items: center;
    gap: .4vw;
    break-inside: avoid-column;
    padding: .2vh .5vw;
    border-bottom: 1px solid #edf0ea;
    font-size: clamp(10px, 1.35vh, 16px);
    line-height: 1.08;
  }

  .position,
  .average,
  .score {
    font-variant-numeric: tabular-nums;
  }

  .average,
  .score {
    text-align: right;
  }

  .pb-star {
    color: #e0a300;
    font-size: .85em;
    margin-right: .3em;
    text-shadow: 0 0 2px rgba(224, 163, 0, .5);
  }

  .average {
    font-size: .82em;
  }

  .score {
    font-size: 1.12em;
  }

  .entry-lines {
    min-width: 0;
    display: grid;
    gap: .1vh;
  }

  .entry-lines strong,
  .entry-lines small {
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .entry-lines small {
    color: #536050;
    font-size: .78em;
  }

  .live-progress {
    position: fixed;
    inset: auto 0 0;
    height: 4px;
    background: #e7eae4;
  }

  .live-progress span {
    display: block;
    height: 100%;
    background: #164a13;
  }

  .no-live-matches {
    height: 100%;
    display: grid;
    place-items: center;
    padding: 24px;
    font-size: clamp(18px, 2.5vh, 30px);
    text-align: center;
  }

  @media (orientation: portrait) {
    .live-results {
      column-count: 2;
      column-gap: 2vw;
    }

    .result-entry {
      min-height: 2.45vh;
      font-size: clamp(10px, 1.5vh, 16px);
    }

    .result-block h2 {
      margin-top: .9vh;
      font-size: clamp(11px, 1.6vh, 17px);
    }

    .entry-lines strong {
      font-size: clamp(10px, 2vh, 18px);
    }
}
</style>