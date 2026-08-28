<script lang="ts">
  import type { ApiClient } from '../api'
  import { labelForError } from '../errors'

  export let api: ApiClient
  export let labels: Record<string, string>
  export let onBack: () => void

  let classifiers: string[] = []
  let editingClassifier: string | null = null
  let editClassifierValue = ''
  let newClassifier = ''
  let classifiersError = ''
  let classifiersMessage = ''

  async function loadClassifiers() {
    classifiers = await api.fetchPersonalBestClassifiers()
  }
  loadClassifiers()

  $: sortedClassifiers = [...classifiers].sort((a, b) => a.localeCompare(b))

  function startEditClassifier(value: string) {
    editingClassifier = value
    editClassifierValue = value
  }

  function commitEditClassifier() {
    if (editingClassifier === null) return
    const trimmed = editClassifierValue.trim()
    if (trimmed) classifiers = classifiers.map((item) => (item === editingClassifier ? trimmed : item))
    editingClassifier = null
  }

  function removeClassifier(value: string) {
    classifiers = classifiers.filter((item) => item !== value)
  }

  function addClassifier() {
    if (!newClassifier.trim()) return
    classifiers = [...classifiers, newClassifier.trim()]
    newClassifier = ''
  }

  async function saveClassifiers() {
    classifiersError = ''
    classifiersMessage = ''
    try {
      classifiers = await api.savePersonalBestClassifiers(classifiers)
      classifiersMessage = labels.classifiersSaved
    } catch (error) {
      classifiersError = labelForError(error, labels, 'classifiersSaveError')
    }
  }
</script>

<button class="back-link" on:click={onBack}>← {labels.personalBest}</button>
<div class="page-intro">
  <div><p class="eyebrow">{labels.eyebrowPersonalBestConfig}</p><h1>{labels.classifiersLabel}</h1></div>
</div>

<section class="panel">
  <p class="muted">{labels.classifiersHint}</p>
  <section class="list-panel">
    {#if sortedClassifiers.length === 0}<p class="empty-state">{labels.emptyState}</p>{/if}
    {#each sortedClassifiers as value}
      <div class="list-row">
        {#if editingClassifier === value}
          <form class="inline-form" on:submit|preventDefault={commitEditClassifier}>
            <input bind:value={editClassifierValue} />
            <button class="primary" type="submit" disabled={!editClassifierValue.trim()}>{labels.save}</button>
          </form>
        {:else}
          <button class="value-name" on:click={() => startEditClassifier(value)}>
            <span class="management-icon">◇</span>
            <strong>{value}</strong>
          </button>
          <button class="icon-button" aria-label={labels.removeValue} on:click={() => removeClassifier(value)}>🗑</button>
        {/if}
      </div>
    {/each}
  </section>
  <form class="inline-form" on:submit|preventDefault={addClassifier}>
    <input placeholder={labels.addClassifier} bind:value={newClassifier} />
    <button class="primary" type="submit" disabled={!newClassifier.trim()}>+ {labels.addClassifier}</button>
  </form>
  {#if classifiersError}<p class="error">{classifiersError}</p>{/if}
  {#if classifiersMessage}<p class="success">{classifiersMessage}</p>{/if}
  <button class="primary" type="button" on:click={saveClassifiers}>{labels.saveClassifiers}</button>
</section>
