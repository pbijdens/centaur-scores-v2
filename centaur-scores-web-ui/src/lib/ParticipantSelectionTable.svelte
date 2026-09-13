<script lang="ts">
  import type { ApiClient } from './api'
  import { labelForError } from './errors'
  import type { Category, ParticipantListMember } from './types'

  export let api: ApiClient
  export let matchId: string
  export let members: ParticipantListMember[]
  export let categories: Category[]
  export let assignedMemberIds: Set<string>
  export let labels: Record<string, string>
  export let onApplied: () => void

  let filterText = ''
  let selectedIds = new Set<string>()
  let applying = false
  let applyError = ''

  function categoryValue(member: ParticipantListMember, category: Category): string {
    return category.values.find((value) => value.valueId === member.categories[category.id])?.name ?? ''
  }

  function matchesFilter(member: ParticipantListMember, filter: string): boolean {
    if (!filter) return true
    const haystack = `${member.fullName || member.lastName} ${member.federationNumber ?? ''}`.toLowerCase()
    return haystack.includes(filter)
  }

  $: normalizedFilter = filterText.trim().toLowerCase()
  $: unassignedRows = members
    .filter((member) => member.isActive && !assignedMemberIds.has(member.id) && matchesFilter(member, normalizedFilter))
    .sort((a, b) => (a.fullName || a.lastName).localeCompare(b.fullName || b.lastName))
  $: assignedRows = members
    .filter((member) => assignedMemberIds.has(member.id))
    .sort((a, b) => (a.fullName || a.lastName).localeCompare(b.fullName || b.lastName))
  $: rows = [...unassignedRows, ...assignedRows]

  function toggle(memberId: string) {
    const next = new Set(selectedIds)
    if (next.has(memberId)) next.delete(memberId)
    else next.add(memberId)
    selectedIds = next
  }

  async function apply() {
    if (selectedIds.size === 0) return
    applyError = ''
    applying = true
    try {
      for (const memberId of selectedIds) {
        const member = members.find((item) => item.id === memberId)
        if (!member) continue
        await api.addMatchParticipant(matchId, { participantListMemberId: member.id, lastName: member.lastName, fullName: member.fullName, federationNumber: member.federationNumber, categories: member.categories })
      }
      selectedIds = new Set()
      onApplied()
    } catch (error) {
      applyError = labelForError(error, labels, 'addParticipantError')
    } finally {
      applying = false
    }
  }
</script>

<div class="participant-select">
  <input class="filter-input" type="text" placeholder={labels.filterParticipantsPlaceholder} bind:value={filterText} />
  <div class="table-scroll">
    <table class="data-table">
      <thead>
        <tr>
          <th class="col-check"></th>
          <th>{labels.federationNumberLabel}</th>
          <th>{labels.fullNameLabel}</th>
          {#each categories as category}<th>{category.name}</th>{/each}
        </tr>
      </thead>
      <tbody>
        {#each rows as member (member.id)}
          {@const isAssigned = assignedMemberIds.has(member.id)}
          <tr class:assigned-row={isAssigned}>
            <td class="col-check"><input type="checkbox" checked={isAssigned || selectedIds.has(member.id)} disabled={isAssigned} on:change={() => toggle(member.id)} /></td>
            <td>{member.federationNumber ?? ''}</td>
            <td>{member.fullName || member.lastName}</td>
            {#each categories as category}<td>{categoryValue(member, category)}</td>{/each}
          </tr>
        {/each}
        {#if rows.length === 0}<tr><td class="empty-row" colspan={3 + categories.length}>{labels.noMatchingParticipants}</td></tr>{/if}
      </tbody>
    </table>
  </div>
  {#if applyError}<p class="error">{applyError}</p>{/if}
  <button class="primary" type="button" disabled={selectedIds.size === 0 || applying} on:click={apply}>{labels.applyLabel}</button>
</div>

<style>
  .participant-select {
    display: flex;
    flex-direction: column;
    gap: 12px;
  }

  .filter-input {
    max-width: 320px;
  }

  .table-scroll {
    overflow-x: auto;
    max-height: 360px;
    overflow-y: auto;
    border: 1px solid var(--line);
  }

  .data-table {
    width: 100%;
    border-collapse: collapse;
  }

  .data-table th,
  .data-table td {
    text-align: left;
    padding: 8px 10px;
    border-bottom: 1px solid var(--line);
    white-space: nowrap;
  }

  .data-table thead th {
    position: sticky;
    top: 0;
    background: var(--paper);
  }

  .col-check {
    width: 32px;
  }

  .assigned-row {
    background: #eaf3e9;
  }

  .empty-row {
    color: var(--muted);
    white-space: normal;
  }
</style>
