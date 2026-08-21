<script lang="ts">
  import type { ApiClient } from './api'
  import { labelForError } from './errors'
  import type { Match } from './types'

  export let api: ApiClient
  export let match: Match
  export let labels: Record<string, string>
  export let onBack: () => void
  export let onChanged: () => void

  let showAddForm = false
  let newDeviceName = ''
  let createError = ''
  let deleteError = ''

  $: devices = match.devices ?? []
  $: participants = match.participants ?? []

  function assignedCount(deviceId: string): number {
    return participants.filter((participant) => participant.deviceId === deviceId).length
  }

  async function submitAdd() {
    if (!newDeviceName.trim()) return
    createError = ''
    try {
      await api.addDevice(match.id, { name: newDeviceName.trim() })
      newDeviceName = ''
      showAddForm = false
      onChanged()
    } catch (error) {
      createError = labelForError(error, labels, 'deviceCreateError')
    }
  }

  async function removeDevice(deviceId: string, name: string) {
    deleteError = ''
    if (!confirm(labels.deleteDeviceConfirm.replace('{name}', name))) return
    try {
      await api.deleteDevice(match.id, deviceId)
      onChanged()
    } catch (error) {
      deleteError = labelForError(error, labels, 'deviceDeleteError')
    }
  }
</script>

<button class="back-link" on:click={onBack}>← {match.name}</button>
<div class="page-intro">
  <div><p class="eyebrow">{labels.eyebrowDevices}</p><h1>{labels.manageDevices}</h1><p class="muted">{labels.devicesHint}</p></div>
  <button class="primary" on:click={() => (showAddForm = !showAddForm)}>+ {labels.newDevice}</button>
</div>
{#if showAddForm}
  <form class="inline-form" on:submit|preventDefault={submitAdd}>
    <label>{labels.deviceNameLabel}<input bind:value={newDeviceName} /></label>
    <button class="primary" type="submit" disabled={!newDeviceName.trim()}>{labels.save}</button>
  </form>
  {#if createError}<p class="error">{createError}</p>{/if}
{/if}
<section class="list-panel">
  {#if devices.length === 0}<p class="empty-state">{labels.emptyState}</p>{/if}
  {#each devices as device}
    <div class="list-row">
      <span class="management-icon">◇</span>
      <span><strong>{device.name}</strong><small>{assignedCount(device.id)} {labels.membersLabel.toLowerCase()}</small></span>
      <button class="icon-button" aria-label={labels.removeValue} on:click={() => removeDevice(device.id, device.name)}>🗑</button>
    </div>
  {/each}
</section>
{#if deleteError}<p class="error">{deleteError}</p>{/if}
