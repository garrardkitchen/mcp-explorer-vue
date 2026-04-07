<template>
  <div class="app-reg-picker">
    <Button
      :label="selectedApp ? selectedApp.displayName : 'Browse App Registrations…'"
      :icon="loading ? 'pi pi-spin pi-spinner' : 'pi pi-users'"
      severity="secondary"
      size="small"
      class="browse-btn"
      @click="open"
    />

    <Dialog v-model:visible="visible" modal header="App Registrations" :style="{ width: '520px' }" :draggable="false">
      <div class="picker-body">
        <IconField iconPosition="left" class="search-field">
          <InputIcon class="pi pi-search" />
          <InputText v-model="query" placeholder="Search by name or App ID…" class="w-full" autofocus />
        </IconField>

        <div v-if="loading" class="picker-loading">
          <ProgressSpinner style="width:28px;height:28px" strokeWidth="4" />
          <span>Loading app registrations…</span>
        </div>

        <div v-else-if="error" class="picker-error">
          <i class="pi pi-exclamation-triangle" />
          <span>{{ error }}</span>
          <Button label="Retry" size="small" text @click="loadApps" />
        </div>

        <div v-else-if="filtered.length === 0" class="picker-empty">
          <i class="pi pi-inbox" />
          <span>No app registrations found</span>
        </div>

        <div v-else class="picker-list" ref="listEl">
          <div
            v-for="app in filtered"
            :key="app.appId"
            class="picker-item"
            :class="{ selected: selectedApp?.appId === app.appId }"
            @click="select(app)"
          >
            <div class="item-icon">
              <i class="pi pi-id-card" />
            </div>
            <div class="item-body">
              <span class="item-name">{{ app.displayName }}</span>
              <span class="item-id">{{ app.appId }}</span>
            </div>
            <div v-if="selectedApp?.appId === app.appId" class="item-check">
              <i class="pi pi-check" />
            </div>
          </div>
        </div>
      </div>

      <template #footer>
        <Button label="Cancel" severity="secondary" @click="visible = false" />
        <Button label="Select" :disabled="!selectedApp" @click="confirm" />
      </template>
    </Dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, nextTick } from 'vue'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'
import InputIcon from 'primevue/inputicon'
import IconField from 'primevue/iconfield'
import ProgressSpinner from 'primevue/progressspinner'
import { azureApi } from '@/api/azure'
import type { AzureAppRegistration } from '@/api/types'

const props = defineProps<{
  /** Pre-select the app registration matching this App ID (e.g. existing clientId) */
  clientId?: string
}>()

const emit = defineEmits<{
  selected: [app: AzureAppRegistration]
}>()

const visible = ref(false)
const loading = ref(false)
const error = ref<string | null>(null)
const apps = ref<AzureAppRegistration[]>([])
const query = ref('')
const selectedApp = ref<AzureAppRegistration | null>(null)

const listEl = ref<HTMLElement | null>(null)

async function scrollSelectedIntoView() {
  await nextTick()
  const selected = listEl.value?.querySelector('.picker-item.selected') as HTMLElement | null
  selected?.scrollIntoView({ block: 'nearest' })
}

const filtered = computed(() => {
  if (!query.value.trim()) return apps.value
  const q = query.value.toLowerCase()
  return apps.value.filter(a =>
    a.displayName.toLowerCase().includes(q) || a.appId.toLowerCase().includes(q)
  )
})

async function loadApps() {
  loading.value = true
  error.value = null
  try {
    apps.value = await azureApi.getAppRegistrations()
  } catch (e: any) {
    error.value = e?.response?.data?.error ?? 'Could not load app registrations. Ensure Azure credentials are available.'
  } finally {
    loading.value = false
  }
}

async function open() {
  visible.value = true
  query.value = ''
  if (apps.value.length === 0) {
    await loadApps()
  }
  // Pre-select the app matching the current clientId
  if (props.clientId) {
    selectedApp.value = apps.value.find(a => a.appId === props.clientId) ?? selectedApp.value
    scrollSelectedIntoView()
  }
}

function select(app: AzureAppRegistration) {
  selectedApp.value = app
}

function confirm() {
  if (selectedApp.value) {
    emit('selected', selectedApp.value)
    visible.value = false
  }
}
</script>

<style scoped>
.browse-btn {
  width: 100%;
  justify-content: flex-start;
  margin-top: 6px;
}

.picker-body {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.search-field {
  width: 100%;
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
  max-height: 320px;
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

.picker-item:hover {
  background: var(--p-highlight-background);
}

.picker-item.selected {
  background: var(--p-highlight-background);
}

.item-icon {
  width: 32px;
  height: 32px;
  border-radius: 6px;
  background: var(--p-primary-100);
  color: var(--p-primary-color);
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  font-size: 0.875rem;
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
  font-size: 0.75rem;
  color: var(--p-text-muted-color);
  font-family: monospace;
}

.item-check {
  color: var(--p-primary-color);
  font-size: 0.875rem;
  flex-shrink: 0;
}
</style>
