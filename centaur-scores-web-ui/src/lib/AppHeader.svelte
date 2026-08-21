<script lang="ts">
  import type { Language, View } from './types'

  export let username: string
  export let language: Language
  export let view: View
  export let labels: Record<string, string>
  export let onNavigate: (path: string) => void
  export let onLanguageChange: (value: string) => void
  export let onLogout: () => void
</script>

<header>
  <button class="brand" on:click={() => onNavigate('/')}>
    <span class="brand-mark small">CS</span>
    <span><strong>Centaur Scores</strong><small>Root Tenant</small></span>
  </button>
  <nav>
    <button class:active={view === 'home'} on:click={() => onNavigate('/')}>{labels.home}</button>
    <button class:active={view === 'matches' || view === 'match'} on:click={() => onNavigate('/matches')}>{labels.matches}</button>
    <button class:active={view === 'competitions'} on:click={() => onNavigate('/competitions')}>{labels.competitions}</button>
  </nav>
  <div class="user-menu">
    <select value={language} on:change={(event) => onLanguageChange(event.currentTarget.value)} aria-label="Language">
      <option value="en">EN</option>
      <option value="nl">NL</option>
    </select>
    <button class="profile-button" class:active={view === 'profile'} on:click={() => onNavigate('/profile')} aria-label={labels.profile}>
      <span class="avatar">{username.slice(0, 1).toUpperCase()}</span>
      <span class="name">{username}</span>
    </button>
    <button class="text-button" on:click={onLogout}>{labels.logout}</button>
  </div>
</header>
