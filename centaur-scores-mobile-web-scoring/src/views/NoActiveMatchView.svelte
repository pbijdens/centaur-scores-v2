<script lang="ts">
  import Icon from '../components/Icon.svelte';
  import { t } from '../lib/i18n';
  import { fetchMatchInfo } from '../lib/syncService';

  let retrying = $state(false);

  async function retry() {
    retrying = true;
    await fetchMatchInfo();
    retrying = false;
  }
</script>

<div class="view">
  <div class="empty-state">
    <Icon name="error" size={64} />
    <h1>{$t('noActiveMatchTitle')}</h1>
    <p>{$t('noActiveMatchBody')}</p>
    <button class="button" onclick={retry} disabled={retrying}>
      {$t('retry')}
    </button>
  </div>
</div>
