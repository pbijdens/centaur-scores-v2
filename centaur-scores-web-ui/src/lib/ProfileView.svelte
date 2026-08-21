<script lang="ts">
  import type { ApiClient } from './api'
  import { labelForError } from './errors'
  import type { Profile } from './types'

  export let api: ApiClient
  export let labels: Record<string, string>
  export let onBack: () => void

  let profile: Profile | null = null
  let displayName = ''
  let email = ''
  let currentPassword = ''
  let newPassword = ''
  let confirmPassword = ''
  let detailsMessage = ''
  let detailsError = ''
  let passwordMessage = ''
  let passwordError = ''

  async function loadProfile() {
    profile = await api.fetchProfile()
    displayName = profile.displayName ?? ''
    email = profile.email ?? ''
  }
  loadProfile()

  async function saveDetails() {
    detailsMessage = ''
    detailsError = ''
    try {
      profile = await api.updateProfile({ displayName, email })
      detailsMessage = labels.profileSaved
    } catch {
      detailsError = labels.profileSaveError
    }
  }

  async function savePassword() {
    passwordMessage = ''
    passwordError = ''
    if (newPassword !== confirmPassword) { passwordError = labels.passwordMismatch; return }
    try {
      await api.changePassword(currentPassword, newPassword)
      currentPassword = ''; newPassword = ''; confirmPassword = ''
      passwordMessage = labels.passwordSaved
    } catch (error) {
      passwordError = labelForError(error, labels, 'currentPasswordWrong')
    }
  }
</script>

<button class="back-link" on:click={onBack}>← {labels.home}</button>
<div class="page-intro">
  <div><p class="eyebrow">{labels.eyebrowAccount}</p><h1>{labels.profile}</h1></div>
</div>
<div class="profile-grid">
  <section class="panel">
    <h2>{labels.profileDetails}</h2>
    <p class="muted">{labels.profileDetailsHint}</p>
    <form on:submit|preventDefault={saveDetails}>
      <label>{labels.displayName}<input bind:value={displayName} /></label>
      <label>{labels.email}<input type="email" bind:value={email} /></label>
      {#if detailsError}<p class="error">{detailsError}</p>{/if}
      {#if detailsMessage}<p class="success">{detailsMessage}</p>{/if}
      <button class="primary" type="submit">{labels.save}</button>
    </form>
  </section>
  <section class="panel">
    <h2>{labels.changePassword}</h2>
    <p class="muted">{labels.changePasswordHint}</p>
    <form on:submit|preventDefault={savePassword}>
      <label>{labels.currentPassword}<input type="password" bind:value={currentPassword} autocomplete="current-password" /></label>
      <label>{labels.newPassword}<input type="password" bind:value={newPassword} autocomplete="new-password" /></label>
      <label>{labels.confirmPassword}<input type="password" bind:value={confirmPassword} autocomplete="new-password" /></label>
      {#if passwordError}<p class="error">{passwordError}</p>{/if}
      {#if passwordMessage}<p class="success">{passwordMessage}</p>{/if}
      <button class="primary" type="submit" disabled={!currentPassword || !newPassword}>{labels.save}</button>
    </form>
  </section>
</div>
