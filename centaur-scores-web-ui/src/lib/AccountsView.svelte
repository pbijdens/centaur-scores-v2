<script lang="ts">
  import type { ApiClient } from './api'
  import { labelForError } from './errors'
  import type { Account } from './types'

  export let api: ApiClient
  export let accounts: Account[]
  export let labels: Record<string, string>
  export let onOpenAccount: (account: Account) => void
  export let onBack: () => void

  let showAddForm = false
  let newAccountUsername = ''
  let createError = ''

  function authorizationLabel(authorization: string): string {
    if (authorization === 'Administrator') return labels.authAdministrator
    if (authorization === 'Manager') return labels.authManager
    return labels.authViewer
  }

  async function submitAdd() {
    if (!newAccountUsername.trim()) return
    createError = ''
    try {
      const account = await api.createAccount({ username: newAccountUsername.trim() })
      newAccountUsername = ''
      showAddForm = false
      onOpenAccount(account)
    } catch (error) {
      createError = labelForError(error, labels, 'accountCreateError')
    }
  }
</script>

<button class="back-link" on:click={onBack}>← {labels.home}</button>
<div class="page-intro">
  <div><p class="eyebrow">{labels.eyebrowAccounts}</p><h1>{labels.accounts}</h1><p class="muted">{labels.accountsHint}</p></div>
  <button class="primary" on:click={() => (showAddForm = !showAddForm)}>+ {labels.newAccount}</button>
</div>
{#if showAddForm}
  <form class="inline-form" on:submit|preventDefault={submitAdd}>
    <label>{labels.usernameLabel}<input bind:value={newAccountUsername} /></label>
    <button class="primary" type="submit" disabled={!newAccountUsername.trim()}>{labels.save}</button>
  </form>
  {#if createError}<p class="error">{createError}</p>{/if}
{/if}
<section class="list-panel">
  {#if accounts.length === 0}<p class="empty-state">{labels.emptyState}</p>{/if}
  {#each accounts as account}
    <button class="list-row" on:click={() => onOpenAccount(account)}>
      <span class="management-icon">◇</span>
      <span><strong>{account.username}</strong><small>{account.displayName ?? ''}</small></span>
      <span class="tag">{authorizationLabel(account.authorization)}</span>
      <span class="arrow">→</span>
    </button>
  {/each}
</section>
