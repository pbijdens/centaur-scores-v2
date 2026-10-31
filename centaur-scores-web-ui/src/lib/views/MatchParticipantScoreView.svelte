<script lang="ts">
  import type { ApiClient } from '../api'
  import MatchPageHeader from '../MatchPageHeader.svelte'
  import type { MatchHeaderHandlers } from '../pageActions'
  import { parseMatchKeyboardConfig } from '../matchConfig'
  import ParticipantScorecard from '../ParticipantScorecard.svelte'
  import ParticipantSignatures from '../ParticipantSignatures.svelte'
  import type { ArrowScore, Language, Match, MatchParticipant } from '../types'

  export let api: ApiClient
  export let match: Match
  export let participant: MatchParticipant
  export let canManage: boolean
  export let language: Language
  export let labels: Record<string, string>
  export let headerHandlers: MatchHeaderHandlers
  export let onBack: () => void

  // The API only supports upserting an arrow to a real key/value - there is no "clear this arrow"
  // endpoint - so an action can only be undone back to a previous *value*, never to "unscored".
  // `previous: null` marks the very first score ever entered for that arrow (in or before this
  // session); undo stops there rather than lying about a state the server can't represent.
  type ScoreAction = { end: number; arrow: number; previous: ArrowScore | null; next: ArrowScore }

  let undoStack: ScoreAction[] = []
  let redoStack: ScoreAction[] = []

  $: keyboardConfig = parseMatchKeyboardConfig(match.keyboardJson)
  $: disabledKeyIds = new Set(
    keyboardConfig.disabledKeyRules
      .filter((rule) => participant.categories[rule.categoryId] === rule.valueId)
      .flatMap((rule) => rule.disabledKeyIds)
  )
  $: entryKeys = keyboardConfig.keyboard.filter((key) => !disabledKeyIds.has(key.keyId))
  $: canUndo = undoStack.length > 0 && undoStack[undoStack.length - 1].previous !== null
  $: canRedo = redoStack.length > 0
  $: canEditScores = !participant.signed || canManage

  function applyLocally(end: number, arrow: number, entry: ArrowScore) {
    const otherScores = (participant.scores ?? []).filter((score) => !(score.end === end && score.arrow === arrow))
    participant = { ...participant, scores: [...otherScores, entry] }
  }

  async function persist(entry: ArrowScore) {
    await api.enterScore(match.id, participant.id, { end: entry.end, arrow: entry.arrow, keyId: entry.keyId, value: entry.value })
  }

  async function onScore(end: number, arrow: number, keyId: string) {
    const key = keyboardConfig.keyboard.find((item) => item.keyId === keyId)
    if (!key) return
    const previous = (participant.scores ?? []).find((score) => score.end === end && score.arrow === arrow) ?? null
    const entered: ArrowScore = { id: `${participant.id}-${end}-${arrow}`, matchParticipantId: participant.id, end, arrow, keyId: key.keyId, value: key.value }
    applyLocally(end, arrow, entered)
    undoStack = [...undoStack, { end, arrow, previous, next: entered }]
    redoStack = []
    await persist(entered)
  }

  async function undo() {
    if (!canUndo) return
    const action = undoStack[undoStack.length - 1]
    if (!action.previous) return
    undoStack = undoStack.slice(0, -1)
    redoStack = [...redoStack, action]
    applyLocally(action.end, action.arrow, action.previous)
    await persist(action.previous)
  }

  async function redo() {
    if (!canRedo) return
    const action = redoStack[redoStack.length - 1]
    redoStack = redoStack.slice(0, -1)
    undoStack = [...undoStack, action]
    applyLocally(action.end, action.arrow, action.next)
    await persist(action.next)
  }

  function onKeydown(event: KeyboardEvent) {
    if (!(event.ctrlKey || event.metaKey) || event.key.toLowerCase() !== 'z') return
    event.preventDefault()
    if (event.shiftKey) redo()
    else undo()
  }
</script>

<svelte:window on:keydown={onKeydown} />

<MatchPageHeader {api} {match} {language} {labels} handlers={headerHandlers} matchActions={false} title={labels.viewEditScores} subtitle={participant.fullName || participant.lastName} backLabel={participant.fullName || participant.lastName} {onBack} pageActions={[
  { label: labels.undo, headerLabel: `↶ ${labels.undo}`, header: 'text', disabled: !canUndo || !canEditScores, onClick: undo },
  { label: labels.redo, headerLabel: `↷ ${labels.redo}`, header: 'text', disabled: !canRedo || !canEditScores, onClick: redo }
]} />
{#if match.signatureMode !== 'none'}
  <div class="panel signature-status-banner" class:is-signed={participant.signed}>
    {participant.signed ? labels.scorecardSignedBanner : labels.scorecardNotSignedBanner}
  </div>
{/if}

<section class="panel">
  {#if canEditScores}
    <ParticipantScorecard {match} scores={participant.scores} keyboard={keyboardConfig.keyboard} {entryKeys} interactive {onScore} closeLabel={labels.closeKeypad} />
  {:else}
    <ParticipantScorecard {match} scores={participant.scores} keyboard={keyboardConfig.keyboard} interactive={false} />
  {/if}
  <ParticipantSignatures {api} matchId={match.id} participantId={participant.id} signed={participant.signed} {labels} />
</section>

