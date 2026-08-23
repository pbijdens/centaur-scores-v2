<script lang="ts">
  import { t } from '../lib/i18n';
  import type { ParticipantCategoryValue, ScoreKeeperCategory } from '../lib/types';

  interface Props {
    categories: ScoreKeeperCategory[];
    initialFederationNumber?: string | null;
    initialName?: string;
    initialCategoryValues?: ParticipantCategoryValue[];
    saving: boolean;
    onSave: (fields: { federationNumber: string | null; name: string; categories: ParticipantCategoryValue[] }) => void;
    onCancel: () => void;
  }

  let {
    categories,
    initialFederationNumber = null,
    initialName = '',
    initialCategoryValues = [],
    saving,
    onSave,
    onCancel,
  }: Props = $props();

  let federationNumber = $state(initialFederationNumber ?? '');
  let name = $state(initialName);
  let selected = $state<Record<string, string>>(
    Object.fromEntries(categories.map((c) => [c.id, initialCategoryValues.find((v) => v.id === c.id)?.value ?? ''])),
  );

  const isValid = $derived(name.trim().length > 0 && categories.every((c) => !!selected[c.id]));

  function submit() {
    if (!isValid || saving) return;
    const categoryValues: ParticipantCategoryValue[] = categories.map((c) => ({
      id: c.id,
      name: c.name,
      value: selected[c.id],
    }));
    onSave({ federationNumber: federationNumber.trim() || null, name: name.trim(), categories: categoryValues });
  }
</script>

<div class="form">
  <div class="field">
    <label for="federation-number">{$t('federationNumber')}</label>
    <input id="federation-number" type="text" bind:value={federationNumber} disabled={saving} />
  </div>

  <div class="field">
    <label for="participant-name">{$t('fullName')}</label>
    <input id="participant-name" type="text" bind:value={name} disabled={saving} required />
  </div>

  {#each categories as category (category.id)}
    <div class="field">
      <label for="category-{category.id}">{category.name}</label>
      <select id="category-{category.id}" bind:value={selected[category.id]} disabled={saving}>
        <option value="" disabled>{$t('categoryRequired')}</option>
        {#each category.values as value (value.id)}
          <option value={value.name}>{value.name}</option>
        {/each}
      </select>
    </div>
  {/each}

  <div class="actions">
    <button class="button secondary" onclick={onCancel} disabled={saving}>{$t('cancel')}</button>
    <button class="button" onclick={submit} disabled={!isValid || saving}>{$t('save')}</button>
  </div>
</div>

<style lang="scss">
  .actions {
    display: flex;
    gap: 0.75rem;
    margin-top: 1.5rem;

    .button {
      flex: 1;
    }
  }
</style>
