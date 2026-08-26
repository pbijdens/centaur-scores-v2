<script lang="ts">
  import DropdownMenu from './DropdownMenu.svelte'
  import type { Language, View } from './types'

  export let username: string
  export let language: Language
  export let view: View
  export let labels: Record<string, string>
  export let tenantName: string | null | undefined = undefined
  export let tenantLogoUrl: string | null | undefined = undefined
  export let onNavigate: (path: string) => void
  export let onLanguageChange: (value: string) => void
  export let onLogout: () => void
</script>

<header>
  <button class="brand" on:click={() => onNavigate('/')}>
    {#if tenantLogoUrl}
      <img class="brand-mark small" src={tenantLogoUrl} alt="" />
    {:else}
      <span class="brand-mark small">{labels.brandInitials}</span>
    {/if}
    <span><strong>{tenantName ?? labels.defaultTenantName}</strong><small>{labels.rootTenantLabel}</small></span>
  </button>
  <nav>
    <button class:active={view === 'home'} on:click={() => onNavigate('/')}>{labels.home}</button>
    <button class:active={view === 'matches' || view === 'match'} on:click={() => onNavigate('/matches')}>{labels.matches}</button>
    <button class:active={view === 'competitions'} on:click={() => onNavigate('/competitions')}>{labels.competitions}</button>
  </nav>
  <div class="user-menu">
    <DropdownMenu ariaLabel={labels.profile} buttonClass="menu-trigger profile-button" align="right">
      <svelte:fragment slot="trigger">
        <span class="avatar">{username.slice(0, 1).toUpperCase()}</span>
        <span class="name">{username}</span>
      </svelte:fragment>
      <div class="menu-section">
        <button
          type="button"
          class="lang-flag-button"
          class:active={language === 'en'}
          aria-label={labels.languageEnglish}
          on:click={() => onLanguageChange('en')}
        >🇬🇧</button>
        <button
          type="button"
          class="lang-flag-button"
          class:active={language === 'nl'}
          aria-label={labels.languageDutch}
          on:click={() => onLanguageChange('nl')}
        >🇳🇱</button>
      </div>
      <hr class="menu-separator" />
      <button class="menu-item" class:active={view === 'profile'} on:click={() => onNavigate('/profile')}>{labels.editMyProfile}</button>
      <button class="menu-item" on:click={onLogout}>{labels.logout}</button>
    </DropdownMenu>
  </div>
</header>
