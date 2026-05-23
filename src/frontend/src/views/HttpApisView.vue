<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useToast } from 'primevue/usetoast'
import { useConfirm } from 'primevue/useconfirm'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Button from 'primevue/button'
import InputText from 'primevue/inputtext'
import Password from 'primevue/password'
import Textarea from 'primevue/textarea'
import Dialog from 'primevue/dialog'
import Select from 'primevue/select'
import Tag from 'primevue/tag'
import Skeleton from 'primevue/skeleton'
import Checkbox from 'primevue/checkbox'
import ConfirmDialog from 'primevue/confirmdialog'
import Tabs from 'primevue/tabs'
import TabList from 'primevue/tablist'
import Tab from 'primevue/tab'
import TabPanels from 'primevue/tabpanels'
import TabPanel from 'primevue/tabpanel'
import JsonViewer from '@/components/common/JsonViewer.vue'
import { useHttpApisStore } from '@/stores/httpApis'
import { httpApisApi } from '@/api/httpApis'
import { apiClient } from '@/api/client'
import type {
  HttpApiDefinition,
  HttpApiGroup,
  HttpApiHeader,
  HttpApiQueryParam,
  HttpApiAuthenticationMode,
  HttpApiInvokeResponse,
  HttpResponseSnapshot,
  HttpApiInvocationRecord,
  HttpSchemaComparisonResult,
} from '@/api/types'

const toast = useToast()
const confirm = useConfirm()
const store = useHttpApisStore()

// ── State ───────────────────────────────────────────────────────────────────
const loading = ref(false)
const searchQuery = ref('')
const selectedGroup = ref<string | null>(null)
const showFavsFirst = ref(false)

// ── Form dialog ──────────────────────────────────────────────────────────────
const showDialog = ref(false)
const editMode = ref(false)
const saving = ref(false)

const AUTH_MODES: { label: string; value: HttpApiAuthenticationMode }[] = [
  { label: 'None',                    value: 'None' },
  { label: 'Custom Headers',          value: 'CustomHeaders' },
  { label: 'API Key',                 value: 'ApiKey' },
  { label: 'Bearer Token',            value: 'Bearer' },
  { label: 'Azure Client Credentials', value: 'AzureClientCredentials' },
]

const HTTP_METHODS = ['GET', 'POST', 'PUT', 'PATCH', 'DELETE', 'HEAD', 'OPTIONS']

const blankForm = (): Partial<HttpApiDefinition> => ({
  name: '', baseUrl: '', method: 'GET', path: '',
  authenticationMode: 'None', headers: [], queryParams: [],
  bodyTemplate: '', groupName: '', tags: [], note: '',
  azureCredentials: undefined, apiKeyOptions: undefined, bearerOptions: undefined
})
const form = ref<Partial<HttpApiDefinition>>(blankForm())
const originalId = ref('')

function openCreate() {
  form.value = blankForm()
  originalId.value = ''
  editMode.value = false
  showDialog.value = true
}

function openEdit(def: HttpApiDefinition) {
  form.value = { ...def, headers: [...def.headers], queryParams: [...def.queryParams], tags: [...def.tags] }
  originalId.value = def.id
  editMode.value = true
  showDialog.value = true
}

async function saveForm() {
  if (!form.value.name?.trim()) {
    toast.add({ severity: 'warn', summary: 'Name required', detail: 'Enter a name for this API definition.', life: 3000 }); return
  }
  if (!form.value.baseUrl?.trim()) {
    toast.add({ severity: 'warn', summary: 'Base URL required', detail: 'Enter a base URL.', life: 3000 }); return
  }
  saving.value = true
  try {
    if (editMode.value) {
      const saved = await httpApisApi.update(originalId.value, form.value)
      store.addOrUpdate(saved)
      toast.add({ severity: 'success', summary: 'Saved', detail: `'${saved.name}' updated.`, life: 3000 })
    } else {
      const saved = await httpApisApi.create(form.value)
      store.addOrUpdate(saved)
      toast.add({ severity: 'success', summary: 'Created', detail: `'${saved.name}' created.`, life: 3000 })
    }
    showDialog.value = false
  } catch (e: any) {
    const msg = e.response?.data?.error ?? e.message
    toast.add({ severity: 'error', summary: 'Save failed', detail: msg, life: 5000 })
  } finally { saving.value = false }
}

function confirmDelete(def: HttpApiDefinition) {
  confirm.require({
    message: `Delete '${def.name}'? This cannot be undone.`,
    header: 'Delete API Definition',
    icon: 'pi pi-trash',
    rejectProps: { label: 'Cancel', severity: 'secondary', outlined: true },
    acceptProps: { label: 'Delete', severity: 'danger' },
    accept: async () => {
      await store.deleteDefinition(def.id)
      if (invokeTarget.value?.id === def.id) invokeTarget.value = null
      toast.add({ severity: 'success', summary: 'Deleted', detail: `'${def.name}' deleted.`, life: 3000 })
    }
  })
}

async function copyDef(def: HttpApiDefinition) {
  const copy = await store.copyDefinition(def.id)
  toast.add({ severity: 'success', summary: 'Copied', detail: `'${copy.name}' created as a copy.`, life: 3000 })
}

// ── Header / query param management in form ──────────────────────────────────
function addHeader() { form.value.headers = [...(form.value.headers ?? []), { name: '', value: '' }] }
function removeHeader(i: number) { form.value.headers = form.value.headers?.filter((_, idx) => idx !== i) }
function addQueryParam() { form.value.queryParams = [...(form.value.queryParams ?? []), { name: '', value: '', enabled: true }] }
function removeQueryParam(i: number) { form.value.queryParams = form.value.queryParams?.filter((_, idx) => idx !== i) }

// ── Filtered list ────────────────────────────────────────────────────────────
const filteredDefs = computed(() => {
  let list = store.definitions
  const q = searchQuery.value.toLowerCase()
  if (q) list = list.filter(d =>
    d.name.toLowerCase().includes(q) ||
    d.baseUrl.toLowerCase().includes(q) ||
    d.path.toLowerCase().includes(q) ||
    d.note.toLowerCase().includes(q) ||
    d.tags.some(t => t.toLowerCase().includes(q))
  )
  if (selectedGroup.value) list = list.filter(d => d.groupName === selectedGroup.value)
  if (showFavsFirst.value)
    list = [...list.filter(d => store.isFavourite(d.id)), ...list.filter(d => !store.isFavourite(d.id))]
  return list
})

// ── Invoke panel ─────────────────────────────────────────────────────────────
const invokeTarget = ref<HttpApiDefinition | null>(null)
const invoking = ref(false)
const invokeResult = ref<HttpApiInvokeResponse | null>(null)

function openInvokePanel(def: HttpApiDefinition) {
  invokeTarget.value = def
  invokeResult.value = null
  compareResult.value = null
  activeTab.value = 'overview'
}

const activeTab = ref('overview')

async function doInvoke() {
  if (!invokeTarget.value) return
  invoking.value = true
  invokeResult.value = null
  try {
    invokeResult.value = await httpApisApi.invoke(invokeTarget.value.id)
    activeTab.value = 'response'
  } catch (e: any) {
    toast.add({ severity: 'error', summary: 'Invocation failed', detail: e.message, life: 5000 })
  } finally { invoking.value = false }
}

async function doBookmark() {
  if (!invokeTarget.value) return
  try {
    const snap = await httpApisApi.bookmark(invokeTarget.value.id)
    toast.add({ severity: 'success', summary: 'Bookmarked', detail: `Snapshot saved at ${new Date(snap.capturedAt).toLocaleTimeString()}.`, life: 3000 })
    await loadSnapshots(invokeTarget.value.id)
  } catch (e: any) {
    toast.add({ severity: 'error', summary: 'Bookmark failed', detail: e.message, life: 5000 })
  }
}

// ── Comparison ────────────────────────────────────────────────────────────────
const comparing = ref(false)
const compareResult = ref<{ comparison: HttpSchemaComparisonResult; liveResponse: any } | null>(null)

async function doCompare() {
  if (!invokeTarget.value) return
  comparing.value = true
  compareResult.value = null
  try {
    compareResult.value = await httpApisApi.compare(invokeTarget.value.id)
    activeTab.value = 'comparison'
  } catch (e: any) {
    const msg = e.response?.data?.error ?? e.message
    toast.add({ severity: 'error', summary: 'Compare failed', detail: msg, life: 5000 })
  } finally { comparing.value = false }
}

function comparisonSeverity(c: HttpSchemaComparisonResult) {
  if (c.isBreaking) return 'danger'
  if (c.isDegraded) return 'warn'
  return 'success'
}
function comparisonLabel(c: HttpSchemaComparisonResult) {
  if (c.isBreaking) return '🔴 Breaking'
  if (c.isDegraded) return '🟡 Degraded'
  return '🟢 OK'
}

// ── Snapshots ─────────────────────────────────────────────────────────────────
const snapshots = ref<HttpResponseSnapshot[]>([])
const snapshotsLoading = ref(false)

async function loadSnapshots(id: string) {
  snapshotsLoading.value = true
  try { snapshots.value = await httpApisApi.getSnapshots(id) }
  finally { snapshotsLoading.value = false }
}

async function deleteSnapshot(snapshotId: string) {
  if (!invokeTarget.value) return
  await httpApisApi.deleteSnapshot(invokeTarget.value.id, snapshotId)
  snapshots.value = snapshots.value.filter(s => s.id !== snapshotId)
  toast.add({ severity: 'success', summary: 'Snapshot deleted', life: 2000 })
}

async function pinSnapshot(snapshotId: string) {
  if (!invokeTarget.value) return
  await httpApisApi.pinSnapshot(invokeTarget.value.id, snapshotId)
  const defInStore = store.definitions.find(d => d.id === invokeTarget.value!.id)
  if (defInStore) defInStore.goldenSnapshotId = snapshotId
  invokeTarget.value = { ...invokeTarget.value, goldenSnapshotId: snapshotId }
  toast.add({ severity: 'success', summary: 'Pinned as golden snapshot', life: 2000 })
}

// ── History ───────────────────────────────────────────────────────────────────
const history = ref<HttpApiInvocationRecord[]>([])
const historyLoading = ref(false)

async function loadHistory(id: string) {
  historyLoading.value = true
  try { history.value = await httpApisApi.getHistory(id, 50) }
  finally { historyLoading.value = false }
}

// ── Open invoke panel + lazy load ─────────────────────────────────────────────
async function openAndLoad(def: HttpApiDefinition) {
  openInvokePanel(def)
  await Promise.all([loadSnapshots(def.id), loadHistory(def.id)])
}

// ── Export dialog ─────────────────────────────────────────────────────────────
const exportDialogVisible   = ref(false)
const exportFilter          = ref('')
const exportSelected        = ref<Set<string>>(new Set())
const exportPassword        = ref('')
const exportPasswordConfirm = ref('')
const exportPasswordCopied  = ref(false)
const exporting             = ref(false)

const exportableDefs = computed(() => {
  const q = exportFilter.value.toLowerCase()
  return store.definitions.filter(d => !q || d.name.toLowerCase().includes(q) || d.baseUrl.toLowerCase().includes(q))
})
const exportAllChecked = computed(() =>
  exportableDefs.value.length > 0 && exportableDefs.value.every(d => exportSelected.value.has(d.id))
)

function openExportDialog() {
  exportFilter.value = ''
  exportSelected.value = new Set(store.definitions.map(d => d.id))
  exportPassword.value = ''
  exportPasswordConfirm.value = ''
  exportPasswordCopied.value = false
  exportDialogVisible.value = true
}
function toggleExportAll() {
  if (exportAllChecked.value) exportSelected.value = new Set()
  else exportSelected.value = new Set(exportableDefs.value.map(d => d.id))
}
function toggleExportItem(id: string) {
  const s = new Set(exportSelected.value)
  s.has(id) ? s.delete(id) : s.add(id)
  exportSelected.value = s
}
function generateExportPassword() {
  // Excludes visually similar characters (I, l, 1, O, 0) to reduce transcription errors.
  // Avoids shell-special characters (`, $, \, ") to ensure safe usage in terminals.
  const chars = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789!@#$%^&*'
  const arr = crypto.getRandomValues(new Uint8Array(20))
  exportPassword.value = Array.from(arr, b => chars[b % chars.length]).join('')
  exportPasswordConfirm.value = exportPassword.value
  exportPasswordCopied.value = false
}
async function copyExportPassword() {
  await navigator.clipboard.writeText(exportPassword.value)
  exportPasswordCopied.value = true
  setTimeout(() => { exportPasswordCopied.value = false }, 2000)
}

async function doExport() {
  if (exportSelected.value.size === 0) {
    toast.add({ severity: 'warn', summary: 'Select definitions', detail: 'Choose at least one definition to export.', life: 3000 }); return
  }
  if (!exportPassword.value) {
    toast.add({ severity: 'warn', summary: 'Password required', life: 3000 }); return
  }
  if (exportPassword.value !== exportPasswordConfirm.value) {
    toast.add({ severity: 'warn', summary: 'Passwords do not match', life: 3000 }); return
  }
  exporting.value = true
  try {
    const res = await apiClient.post('/http-apis/export',
      { ids: [...exportSelected.value], password: exportPassword.value },
      { responseType: 'blob' })
    const url = URL.createObjectURL(new Blob([res.data], { type: 'application/json' }))
    const a = document.createElement('a'); a.href = url; a.download = 'http-apis-export.json'; a.click(); URL.revokeObjectURL(url)
    exportDialogVisible.value = false
    toast.add({ severity: 'success', summary: 'Exported', detail: `${exportSelected.value.size} definition(s) exported.`, life: 3000 })
  } catch (e: any) {
    toast.add({ severity: 'error', summary: 'Export failed', detail: e.message, life: 5000 })
  } finally { exporting.value = false }
}

// ── Import dialog ─────────────────────────────────────────────────────────────
const importDialogVisible = ref(false)
const importFile          = ref<File | null>(null)
const importPassword      = ref('')
const importing           = ref(false)
const importFileInput     = ref<HTMLInputElement>()

function openImportDialog() {
  importFile.value = null
  importPassword.value = ''
  importDialogVisible.value = true
}
function onImportFileDrop(e: DragEvent) {
  const file = e.dataTransfer?.files?.[0]
  if (file?.name.endsWith('.json')) importFile.value = file
  else toast.add({ severity: 'warn', summary: 'Invalid file', detail: 'Drop a .json export file.', life: 3000 })
}
function onImportFileInput(e: Event) {
  const file = (e.target as HTMLInputElement).files?.[0]
  if (file) importFile.value = file
}
async function doImport() {
  if (!importFile.value) { toast.add({ severity: 'warn', summary: 'No file selected', life: 3000 }); return }
  if (!importPassword.value) { toast.add({ severity: 'warn', summary: 'Password required', life: 3000 }); return }
  importing.value = true
  try {
    const text    = await importFile.value.text()
    const payload = JSON.parse(text)
    const res = await apiClient.post<{ imported: number; total: number }>('/http-apis/import', { payload, password: importPassword.value })
    await store.loadAll()
    importDialogVisible.value = false
    const { imported, total } = res.data
    const skipped = total - imported
    const msg = skipped > 0 ? `${imported} imported, ${skipped} renamed (v2, v3…).` : `${imported} definition(s) imported.`
    toast.add({ severity: 'success', summary: 'Import complete', detail: msg, life: 5000 })
  } catch (e: any) {
    const detail = e.response?.data?.error ?? e.message
    toast.add({ severity: 'error', summary: 'Import failed', detail, life: 6000 })
  } finally {
    importing.value = false
    if (importFileInput.value) importFileInput.value.value = ''
  }
}

// ── Group dialog ──────────────────────────────────────────────────────────────
const groupDialog = ref(false)
const groupForm = ref<HttpApiGroup>({ name: '', color: '#6366f1', description: '' })
const groupEditMode = ref(false)
const groupFormOriginalName = ref('')

function openCreateGroup() {
  groupForm.value = { name: '', color: '#6366f1', description: '' }
  groupEditMode.value = false
  groupDialog.value = true
}
function openEditGroup(g: HttpApiGroup) {
  groupForm.value = { ...g }
  groupFormOriginalName.value = g.name
  groupEditMode.value = true
  groupDialog.value = true
}
async function saveGroup() {
  if (!groupForm.value.name.trim()) { toast.add({ severity: 'warn', summary: 'Name required', life: 3000 }); return }
  try {
    if (groupEditMode.value) {
      await httpApisApi.updateGroup(groupFormOriginalName.value, groupForm.value)
    } else {
      await httpApisApi.createGroup(groupForm.value)
    }
    await store.loadGroups()
    groupDialog.value = false
    toast.add({ severity: 'success', summary: groupEditMode.value ? 'Group updated' : 'Group created', life: 2000 })
  } catch (e: any) {
    toast.add({ severity: 'error', summary: 'Failed', detail: e.response?.data?.error ?? e.message, life: 5000 })
  }
}
async function deleteGroup(g: HttpApiGroup) {
  confirm.require({
    message: `Delete group '${g.name}'?`,
    header: 'Delete Group',
    icon: 'pi pi-trash',
    rejectProps: { label: 'Cancel', severity: 'secondary', outlined: true },
    acceptProps: { label: 'Delete', severity: 'danger' },
    accept: async () => {
      await httpApisApi.deleteGroup(g.name)
      await store.loadGroups()
      if (selectedGroup.value === g.name) selectedGroup.value = null
      toast.add({ severity: 'success', summary: 'Group deleted', life: 2000 })
    }
  })
}

// ── Schema display helper ─────────────────────────────────────────────────────
function schemaToString(schema: Record<string, unknown>): string {
  return JSON.stringify(schema, null, 2)
}

// ── Auth mode helpers ─────────────────────────────────────────────────────────
const AUTH_SEVERITY: Record<string, string> = {
  None: 'secondary', CustomHeaders: 'secondary',
  ApiKey: 'warn', Bearer: 'info', AzureClientCredentials: 'success',
}

function statusSeverity(code: number) {
  if (code >= 200 && code < 300) return 'success'
  if (code >= 400 && code < 500) return 'warn'
  if (code >= 500) return 'danger'
  return 'secondary'
}

// ── Mount ─────────────────────────────────────────────────────────────────────
onMounted(async () => {
  loading.value = true
  try { await Promise.all([store.loadAll(), store.loadGroups()]) }
  finally { loading.value = false }
})
</script>

<template>
  <div class="http-apis-view">
    <ConfirmDialog />

    <!-- ── Toolbar ──────────────────────────────────────────────────────────── -->
    <div class="toolbar">
      <div class="toolbar-left">
        <span class="p-input-icon-left search-wrap">
          <i class="pi pi-search" />
          <InputText v-model="searchQuery" placeholder="Search definitions…" class="search-input" />
        </span>
        <div class="group-chips">
          <Tag
            v-for="g in store.groups" :key="g.name"
            :value="g.name"
            :style="{ backgroundColor: g.color + '22', color: g.color, cursor: 'pointer', border: selectedGroup === g.name ? `2px solid ${g.color}` : 'none' }"
            class="group-chip"
            @click="selectedGroup = selectedGroup === g.name ? null : g.name"
          />
        </div>
        <Button
          icon="pi pi-star"
          :severity="showFavsFirst ? 'warning' : 'secondary'"
          :outlined="!showFavsFirst"
          rounded size="small"
          v-tooltip.top="showFavsFirst ? 'Showing favourites first' : 'Show favourites first'"
          @click="showFavsFirst = !showFavsFirst"
        />
      </div>
      <div class="toolbar-right">
        <Button label="Import" icon="pi pi-upload" severity="secondary" outlined size="small" @click="openImportDialog" />
        <Button label="Export" icon="pi pi-download" severity="secondary" outlined size="small" @click="openExportDialog" />
        <Button label="Group" icon="pi pi-tag" severity="secondary" outlined size="small" @click="openCreateGroup" />
        <Button label="New API" icon="pi pi-plus" size="small" @click="openCreate" />
      </div>
    </div>

    <!-- ── Main split: list + invoke panel ────────────────────────────────── -->
    <div class="main-layout">
      <!-- Definition list -->
      <div class="def-list">
        <Skeleton v-if="loading" height="4rem" class="mb-2" v-for="i in 4" :key="i" />

        <div
          v-if="!loading"
          v-for="def in filteredDefs"
          :key="def.id"
          class="def-card"
          :class="{ 'def-card--active': invokeTarget?.id === def.id }"
          @click="openAndLoad(def)"
        >
          <div class="def-card__header">
            <div class="def-card__title-row">
              <Button
                :icon="store.isFavourite(def.id) ? 'pi pi-star-fill' : 'pi pi-star'"
                :severity="store.isFavourite(def.id) ? 'warning' : 'secondary'"
                text rounded size="small"
                class="fav-btn"
                @click.stop="store.toggleFavourite(def.id)"
              />
              <span class="def-name">{{ def.name }}</span>
              <Tag :value="def.method" :severity="def.method === 'GET' ? 'info' : def.method === 'DELETE' ? 'danger' : 'secondary'" class="method-tag" />
              <Tag :value="def.authenticationMode" :severity="AUTH_SEVERITY[def.authenticationMode] ?? 'secondary'" class="auth-tag" />
            </div>
            <div class="def-card__url">{{ def.baseUrl }}{{ def.path }}</div>
            <div v-if="def.note" class="def-card__note">{{ def.note }}</div>
            <div v-if="def.tags.length" class="def-card__tags">
              <Tag v-for="t in def.tags" :key="t" :value="t" severity="secondary" rounded class="tag-pill" />
            </div>
          </div>
          <div class="def-card__actions" @click.stop>
            <Button icon="pi pi-copy" text rounded size="small" v-tooltip.top="'Duplicate'" @click="copyDef(def)" />
            <Button icon="pi pi-pencil" text rounded size="small" v-tooltip.top="'Edit'" @click="openEdit(def)" />
            <Button icon="pi pi-trash" text rounded size="small" severity="danger" v-tooltip.top="'Delete'" @click="confirmDelete(def)" />
          </div>
        </div>

        <div v-if="!loading && filteredDefs.length === 0" class="empty-state">
          <i class="pi pi-send empty-icon" />
          <p>No HTTP API definitions yet.</p>
          <Button label="Create one" size="small" @click="openCreate" />
        </div>
      </div>

      <!-- Invoke panel -->
      <div v-if="invokeTarget" class="invoke-panel">
        <div class="invoke-panel__header">
          <span class="invoke-panel__title">{{ invokeTarget.name }}</span>
          <div class="invoke-panel__actions">
            <Button label="Invoke" icon="pi pi-play" size="small" :loading="invoking" @click="doInvoke" />
            <Button label="Bookmark" icon="pi pi-bookmark" severity="secondary" outlined size="small" @click="doBookmark" />
            <Button label="Compare" icon="pi pi-sync" severity="info" outlined size="small" :loading="comparing" @click="doCompare" />
          </div>
        </div>

        <Tabs v-model:value="activeTab" class="invoke-tabs">
          <TabList>
            <Tab value="overview">Overview</Tab>
            <Tab value="response">Response</Tab>
            <Tab value="comparison">Comparison</Tab>
            <Tab value="snapshots">Snapshots</Tab>
            <Tab value="history">History</Tab>
          </TabList>
          <TabPanels>
          <!-- Overview -->
          <TabPanel value="overview">
            <div class="overview-grid">
              <div class="ov-row"><span class="ov-label">URL</span><code>{{ invokeTarget.baseUrl }}{{ invokeTarget.path }}</code></div>
              <div class="ov-row"><span class="ov-label">Method</span><Tag :value="invokeTarget.method" severity="info" /></div>
              <div class="ov-row"><span class="ov-label">Auth</span><Tag :value="invokeTarget.authenticationMode" :severity="AUTH_SEVERITY[invokeTarget.authenticationMode]" /></div>
              <div v-if="invokeTarget.note" class="ov-row"><span class="ov-label">Note</span><span>{{ invokeTarget.note }}</span></div>
              <div v-if="invokeTarget.lastInvokedAt" class="ov-row"><span class="ov-label">Last invoked</span><span>{{ new Date(invokeTarget.lastInvokedAt).toLocaleString() }}</span></div>
            </div>

            <!-- Headers preview -->
            <div v-if="invokeTarget.headers.length" class="section-label">Request Headers</div>
            <DataTable v-if="invokeTarget.headers.length" :value="invokeTarget.headers" size="small" class="mt-1">
              <Column field="name" header="Name" />
              <Column field="value" header="Value" />
            </DataTable>

            <!-- Query params preview -->
            <div v-if="invokeTarget.queryParams.filter(q => q.enabled).length" class="section-label mt-2">Query Params</div>
            <DataTable v-if="invokeTarget.queryParams.filter(q => q.enabled).length" :value="invokeTarget.queryParams.filter(q => q.enabled)" size="small">
              <Column field="name" header="Name" />
              <Column field="value" header="Value" />
            </DataTable>
          </TabPanel>

          <!-- Response -->
          <TabPanel value="response">
            <div v-if="!invokeResult" class="no-result">Hit <strong>Invoke</strong> to see the response.</div>
            <div v-else>
              <div class="response-meta">
                <Tag :value="`${invokeResult.statusCode}`" :severity="statusSeverity(invokeResult.statusCode)" />
                <span class="latency">{{ invokeResult.latencyMs }} ms</span>
                <span v-if="invokeResult.contentType" class="content-type">{{ invokeResult.contentType }}</span>
              </div>
              <div v-if="invokeResult.errorMessage" class="error-box">{{ invokeResult.errorMessage }}</div>
              <div class="section-label mt-2">Body</div>
              <JsonViewer v-if="invokeResult.body" :data="invokeResult.body" />
              <div class="section-label mt-2">Inferred Schema</div>
              <JsonViewer :data="schemaToString(invokeResult.inferredSchema)" />
              <div class="section-label mt-2">Response Headers</div>
              <DataTable :value="Object.entries(invokeResult.responseHeaders).map(([k,v]) => ({name:k,value:v}))" size="small">
                <Column field="name" header="Name" />
                <Column field="value" header="Value" />
              </DataTable>
            </div>
          </TabPanel>

          <!-- Comparison -->
          <TabPanel value="comparison">
            <div v-if="!compareResult" class="no-result">Hit <strong>Compare</strong> to diff against the latest snapshot.</div>
            <div v-else>
              <div class="compare-summary">
                <Tag :value="comparisonLabel(compareResult.comparison)" :severity="comparisonSeverity(compareResult.comparison)" />
                <span class="latency-ratio">
                  Latency: {{ compareResult.comparison.liveLatencyMs }} ms vs {{ compareResult.comparison.snapshotLatencyMs }} ms
                  ({{ (compareResult.comparison.latencyRatio * 100).toFixed(0) }}%)
                </span>
              </div>
              <div v-if="compareResult.comparison.removedProperties.length" class="diff-section diff-section--removed">
                <div class="diff-header">🔴 Removed ({{ compareResult.comparison.removedProperties.length }})</div>
                <div v-for="p in compareResult.comparison.removedProperties" :key="p" class="diff-item diff-item--removed">{{ p }}</div>
              </div>
              <div v-if="compareResult.comparison.addedProperties.length" class="diff-section diff-section--added">
                <div class="diff-header">🟢 Added ({{ compareResult.comparison.addedProperties.length }})</div>
                <div v-for="p in compareResult.comparison.addedProperties" :key="p" class="diff-item diff-item--added">{{ p }}</div>
              </div>
              <div v-if="compareResult.comparison.changedTypes.length" class="diff-section diff-section--changed">
                <div class="diff-header">🟡 Type changed ({{ compareResult.comparison.changedTypes.length }})</div>
                <div v-for="c in compareResult.comparison.changedTypes" :key="c.propertyPath" class="diff-item diff-item--changed">
                  <code>{{ c.propertyPath }}</code>: <del>{{ c.previousType }}</del> → <strong>{{ c.currentType }}</strong>
                </div>
              </div>
              <div v-if="!compareResult.comparison.isBreaking && !compareResult.comparison.isDegraded" class="diff-ok">
                ✅ No breaking changes or degradation detected.
              </div>
            </div>
          </TabPanel>

          <!-- Snapshots -->
          <TabPanel value="snapshots">
            <Skeleton v-if="snapshotsLoading" height="3rem" class="mb-2" />
            <div v-if="!snapshotsLoading && snapshots.length === 0" class="no-result">
              No bookmarks yet. Invoke and then hit <strong>Bookmark</strong>.
            </div>
            <div v-for="snap in snapshots" :key="snap.id" class="snapshot-row">
              <div class="snapshot-row__meta">
                <Tag :value="`${snap.statusCode}`" :severity="statusSeverity(snap.statusCode)" />
                <span class="snapshot-time">{{ new Date(snap.capturedAt).toLocaleString() }}</span>
                <span class="latency">{{ snap.latencyMs }} ms</span>
                <Tag v-if="snap.isGolden || invokeTarget?.goldenSnapshotId === snap.id" value="Golden" severity="warn" />
                <span v-if="snap.label" class="snap-label">{{ snap.label }}</span>
              </div>
              <div class="snapshot-row__schema">
                <JsonViewer :data="schemaToString(snap.inferredSchema)" />
              </div>
              <div class="snapshot-row__actions">
                <Button icon="pi pi-star" text rounded size="small" v-tooltip.top="'Pin as golden'" @click="pinSnapshot(snap.id)" />
                <Button icon="pi pi-trash" text rounded size="small" severity="danger" v-tooltip.top="'Delete'" @click="deleteSnapshot(snap.id)" />
              </div>
            </div>
          </TabPanel>

          <!-- History -->
          <TabPanel value="history">
            <Skeleton v-if="historyLoading" height="3rem" class="mb-2" />
            <DataTable
              v-if="!historyLoading"
              :value="history"
              size="small"
              :paginator="history.length > 20"
              :rows="20"
              class="history-table"
            >
              <Column field="invokedAt" header="When">
                <template #body="{ data }">{{ new Date(data.invokedAt).toLocaleString() }}</template>
              </Column>
              <Column field="statusCode" header="Status">
                <template #body="{ data }">
                  <Tag :value="`${data.statusCode}`" :severity="statusSeverity(data.statusCode)" />
                </template>
              </Column>
              <Column field="latencyMs" header="Latency">
                <template #body="{ data }">{{ data.latencyMs }} ms</template>
              </Column>
              <Column field="schemaMatchedSnapshot" header="Schema">
                <template #body="{ data }">
                  <Tag
                    v-if="data.schemaMatchedSnapshot !== null && data.schemaMatchedSnapshot !== undefined"
                    :value="data.schemaMatchedSnapshot ? 'Match' : 'Drift'"
                    :severity="data.schemaMatchedSnapshot ? 'success' : 'danger'"
                  />
                  <span v-else class="text-muted">—</span>
                </template>
              </Column>
              <Column field="errorMessage" header="Error">
                <template #body="{ data }">
                  <span v-if="data.errorMessage" class="error-text" v-tooltip.top="data.errorMessage">⚠️</span>
                </template>
              </Column>
            </DataTable>
          </TabPanel>
          </TabPanels>
        </Tabs>
      </div>

      <div v-if="!invokeTarget" class="invoke-placeholder">
        <i class="pi pi-arrow-left placeholder-icon" />
        <p>Select a definition to invoke and inspect.</p>
      </div>
    </div>

    <!-- ── Create / Edit dialog ─────────────────────────────────────────────── -->
    <Dialog v-model:visible="showDialog" :header="editMode ? 'Edit API Definition' : 'New API Definition'" modal :style="{ width: '680px' }" class="http-api-dialog">
      <div class="form-grid">
        <div class="form-row">
          <label>Name *</label>
          <InputText v-model="form.name" placeholder="My API" class="w-full" />
        </div>
        <div class="form-row-two">
          <div>
            <label>Base URL *</label>
            <InputText v-model="form.baseUrl" placeholder="https://api.example.com" class="w-full" />
          </div>
          <div>
            <label>Path</label>
            <InputText v-model="form.path" placeholder="/users" class="w-full" />
          </div>
        </div>
        <div class="form-row-two">
          <div>
            <label>Method</label>
            <Select v-model="form.method" :options="HTTP_METHODS" class="w-full" />
          </div>
          <div>
            <label>Auth Mode</label>
            <Select v-model="form.authenticationMode" :options="AUTH_MODES" optionLabel="label" optionValue="value" class="w-full" />
          </div>
        </div>
        <div class="form-row-two">
          <div>
            <label>Group</label>
            <Select v-model="form.groupName" :options="store.groups" optionLabel="name" optionValue="name" placeholder="No group" showClear class="w-full" />
          </div>
          <div>
            <label>Note</label>
            <InputText v-model="form.note" placeholder="Optional description" class="w-full" />
          </div>
        </div>

        <!-- API Key options -->
        <template v-if="form.authenticationMode === 'ApiKey'">
          <div class="form-section-label">API Key</div>
          <div class="form-row-two">
            <div>
              <label>Header Name</label>
              <InputText v-model="(form.apiKeyOptions as any).headerName" placeholder="X-Api-Key" class="w-full" @click="form.apiKeyOptions = form.apiKeyOptions ?? { headerName: 'X-Api-Key', apiKey: '' }" />
            </div>
            <div>
              <label>Prefix (optional)</label>
              <InputText v-model="(form.apiKeyOptions as any).prefix" placeholder="Bearer " class="w-full" />
            </div>
          </div>
          <div class="form-row">
            <label>API Key *</label>
            <Password v-model="(form.apiKeyOptions as any).apiKey" :feedback="false" toggleMask class="w-full" />
          </div>
        </template>

        <!-- Bearer options -->
        <template v-if="form.authenticationMode === 'Bearer'">
          <div class="form-section-label">Bearer Token</div>
          <div class="form-row">
            <label>Token *</label>
            <Password v-model="(form.bearerOptions as any).token" :feedback="false" toggleMask class="w-full" @click="form.bearerOptions = form.bearerOptions ?? { token: '' }" />
          </div>
        </template>

        <!-- Azure Client Credentials -->
        <template v-if="form.authenticationMode === 'AzureClientCredentials'">
          <div class="form-section-label">Azure Client Credentials</div>
          <div class="form-row-two">
            <div>
              <label>Tenant ID</label>
              <InputText v-model="(form.azureCredentials as any).tenantId" class="w-full" @click="form.azureCredentials = form.azureCredentials ?? { tenantId: '', clientId: '', clientSecret: '', scope: '' }" />
            </div>
            <div>
              <label>Client ID</label>
              <InputText v-model="(form.azureCredentials as any).clientId" class="w-full" />
            </div>
          </div>
          <div class="form-row-two">
            <div>
              <label>Client Secret</label>
              <Password v-model="(form.azureCredentials as any).clientSecret" :feedback="false" toggleMask class="w-full" />
            </div>
            <div>
              <label>Scope</label>
              <InputText v-model="(form.azureCredentials as any).scope" placeholder="https://..." class="w-full" />
            </div>
          </div>
        </template>

        <!-- Custom Headers section -->
        <div class="form-section-label">
          Request Headers
          <Button icon="pi pi-plus" text rounded size="small" @click="addHeader" />
        </div>
        <div v-for="(h, i) in form.headers" :key="i" class="form-row-three">
          <InputText v-model="h.name" placeholder="Header" class="w-full" />
          <InputText v-model="h.value" placeholder="Value" class="w-full" />
          <Button icon="pi pi-times" text rounded size="small" severity="danger" @click="removeHeader(i)" />
        </div>

        <!-- Query params -->
        <div class="form-section-label">
          Query Params
          <Button icon="pi pi-plus" text rounded size="small" @click="addQueryParam" />
        </div>
        <div v-for="(q, i) in form.queryParams" :key="i" class="form-row-three">
          <InputText v-model="q.name" placeholder="Name" class="w-full" />
          <InputText v-model="q.value" placeholder="Value" class="w-full" />
          <Button icon="pi pi-times" text rounded size="small" severity="danger" @click="removeQueryParam(i)" />
        </div>

        <!-- Body template -->
        <div v-if="form.method && !['GET','HEAD'].includes(form.method)" class="form-row">
          <label>Body Template (JSON)</label>
          <Textarea v-model="form.bodyTemplate" rows="4" class="w-full font-mono" placeholder='{"key": "value"}' />
        </div>
      </div>

      <template #footer>
        <Button label="Cancel" severity="secondary" outlined @click="showDialog = false" />
        <Button :label="editMode ? 'Save' : 'Create'" icon="pi pi-check" :loading="saving" @click="saveForm" />
      </template>
    </Dialog>

    <!-- ── Group dialog ─────────────────────────────────────────────────────── -->
    <Dialog v-model:visible="groupDialog" :header="groupEditMode ? 'Edit Group' : 'New Group'" modal :style="{ width: '400px' }">
      <div class="form-grid">
        <div class="form-row"><label>Name</label><InputText v-model="groupForm.name" class="w-full" /></div>
        <div class="form-row"><label>Color</label><input type="color" v-model="groupForm.color" class="color-picker" /></div>
        <div class="form-row"><label>Description</label><InputText v-model="groupForm.description" class="w-full" /></div>
      </div>
      <div v-if="groupEditMode" class="group-manager-list mt-3">
        <div v-for="g in store.groups" :key="g.name" class="group-manager-row">
          <span :style="{ color: g.color }">●</span> {{ g.name }}
          <span class="flex-spacer" />
          <Button icon="pi pi-pencil" text rounded size="small" @click="openEditGroup(g)" />
          <Button icon="pi pi-trash" text rounded size="small" severity="danger" @click="deleteGroup(g)" />
        </div>
      </div>
      <template #footer>
        <Button label="Cancel" severity="secondary" outlined @click="groupDialog = false" />
        <Button label="Save" icon="pi pi-check" @click="saveGroup" />
      </template>
    </Dialog>

    <!-- ── Export dialog ────────────────────────────────────────────────────── -->
    <Dialog v-model:visible="exportDialogVisible" header="Export HTTP API Definitions" modal :style="{ width: '560px' }">
      <div class="export-filter-row">
        <InputText v-model="exportFilter" placeholder="Filter definitions…" class="w-full" />
      </div>
      <div class="export-select-all">
        <Checkbox :modelValue="exportAllChecked" binary @update:modelValue="toggleExportAll" inputId="exportAll" />
        <label for="exportAll">Select all</label>
      </div>
      <div class="export-list">
        <div v-for="d in exportableDefs" :key="d.id" class="export-item">
          <Checkbox :modelValue="exportSelected.has(d.id)" binary @update:modelValue="toggleExportItem(d.id)" :inputId="`exp-${d.id}`" />
          <label :for="`exp-${d.id}`">{{ d.name }} <span class="export-url">{{ d.baseUrl }}{{ d.path }}</span></label>
        </div>
      </div>
      <div class="export-password-section">
        <label>Password *</label>
        <div class="export-pw-row">
          <Password v-model="exportPassword" :feedback="false" toggleMask class="flex-1" />
          <Button icon="pi pi-refresh" text rounded v-tooltip.top="'Generate'" @click="generateExportPassword" />
          <Button :icon="exportPasswordCopied ? 'pi pi-check' : 'pi pi-copy'" text rounded v-tooltip.top="'Copy'" @click="copyExportPassword" />
        </div>
        <label>Confirm</label>
        <Password v-model="exportPasswordConfirm" :feedback="false" toggleMask class="w-full" />
      </div>
      <template #footer>
        <Button label="Cancel" severity="secondary" outlined @click="exportDialogVisible = false" />
        <Button label="Export & Download" icon="pi pi-download" :loading="exporting" @click="doExport" />
      </template>
    </Dialog>

    <!-- ── Import dialog ────────────────────────────────────────────────────── -->
    <Dialog v-model:visible="importDialogVisible" header="Import HTTP API Definitions" modal :style="{ width: '460px' }">
      <div
        class="import-drop-zone"
        @dragover.prevent
        @drop.prevent="onImportFileDrop"
      >
        <i class="pi pi-upload import-icon" />
        <p v-if="!importFile">Drag &amp; drop or <label for="importFileInput" class="import-browse">browse</label></p>
        <p v-else class="import-file-name">📄 {{ importFile.name }}</p>
        <input id="importFileInput" ref="importFileInput" type="file" accept=".json" style="display:none" @change="onImportFileInput" />
      </div>
      <div class="form-row mt-2">
        <label>Password *</label>
        <Password v-model="importPassword" :feedback="false" toggleMask class="w-full" />
      </div>
      <template #footer>
        <Button label="Cancel" severity="secondary" outlined @click="importDialogVisible = false" />
        <Button label="Import" icon="pi pi-upload" :loading="importing" @click="doImport" />
      </template>
    </Dialog>
  </div>
</template>

<style scoped>
.http-apis-view { display: flex; flex-direction: column; height: 100%; gap: 0.75rem; padding: 1rem; }

.toolbar { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 0.5rem; }
.toolbar-left { display: flex; align-items: center; gap: 0.5rem; flex-wrap: wrap; }
.toolbar-right { display: flex; align-items: center; gap: 0.5rem; }
.search-wrap { position: relative; }
.search-input { padding-left: 2rem; }
.group-chips { display: flex; gap: 0.25rem; flex-wrap: wrap; }
.group-chip { cursor: pointer; }

.main-layout { display: grid; grid-template-columns: 320px 1fr; gap: 1rem; flex: 1; min-height: 0; overflow: hidden; }

.def-list { overflow-y: auto; display: flex; flex-direction: column; gap: 0.5rem; }
.def-card { background: var(--surface-card); border: 1px solid var(--surface-border); border-radius: 8px; padding: 0.75rem; cursor: pointer; display: flex; justify-content: space-between; transition: border-color 0.15s; }
.def-card:hover, .def-card--active { border-color: var(--primary-color); }
.def-card__header { flex: 1; min-width: 0; }
.def-card__title-row { display: flex; align-items: center; gap: 0.4rem; flex-wrap: wrap; }
.def-name { font-weight: 600; font-size: 0.9rem; }
.method-tag, .auth-tag { font-size: 0.7rem !important; }
.def-card__url { font-size: 0.75rem; color: var(--text-color-secondary); margin-top: 0.2rem; word-break: break-all; }
.def-card__note { font-size: 0.75rem; color: var(--text-color-secondary); margin-top: 0.15rem; }
.def-card__tags { display: flex; gap: 0.25rem; flex-wrap: wrap; margin-top: 0.3rem; }
.tag-pill { font-size: 0.65rem !important; }
.def-card__actions { display: flex; flex-direction: column; gap: 0.25rem; justify-content: flex-start; opacity: 0; transition: opacity 0.15s; }
.def-card:hover .def-card__actions { opacity: 1; }
.fav-btn { flex-shrink: 0; }

.empty-state { text-align: center; padding: 2rem; color: var(--text-color-secondary); }
.empty-icon { font-size: 2rem; margin-bottom: 0.5rem; display: block; }

.invoke-panel { overflow-y: auto; border: 1px solid var(--surface-border); border-radius: 8px; padding: 0.75rem; background: var(--surface-card); }
.invoke-panel__header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 0.75rem; flex-wrap: wrap; gap: 0.5rem; }
.invoke-panel__title { font-weight: 700; font-size: 1rem; }
.invoke-panel__actions { display: flex; gap: 0.4rem; }
.invoke-tabs { height: 100%; }

.invoke-placeholder { display: flex; flex-direction: column; align-items: center; justify-content: center; color: var(--text-color-secondary); }
.placeholder-icon { font-size: 2rem; margin-bottom: 0.5rem; }

.overview-grid { display: flex; flex-direction: column; gap: 0.4rem; }
.ov-row { display: flex; align-items: center; gap: 0.5rem; font-size: 0.85rem; }
.ov-label { font-weight: 600; min-width: 90px; color: var(--text-color-secondary); }
.section-label { font-size: 0.75rem; font-weight: 600; color: var(--text-color-secondary); text-transform: uppercase; letter-spacing: 0.05em; margin-top: 0.75rem; margin-bottom: 0.25rem; }
.form-section-label { font-size: 0.75rem; font-weight: 600; color: var(--text-color-secondary); text-transform: uppercase; display: flex; align-items: center; gap: 0.3rem; margin-top: 0.5rem; }
.mt-2 { margin-top: 0.5rem; }

.no-result { text-align: center; padding: 2rem; color: var(--text-color-secondary); }
.response-meta { display: flex; align-items: center; gap: 0.5rem; }
.latency { font-size: 0.8rem; color: var(--text-color-secondary); }
.content-type { font-size: 0.75rem; color: var(--text-color-secondary); }
.error-box { background: #fee2e2; color: #991b1b; border-radius: 6px; padding: 0.5rem; font-size: 0.8rem; margin-top: 0.4rem; }
.error-text { cursor: help; }

.compare-summary { display: flex; align-items: center; gap: 0.75rem; margin-bottom: 0.75rem; }
.latency-ratio { font-size: 0.8rem; color: var(--text-color-secondary); }
.diff-section { margin-top: 0.5rem; border-radius: 6px; padding: 0.5rem; }
.diff-section--removed { background: #fee2e2; }
.diff-section--added { background: #dcfce7; }
.diff-section--changed { background: #fef9c3; }
.diff-header { font-weight: 600; font-size: 0.8rem; margin-bottom: 0.3rem; }
.diff-item { font-size: 0.8rem; font-family: monospace; padding: 0.1rem 0; }
.diff-item--removed { color: #991b1b; }
.diff-item--added { color: #166534; }
.diff-item--changed { color: #854d0e; }
.diff-ok { text-align: center; padding: 1rem; color: #166534; font-weight: 600; }

.snapshot-row { border: 1px solid var(--surface-border); border-radius: 6px; padding: 0.5rem; margin-bottom: 0.5rem; }
.snapshot-row__meta { display: flex; align-items: center; gap: 0.5rem; margin-bottom: 0.3rem; }
.snapshot-time { font-size: 0.8rem; color: var(--text-color-secondary); }
.snap-label { font-size: 0.75rem; color: var(--text-color-secondary); font-style: italic; }
.snapshot-row__actions { display: flex; gap: 0.25rem; margin-top: 0.3rem; }
.snapshot-row__schema { font-size: 0.75rem; max-height: 120px; overflow-y: auto; }

.history-table { font-size: 0.82rem; }

/* Form dialog */
.form-grid { display: flex; flex-direction: column; gap: 0.6rem; }
.form-row { display: flex; flex-direction: column; gap: 0.2rem; }
.form-row label { font-size: 0.8rem; font-weight: 600; color: var(--text-color-secondary); }
.form-row-two { display: grid; grid-template-columns: 1fr 1fr; gap: 0.5rem; }
.form-row-two label { font-size: 0.8rem; font-weight: 600; color: var(--text-color-secondary); }
.form-row-three { display: grid; grid-template-columns: 1fr 1fr auto; gap: 0.4rem; align-items: center; }
.w-full { width: 100%; }
.font-mono { font-family: monospace; }

/* Export / Import */
.export-filter-row { margin-bottom: 0.5rem; }
.export-select-all { display: flex; align-items: center; gap: 0.4rem; margin-bottom: 0.25rem; font-size: 0.85rem; }
.export-list { max-height: 180px; overflow-y: auto; display: flex; flex-direction: column; gap: 0.3rem; padding: 0.25rem 0; }
.export-item { display: flex; align-items: center; gap: 0.4rem; font-size: 0.85rem; }
.export-url { color: var(--text-color-secondary); font-size: 0.75rem; }
.export-password-section { display: flex; flex-direction: column; gap: 0.25rem; margin-top: 0.75rem; }
.export-pw-row { display: flex; align-items: center; gap: 0.25rem; }
.flex-1 { flex: 1; }

.import-drop-zone { border: 2px dashed var(--surface-border); border-radius: 8px; padding: 1.5rem; text-align: center; cursor: pointer; }
.import-drop-zone:hover { border-color: var(--primary-color); }
.import-icon { font-size: 2rem; display: block; margin-bottom: 0.5rem; }
.import-browse { color: var(--primary-color); cursor: pointer; text-decoration: underline; }
.import-file-name { font-weight: 600; }

/* Groups */
.group-manager-list { display: flex; flex-direction: column; gap: 0.3rem; }
.group-manager-row { display: flex; align-items: center; gap: 0.5rem; font-size: 0.85rem; }
.flex-spacer { flex: 1; }
.color-picker { width: 3rem; height: 2rem; border: none; cursor: pointer; border-radius: 4px; }
.mt-1 { margin-top: 0.25rem; }
.mt-3 { margin-top: 0.75rem; }
.text-muted { color: var(--text-color-secondary); }
</style>
