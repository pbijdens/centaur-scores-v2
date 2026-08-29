<script lang="ts">
  export let labels: Record<string, string>
  export let onLogout: () => void
</script>

<main class="login-shell">
  <section class="login-card">
    <div class="brand-mark">{labels.brandInitials}</div>
    <p class="eyebrow">{labels.noTenantAccessTitle}</p>
    <p>{labels.noTenantAccessMessage}</p>
    <button class="primary" on:click={onLogout}>{labels.logout}</button>
  </section>
</main>
