<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useToast } from 'primevue/usetoast'
import { useConfirm } from 'primevue/useconfirm'
import Button from 'primevue/button'
import InputText from 'primevue/inputtext'
import Textarea from 'primevue/textarea'
import Dialog from 'primevue/dialog'
import Tag from 'primevue/tag'
import Skeleton from 'primevue/skeleton'
import ConfirmDialog from 'primevue/confirmdialog'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import { httpApisApi } from '@/api/httpApis'
import { useHttpApisStore } from '@/stores/httpApis'
import type { HttpApiCollection, HttpApiCollectionRunResult, HttpApiCollectionRunItem } from '@/api/types'

const toast = useToast()
const confirm = useConfirm()
const store = useHttpApisStore()

const collections = ref<HttpApiCollection[]>([])
const loading = ref(false)
const searchQuery = ref('')

// ── Form dialog ───────────────────────────────────────────────────────────────
const showDialog = ref(false)
const editMode = ref(false)
const saving = ref(false)
const form = ref<Partial<HttpApiCollection>>({ name: '', description: '', endpointIds: [] })
const originalId = ref('')

const filteredCollections = computed(() => {
  const q = searchQuery.value.toLowerCase()
  return collections.value.filter(c => !q || c.name.toLowerCase().includes(q) || c.description.toLowerCase().includes(q))
})

function openCreate() {
  form.value = { name: '', description: '', endpointIds: [] }
  originalId.value = ''
  editMode.value = false
  showDialog.value = true
}

function openEdit(c: HttpApiCollection) {
  form.value = { ...c, endpointIds: [...c.endpointIds] }
  originalId.value = c.id
  editMode.value = true
  showDialog.value = true
}

async function saveForm() {
  if (!form.value.name?.trim()) {
    toast.add({ severity: 'warn', summary: 'Name required', life: 3000 }); return
  }
  saving.value = true
  try {
    if (editMode.value) {
      const saved = await httpApisApi.updateCollection(originalId.value, form.value)
      const idx = collections.value.findIndex(c => c.id === saved.id)
      if (idx >= 0) collections.value[idx] = saved
      toast.add({ severity: 'success', summary: 'Saved', detail: `'${saved.name}' updated.`, life: 3000 })
    } else {
      const saved = await httpApisApi.createCollection(form.value)
      collections.value.push(saved)
      toast.add({ severity: 'success', summary: 'Created', detail: `'${saved.name}' created.`, life: 3000 })
    }
    showDialog.value = false
  } catch (e: any) {
    const msg = e.response?.data?.error ?? e.message
    toast.add({ severity: 'error', summary: 'Save failed', detail: msg, life: 5000 })
  } finally { saving.value = false }
}

function confirmDelete(c: HttpApiCollection) {
  confirm.require({
    message: `Delete collection '${c.name}'?`,
    header: 'Delete Collection',
    icon: 'pi pi-trash',
    rejectProps: { label: 'Cancel', severity: 'secondary', outlined: true },
    acceptProps: { label: 'Delete', severity: 'danger' },
    accept: async () => {
      await httpApisApi.deleteCollection(c.id)
      collections.value = collections.value.filter(x => x.id !== c.id)
      if (runTarget.value?.id === c.id) runTarget.value = null
      toast.add({ severity: 'success', summary: 'Deleted', life: 2000 })
    }
  })
}

// ── Endpoint selection in form ─────────────────────────────────────────────────
const endpointSearch = ref('')
const availableDefs = computed(() => {
  const q = endpointSearch.value.toLowerCase()
  return store.definitions.filter(d => !q || d.name.toLowerCase().includes(q) || d.baseUrl.toLowerCase().includes(q))
})
function toggleEndpoint(id: string) {
  const ids = form.value.endpointIds ?? []
  const has = ids.includes(id)
  form.value.endpointIds = has ? ids.filter(i => i !== id) : [...ids, id]
}
function moveUp(idx: number) {
  if (idx <= 0 || !form.value.endpointIds) return
  const ids = [...form.value.endpointIds]
  ;[ids[idx - 1], ids[idx]] = [ids[idx], ids[idx - 1]]
  form.value.endpointIds = ids
}
function moveDown(idx: number) {
  if (!form.value.endpointIds || idx >= form.value.endpointIds.length - 1) return
  const ids = [...form.value.endpointIds]
  ;[ids[idx], ids[idx + 1]] = [ids[idx + 1], ids[idx]]
  form.value.endpointIds = ids
}
function defName(id: string) {
  return store.definitions.find(d => d.id === id)?.name ?? id
}

// ── Run panel ──────────────────────────────────────────────────────────────────
const runTarget = ref<HttpApiCollection | null>(null)
const running = ref(false)
const runResult = ref<HttpApiCollectionRunResult | null>(null)
const showRunInputDialog = ref(false)
const runInputFields = ref<Array<{ name: string; defaultValue: string; value: string }>>([])
const runInputPattern = /\{(?<name>[A-Za-z_][A-Za-z0-9_.-]*)(:(?<default>[^{}]*))?\}/g

function openRun(c: HttpApiCollection) {
  runTarget.value = c
  runResult.value = null
}

function extractInputsFromTemplate(template: string | null | undefined, bucket: Map<string, string>) {
  if (!template) return
  for (const match of template.matchAll(runInputPattern)) {
    const name = match.groups?.name?.trim()
    if (!name || bucket.has(name)) continue
    bucket.set(name, match.groups?.default ?? '')
  }
}

function collectCollectionInputFields(collection: HttpApiCollection) {
  const placeholders = new Map<string, string>()
  const defs = collection.endpointIds
    .map(id => store.definitions.find(d => d.id === id))
    .filter((d): d is NonNullable<typeof d> => !!d)

  for (const def of defs) {
    extractInputsFromTemplate(def.baseUrl, placeholders)
    extractInputsFromTemplate(def.path, placeholders)
    extractInputsFromTemplate(def.bodyTemplate, placeholders)
    def.headers.forEach(h => {
      extractInputsFromTemplate(h.name, placeholders)
      extractInputsFromTemplate(h.value, placeholders)
    })
    def.queryParams.forEach(q => {
      extractInputsFromTemplate(q.name, placeholders)
      extractInputsFromTemplate(q.value, placeholders)
    })
    extractInputsFromTemplate(def.apiKeyOptions?.headerName, placeholders)
    extractInputsFromTemplate(def.apiKeyOptions?.apiKey, placeholders)
    extractInputsFromTemplate(def.apiKeyOptions?.prefix, placeholders)
    extractInputsFromTemplate(def.bearerOptions?.token, placeholders)
    extractInputsFromTemplate(def.azureCredentials?.tenantId, placeholders)
    extractInputsFromTemplate(def.azureCredentials?.clientId, placeholders)
    extractInputsFromTemplate(def.azureCredentials?.clientSecret, placeholders)
    extractInputsFromTemplate(def.azureCredentials?.scope, placeholders)
    extractInputsFromTemplate(def.azureCredentials?.authorityHost, placeholders)
  }

  return Array.from(placeholders.entries()).map(([name, defaultValue]) => ({
    name,
    defaultValue,
    value: defaultValue,
  }))
}

function resolveRunInputs() {
  const resolved: Record<string, string> = {}
  for (const field of runInputFields.value) {
    const value = field.value?.trim() || field.defaultValue?.trim()
    if (value) {
      resolved[field.name] = value
    }
  }
  return resolved
}

async function doRun(inputs?: Record<string, string>) {
  if (!runTarget.value) return
  if (!inputs) {
    const fields = collectCollectionInputFields(runTarget.value)
    if (fields.length > 0) {
      runInputFields.value = fields
      showRunInputDialog.value = true
      return
    }
  }
  running.value = true
  runResult.value = null
  try {
    runResult.value = await httpApisApi.runCollection(runTarget.value.id, inputs)
    const idx = collections.value.findIndex(c => c.id === runTarget.value!.id)
    if (idx >= 0) collections.value[idx].lastRunAt = runResult.value.ranAt
    toast.add({ severity: 'success', summary: 'Collection run complete', life: 3000 })
  } catch (e: any) {
    toast.add({ severity: 'error', summary: 'Run failed', detail: e.message, life: 5000 })
  } finally { running.value = false }
}

async function confirmRunWithInputs() {
  showRunInputDialog.value = false
  await doRun(resolveRunInputs())
}

function statusSeverity(code: number) {
  if (code >= 200 && code < 300) return 'success'
  if (code >= 400 && code < 500) return 'warn'
  if (code >= 500) return 'danger'
  return 'secondary'
}

function itemSeverity(item: HttpApiCollectionRunItem) {
  if (item.skipped) return 'secondary'
  if (!item.isSuccess) return 'danger'
  if (item.comparison?.isBreaking) return 'danger'
  if (item.comparison?.isDegraded) return 'warn'
  return 'success'
}

function itemLabel(item: HttpApiCollectionRunItem) {
  if (item.skipped) return '⏭ Skipped'
  if (!item.isSuccess) return '🔴 Failed'
  if (item.comparison?.isBreaking) return '🔴 Breaking'
  if (item.comparison?.isDegraded) return '🟡 Degraded'
  return '🟢 OK'
}

// ── Mount ──────────────────────────────────────────────────────────────────────
onMounted(async () => {
  loading.value = true
  try {
    ;[collections.value] = await Promise.all([
      httpApisApi.getCollections(),
      store.definitions.length === 0 ? store.loadAll() : Promise.resolve()
    ])
  } finally { loading.value = false }
})
</script>

<template>
  <div class="collections-view">
    <ConfirmDialog />

    <div class="toolbar">
      <span class="p-input-icon-left">
        <i class="pi pi-search" />
        <InputText v-model="searchQuery" placeholder="Search collections…" />
      </span>
      <Button label="New Collection" icon="pi pi-plus" size="small" @click="openCreate" />
    </div>

    <div class="main-layout">
      <!-- Collection list -->
      <div class="col-list">
        <Skeleton v-if="loading" height="3rem" v-for="i in 3" :key="i" class="mb-2" />
        <div
          v-if="!loading"
          v-for="c in filteredCollections" :key="c.id"
          class="col-card"
          :class="{ 'col-card--active': runTarget?.id === c.id }"
          @click="openRun(c)"
        >
          <div class="col-card__info">
            <div class="col-name">{{ c.name }}</div>
            <div v-if="c.description" class="col-desc">{{ c.description }}</div>
            <div class="col-meta">
              <Tag :value="`${c.endpointIds.length} endpoint(s)`" severity="secondary" />
              <span v-if="c.lastRunAt" class="last-run">Last run: {{ new Date(c.lastRunAt).toLocaleString() }}</span>
            </div>
          </div>
          <div class="col-card__actions" @click.stop>
            <Button icon="pi pi-pencil" text rounded size="small" @click="openEdit(c)" />
            <Button icon="pi pi-trash" text rounded size="small" severity="danger" @click="confirmDelete(c)" />
          </div>
        </div>
        <div v-if="!loading && filteredCollections.length === 0" class="empty-state">
          <i class="pi pi-list empty-icon" />
          <p>No collections yet.</p>
          <Button label="Create one" size="small" @click="openCreate" />
        </div>
      </div>

      <!-- Run panel -->
      <div v-if="runTarget" class="run-panel">
        <div class="run-panel__header">
          <span class="run-title">{{ runTarget.name }}</span>
          <Button label="Run Collection" icon="pi pi-play" size="small" :loading="running" @click="() => doRun()" />
        </div>

        <div v-if="!runResult" class="no-result">
          Click <strong>Run Collection</strong> to invoke all {{ runTarget.endpointIds.length }} endpoint(s) and compare against their baselines.
        </div>

        <div v-else class="run-result">
          <div class="run-summary">
            <Tag :value="`${runResult.results.filter((r: any) => !r.skipped && r.isSuccess && !r.comparison?.isBreaking).length} passed`" severity="success" />
            <Tag :value="`${runResult.results.filter((r: any) => r.comparison?.isBreaking || !r.isSuccess).length} failed`" severity="danger" />
            <Tag :value="`${runResult.results.filter((r: any) => r.comparison?.isDegraded).length} degraded`" severity="warn" />
            <span class="run-time">{{ new Date(runResult.ranAt).toLocaleString() }}</span>
          </div>

          <DataTable :value="runResult.results" size="small" class="result-table">
            <Column field="endpointName" header="Endpoint" />
            <Column field="statusCode" header="Status">
              <template #body="{ data }">
                <Tag v-if="!data.skipped" :value="`${data.statusCode}`" :severity="statusSeverity(data.statusCode)" />
                <span v-else>—</span>
              </template>
            </Column>
            <Column field="latencyMs" header="Latency">
              <template #body="{ data }">{{ data.skipped ? '—' : `${data.latencyMs} ms` }}</template>
            </Column>
            <Column header="Result">
              <template #body="{ data }">
                <Tag :value="itemLabel(data)" :severity="itemSeverity(data)" />
              </template>
            </Column>
            <Column header="Schema changes">
              <template #body="{ data }">
                <span v-if="data.comparison">
                  <span v-if="data.comparison.removedProperties.length" class="change-badge removed">-{{ data.comparison.removedProperties.length }}</span>
                  <span v-if="data.comparison.addedProperties.length" class="change-badge added">+{{ data.comparison.addedProperties.length }}</span>
                  <span v-if="data.comparison.changedTypes.length" class="change-badge changed">~{{ data.comparison.changedTypes.length }}</span>
                  <span v-if="!data.comparison.isBreaking && !data.comparison.isDegraded">✅</span>
                </span>
                <span v-else class="text-muted">No baseline</span>
              </template>
            </Column>
          </DataTable>
        </div>
      </div>

      <div v-if="!runTarget" class="run-placeholder">
        <i class="pi pi-list placeholder-icon" />
        <p>Select a collection to run.</p>
      </div>
    </div>

    <!-- Create / Edit dialog -->
    <Dialog v-model:visible="showDialog" :header="editMode ? 'Edit Collection' : 'New Collection'" modal :style="{ width: '560px' }">
      <div class="form-grid">
        <div class="form-row"><label>Name *</label><InputText v-model="form.name" class="w-full" /></div>
        <div class="form-row"><label>Description</label><Textarea v-model="form.description" rows="2" class="w-full" /></div>

        <div class="form-section-label">Endpoints (in order)</div>
        <div v-if="form.endpointIds?.length" class="ordered-list">
          <div v-for="(id, idx) in form.endpointIds" :key="id" class="ordered-item">
            <span class="ordered-num">{{ idx + 1 }}.</span>
            <span class="ordered-name">{{ defName(id) }}</span>
            <div class="ordered-btns">
              <Button icon="pi pi-arrow-up" text rounded size="small" :disabled="idx === 0" @click="moveUp(idx)" />
              <Button icon="pi pi-arrow-down" text rounded size="small" :disabled="idx === (form.endpointIds?.length ?? 0) - 1" @click="moveDown(idx)" />
              <Button icon="pi pi-times" text rounded size="small" severity="danger" @click="toggleEndpoint(id)" />
            </div>
          </div>
        </div>
        <div class="form-section-label">Add endpoints</div>
        <InputText v-model="endpointSearch" placeholder="Filter available endpoints…" class="w-full" />
        <div class="available-list">
          <div
            v-for="d in availableDefs" :key="d.id"
            class="available-item"
            :class="{ 'available-item--selected': form.endpointIds?.includes(d.id) }"
            @click="toggleEndpoint(d.id)"
          >
            <i :class="form.endpointIds?.includes(d.id) ? 'pi pi-check-circle' : 'pi pi-circle'" />
            <span>{{ d.name }}</span>
            <span class="avail-url">{{ d.method }} {{ d.baseUrl }}{{ d.path }}</span>
          </div>
        </div>
      </div>
      <template #footer>
        <Button label="Cancel" severity="secondary" outlined @click="showDialog = false" />
        <Button :label="editMode ? 'Save' : 'Create'" icon="pi pi-check" :loading="saving" @click="saveForm" />
      </template>
    </Dialog>

    <Dialog
      v-model:visible="showRunInputDialog"
      header="Run Collection"
      modal
      :style="{ width: '520px' }"
    >
      <div class="form-grid">
        <div class="form-row">
          <label>
            This collection includes placeholder inputs (for example:
            <code>{{ '{upn}' }}</code> or <code>{{ '{upn:user@contoso.com}' }}</code>).
          </label>
        </div>
        <div v-for="field in runInputFields" :key="field.name" class="form-row">
          <label>{{ field.name }}</label>
          <InputText v-model="field.value" :placeholder="field.defaultValue || 'Required if no default'" class="w-full" />
          <small v-if="field.defaultValue" class="text-muted">Default: {{ field.defaultValue }}</small>
        </div>
      </div>
      <template #footer>
        <Button label="Cancel" severity="secondary" outlined @click="showRunInputDialog = false" />
        <Button label="Run Collection" icon="pi pi-play" :loading="running" @click="confirmRunWithInputs" />
      </template>
    </Dialog>
  </div>
</template>

<style scoped>
.collections-view { display: flex; flex-direction: column; height: 100%; gap: 0.75rem; padding: 1rem; }
.toolbar { display: flex; align-items: center; justify-content: space-between; gap: 0.5rem; }
.main-layout { display: grid; grid-template-columns: 300px 1fr; gap: 1rem; flex: 1; min-height: 0; overflow: hidden; }

.col-list { overflow-y: auto; display: flex; flex-direction: column; gap: 0.5rem; }
.col-card { background: var(--surface-card); border: 1px solid var(--surface-border); border-radius: 8px; padding: 0.75rem; cursor: pointer; display: flex; justify-content: space-between; }
.col-card:hover, .col-card--active { border-color: var(--primary-color); }
.col-name { font-weight: 600; font-size: 0.9rem; }
.col-desc { font-size: 0.78rem; color: var(--text-color-secondary); margin-top: 0.1rem; }
.col-meta { display: flex; align-items: center; gap: 0.5rem; margin-top: 0.3rem; }
.last-run { font-size: 0.72rem; color: var(--text-color-secondary); }
.col-card__actions { display: flex; flex-direction: column; gap: 0.25rem; opacity: 0; transition: opacity 0.15s; }
.col-card:hover .col-card__actions { opacity: 1; }

.empty-state { text-align: center; padding: 2rem; color: var(--text-color-secondary); }
.empty-icon { font-size: 2rem; display: block; margin-bottom: 0.5rem; }

.run-panel { overflow-y: auto; border: 1px solid var(--surface-border); border-radius: 8px; padding: 0.75rem; background: var(--surface-card); }
.run-panel__header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 0.75rem; }
.run-title { font-weight: 700; font-size: 1rem; }
.run-placeholder { display: flex; flex-direction: column; align-items: center; justify-content: center; color: var(--text-color-secondary); }
.placeholder-icon { font-size: 2rem; margin-bottom: 0.5rem; }
.no-result { text-align: center; padding: 2rem; color: var(--text-color-secondary); }

.run-summary { display: flex; align-items: center; gap: 0.5rem; margin-bottom: 0.75rem; }
.run-time { font-size: 0.78rem; color: var(--text-color-secondary); }
.result-table { font-size: 0.82rem; }
.change-badge { border-radius: 4px; padding: 0.1rem 0.3rem; font-size: 0.72rem; font-weight: 700; margin-right: 0.15rem; }
.change-badge.removed { background: #fee2e2; color: #991b1b; }
.change-badge.added { background: #dcfce7; color: #166534; }
.change-badge.changed { background: #fef9c3; color: #854d0e; }
.text-muted { color: var(--text-color-secondary); }

/* Form dialog */
.form-grid { display: flex; flex-direction: column; gap: 0.6rem; }
.form-row { display: flex; flex-direction: column; gap: 0.2rem; }
.form-row label { font-size: 0.8rem; font-weight: 600; color: var(--text-color-secondary); }
.form-section-label { font-size: 0.75rem; font-weight: 600; color: var(--text-color-secondary); text-transform: uppercase; margin-top: 0.5rem; }
.w-full { width: 100%; }

.ordered-list { display: flex; flex-direction: column; gap: 0.3rem; border: 1px solid var(--surface-border); border-radius: 6px; padding: 0.4rem; max-height: 140px; overflow-y: auto; }
.ordered-item { display: flex; align-items: center; gap: 0.4rem; font-size: 0.83rem; }
.ordered-num { min-width: 1.2rem; color: var(--text-color-secondary); font-size: 0.75rem; }
.ordered-name { flex: 1; }
.ordered-btns { display: flex; gap: 0.1rem; }

.available-list { max-height: 160px; overflow-y: auto; border: 1px solid var(--surface-border); border-radius: 6px; padding: 0.3rem; }
.available-item { display: flex; align-items: center; gap: 0.4rem; padding: 0.25rem 0.3rem; border-radius: 4px; cursor: pointer; font-size: 0.82rem; }
.available-item:hover { background: var(--surface-hover); }
.available-item--selected {
  background: color-mix(in srgb, var(--primary-color) 18%, var(--surface-card, var(--surface-ground)));
  outline: 1px solid color-mix(in srgb, var(--primary-color) 60%, transparent);
  outline-offset: -1px;
  color: var(--text-color);
}
.avail-url { color: var(--text-color-secondary); font-size: 0.72rem; margin-left: auto; }
</style>
