<script lang="ts">
  import { t } from '../lib/i18n';

  interface Props {
    onConfirm: () => void;
    onCancel: () => void;
  }

  let { onConfirm, onCancel }: Props = $props();
</script>

<div
  class="overlay"
  role="alertdialog"
  aria-modal="true"
  onpointerdown={(event) => event.stopPropagation()}
  onpointermove={(event) => event.stopPropagation()}
  onpointerup={(event) => event.stopPropagation()}
  onpointercancel={(event) => event.stopPropagation()}
>
  <div class="dialog">
    <h2>{$t('signConfirmTitle')}</h2>
    <p>{$t('signConfirmBody')}</p>
    <div class="choices">
      <button type="button" class="button secondary" onclick={onCancel}>{$t('cancel')}</button>
      <button type="button" class="button" onclick={onConfirm}>{$t('confirmAction')}</button>
    </div>
  </div>
</div>

<style lang="scss">
  @use '../styles/variables' as v;

  .overlay {
    position: fixed;
    inset: 0;
    background: rgba(0, 0, 0, 0.5);
    display: flex;
    align-items: flex-end;
    justify-content: center;
    z-index: 100;

    @media (min-width: v.$breakpoint-tablet) {
      align-items: center;
    }
  }

  .dialog {
    width: 100%;
    max-width: 32rem;
    background: v.$color-surface;
    border-radius: v.$radius v.$radius 0 0;
    padding: 1.25rem;

    @media (min-width: v.$breakpoint-tablet) {
      border-radius: v.$radius;
    }
  }

  .choices {
    display: flex;
    gap: 0.6rem;
    margin-top: 1rem;
  }

  .choices .button {
    flex: 1;
  }
</style>
