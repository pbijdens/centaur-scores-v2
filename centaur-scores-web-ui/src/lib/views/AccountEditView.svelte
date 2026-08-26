<script lang="ts">
  import type { ApiClient } from '../api'
  import type { Account } from '../types'

  export let api: ApiClient
  export let accountId: string
  export let currentAccountId: string | null
  export let labels: Record<string, string>
  export let onBack: () => void

  let account: Account | null = null
  let displayName = ''
  let email = ''
  let authorization = ''
  let newPassword = ''
  let saveMessage = ''
  let saveError = ''

  async function loadAccount() {
    account = await api.fetchAccount(accountId)
    displayName = account.displayName ?? ''
    email = account.email ?? ''
    authorization = account.authorization
  }
  loadAccount()

  $: isOwnAccount = account?.id === currentAccountId

  async function save() {
    if (!account) return
    saveMessage = ''
    saveError = ''
    try {
      account = await api.updateAccount(account.id, {
        username: account.username,
        password: newPassword || undefined,
        displayName,
        email,
        authorization
      })
      newPassword = ''
      saveMessage = labels.accountSaved
    } catch {
      saveError = labels.accountSaveError
    }
  }
</script>

<button class="back-link" on:click={onBack}>← {labels.accounts}</button>
{#if account}
  <div class="page-intro">
    <div><p class="eyebrow">{labels.eyebrowAccounts}</p><h1>{account.username}</h1></div>
  </div>
  <section class="panel">
    <form on:submit|preventDefault={save}>
      <label>{labels.accountRealName}<input bind:value={displayName} /></label>
      <label>{labels.email}<input type="email" bind:value={email} /></label>
      <label>{labels.accountAuthorization}
        <select bind:value={authorization} disabled={isOwnAccount}>
          <option value="Viewer">{labels.authViewer}</option>
          <option value="Manager">{labels.authManager}</option>
          <option value="Administrator">{labels.authAdministrator}</option>
        </select>
      </label>
      {#if isOwnAccount}<p class="muted">{labels.ownAuthorizationHint}</p>{/if}
      <label>{labels.newPassword}<input type="password" bind:value={newPassword} autocomplete="new-password" /></label>
      <p class="muted">{labels.newPasswordOptionalHint}</p>
      {#if saveError}<p class="error">{saveError}</p>{/if}
      {#if saveMessage}<p class="success">{saveMessage}</p>{/if}
      <button class="primary" type="submit">{labels.save}</button>
    </form>
  </section>
{/if}
