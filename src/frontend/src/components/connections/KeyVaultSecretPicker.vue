<template>
  <div class="kv-picker">
    <!-- When a reference is already set, show it as a chip -->
    <div v-if="modelValue" class="kv-reference-chip">
      <i class="pi pi-lock" />
      <span class="kv-ref-text">{{ modelValue.vaultName }} / {{ modelValue.secretName }}</span>
      <span class="kv-ref-badge">KV Ref</span>
      <Button icon="pi pi-pencil" text size="small" @click="open" v-tooltip="'Change secret'" class="kv-edit-btn" />
      <Button icon="pi pi-times" text size="small" severity="danger" @click="clear" v-tooltip="'Clear — use manual secret instead'" class="kv-clear-btn" />
    </div>
    <Button
      v-else
      label="Pick from Key Vault…"
      icon="pi pi-lock"
      severity="secondary"
      size="small"
      class="browse-btn"
      @click="open"
    />

    <!-- Step 1 — Vault selection -->
    <Dialog v-model:visible="step1Visible" modal header="Select Key Vault" :style="{ width: '460px' }" :draggable="false">
      <div class="picker-body">
        <IconField iconPosition="left" class="search-field">
          <InputIcon class="pi pi-search" />
          <InputText v-model="vaultQuery" placeholder="Search vaults…" class="w-full" autofocus />
        </IconField>

        <div v-if="vaultsLoading" class="picker-loading">
          <ProgressSpinner style="width:28px;height:28px" strokeWidth="4" />
          <span>Loading Key Vaults…</span>
        </div>

        <div v-else-if="vaultsError" class="picker-error">
          <i class="pi pi-exclamation-triangle" />
          <span>{{ vaultsError }}</span>
          <Button label="Retry" size="small" text @click="loadVaults" />
        </div>

        <div v-else-if="filteredVaults.length === 0" class="picker-empty">
          <i class="pi pi-inbox" />
          <span>No Key Vaults found</span>
        </div>

        <div v-else class="picker-list">
          <div
            v-for="vault in filteredVaults"
            :key="vault.name"
            class="picker-item"
            :class="{ selected: selectedVault?.name === vault.name }"
            @click="selectVault(vault)"
          >
            <div class="item-icon vault">
              <i class="pi pi-lock" />
            </div>
            <div class="item-body">
              <span class="item-name">{{ vault.name }}</span>
              <span class="item-id">{{ vault.resourceGroup || '' }}{{ vault.location ? ` · ${vault.location}` : '' }}</span>
            </div>
            <div v-if="selectedVault?.name === vault.name" class="item-check">
              <i class="pi pi-check" />
            </div>
          </div>
        </div>
      </div>

      <template #footer>
        <Button label="Cancel" severity="secondary" @click="step1Visible = false" />
        <Button label="Next: Choose Secret →" :disabled="!selectedVault" @click="goToStep2" />
      </template>
    </Dialog>

    <!-- Step 2 — Secret selection -->
    <Dialog v-model:visible="step2Visible" modal :header="`Secrets in ${selectedVault?.name}`" :style="{ width: '460px' }" :draggable="false">
      <div class="picker-body">
        <div class="breadcrumb">
          <Button icon="pi pi-arrow-left" text size="small" @click="backToStep1" label="Change vault" class="back-btn" />
          <Tag :value="selectedVault?.name" severity="secondary" />
        </div>

        <IconField iconPosition="left" class="search-field">
          <InputIcon class="pi pi-search" />
          <InputText v-model="secretQuery" placeholder="Search secrets…" class="w-full" autofocus />
        </IconField>

        <div v-if="secretsLoading" class="picker-loading">
          <ProgressSpinner style="width:28px;height:28px" strokeWidth="4" />
          <span>Loading secrets…</span>
        </div>

        <div v-else-if="secretsError" class="picker-error">
          <i class="pi pi-exclamation-triangle" />
          <span>{{ secretsError }}</span>
          <Button label="Retry" size="small" text @click="loadSecrets" />
        </div>

        <div v-else-if="filteredSecrets.length === 0" class="picker-empty">
          <i class="pi pi-inbox" />
          <span>No secrets found</span>
        </div>

        <div v-else class="picker-list">
          <div
            v-for="secret in filteredSecrets"
            :key="secret"
            class="picker-item"
            :class="{ selected: selectedSecret === secret }"
            @click="selectedSecret = secret"
          >
            <div class="item-icon secret">
              <i class="pi pi-key" />
            </div>
            <div class="item-body">
              <span class="item-name">{{ secret }}</span>
              <span class="item-id">Resolved at runtime via DefaultAzureCredential</span>
            </div>
            <div v-if="selectedSecret === secret" class="item-check">
              <i class="pi pi-check" />
            </div>
          </div>
        </div>
      </div>

      <template #footer>
        <Button label="Cancel" severity="secondary" @click="step2Visible = false" />
        <Button label="Use this secret" :disabled="!selectedSecret" @click="confirmSecret" />
      </template>
    </Dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'
import InputIcon from 'primevue/inputicon'
import IconField from 'primevue/iconfield'
import ProgressSpinner from 'primevue/progressspinner'
import Tag from 'primevue/tag'
import { azureApi } from '@/api/azure'
import type { AzureKeyVaultInfo, KeyVaultSecretReference } from '@/api/types'

const props = defineProps<{
  modelValue?: KeyVaultSecretReference | null
  subscriptionId?: string
}>()

const emit = defineEmits<{
  'update:modelValue': [value: KeyVaultSecretReference | null]
}>()

// ── Vaults (step 1) ──────────────────────────────────────────────────────────
const step1Visible = ref(false)
const vaultsLoading = ref(false)
const vaultsError = ref<string | null>(null)
const vaults = ref<AzureKeyVaultInfo[]>([])
const vaultQuery = ref('')
const selectedVault = ref<AzureKeyVaultInfo | null>(null)

const filteredVaults = computed(() => {
  if (!vaultQuery.value.trim()) return vaults.value
  const q = vaultQuery.value.toLowerCase()
  return vaults.value.filter(v => v.name.toLowerCase().includes(q))
})

// ── Secrets (step 2) ─────────────────────────────────────────────────────────
const step2Visible = ref(false)
const secretsLoading = ref(false)
const secretsError = ref<string | null>(null)
const secrets = ref<string[]>([])
const secretQuery = ref('')
const selectedSecret = ref<string | null>(null)

const filteredSecrets = computed(() => {
  if (!secretQuery.value.trim()) return secrets.value
  const q = secretQuery.value.toLowerCase()
  return secrets.value.filter(s => s.toLowerCase().includes(q))
})

async function loadVaults() {
  vaultsLoading.value = true
  vaultsError.value = null
  vaults.value = []
  try {
    vaults.value = await azureApi.getKeyVaults(props.subscriptionId)
  } catch (e: any) {
    vaultsError.value = e?.response?.data?.error ?? 'Could not load Key Vaults.'
  } finally {
    vaultsLoading.value = false
  }
}

async function loadSecrets() {
  if (!selectedVault.value) return
  secretsLoading.value = true
  secretsError.value = null
  secrets.value = []
  try {
    secrets.value = await azureApi.getKeyVaultSecrets(selectedVault.value.name)
  } catch (e: any) {
    secretsError.value = e?.response?.data?.error ?? 'Could not load secrets.'
  } finally {
    secretsLoading.value = false
  }
}

async function open() {
  vaultQuery.value = ''
  step1Visible.value = true
  if (vaults.value.length === 0) await loadVaults()

  // Pre-select the vault that matches the current reference (editing case).
  if (props.modelValue?.vaultName) {
    const existing = vaults.value.find(v => v.name === props.modelValue!.vaultName)
    selectedVault.value = existing ?? null
  } else {
    selectedVault.value = null
  }
}

function selectVault(vault: AzureKeyVaultInfo) {
  selectedVault.value = vault
}

async function goToStep2() {
  step1Visible.value = false
  secretQuery.value = ''
  step2Visible.value = true
  await loadSecrets()
  // Pre-select the secret that matches the current reference (editing case).
  selectedSecret.value = props.modelValue?.vaultName === selectedVault.value?.name
    ? (props.modelValue?.secretName ?? null)
    : null
}

function backToStep1() {
  step2Visible.value = false
  step1Visible.value = true
}

function confirmSecret() {
  if (selectedVault.value && selectedSecret.value) {
    emit('update:modelValue', {
      vaultName: selectedVault.value.name,
      secretName: selectedSecret.value
    })
    step2Visible.value = false
  }
}

function clear() {
  emit('update:modelValue', null)
}

// Reload vaults when the subscription changes (while picker may already have cached data)
watch(() => props.subscriptionId, () => {
  vaults.value = []
  selectedVault.value = null
})
</script>

<style scoped>
.kv-picker {
  min-width: 0;
  width: 100%;
}

.browse-btn {
  width: 100%;
  justify-content: flex-start;
  margin-top: 6px;
}

.kv-reference-chip {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 7px 10px;
  margin-top: 6px;
  background: var(--p-green-50, #f0fdf4);
  border: 1.5px solid var(--p-green-200, #bbf7d0);
  border-radius: 6px;
  font-size: 0.8rem;
  color: var(--p-green-800, #166534);
}

.kv-ref-text {
  flex: 1;
  font-family: monospace;
  font-size: 0.78rem;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.kv-ref-badge {
  font-size: 0.65rem;
  font-weight: 700;
  background: var(--p-green-200, #bbf7d0);
  color: var(--p-green-800, #166534);
  border-radius: 10px;
  padding: 1px 7px;
  white-space: nowrap;
}

.kv-edit-btn,
.kv-clear-btn {
  padding: 0 !important;
  flex-shrink: 0;
}

.picker-body {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.search-field {
  width: 100%;
}

.breadcrumb {
  display: flex;
  align-items: center;
  gap: 8px;
}

.back-btn {
  padding-left: 0 !important;
}

.picker-loading,
.picker-error,
.picker-empty {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 10px;
  padding: 32px 16px;
  color: var(--p-text-muted-color);
  font-size: 0.875rem;
}

.picker-error {
  color: var(--p-red-500);
}

.picker-list {
  max-height: 300px;
  overflow-y: auto;
  border: 1px solid var(--p-content-border-color);
  border-radius: 6px;
}

.picker-item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 12px;
  cursor: pointer;
  transition: background 0.15s;
  border-bottom: 1px solid var(--p-content-border-color);
}

.picker-item:last-child {
  border-bottom: none;
}

.picker-item:hover,
.picker-item.selected {
  background: var(--p-highlight-background);
}

.item-icon {
  width: 32px;
  height: 32px;
  border-radius: 6px;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  font-size: 0.875rem;
}

.item-icon.vault {
  background: var(--p-blue-50, #eff6ff);
  color: var(--p-blue-600, #2563eb);
}

.item-icon.secret {
  background: var(--p-green-50, #f0fdf4);
  color: var(--p-green-700, #15803d);
}

.item-body {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 2px;
  overflow: hidden;
}

.item-name {
  font-size: 0.875rem;
  font-weight: 500;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.item-id {
  font-size: 0.72rem;
  color: var(--p-text-muted-color);
}

.item-check {
  color: var(--p-primary-color);
  font-size: 0.875rem;
  flex-shrink: 0;
}
</style>
