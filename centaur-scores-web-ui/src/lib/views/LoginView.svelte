<script lang="ts">
  import type { Tenant } from '../types'
  import type { Language } from '../types'

  export let tenant: string
  export let tenants: Tenant[]
  export let tenantsLoading: boolean
  export let tenantsError: string
  export let username: string
  export let password: string
  export let loginError: string
  export let language: Language
  export let labels: Record<string, string>
  export let onSubmit: () => void
  export let onLanguageChange: (value: string) => void
</script>

<main class="login-shell">
  <section class="login-card">
    <div class="brand-mark">{labels.brandInitials}</div>
    <p class="eyebrow">{labels.eyebrowLogin}</p>
    <h1>{labels.login}</h1>
    <form autocomplete="off" on:submit|preventDefault={onSubmit}>
      <label>{labels.tenantLabel}
        <select bind:value={tenant} autocomplete="off" disabled={tenantsLoading || tenants.length === 0}>
          <option value="" disabled>{tenantsLoading ? labels.loadingTenants : labels.selectTenant}</option>
          {#each tenants as availableTenant}<option value={availableTenant.id}>{availableTenant.name}</option>{/each}
        </select>
      </label>
      {#if tenantsError}<p class="error">{tenantsError}</p>{/if}
      <label>{labels.usernameLabel}<input type="text" bind:value={username} autocomplete="username" autocapitalize="off" autocorrect="off" spellcheck="false" /></label>
      <label>{labels.passwordLabel}<input type="password" bind:value={password} autocomplete="current-password" autocapitalize="off" autocorrect="off" spellcheck="false" /></label>
      {#if loginError}<p class="error">{loginError}</p>{/if}
      <button class="primary" disabled={!username || !password || tenants.length === 0}>{labels.signIn}<span>→</span></button>
    </form>
    <select class="language" value={language} on:change={(event) => onLanguageChange(event.currentTarget.value)}>
      <option value="en">{labels.languageEnglish}</option>
      <option value="nl">{labels.languageDutch}</option>
    </select>
  </section>
</main>
