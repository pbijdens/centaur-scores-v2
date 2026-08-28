<script lang="ts">
  import type { ApiClient } from '../api'
  import { labelForError } from '../errors'
  import type { PersonalBestAvailableValue } from '../types'

  export let api: ApiClient
  export let labels: Record<string, string>
  export let onBack: () => void

  type DraftValue = { tenantId: string; tenantName: string; categoryId: string; categoryName: string; valueId: number; valueName: string }
  type DraftDiscipline = { id: string | null; name: string; values: DraftValue[] }

  let draftDisciplines: DraftDiscipline[] = []
  let availableValues: PersonalBestAvailableValue[] = []
  let editingDisciplineIndex: number | null = null
  let disciplinesError = ''
  let disciplinesMessage = ''

  async function loadDisciplines() {
    const [disciplines, values] = await Promise.all([api.fetchPersonalBestDisciplines(), api.fetchPersonalBestAvailableValues()])
    availableValues = values
    draftDisciplines = disciplines.map((discipline) => ({ id: discipline.id, name: discipline.name, values: discipline.values.map((value) => ({ ...value })) }))
  }
  loadDisciplines()

  function sameValue(a: { tenantId: string; categoryId: string; valueId: number }, b: { tenantId: string; categoryId: string; valueId: number }): boolean {
    return a.tenantId === b.tenantId && a.categoryId === b.categoryId && a.valueId === b.valueId
  }

  function isValueTakenElsewhere(value: PersonalBestAvailableValue, currentIndex: number): boolean {
    return draftDisciplines.some((discipline, index) => index !== currentIndex && discipline.values.some((item) => sameValue(item, value)))
  }

  function isValueSelected(value: PersonalBestAvailableValue, index: number): boolean {
    return draftDisciplines[index].values.some((item) => sameValue(item, value))
  }

  function toggleValue(value: PersonalBestAvailableValue, index: number) {
    const discipline = draftDisciplines[index]
    const values = isValueSelected(value, index)
      ? discipline.values.filter((item) => !sameValue(item, value))
      : [...discipline.values, { tenantId: value.tenantId, tenantName: value.tenantName, categoryId: value.categoryId, categoryName: value.categoryName, valueId: value.valueId, valueName: value.valueName }]
    draftDisciplines[index] = { ...discipline, values }
  }

  function addDiscipline() {
    draftDisciplines = [...draftDisciplines, { id: null, name: '', values: [] }]
    editingDisciplineIndex = draftDisciplines.length - 1
  }

  function removeDiscipline(index: number) {
    draftDisciplines = draftDisciplines.filter((_, i) => i !== index)
    if (editingDisciplineIndex === index) editingDisciplineIndex = null
  }

  async function saveDisciplines() {
    disciplinesError = ''
    disciplinesMessage = ''
    if (draftDisciplines.some((discipline) => !discipline.name.trim())) {
      disciplinesError = labels.disciplinesSaveError
      return
    }
    try {
      const saved = await api.savePersonalBestDisciplines(
        draftDisciplines.map((discipline) => ({
          id: discipline.id,
          name: discipline.name.trim(),
          values: discipline.values.map((value) => ({ tenantId: value.tenantId, categoryId: value.categoryId, valueId: value.valueId }))
        }))
      )
      draftDisciplines = saved.map((discipline) => ({ id: discipline.id, name: discipline.name, values: discipline.values.map((value) => ({ ...value })) }))
      editingDisciplineIndex = null
      disciplinesMessage = labels.disciplinesSaved
    } catch (error) {
      disciplinesError = labelForError(error, labels, 'disciplinesSaveError')
    }
  }
</script>

<button class="back-link" on:click={onBack}>← {labels.personalBest}</button>
<div class="page-intro">
  <div><p class="eyebrow">{labels.eyebrowPersonalBestConfig}</p><h1>{labels.disciplinesLabel}</h1></div>
</div>

<section class="panel">
  <p class="muted">{labels.disciplinesHint}</p>
  <section class="list-panel">
    {#if draftDisciplines.length === 0}<p class="empty-state">{labels.emptyState}</p>{/if}
    {#each draftDisciplines as discipline, index}
      <div class="discipline-block">
        {#if editingDisciplineIndex === index}
          <div class="discipline-editor">
            <label>{labels.disciplineNameLabel}<input bind:value={discipline.name} /></label>
            <h3>{labels.disciplineValuesLabel}</h3>
            <div class="checkbox-grid">
              {#each availableValues as value}
                {@const taken = isValueTakenElsewhere(value, index)}
                <label class="checkbox-label" class:disabled={taken}>
                  <input type="checkbox" disabled={taken} checked={isValueSelected(value, index)} on:change={() => toggleValue(value, index)} />
                  {value.tenantName}/{value.categoryName}/{value.valueName}
                </label>
              {/each}
            </div>
            <button class="primary" type="button" on:click={() => (editingDisciplineIndex = null)}>{labels.save}</button>
          </div>
        {:else}
          <div class="list-row">
            <button class="value-name" on:click={() => (editingDisciplineIndex = index)}>
              <span class="management-icon">◇</span>
              <strong>{discipline.name || labels.disciplineNameLabel}</strong>
            </button>
            <button class="icon-button" aria-label={labels.removeValue} on:click={() => removeDiscipline(index)}>🗑</button>
          </div>
          <ul class="discipline-values">
            {#each discipline.values as value}<li>{value.tenantName}/{value.categoryName}/{value.valueName}</li>{/each}
          </ul>
        {/if}
      </div>
    {/each}
  </section>
  <button class="primary" type="button" on:click={addDiscipline}>+ {labels.addDiscipline}</button>
  {#if disciplinesError}<p class="error">{disciplinesError}</p>{/if}
  {#if disciplinesMessage}<p class="success">{disciplinesMessage}</p>{/if}
  <button class="primary" type="button" on:click={saveDisciplines}>{labels.saveDisciplines}</button>
</section>

<style>
  .discipline-block {
    display: flex;
    flex-direction: column;
    gap: 4px;
    padding: 10px 0;
    border-bottom: 1px solid var(--line);
  }

  .discipline-values {
    margin: 0 0 0 44px;
    padding: 0;
    list-style: none;
    color: var(--muted);
    font-size: 0.9em;
  }

  .discipline-editor {
    display: flex;
    flex-direction: column;
    gap: 10px;
  }

  .checkbox-grid label.disabled {
    opacity: 0.5;
  }
</style>
