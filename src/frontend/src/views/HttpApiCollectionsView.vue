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
import { systemApi } from '@/api/system'
import { useHttpApisStore } from '@/stores/httpApis'
import SparklineChart from '@/components/common/SparklineChart.vue'
import type { SparklineBar } from '@/components/common/SparklineChart.vue'
import type { HttpApiCollection, HttpApiCollectionRunResult, HttpApiCollectionRunItem, HttpApiCollectionRunRecord, HttpApiCollectionRunSparklinePoint } from '@/api/types'

const toast = useToast()
const confirm = useConfirm()
const store = useHttpApisStore()

const collections = ref<HttpApiCollection[]>([])
const loading = ref(false)
const searchQuery = ref('')

// ── Expand state ──────────────────────────────────────────────────────────────
const expandedIds = ref<Set<string>>(new Set())
const runHistory = ref<Map<string, HttpApiCollectionRunRecord[]>>(new Map())
const runHistoryLoading = ref<Set<string>>(new Set())

// ── Sparklines ────────────────────────────────────────────────────────────────
const collectionSparklines = ref<Map<string, SparklineBar[]>>(new Map())

function collectionSparklineColor(pt: HttpApiCollectionRunSparklinePoint): string {
  if (pt.totalCount === 0) return '#6b7280'
  const ratio = pt.successCount / pt.totalCount
  if (ratio >= 1.0) return '#22c55e'   // green — all passed
  if (ratio >= 0.5) return '#f97316'   // orange — partial
  return '#ef4444'                      // red — mostly failed
}

function buildCollectionSparkline(points: HttpApiCollectionRunSparklinePoint[]): SparklineBar[] {
  return points.map(pt => ({ value: pt.durationMs, color: collectionSparklineColor(pt) }))
}

async function loadCollectionSparklines() {
  try {
    const data = await httpApisApi.getCollectionSparklines()
    const map = new Map<string, SparklineBar[]>()
    for (const [id, points] of Object.entries(data)) {
      map.set(id, buildCollectionSparkline(points))
    }
    collectionSparklines.value = map
  } catch {
    // non-critical — sparklines degrade gracefully
  }
}

/** Build an endpoint-level sparkline from collection run history for a specific endpoint. */
function endpointRunSparkline(collectionId: string, endpointId: string): SparklineBar[] {
  const history = runHistory.value.get(collectionId) ?? []
  return history
    .slice()
    .sort((a, b) => new Date(a.ranAt).getTime() - new Date(b.ranAt).getTime())
    .map(run => {
      const ep = run.endpointSummaries.find(e => e.endpointId === endpointId)
      if (!ep || ep.skipped) return { value: 0, color: '#6b7280' }
      return {
        value: ep.latencyMs,
        color: ep.isSuccess ? '#22c55e' : ep.statusCode >= 400 ? '#f97316' : '#ef4444'
      }
    })
}

async function toggleExpand(id: string) {
  if (expandedIds.value.has(id)) {
    expandedIds.value.delete(id)
  } else {
    expandedIds.value.add(id)
    if (!runHistory.value.has(id)) {
      runHistoryLoading.value.add(id)
      try {
        const history = await httpApisApi.getCollectionRunHistory(id)
        runHistory.value.set(id, history)
      } catch {
        runHistory.value.set(id, [])
      } finally {
        runHistoryLoading.value.delete(id)
      }
    }
  }
}

// ── CLI copy ──────────────────────────────────────────────────────────────────
const copiedCliId = ref<string | null>(null)
const dataPath = ref<string | null>(null)

function cliRunCommand(c: HttpApiCollection): string {
  const parts: string[] = ['mcp-http', `http collection run --name "${c.name.replace(/"/g, '\\"')}"`]
  if (dataPath.value) parts.push(`--data-path "${dataPath.value}"`)
  return parts.join(' ')
}

async function copyCliCommand(c: HttpApiCollection) {
  try {
    await navigator.clipboard.writeText(cliRunCommand(c))
    copiedCliId.value = c.id
    setTimeout(() => { if (copiedCliId.value === c.id) copiedCliId.value = null }, 2000)
    toast.add({ severity: 'info', summary: 'CLI command copied', life: 2000 })
  } catch {
    toast.add({ severity: 'error', summary: 'Copy failed — clipboard unavailable', life: 3000 })
  }
}

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
      expandedIds.value.delete(c.id)
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

// ── Run ────────────────────────────────────────────────────────────────────────
const runningId = ref<string | null>(null)
const runResultMap = ref<Record<string, HttpApiCollectionRunResult>>({})
const showRunResultDialog = ref(false)
const activeRunResult = ref<HttpApiCollectionRunResult | null>(null)
const activeRunCollection = ref<HttpApiCollection | null>(null)
const showRunInputDialog = ref(false)
const runInputTarget = ref<HttpApiCollection | null>(null)
const runInputFields = ref<Array<{ name: string; defaultValue: string; value: string }>>([])
const runInputPattern = /\{(?<name>[A-Za-z_][A-Za-z0-9_.-]*)(:(?<default>[^{}]*))?\}/g

function successPercent(c: HttpApiCollection): number | null {
  if (c.lastRunTotalCount == null || c.lastRunTotalCount === 0) return null
  return Math.round(((c.lastRunSuccessCount ?? 0) / c.lastRunTotalCount) * 100)
}

function successSeverity(pct: number | null) {
  if (pct === null) return 'secondary'
  if (pct >= 90) return 'success'
  if (pct >= 50) return 'warn'
  return 'danger'
}

function formatDuration(ms: number | null | undefined) {
  if (ms == null) return '—'
  if (ms < 1000) return `${ms} ms`
  return `${(ms / 1000).toFixed(1)} s`
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
    if (value) resolved[field.name] = value
  }
  return resolved
}

async function startRun(c: HttpApiCollection) {
  const fields = collectCollectionInputFields(c)
  if (fields.length > 0) {
    runInputTarget.value = c
    runInputFields.value = fields
    showRunInputDialog.value = true
    return
  }
  await doRun(c, undefined)
}

async function confirmRunWithInputs() {
  showRunInputDialog.value = false
  if (!runInputTarget.value) return
  await doRun(runInputTarget.value, resolveRunInputs())
}

async function doRun(c: HttpApiCollection, inputs?: Record<string, string>) {
  runningId.value = c.id
  try {
    const result = await httpApisApi.runCollection(c.id, inputs)
    // Update collection in-place with new run stats
    const idx = collections.value.findIndex(x => x.id === c.id)
    if (idx >= 0) {
      collections.value[idx] = {
        ...collections.value[idx],
        lastRunAt:               result.ranAt,
        lastRunDurationMs:       result.durationMs,
        lastRunSuccessCount:     result.successCount,
        lastRunTotalCount:       result.totalCount,
        lastRunId:               result.runId,
        lastRunInvokedVia:       result.invokedVia,
        lastRunEndpointSummaries: result.endpointSummaries
      }
    }
    runResultMap.value[c.id] = result
    activeRunResult.value = result
    activeRunCollection.value = idx >= 0 ? collections.value[idx] : c
    showRunResultDialog.value = true
    toast.add({ severity: 'success', summary: 'Collection run complete', life: 3000 })
    // Invalidate cached run history so next expand fetches fresh data
    runHistory.value.delete(c.id)
    loadCollectionSparklines() // refresh sparklines async, non-blocking
  } catch (e: any) {
    toast.add({ severity: 'error', summary: 'Run failed', detail: e.message, life: 5000 })
  } finally {
    runningId.value = null
  }
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
      store.definitions.length === 0 ? store.loadAll() : Promise.resolve(),
      systemApi.getInfo().catch(() => ({ apiVersion: '', dotnetVersion: '', dataPath: null }))
        .then(info => { dataPath.value = info.dataPath ?? null }),
    ])
    await loadCollectionSparklines()
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

    <!-- Skeleton loading -->
    <div v-if="loading" class="skeleton-list">
      <Skeleton v-for="i in 3" :key="i" height="4rem" class="mb-2" />
    </div>

    <!-- Empty state -->
    <div v-if="!loading && filteredCollections.length === 0" class="empty-state">
      <i class="pi pi-list empty-icon" />
      <p>No collections yet.</p>
      <Button label="Create one" size="small" @click="openCreate" />
    </div>

    <!-- Collections table -->
    <div v-if="!loading && filteredCollections.length > 0" class="col-table">
      <!-- Header row -->
      <div class="col-header">
        <div class="col-h-expand"></div>
        <div class="col-h-name">Name / Description</div>
        <div class="col-h-count">Endpoints</div>
        <div class="col-h-lastrun">Last Run</div>
        <div class="col-h-duration">Duration</div>
        <div class="col-h-success">% Success</div>
        <div class="col-h-trend">Trend</div>
        <div class="col-h-actions">Actions</div>
      </div>

      <template v-for="c in filteredCollections" :key="c.id">
        <!-- Main row -->
        <div class="col-row">
          <div class="col-expand">
            <Button
              :icon="expandedIds.has(c.id) ? 'pi pi-chevron-down' : 'pi pi-chevron-right'"
              text rounded size="small"
              :disabled="false"
              @click="toggleExpand(c.id)"
            />
          </div>
          <div class="col-name-cell">
            <span class="col-name">{{ c.name }}</span>
            <span v-if="c.description" class="col-desc">{{ c.description }}</span>
          </div>
          <div class="col-count">
            <Tag :value="`${c.endpointIds.length}`" severity="secondary" />
          </div>
          <div class="col-lastrun">
            <span v-if="c.lastRunAt" class="meta-text">{{ new Date(c.lastRunAt).toLocaleString() }}</span>
            <span v-else class="meta-muted">—</span>
          </div>
          <div class="col-duration">
            <span class="meta-text">{{ formatDuration(c.lastRunDurationMs) }}</span>
          </div>
          <div class="col-success">
            <Tag
              v-if="successPercent(c) !== null"
              :value="`${successPercent(c)}%`"
              :severity="successSeverity(successPercent(c))"
            />
            <span v-else class="meta-muted">—</span>
          </div>
          <div class="col-trend">
            <SparklineChart :bars="collectionSparklines.get(c.id) ?? []" />
          </div>
          <div class="col-actions">
            <Button icon="pi pi-pencil" text rounded size="small" title="Edit" @click="openEdit(c)" />
            <Button icon="pi pi-trash" text rounded size="small" severity="danger" title="Delete" @click="confirmDelete(c)" />
            <Button
              :icon="copiedCliId === c.id ? 'pi pi-check' : 'pi pi-clipboard'"
              text rounded size="small"
              title="Copy CLI command"
              @click="copyCliCommand(c)"
            />
            <Button
              icon="pi pi-play"
              size="small"
              severity="success"
              title="Run"
              :loading="runningId === c.id"
              @click="startRun(c)"
            />
          </div>
        </div>

        <!-- Expanded endpoint details -->
        <div v-if="expandedIds.has(c.id)" class="col-expand-panel">
          <!-- Last run endpoint summary -->
          <template v-if="c.lastRunEndpointSummaries?.length">
            <div class="ep-run-meta">
              <span class="meta-text">Last run: {{ c.lastRunAt ? new Date(c.lastRunAt).toLocaleString() : '—' }}</span>
              <Tag
                v-if="c.lastRunInvokedVia"
                :value="c.lastRunInvokedVia === 'CLI' ? '⌨ CLI' : '🖥 App'"
                :severity="c.lastRunInvokedVia === 'CLI' ? 'secondary' : 'info'"
                class="ep-via-tag"
              />
            </div>
            <table class="ep-table">
              <thead>
                <tr>
                  <th>Endpoint</th>
                  <th>Status</th>
                  <th>Duration</th>
                  <th>Trend</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="ep in c.lastRunEndpointSummaries" :key="ep.endpointId">
                  <td class="ep-name">{{ ep.endpointName }}</td>
                  <td>
                    <span v-if="ep.skipped" class="meta-muted">Skipped</span>
                    <Tag v-else :value="`${ep.statusCode}`" :severity="statusSeverity(ep.statusCode)" />
                  </td>
                  <td class="ep-latency">{{ ep.skipped ? '—' : `${ep.latencyMs} ms` }}</td>
                  <td>
                    <SparklineChart :bars="endpointRunSparkline(c.id, ep.endpointId)" />
                  </td>
                </tr>
              </tbody>
            </table>
          </template>

          <!-- Run history -->
          <div class="run-history-section">
            <div class="run-history-header">Run History</div>
            <Skeleton v-if="runHistoryLoading.has(c.id)" height="2.5rem" class="mb-1" />
            <template v-else-if="runHistory.get(c.id)?.length">
              <table class="ep-table run-history-table">
                <thead>
                  <tr>
                    <th>When</th>
                    <th>Duration</th>
                    <th>Passed</th>
                    <th>Failed</th>
                    <th>Source</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="run in runHistory.get(c.id)" :key="run.runId">
                    <td class="ep-name">{{ new Date(run.ranAt).toLocaleString() }}</td>
                    <td class="ep-latency">{{ formatDuration(run.durationMs) }}</td>
                    <td><Tag :value="`${run.successCount}`" severity="success" /></td>
                    <td><Tag v-if="run.totalCount - run.successCount > 0" :value="`${run.totalCount - run.successCount}`" severity="danger" /><span v-else class="meta-muted">0</span></td>
                    <td>
                      <Tag v-if="run.invokedVia" :value="run.invokedVia === 'CLI' ? '⌨ CLI' : '🖥 App'" :severity="run.invokedVia === 'CLI' ? 'secondary' : 'info'" />
                      <span v-else class="meta-muted">—</span>
                    </td>
                  </tr>
                </tbody>
              </table>
            </template>
            <span v-else class="meta-muted">No history yet</span>
          </div>
        </div>
      </template>
    </div>

    <!-- Run result dialog -->
    <Dialog v-model:visible="showRunResultDialog" header="Collection Run Results" modal :style="{ width: '760px' }">
      <div v-if="activeRunResult" class="run-result">
        <div class="run-summary">
          <Tag :value="`${activeRunResult.successCount} passed`" severity="success" />
          <Tag :value="`${activeRunResult.totalCount - activeRunResult.successCount} failed`" severity="danger" />
          <span class="meta-text">{{ formatDuration(activeRunResult.durationMs) }}</span>
          <span class="meta-muted">{{ new Date(activeRunResult.ranAt).toLocaleString() }}</span>
        </div>
        <DataTable :value="activeRunResult.results" size="small" class="result-table">
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
              <span v-else class="meta-muted">No baseline</span>
            </template>
          </Column>
        </DataTable>
      </div>
      <template #footer>
        <Button label="Close" severity="secondary" outlined @click="showRunResultDialog = false" />
      </template>
    </Dialog>

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

    <!-- Run inputs dialog -->
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
          <small v-if="field.defaultValue" class="meta-muted">Default: {{ field.defaultValue }}</small>
        </div>
      </div>
      <template #footer>
        <Button label="Cancel" severity="secondary" outlined @click="showRunInputDialog = false" />
        <Button label="Run Collection" icon="pi pi-play" :loading="runningId !== null" @click="confirmRunWithInputs" />
      </template>
    </Dialog>
  </div>
</template>

<style scoped>
.collections-view { display: flex; flex-direction: column; height: 100%; gap: 0.75rem; padding: 1rem; }
.toolbar { display: flex; align-items: center; justify-content: space-between; gap: 0.5rem; }
.skeleton-list { display: flex; flex-direction: column; gap: 0.4rem; }

/* ── Table layout ─────────────────────────────────────────────── */
.col-table { display: flex; flex-direction: column; border: 1px solid var(--surface-border); border-radius: 8px; overflow: hidden; }

.col-header {
  display: grid;
  grid-template-columns: 2.5rem 1fr 6rem 11rem 6rem 6rem 7rem 9rem;
  align-items: center;
  padding: 0.45rem 0.75rem;
  background: var(--surface-ground);
  border-bottom: 1px solid var(--surface-border);
  font-size: 0.74rem;
  font-weight: 600;
  color: var(--text-color-secondary);
  text-transform: uppercase;
  letter-spacing: 0.04em;
  gap: 0.5rem;
}

.col-row {
  display: grid;
  grid-template-columns: 2.5rem 1fr 6rem 11rem 6rem 6rem 7rem 9rem;
  align-items: center;
  padding: 0.6rem 0.75rem;
  border-bottom: 1px solid var(--surface-border);
  gap: 0.5rem;
  background: var(--surface-card);
  transition: background 0.1s;
}
.col-row:last-child { border-bottom: none; }
.col-row:hover { background: var(--surface-hover); }

.col-expand { display: flex; justify-content: center; }
.col-name-cell { display: flex; flex-direction: column; gap: 0.1rem; min-width: 0; }
.col-name { font-weight: 600; font-size: 0.9rem; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.col-desc { font-size: 0.76rem; color: var(--text-color-secondary); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.col-count { display: flex; }
.col-lastrun, .col-duration, .col-success { display: flex; align-items: center; }
.col-trend { display: flex; align-items: center; }
.col-actions { display: flex; align-items: center; gap: 0.2rem; justify-content: flex-end; }

.meta-text { font-size: 0.78rem; color: var(--text-color-secondary); }
.meta-muted { font-size: 0.78rem; color: var(--text-color-secondary); opacity: 0.5; }

/* Header cell labels */
.col-h-expand, .col-h-name, .col-h-count, .col-h-lastrun,
.col-h-duration, .col-h-success, .col-h-trend, .col-h-actions { white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.col-h-actions { text-align: right; }

/* ── Expand panel ─────────────────────────────────────────────── */
.col-expand-panel {
  padding: 0.5rem 0.75rem 0.5rem 3rem;
  background: var(--surface-ground);
  border-bottom: 1px solid var(--surface-border);
}

.ep-run-meta {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 0.4rem;
  font-size: 0.78rem;
}
.ep-via-tag { font-size: 0.72rem; }

.ep-table { width: 100%; border-collapse: collapse; font-size: 0.82rem; }
.ep-table th { text-align: left; padding: 0.3rem 0.5rem; color: var(--text-color-secondary); font-size: 0.72rem; font-weight: 600; text-transform: uppercase; border-bottom: 1px solid var(--surface-border); }
.ep-table td { padding: 0.35rem 0.5rem; border-bottom: 1px solid var(--surface-border); }
.ep-table tr:last-child td { border-bottom: none; }
.ep-name { font-weight: 500; }
.ep-latency { color: var(--text-color-secondary); }

/* ── Run history section ─────────────────────────────────────── */
.run-history-section { margin-top: 1rem; }
.run-history-header { font-size: 0.72rem; font-weight: 600; text-transform: uppercase; color: var(--text-color-secondary); margin-bottom: 0.4rem; letter-spacing: 0.04em; }
.run-history-table .ep-name { font-weight: 400; color: var(--text-color); }

/* ── Empty state ──────────────────────────────────────────────── */
.empty-state { text-align: center; padding: 2rem; color: var(--text-color-secondary); }
.empty-icon { font-size: 2rem; display: block; margin-bottom: 0.5rem; }

/* ── Run result dialog ────────────────────────────────────────── */
.run-result { display: flex; flex-direction: column; gap: 0.75rem; }
.run-summary { display: flex; align-items: center; gap: 0.5rem; flex-wrap: wrap; }
.result-table { font-size: 0.82rem; }
.change-badge { border-radius: 4px; padding: 0.1rem 0.3rem; font-size: 0.72rem; font-weight: 700; margin-right: 0.15rem; }
.change-badge.removed { background: #fee2e2; color: #991b1b; }
.change-badge.added { background: #dcfce7; color: #166534; }
.change-badge.changed { background: #fef9c3; color: #854d0e; }

/* ── Form dialog ──────────────────────────────────────────────── */
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
