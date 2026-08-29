<script lang="ts">
  import type { ApiClient } from '../api'
  import { formatLocalDate } from '../date'
  import DropdownMenu from '../DropdownMenu.svelte'
  import { labelForError } from '../errors'
  import RowActions from '../RowActions.svelte'
  import type { Category, Competition, CompetitionRound, CompetitionScoreRule, Language, MatchListItem } from '../types'

  export let api: ApiClient
  export let competition: Competition
  export let categories: Category[]
  export let matches: MatchListItem[]
  export let language: Language
  export let labels: Record<string, string>
  export let onBack: () => void
  export let onChanged: () => void | Promise<void>
  export let onDeleted: () => void
  export let onViewResults: () => void
  export let onCopied: (competition: Competition) => void

  let name = competition.name
  let startDate = competition.startDate.slice(0, 10)
  let endDate = competition.endDate.slice(0, 10)
  let groupByCategoryIds: string[] = JSON.parse(competition.groupByCategoryIdsJson || '[]')
  let saveMessage = ''
  let saveError = ''
  let deleteError = ''
  let roundError = ''
  let ruleError = ''
  let copyError = ''

  let showAddRound = false
  let newRoundShortName = ''
  let newRoundLongName = ''
  let openMatchEditorRoundId: string | null = null
  let pendingMatchSelections: Record<string, string> = {}

  let editingRoundId: string | null = null
  let editRoundShortName = ''
  let editRoundLongName = ''

  let showAddRule = false
  let newRuleName = ''
  let newRuleRoundIds: string[] = []
  let newRuleHighestScores = 1
  let newRuleMinimumScores = 0
  let newRuleAggregation: 'total' | 'f1' = 'total'

  let editingRuleId: string | null = null
  let editRuleName = ''
  let editRuleRoundIds: string[] = []
  let editRuleHighestScores = 1
  let editRuleMinimumScores = 0
  let editRuleAggregation: 'total' | 'f1' = 'total'

  $: rounds = [...(competition.rounds ?? [])].sort((a, b) => a.order - b.order)
  $: rules = [...(competition.scoringRules ?? [])].sort((a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0))
  $: orderedCategories = [
    ...groupByCategoryIds.map((id) => categories.find((category) => category.id === id)).filter((category): category is Category => !!category),
    ...categories.filter((category) => !groupByCategoryIds.includes(category.id))
  ]
  $: eligibleMatches = matches.filter((match) => match.date >= startDate && match.date <= endDate)

  function toggleGroupCategory(categoryId: string) {
    groupByCategoryIds = groupByCategoryIds.includes(categoryId)
      ? groupByCategoryIds.filter((id) => id !== categoryId)
      : [...groupByCategoryIds, categoryId]
  }

  function moveGroupCategory(index: number, direction: -1 | 1) {
    const order = [...groupByCategoryIds]
    const target = index + direction
    if (target < 0 || target >= order.length) return
    ;[order[index], order[target]] = [order[target], order[index]]
    groupByCategoryIds = order
  }

  async function saveMetadata() {
    saveMessage = ''
    saveError = ''
    try {
      await api.updateCompetition(competition.id, { name: name.trim(), startDate, endDate, groupByCategoryIds })
      await onChanged()
      saveMessage = labels.competitionSaved
    } catch (error) {
      saveError = labelForError(error, labels, 'competitionSaveError')
    }
  }

  async function deleteCompetition() {
    deleteError = ''
    if (!confirm(labels.deleteCompetitionConfirm.replace('{name}', competition.name))) return
    try {
      await api.deleteCompetition(competition.id)
      onDeleted()
    } catch (error) {
      deleteError = labelForError(error, labels, 'competitionDeleteError')
    }
  }

  async function copyCompetition() {
    copyError = ''
    try {
      const copy = await api.createCompetition({ name: `Copy of ${competition.name}`, startDate: competition.startDate, endDate: competition.endDate, groupByCategoryIds })
      const roundIdMap = new Map<string, string>()
      for (const round of rounds) {
        const newRound = await api.addCompetitionRound(copy.id, { order: round.order, shortName: round.shortName, longName: round.longName })
        roundIdMap.set(round.id, newRound.id)
        for (const roundMatch of round.matches ?? []) {
          await api.assignMatchToRound(copy.id, newRound.id, roundMatch.matchId)
        }
      }
      for (const rule of rules) {
        const oldRoundIds: string[] = JSON.parse(rule.roundIdsJson || '[]')
        const newRoundIds = oldRoundIds.map((id) => roundIdMap.get(id)).filter((id): id is string => !!id)
        await api.addCompetitionRule(copy.id, { name: rule.name, roundIds: newRoundIds, highestScores: rule.highestScores, minimumScores: rule.minimumScores, aggregation: rule.aggregation })
      }
      onCopied(copy)
    } catch (error) {
      copyError = labelForError(error, labels, 'competitionCopyError')
    }
  }

  async function addRound() {
    if (!newRoundShortName.trim() || !newRoundLongName.trim()) return
    roundError = ''
    try {
      await api.addCompetitionRound(competition.id, { order: rounds.length, shortName: newRoundShortName.trim(), longName: newRoundLongName.trim() })
      newRoundShortName = ''
      newRoundLongName = ''
      showAddRound = false
      await onChanged()
    } catch (error) {
      roundError = labelForError(error, labels, 'roundCreateError')
    }
  }

  async function moveRound(roundId: string, direction: -1 | 1) {
    const index = rounds.findIndex((round) => round.id === roundId)
    const target = index + direction
    if (index < 0 || target < 0 || target >= rounds.length) return
    const reordered = [...rounds]
    const [moved] = reordered.splice(index, 1)
    reordered.splice(target, 0, moved)
    roundError = ''
    try {
      await api.reorderCompetitionRounds(competition.id, reordered.map((round) => round.id))
      await onChanged()
    } catch (error) {
      roundError = labelForError(error, labels, 'roundSaveError')
    }
  }

  async function removeRound(round: CompetitionRound) {
    roundError = ''
    if (!confirm(labels.deleteRoundConfirm.replace('{name}', round.longName))) return
    try {
      await api.deleteCompetitionRound(competition.id, round.id)
      await onChanged()
    } catch (error) {
      roundError = labelForError(error, labels, 'roundDeleteError')
    }
  }

  function startEditRound(round: CompetitionRound) {
    editingRoundId = editingRoundId === round.id ? null : round.id
    if (editingRoundId === null) return
    editRoundShortName = round.shortName
    editRoundLongName = round.longName
  }

  async function saveRoundEdit(round: CompetitionRound) {
    if (!editRoundShortName.trim() || !editRoundLongName.trim()) return
    roundError = ''
    try {
      await api.updateCompetitionRound(competition.id, round.id, { shortName: editRoundShortName.trim(), longName: editRoundLongName.trim() })
      editingRoundId = null
      await onChanged()
    } catch (error) {
      roundError = labelForError(error, labels, 'roundSaveError')
    }
  }

  async function assignMatch(roundId: string) {
    const matchId = pendingMatchSelections[roundId]
    if (!matchId) return
    roundError = ''
    try {
      await api.assignMatchToRound(competition.id, roundId, matchId)
      pendingMatchSelections = { ...pendingMatchSelections, [roundId]: '' }
      openMatchEditorRoundId = null
      await onChanged()
    } catch (error) {
      roundError = labelForError(error, labels, 'matchAssignError')
    }
  }

  async function unassignMatch(roundId: string, matchId: string) {
    roundError = ''
    try {
      await api.unassignMatchFromRound(competition.id, roundId, matchId)
      await onChanged()
    } catch (error) {
      roundError = labelForError(error, labels, 'matchUnassignError')
    }
  }

  function matchName(matchId: string): string {
    return matches.find((match) => match.id === matchId)?.name ?? matchId
  }

  function toggleNewRuleRound(roundId: string) {
    newRuleRoundIds = newRuleRoundIds.includes(roundId)
      ? newRuleRoundIds.filter((id) => id !== roundId)
      : [...newRuleRoundIds, roundId]
  }

  async function addRule() {
    if (!newRuleName.trim()) return
    ruleError = ''
    try {
      await api.addCompetitionRule(competition.id, { name: newRuleName.trim(), roundIds: newRuleRoundIds, highestScores: newRuleHighestScores, minimumScores: newRuleMinimumScores, aggregation: newRuleAggregation })
      newRuleName = ''
      newRuleRoundIds = []
      newRuleHighestScores = 1
      newRuleMinimumScores = 0
      newRuleAggregation = 'total'
      showAddRule = false
      await onChanged()
    } catch (error) {
      ruleError = labelForError(error, labels, 'ruleCreateError')
    }
  }

  async function moveRule(ruleId: string, direction: -1 | 1) {
    const index = rules.findIndex((rule) => rule.id === ruleId)
    const target = index + direction
    if (index < 0 || target < 0 || target >= rules.length) return
    const reordered = [...rules]
    const [moved] = reordered.splice(index, 1)
    reordered.splice(target, 0, moved)
    ruleError = ''
    try {
      await api.reorderCompetitionRules(competition.id, reordered.map((rule) => rule.id))
      await onChanged()
    } catch (error) {
      ruleError = labelForError(error, labels, 'ruleSaveError')
    }
  }

  async function removeRule(rule: CompetitionScoreRule) {
    ruleError = ''
    try {
      await api.deleteCompetitionRule(competition.id, rule.id)
      await onChanged()
    } catch (error) {
      ruleError = labelForError(error, labels, 'ruleDeleteError')
    }
  }

  function ruleRoundNames(rule: CompetitionScoreRule): string {
    const ids: string[] = JSON.parse(rule.roundIdsJson || '[]')
    return ids.map((id) => rounds.find((round) => round.id === id)?.shortName ?? id).join(', ')
  }

  function startEditRule(rule: CompetitionScoreRule) {
    editingRuleId = editingRuleId === rule.id ? null : rule.id
    if (editingRuleId === null) return
    editRuleName = rule.name
    editRuleRoundIds = JSON.parse(rule.roundIdsJson || '[]')
    editRuleHighestScores = rule.highestScores
    editRuleMinimumScores = rule.minimumScores
    editRuleAggregation = rule.aggregation
  }

  function toggleEditRuleRound(roundId: string) {
    editRuleRoundIds = editRuleRoundIds.includes(roundId)
      ? editRuleRoundIds.filter((id) => id !== roundId)
      : [...editRuleRoundIds, roundId]
  }

  async function saveRuleEdit(rule: CompetitionScoreRule) {
    if (!editRuleName.trim()) return
    ruleError = ''
    try {
      await api.updateCompetitionRule(competition.id, rule.id, { name: editRuleName.trim(), roundIds: editRuleRoundIds, highestScores: editRuleHighestScores, minimumScores: editRuleMinimumScores, aggregation: editRuleAggregation })
      editingRuleId = null
      await onChanged()
    } catch (error) {
      ruleError = labelForError(error, labels, 'ruleSaveError')
    }
  }
</script>

<button class="back-link" on:click={onBack}>← {labels.competitions}</button>
<div class="page-intro">
  <div><p class="eyebrow">{labels.eyebrowCompetitionDetail}</p><h1>{competition.name}</h1></div>
  <div class="match-header-actions">
    <button class="primary" on:click={onViewResults}>{labels.viewResults}</button>
    <DropdownMenu ariaLabel={labels.matchActions} buttonClass="actions-trigger" align="right">
      <svelte:fragment slot="trigger">⋯</svelte:fragment>
      <button class="menu-item" on:click={copyCompetition}>{labels.saveCopy}</button>
      <hr class="menu-separator" />
      <button class="menu-item menu-item-danger" on:click={deleteCompetition}>{labels.deleteCompetition}</button>
    </DropdownMenu>
  </div>
</div>
{#if copyError}<p class="error">{copyError}</p>{/if}
{#if deleteError}<p class="error">{deleteError}</p>{/if}

<section class="panel section-gap">
  <label>{labels.competitionNameLabel}<input bind:value={name} /></label>
  <label>{labels.competitionStartDateLabel}<input type="date" bind:value={startDate} /></label>
  <label>{labels.competitionEndDateLabel}<input type="date" bind:value={endDate} /></label>
  <div class="grouping-block">
    <h2>{labels.groupingLabel}</h2>
    <p class="muted">{labels.groupingHint}</p>
    <div class="list-panel">
    {#each orderedCategories as category, index (category.id)}
      <div class="editor-row">
        <label class="checkbox-label">
          <input type="checkbox" checked={groupByCategoryIds.includes(category.id)} on:change={() => toggleGroupCategory(category.id)} />
          {category.name}
        </label>
        {#if groupByCategoryIds.includes(category.id)}
          <RowActions
            {labels}
            canMoveUp={index > 0}
            canMoveDown={index < groupByCategoryIds.length - 1}
            onMoveUp={() => moveGroupCategory(index, -1)}
            onMoveDown={() => moveGroupCategory(index, 1)}
          />
        {/if}
      </div>
    {/each}
    </div>
  </div>
  <button class="primary save-button" on:click={saveMetadata} disabled={!name.trim() || !startDate || !endDate}>{labels.save}</button>
  {#if saveError}<p class="error">{saveError}</p>{/if}
  {#if saveMessage}<p class="success">{saveMessage}</p>{/if}
</section>

<section class="panel section-gap">
  <div class="page-intro">
    <div><h2>{labels.eyebrowRounds}</h2><p class="muted">{labels.roundsHint}</p></div>
    <button class="primary" on:click={() => (showAddRound = !showAddRound)}>+ {labels.newRound}</button>
  </div>
  {#if showAddRound}
    <form class="inline-form" on:submit|preventDefault={addRound}>
      <label>{labels.roundShortNameLabel}<input bind:value={newRoundShortName} /></label>
      <label>{labels.roundLongNameLabel}<input bind:value={newRoundLongName} /></label>
      <button class="primary" type="submit" disabled={!newRoundShortName.trim() || !newRoundLongName.trim()}>{labels.save}</button>
    </form>
  {/if}
  <div class="list-panel">
    {#each rounds as round, roundIndex}
      <div class="device-block">
        <div class="list-row">
          <span class="management-icon">◇</span>
          <span><strong>{round.longName}</strong><small>{round.shortName} · {round.matches?.length ?? 0} matches</small></span>
          <div class="device-actions">
            <button class="icon-button" aria-label={labels.addMatchToRound} on:click={() => (openMatchEditorRoundId = openMatchEditorRoundId === round.id ? null : round.id)}>+</button>
            <button class="icon-button" aria-label={labels.edit} aria-expanded={editingRoundId === round.id} on:click={() => startEditRound(round)}>✎</button>
            <RowActions
              {labels}
              pushRight={false}
              canMoveUp={roundIndex > 0}
              canMoveDown={roundIndex < rounds.length - 1}
              onMoveUp={() => moveRound(round.id, -1)}
              onMoveDown={() => moveRound(round.id, 1)}
              onDelete={() => removeRound(round)}
            />
          </div>
        </div>
        {#if editingRoundId === round.id}
          <form class="inline-form" on:submit|preventDefault={() => saveRoundEdit(round)}>
            <label>{labels.roundShortNameLabel}<input bind:value={editRoundShortName} /></label>
            <label>{labels.roundLongNameLabel}<input bind:value={editRoundLongName} /></label>
            <button class="primary" type="submit" disabled={!editRoundShortName.trim() || !editRoundLongName.trim()}>{labels.save}</button>
          </form>
        {/if}
        {#if openMatchEditorRoundId === round.id}
          <div class="inline-form participant-add-row">
            <label>{labels.selectMatchLabel}
              <select value={pendingMatchSelections[round.id] ?? ''} on:change={(event) => (pendingMatchSelections = { ...pendingMatchSelections, [round.id]: event.currentTarget.value })}>
                <option value="">{labels.selectValue}</option>
                {#each [...eligibleMatches].sort((a, b) => a.date.localeCompare(b.date) || a.name.localeCompare(b.name)) as match}<option value={match.id}>{match.name} ({formatLocalDate(match.date, language)})</option>{/each}
              </select>
            </label>
            <button class="primary" on:click={() => assignMatch(round.id)} disabled={!(pendingMatchSelections[round.id] ?? '').trim()}>+ {labels.addMatchToRound}</button>
          </div>
        {/if}
        <div class="participants-list">
          {#if (round.matches ?? []).length === 0}
            <p class="muted">{labels.emptyState}</p>
          {:else}
            {#each round.matches ?? [] as roundMatch}
              <div class="list-row participant-row">
                <span><strong>{matchName(roundMatch.matchId)}</strong></span>
                <div class="device-actions">
                  <button class="icon-button danger-icon-button" aria-label={labels.removeValue} on:click={() => unassignMatch(round.id, roundMatch.matchId)}>✕</button>
                </div>
              </div>
            {/each}
          {/if}
        </div>
      </div>
    {/each}
  </div>
  {#if roundError}<p class="error">{roundError}</p>{/if}
</section>

<section class="panel section-gap">
  <div class="page-intro">
    <div><h2>{labels.eyebrowScoringRules}</h2><p class="muted">{labels.competitionRulesHint}</p></div>
    <button class="primary" on:click={() => (showAddRule = !showAddRule)}>+ {labels.newScoringRuleButton}</button>
  </div>
  {#if showAddRule}
    <form class="inline-form" on:submit|preventDefault={addRule}>
      <label>{labels.ruleNameLabel}<input bind:value={newRuleName} /></label>
      <label>{labels.ruleHighestScoresLabel}<input type="number" min="0" bind:value={newRuleHighestScores} /></label>
      <label>{labels.ruleMinimumScoresLabel}<input type="number" min="0" bind:value={newRuleMinimumScores} /></label>
      <label>{labels.ruleAggregationLabel}
        <select bind:value={newRuleAggregation}>
          <option value="total">{labels.aggregationTotal}</option>
          <option value="f1">{labels.aggregationF1}</option>
        </select>
      </label>
      <button class="primary" type="submit" disabled={!newRuleName.trim()}>{labels.save}</button>
    </form>
    <div class="list-panel">
      <p class="muted">{labels.ruleRoundsLabel}</p>
      {#each rounds as round}
        <label class="checkbox-label">
          <input type="checkbox" checked={newRuleRoundIds.includes(round.id)} on:change={() => toggleNewRuleRound(round.id)} />
          {round.shortName}
        </label>
      {/each}
    </div>
  {/if}
  <div class="list-panel">
    {#each rules as rule, ruleIndex}
      <div class="device-block">
        <div class="list-row">
          <span><strong>{rule.name}</strong><small>{ruleRoundNames(rule)} · {rule.aggregation === 'f1' ? labels.aggregationF1 : labels.aggregationTotal} · {rule.highestScores}/{rule.minimumScores}</small></span>
          <div class="device-actions">
            <button class="icon-button" aria-label={labels.edit} aria-expanded={editingRuleId === rule.id} on:click={() => startEditRule(rule)}>✎</button>
            <RowActions
              {labels}
              pushRight={false}
              canMoveUp={ruleIndex > 0}
              canMoveDown={ruleIndex < rules.length - 1}
              onMoveUp={() => moveRule(rule.id, -1)}
              onMoveDown={() => moveRule(rule.id, 1)}
              onDelete={() => removeRule(rule)}
            />
          </div>
        </div>
        {#if editingRuleId === rule.id}
          <form class="inline-form" on:submit|preventDefault={() => saveRuleEdit(rule)}>
            <label>{labels.ruleNameLabel}<input bind:value={editRuleName} /></label>
            <label>{labels.ruleHighestScoresLabel}<input type="number" min="0" bind:value={editRuleHighestScores} /></label>
            <label>{labels.ruleMinimumScoresLabel}<input type="number" min="0" bind:value={editRuleMinimumScores} /></label>
            <label>{labels.ruleAggregationLabel}
              <select bind:value={editRuleAggregation}>
                <option value="total">{labels.aggregationTotal}</option>
                <option value="f1">{labels.aggregationF1}</option>
              </select>
            </label>
            <button class="primary" type="submit" disabled={!editRuleName.trim()}>{labels.save}</button>
          </form>
          <div class="list-panel">
            <p class="muted">{labels.ruleRoundsLabel}</p>
            {#each rounds as round (round.id)}
              <label class="checkbox-label">
                <input type="checkbox" checked={editRuleRoundIds.includes(round.id)} on:change={() => toggleEditRuleRound(round.id)} />
                {round.shortName}
              </label>
            {/each}
          </div>
        {/if}
      </div>
    {/each}
  </div>
  {#if ruleError}<p class="error">{ruleError}</p>{/if}
</section>

<style>
  .section-gap {
    margin-top: 20px;
  }

  .device-block {
    padding: 20px;
    background: var(--paper);
    border: 1px solid var(--line);
    margin-top: 16px;
    margin-bottom: 16px;
  }

  .device-block > .list-row {
    padding-top: 0;
    border-top: 0;
  }

  .device-actions {
    display: flex;
    flex-wrap: wrap;
    gap: 10px;
    align-items: center;
    justify-content: flex-end;
    margin-left: auto;
  }

  .device-actions .icon-button {
    display: grid;
    place-items: center;
    flex: 0 0 44px;
    width: 44px;
    height: 44px;
    margin-left: 0;
    padding: 0;
    border: 1px solid var(--line);
    background: var(--paper);
  }

  .device-actions .icon-button:hover,
  .device-actions .icon-button:focus-visible {
    color: var(--green);
    border-color: var(--green);
    background: var(--neutral);
  }

  .device-actions .danger-icon-button:hover,
  .device-actions .danger-icon-button:focus-visible {
    color: #b84232;
    border-color: #e8755b;
    background: #fdeeea;
  }

  .participant-add-row {
    align-items: end;
    margin-top: 12px;
  }

  .participants-list {
    margin-top: 12px;
  }

  .participant-row {
    border-top: 1px solid var(--line);
    margin-top: 8px;
    padding-top: 8px;
  }

  .editor-row {
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    gap: 10px;
    padding: 6px 0;
  }

  .grouping-block {
    margin: 16px 0;
  }

  .save-button {
    margin-top: 16px;
  }

  .checkbox-label {
    display: flex;
    align-items: center;
    gap: 8px;
  }

  @media (max-width: 720px) {
    .device-block {
      padding: 14px;
    }

    .device-block .list-row {
      flex-wrap: wrap;
      align-items: flex-start;
    }

    .device-actions {
      width: 100%;
      justify-content: flex-end;
      margin-left: 0;
    }

    .editor-row {
      align-items: flex-start;
    }
  }
</style>
