<script lang="ts">
  import { onDestroy, onMount } from 'svelte'
  import { makeExpiryCache } from './sessionWatch'

  export let token: string
  export let labels: Record<string, string>
  export let onExpired: () => void

  const CHECK_INTERVAL_MS = 15000
  const expiryMsFor = makeExpiryCache()
  let handled = false
  let interval: ReturnType<typeof setInterval> | undefined

  function checkExpiry() {
    if (handled) return
    const expiryMs = expiryMsFor(token)
    if (expiryMs !== null && Date.now() >= expiryMs) {
      handled = true
      window.alert(labels.sessionExpiredMessage)
      onExpired()
    }
  }

  function handleVisibilityChange() {
    if (document.visibilityState === 'visible') checkExpiry()
  }

  onMount(() => {
    checkExpiry()
    interval = setInterval(checkExpiry, CHECK_INTERVAL_MS)
    document.addEventListener('visibilitychange', handleVisibilityChange)
  })

  onDestroy(() => {
    if (interval) clearInterval(interval)
    document.removeEventListener('visibilitychange', handleVisibilityChange)
  })
</script>
