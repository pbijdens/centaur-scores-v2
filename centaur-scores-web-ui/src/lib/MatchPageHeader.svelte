<script lang="ts">
  import type { ApiClient } from './api'
  import { labelForError } from './errors'
  import { duplicateMatch, saveBlob } from './matchActions'
  import { asGroup, type MatchHeaderHandlers, type PageAction } from './pageActions'
  import PageHeader from './PageHeader.svelte'
  import { matchDevicesPath, matchEditPath, matchPrintPath, matchQrPath, matchResultsPath } from './router'
  import type { Language, Match } from './types'

  // The header shared by the match page and every page below it. Unless matchActions is off, it offers the match-level
  // actions - Activate/Deactivate, QR codes and Results next to the title, and the match's full ⋯ menu -
  // with the current page's own actions (pageActions) listed between the two in that menu.
  export let api: ApiClient
  export let match: Match
  export let language: Language
  export let labels: Record<string, string>
  export let handlers: MatchHeaderHandlers
  export let title: string
  export let subtitle: string | null | undefined = undefined
  export let backLabel: string
  export let onBack: () => void
  export let pageActions: PageAction[] = []
  // The match sub-page being shown, so the menu leaves out the link to itself.
  export let current: 'match' | 'metadata' | 'devices' | 'print' | null = null
  // Participant pages turn this off: their header then offers only the participant's own actions, so a
  // destructive match-level item (Delete match) can't be mistaken for the participant's Remove.
  export let matchActions = true

  let actionError = ''
  let showCopyForm = false
  let copyIncludeParticipants = false
  let copying = false
  let copyError = ''

  $: sortedLiveScopes = [...(match.liveScopes ?? [])].sort((a, b) => a.scope.localeCompare(b.scope))
  $: resultsAction = sortedLiveScopes.length === 1
    ? [{ label: labels.resultsLabel, header: 'primary', href: matchResultsPath(match.id, sortedLiveScopes[0].scope), newTab: true } satisfies PageAction]
    : sortedLiveScopes.length > 1
      ? [{ label: labels.resultsLabel, header: 'primary', children: sortedLiveScopes.map((scope) => ({ label: scope.scope, href: matchResultsPath(match.id, scope.scope), newTab: true })) } satisfies PageAction]
      : []
  $: matchItems = [
    ...(current !== 'metadata' ? [navigationItem(labels.editMetadata, matchEditPath(match.id))] : []),
    ...(current !== 'devices' ? [navigationItem(labels.manageDevices, matchDevicesPath(match.id))] : []),
    ...(current !== 'print' ? [navigationItem(labels.printScorecards, matchPrintPath(match.id))] : []),
    { label: labels.importExport, children: [
      { label: labels.exportCsv, onClick: exportCsv },
      { label: labels.exportLaneAssignments, onClick: exportLaneAssignments }
    ] },
    { label: labels.copyMatch, onClick: () => (showCopyForm = !showCopyForm) }
  ] satisfies PageAction[]
  $: actions = !matchActions ? pageActions : [
    { label: match.isOpen ? labels.deactivate : labels.activate, header: 'highlight', active: match.isOpen, onClick: handlers.onToggleOpen },
    { label: labels.viewQrCodes, header: 'icon', headerLabel: '▦', href: matchQrPath(match.id), newTab: true },
    ...resultsAction,
    ...asGroup(pageActions),
    ...asGroup(matchItems),
    { label: labels.deleteMatch, danger: true, separatorBefore: true, onClick: remove }
  ] satisfies PageAction[]

  function navigationItem(label: string, path: string): PageAction {
    return { label, href: path, onClick: () => handlers.onNavigate(path) }
  }

  async function download(fetchFile: () => Promise<{ blob: Blob; filename: string }>, errorKey: string) {
    actionError = ''
    try {
      const { blob, filename } = await fetchFile()
      saveBlob(blob, filename)
    } catch (error) {
      actionError = labelForError(error, labels, errorKey)
    }
  }

  function exportCsv() {
    return download(() => api.downloadMatchExport(match.id, language), 'exportError')
  }

  function exportLaneAssignments() {
    return download(() => api.downloadLaneAssignmentExport(match.id, language), 'exportLaneAssignmentsError')
  }

  async function remove() {
    actionError = ''
    if (!confirm(labels.deleteMatchConfirm.replace('{name}', match.name))) return
    try {
      await api.deleteMatch(match.id)
      handlers.onDeleted()
    } catch (error) {
      actionError = labelForError(error, labels, 'matchDeleteError')
    }
  }

  async function copyMatch() {
    copyError = ''
    copying = true
    try {
      const copy = await duplicateMatch(api, match, labels.copyOfMatchName.replace('{name}', match.name), copyIncludeParticipants)
      showCopyForm = false
      handlers.onCopied(copy)
    } catch (error) {
      copyError = labelForError(error, labels, 'matchCopyError')
    } finally {
      copying = false
    }
  }
</script>

<PageHeader eyebrow={labels.matches} {title} {subtitle} {backLabel} {onBack} {actions} menuLabel={labels.matchActions}><slot /></PageHeader>
{#if actionError}<p class="error">{actionError}</p>{/if}
{#if showCopyForm}
  <div class="panel entry-card duplicate-card">
    <h2>{labels.copyMatch}</h2>
    <label class="checkbox-label"><input type="checkbox" bind:checked={copyIncludeParticipants} /> {labels.copyIncludeParticipantsLabel}</label>
    <div class="duplicate-actions">
      <button type="button" class="text-button" on:click={() => (showCopyForm = false)}>{labels.cancel}</button>
      <button class="primary" type="button" disabled={copying} on:click={copyMatch}>{labels.copyMatch}</button>
    </div>
    {#if copyError}<p class="error">{copyError}</p>{/if}
  </div>
{/if}

<style>
  .duplicate-card {
    display: grid;
    gap: 18px;
    margin-bottom: 24px;
  }

  .duplicate-actions {
    display: flex;
    justify-content: flex-end;
    align-items: center;
    gap: 16px;
    padding-top: 16px;
    border-top: 1px solid var(--line);
  }

  .duplicate-card .error {
    margin: 0;
  }
</style>
