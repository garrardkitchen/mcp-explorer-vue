<template>
  <div class="azure-context-banner" :class="{ loading: state === 'loading', error: state === 'error' }">
    <div class="banner-icon">
      <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">
        <path d="M13.5 2L3 14h8l-1.5 8L21 10h-8z"/>
      </svg>
    </div>

    <template v-if="state === 'loading'">
      <div class="banner-content">
        <span class="banner-name">Connecting to Azure…</span>
        <span class="banner-sub">Checking DefaultAzureCredential</span>
      </div>
      <ProgressSpinner style="width:18px;height:18px" strokeWidth="4" />
    </template>

    <template v-else-if="state === 'error'">
      <div class="banner-content">
        <span class="banner-name">Azure not connected</span>
        <span class="banner-sub">
          Run <code>az login</code> locally · or set <code>HOST_AZURE_CONFIG_DIR=~/.azure</code> in <code>.env</code> and rebuild
        </span>
      </div>
      <Button icon="pi pi-refresh" text size="small" class="refresh-btn" @click="load" v-tooltip.left="'Retry'" />
    </template>

    <template v-else-if="account">
      <div class="banner-content">
        <span class="banner-name">{{ account.userPrincipalName || account.subscriptionName }}</span>
        <span class="banner-sub">{{ account.tenantId }}</span>
      </div>

      <!-- Subscription selector -->
      <div class="sub-selector">
        <Select
          v-model="selectedSubscription"
          :options="subscriptions"
          optionLabel="name"
          optionValue="id"
          placeholder="Loading…"
          :loading="subsLoading"
          filter
          filterPlaceholder="Search subscriptions…"
          class="sub-select"
          @change="onSubscriptionChange"
          v-tooltip.top="'Switch subscription'"
        >
          <template #value="{ value }">
            <div class="sub-value">
              <i class="pi pi-database sub-icon" />
              <span class="sub-name">{{ subscriptions.find(s => s.id === value)?.name ?? 'Select subscription…' }}</span>
            </div>
          </template>
          <template #option="{ option }">
            <div class="sub-option">
              <span class="sub-opt-name">{{ option.name }}</span>
              <span v-if="option.isDefault" class="sub-default-badge">default</span>
            </div>
          </template>
        </Select>
      </div>

      <div class="banner-status">
        <span class="status-dot" />
        Connected
      </div>
      <Button icon="pi pi-refresh" text size="small" class="refresh-btn" @click="load" v-tooltip.left="'Re-fetch'" />
    </template>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, watch } from 'vue'
import Button from 'primevue/button'
import Select from 'primevue/select'
import ProgressSpinner from 'primevue/progressspinner'
import { azureApi } from '@/api/azure'
import type { AzureAccountInfo, AzureSubscription } from '@/api/types'

const props = defineProps<{
  /** Pre-select this subscription ID (e.g. persisted from a connection) */
  subscriptionId?: string
}>()

const emit = defineEmits<{
  accountLoaded: [account: AzureAccountInfo | null]
  subscriptionChanged: [subscription: AzureSubscription]
}>()

const state = ref<'loading' | 'ready' | 'error'>('loading')
const account = ref<AzureAccountInfo | null>(null)

const subscriptions = ref<AzureSubscription[]>([])
const subsLoading = ref(false)
const selectedSubscription = ref<string | null>(null)

async function load() {
  state.value = 'loading'
  try {
    account.value = await azureApi.getAccount()
    state.value = 'ready'
    emit('accountLoaded', account.value)
    await loadSubscriptions()
  } catch {
    state.value = 'error'
    emit('accountLoaded', null)
  }
}

async function loadSubscriptions() {
  subsLoading.value = true
  try {
    subscriptions.value = await azureApi.getSubscriptions()
    // Prefer the persisted subscription from prop, then fall back to the account default
    const preferred = props.subscriptionId ?? account.value?.subscriptionId ?? null
    selectedSubscription.value = subscriptions.value.find(s => s.id === preferred)?.id
      ?? subscriptions.value.find(s => s.isDefault)?.id
      ?? null
    // Emit so the parent immediately reflects the resolved selection
    emitCurrentSelection()
  } catch {
    // Non-fatal — subscription picker just won't populate
  } finally {
    subsLoading.value = false
  }
}

function emitCurrentSelection() {
  const sub = subscriptions.value.find(s => s.id === selectedSubscription.value)
  if (sub) emit('subscriptionChanged', sub)
}

function onSubscriptionChange() {
  emitCurrentSelection()
}

// If the parent updates the prop after load (e.g. different connection opened), sync the visual selection
watch(() => props.subscriptionId, (id) => {
  if (id && subscriptions.value.length > 0) {
    const match = subscriptions.value.find(s => s.id === id)
    if (match) selectedSubscription.value = match.id
  }
})

onMounted(load)

defineExpose({ account, selectedSubscription, reload: load })
</script>

<style scoped>
.azure-context-banner {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 14px;
  border-radius: 8px;
  background: linear-gradient(135deg, #1e3a5f 0%, #0078d4 100%);
  margin-bottom: 16px;
  min-height: 52px;
}
.azure-context-banner.error {
  background: linear-gradient(135deg, #3d1a1a 0%, #b91c1c 100%);
}
.banner-icon {
  width: 32px;
  height: 32px;
  background: rgba(255, 255, 255, 0.15);
  border-radius: 6px;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  color: white;
}
.banner-icon svg {
  width: 18px;
  height: 18px;
}
.banner-content {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 1px;
  overflow: hidden;
}
.banner-name {
  font-size: 0.8rem;
  font-weight: 600;
  color: rgba(255, 255, 255, 0.95);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.banner-sub {
  font-size: 0.7rem;
  color: rgba(255, 255, 255, 0.65);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.banner-sub code {
  background: rgba(255, 255, 255, 0.15);
  border-radius: 3px;
  padding: 0 4px;
  font-size: 0.68rem;
}

/* ── Subscription selector ─────────────────────────────────────── */
.sub-selector {
  flex-shrink: 0;
}
:deep(.sub-select) {
  background: rgba(255,255,255,0.12) !important;
  border-color: rgba(255,255,255,0.25) !important;
  border-radius: 6px !important;
  min-width: 200px;
  max-width: 260px;
}
:deep(.sub-select .p-select-label) {
  padding: 4px 8px !important;
  font-size: 0.75rem !important;
  color: rgba(255,255,255,0.9) !important;
}
:deep(.sub-select .p-select-dropdown) {
  color: rgba(255,255,255,0.7) !important;
}
:deep(.sub-select:hover) {
  border-color: rgba(255,255,255,0.5) !important;
  background: rgba(255,255,255,0.18) !important;
}
.sub-value {
  display: flex;
  align-items: center;
  gap: 6px;
}
.sub-icon {
  font-size: 0.7rem;
  opacity: 0.7;
}
.sub-name {
  font-size: 0.75rem;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 200px;
}
.sub-option {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  width: 100%;
}
.sub-opt-name {
  font-size: 0.85rem;
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.sub-default-badge {
  font-size: 0.65rem;
  background: #0078d4;
  color: white;
  padding: 1px 5px;
  border-radius: 3px;
  flex-shrink: 0;
}

.banner-status {
  display: flex;
  align-items: center;
  gap: 5px;
  font-size: 0.7rem;
  font-weight: 600;
  color: #86efac;
  white-space: nowrap;
  flex-shrink: 0;
}
.status-dot {
  width: 7px;
  height: 7px;
  border-radius: 50%;
  background: #4ade80;
  flex-shrink: 0;
}
.refresh-btn {
  color: rgba(255, 255, 255, 0.7) !important;
  flex-shrink: 0;
}
.refresh-btn:hover {
  color: white !important;
  background: rgba(255, 255, 255, 0.1) !important;
}
</style>


<style scoped>
.azure-context-banner {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 14px;
  border-radius: 8px;
  background: linear-gradient(135deg, #1e3a5f 0%, #0078d4 100%);
  margin-bottom: 16px;
  min-height: 52px;
}
.azure-context-banner.error {
  background: linear-gradient(135deg, #3d1a1a 0%, #b91c1c 100%);
}
.banner-icon {
  width: 32px;
  height: 32px;
  background: rgba(255, 255, 255, 0.15);
  border-radius: 6px;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  color: white;
}
.banner-icon svg {
  width: 18px;
  height: 18px;
}
.banner-content {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 1px;
  overflow: hidden;
}
.banner-name {
  font-size: 0.8rem;
  font-weight: 600;
  color: rgba(255, 255, 255, 0.95);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.banner-sub {
  font-size: 0.7rem;
  color: rgba(255, 255, 255, 0.65);
}
.banner-sub code {
  background: rgba(255, 255, 255, 0.15);
  border-radius: 3px;
  padding: 0 4px;
  font-size: 0.68rem;
}
.banner-status {
  display: flex;
  align-items: center;
  gap: 5px;
  font-size: 0.7rem;
  font-weight: 600;
  color: #86efac;
  white-space: nowrap;
}
.status-dot {
  width: 7px;
  height: 7px;
  border-radius: 50%;
  background: #4ade80;
  flex-shrink: 0;
}
.refresh-btn {
  color: rgba(255, 255, 255, 0.7) !important;
  flex-shrink: 0;
}
.refresh-btn:hover {
  color: white !important;
  background: rgba(255, 255, 255, 0.1) !important;
}
</style>
