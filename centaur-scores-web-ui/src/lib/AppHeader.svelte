<script lang="ts">
  import DropdownMenu from './DropdownMenu.svelte'
  import { navigateOnClick } from './router'
  import type { Language, View } from './types'

  export let username: string
  export let language: Language
  export let view: View
  export let labels: Record<string, string>
  export let tenantName: string | null | undefined = undefined
  export let tenantLogoUrl: string | null | undefined = undefined
  export let showTenantSwitch: boolean = false
  export let onNavigate: (path: string) => void
  export let onLanguageChange: (value: string) => void
  export let onLogout: () => void
</script>

<header>
  <a class="brand" href="/" on:click={(event) => navigateOnClick(event, () => onNavigate('/'))}>
    {#if tenantLogoUrl}
      <img class="brand-mark small" src={tenantLogoUrl} alt="" />
    {:else}
      <span class="brand-mark small">{labels.brandInitials}</span>
    {/if}
    <span><strong>{tenantName ?? labels.defaultTenantName}</strong><small>{labels.rootTenantLabel}</small></span>
  </a>
  <nav>
    <a class:active={view === 'home'} href="/" on:click={(event) => navigateOnClick(event, () => onNavigate('/'))}>{labels.home}</a>
    <a class:active={view === 'matches' || view === 'match'} href="/matches" on:click={(event) => navigateOnClick(event, () => onNavigate('/matches'))}>{labels.matches}</a>
    <a class:active={view === 'competitions'} href="/competitions" on:click={(event) => navigateOnClick(event, () => onNavigate('/competitions'))}>{labels.competitions}</a>
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
        >
          <svg class="flag-icon" viewBox="0 0 60 36" aria-hidden="true" focusable="false">
            <rect width="60" height="36" fill="#00247d" />
            <path d="M0,0 60,36 M60,0 0,36" stroke="#fff" stroke-width="6" />
            <path d="M0,0 60,36 M60,0 0,36" stroke="#cf142b" stroke-width="2" />
            <path d="M30,0 30,36 M0,18 60,18" stroke="#fff" stroke-width="10" />
            <path d="M30,0 30,36 M0,18 60,18" stroke="#cf142b" stroke-width="6" />
          </svg>
        </button>
        <button
          type="button"
          class="lang-flag-button"
          class:active={language === 'nl'}
          aria-label={labels.languageDutch}
          on:click={() => onLanguageChange('nl')}
        >
          <svg class="flag-icon" viewBox="0 0 60 36" aria-hidden="true" focusable="false">
            <rect width="60" height="12" y="0" fill="#ae1c28" />
            <rect width="60" height="12" y="12" fill="#fff" />
            <rect width="60" height="12" y="24" fill="#21468b" />
          </svg>
        </button>
      </div>
      <hr class="menu-separator" />
      {#if showTenantSwitch}
        <a class="menu-item" class:active={view === 'select-tenant'} href="/select-tenant" on:click={(event) => navigateOnClick(event, () => onNavigate('/select-tenant'))}>{labels.switchTenant}</a>
      {/if}
      <a class="menu-item" class:active={view === 'profile'} href="/profile" on:click={(event) => navigateOnClick(event, () => onNavigate('/profile'))}>{labels.editMyProfile}</a>
      <button class="menu-item" on:click={onLogout}>{labels.logout}</button>
    </DropdownMenu>
  </div>
</header>
