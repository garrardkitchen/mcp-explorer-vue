<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
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
import AzureContextBanner from '@/components/connections/AzureContextBanner.vue'
import AppRegistrationPicker from '@/components/connections/AppRegistrationPicker.vue'
import KeyVaultSecretPicker from '@/components/connections/KeyVaultSecretPicker.vue'
import { useHttpApisStore } from '@/stores/httpApis'
import { httpApisApi } from '@/api/httpApis'
import { systemApi } from '@/api/system'
import { apiClient } from '@/api/client'
import type {
  AzureAccountInfo,
  AzureAppRegistration,
  AzureSubscription,
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
const route = useRoute()

// ── State ───────────────────────────────────────────────────────────────────
const loading = ref(false)
const searchQuery = ref('')
const selectedGroup = ref<string | null>(null)
const showFavsFirst = ref(false)

// ── Form dialog ──────────────────────────────────────────────────────────────
const showDialog = ref(false)
const editMode = ref(false)
const saving = ref(false)
const isConnectionsMode = computed(() => route.name === 'http-api-connections')
const isInvokeMode = computed(() => !isConnectionsMode.value)
const selectedSubscriptionId = ref<string | undefined>(undefined)

type AuthorizationType = 'None' | 'Bearer' | 'Basic'
type HttpApiEditorHeader = HttpApiHeader & { authorizationType?: AuthorizationType }

const authorizationTypeOptions: { label: string; value: AuthorizationType }[] = [
  { label: 'Raw', value: 'None' },
  { label: 'Bearer', value: 'Bearer' },
  { label: 'Basic', value: 'Basic' },
]

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
const formHeaders = computed(() => (form.value.headers ?? []) as HttpApiEditorHeader[])

function isAuthorizationHeader(header: Pick<HttpApiEditorHeader, 'name'>) {
  return header.name?.trim().toLowerCase() === 'authorization'
}

function isAuthorizationValueForScheme(value: string, scheme: string) {
  return value === scheme || value.startsWith(`${scheme} `)
}

function inferAuthorizationType(header: HttpApiEditorHeader): AuthorizationType {
  if (!isAuthorizationHeader(header)) return 'None'
  const value = header.value?.trim().toLowerCase() ?? ''
  if (isAuthorizationValueForScheme(value, 'basic')) return 'Basic'
  if (isAuthorizationValueForScheme(value, 'bearer')) return 'Bearer'
  return 'None'
}

function normalizeAuthorizationHeaders(headers?: HttpApiEditorHeader[]) {
  headers?.forEach((header) => {
    if (!header.authorizationType) {
      header.authorizationType = inferAuthorizationType(header)
    }
  })
}

function ensureAuthorizationHeaderValue(header: HttpApiEditorHeader) {
  if (!isAuthorizationHeader(header)) return
  const trimmed = (header.value ?? '').trim()
  if (!trimmed) return

  const normalized = stripKnownAuthorizationScheme(trimmed)
  if (header.authorizationType === 'Bearer') {
    header.value = normalized ? `Bearer ${normalized}` : 'Bearer'
  } else if (header.authorizationType === 'Basic') {
    header.value = normalized ? `Basic ${normalized}` : 'Basic'
  } else {
    header.value = trimmed
  }
}

function stripKnownAuthorizationScheme(value: string) {
  return value.replace(/^(basic|bearer)\b\s*/i, '').trim()
}

function headerValuePlaceholder(header: HttpApiEditorHeader) {
  if (!isAuthorizationHeader(header)) return 'Value'
  switch (header.authorizationType) {
    case 'Bearer':
      return 'Token, with or without Bearer'
    case 'Basic':
      return 'Base64 credentials, with or without Basic'
    default:
      return 'Full value, e.g. Basic <token>'
  }
}

function ensureAuthOptionModels() {
  switch (form.value.authenticationMode) {
    case 'ApiKey':
      form.value.apiKeyOptions = form.value.apiKeyOptions ?? { headerName: 'X-Api-Key', apiKey: '', prefix: '' }
      break
    case 'Bearer':
      form.value.bearerOptions = form.value.bearerOptions ?? { token: '' }
      break
    case 'AzureClientCredentials':
      form.value.azureCredentials = form.value.azureCredentials ?? { tenantId: '', clientId: '', clientSecret: '', scope: '', subscriptionId: selectedSubscriptionId.value }
      break
  }
}

function onAuthModeChanged() {
  ensureAuthOptionModels()
}

function onAzureAccountLoaded(account: AzureAccountInfo | null) {
  if (!account || !form.value.azureCredentials) return
  if (!form.value.azureCredentials.tenantId.trim()) {
    form.value.azureCredentials = { ...form.value.azureCredentials, tenantId: account.tenantId }
  }
}

function onSubscriptionChanged(subscription: AzureSubscription) {
  selectedSubscriptionId.value = subscription.id
  if (form.value.azureCredentials) {
    form.value.azureCredentials = {
      ...form.value.azureCredentials,
      tenantId: subscription.tenantId,
      subscriptionId: subscription.id,
    }
  }
}

function onAppRegistrationSelected(app: AzureAppRegistration) {
  if (!form.value.azureCredentials) return
  const scope = app.firstApiResourceId ? `api://${app.firstApiResourceId}/.default` : ''
  form.value.azureCredentials = {
    ...form.value.azureCredentials,
    clientId: app.appId,
    scope: scope || form.value.azureCredentials.scope,
  }
}

function openCreate() {
  form.value = blankForm()
  normalizeAuthorizationHeaders(form.value.headers as HttpApiEditorHeader[])
  selectedSubscriptionId.value = undefined
  originalId.value = ''
  editMode.value = false
  showDialog.value = true
}

function openEdit(def: HttpApiDefinition) {
  const headers = [...def.headers] as HttpApiEditorHeader[]
  normalizeAuthorizationHeaders(headers)
  form.value = { ...def, headers, queryParams: [...def.queryParams], tags: [...def.tags] }
  selectedSubscriptionId.value = form.value.azureCredentials?.subscriptionId ?? undefined
  ensureAuthOptionModels()
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
  if (form.value.authenticationMode === 'AzureClientCredentials') {
    const az = form.value.azureCredentials
    const hasSecret = !!az?.clientSecret?.trim() || !!az?.keyVaultSecretRef
    if (!az?.tenantId?.trim() || !az?.clientId?.trim() || !hasSecret || !az?.scope?.trim()) {
      toast.add({ severity: 'warn', summary: 'Validation', detail: 'Tenant ID, Client ID, Client Secret (or Key Vault reference) and Scope are required for Azure Client Credentials', life: 4000 }); return
    }
  }
  ensureAuthOptionModels()
  const headers = (form.value.headers ?? []) as HttpApiEditorHeader[]
  headers.forEach(ensureAuthorizationHeaderValue)
  form.value.headers = headers.map(h => ({ name: h.name, value: h.value }))

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
const expandedId = ref<string | null>(null)
const invoking = ref(false)
const invokeResult = ref<HttpApiInvokeResponse | null>(null)
const showInvokeInputDialog = ref(false)
const invokeInputFields = ref<Array<{ name: string; defaultValue: string; value: string }>>([])
const pendingInvokeAction = ref<'invoke' | 'bookmark' | 'compare'>('invoke')
const invokeInputPattern = /\{(?<name>[A-Za-z_][A-Za-z0-9_.-]*)(:(?<default>[^{}]*))?\}/g

function openInvokePanel(def: HttpApiDefinition) {
  invokeTarget.value = def
  invokeResult.value = null
  compareResult.value = null
  activeTab.value = 'overview'
}

const activeTab = ref('overview')

function extractInputsFromTemplate(template: string | null | undefined, bucket: Map<string, string>) {
  if (!template) return
  for (const match of template.matchAll(invokeInputPattern)) {
    const name = match.groups?.name?.trim()
    if (!name || bucket.has(name)) continue
    bucket.set(name, match.groups?.default ?? '')
  }
}

function collectInvokeInputFields(def: HttpApiDefinition) {
  const placeholders = new Map<string, string>()
  extractInputsFromTemplate(def.baseUrl, placeholders)
  extractInputsFromTemplate(def.path, placeholders)
  extractInputsFromTemplate(def.bodyTemplate, placeholders)

  for (const h of def.headers) {
    extractInputsFromTemplate(h.name, placeholders)
    extractInputsFromTemplate(h.value, placeholders)
  }
  for (const q of def.queryParams) {
    extractInputsFromTemplate(q.name, placeholders)
    extractInputsFromTemplate(q.value, placeholders)
  }

  extractInputsFromTemplate(def.apiKeyOptions?.headerName, placeholders)
  extractInputsFromTemplate(def.apiKeyOptions?.apiKey, placeholders)
  extractInputsFromTemplate(def.apiKeyOptions?.prefix, placeholders)
  extractInputsFromTemplate(def.bearerOptions?.token, placeholders)
  extractInputsFromTemplate(def.azureCredentials?.tenantId, placeholders)
  extractInputsFromTemplate(def.azureCredentials?.clientId, placeholders)
  extractInputsFromTemplate(def.azureCredentials?.clientSecret, placeholders)
  extractInputsFromTemplate(def.azureCredentials?.scope, placeholders)
  extractInputsFromTemplate(def.azureCredentials?.authorityHost, placeholders)

  return Array.from(placeholders.entries()).map(([name, defaultValue]) => ({
    name,
    defaultValue,
    value: defaultValue,
  }))
}

function resolveInvokeInputs() {
  const resolved: Record<string, string> = {}
  for (const field of invokeInputFields.value) {
    const value = field.value?.trim() || field.defaultValue?.trim()
    if (value) {
      resolved[field.name] = value
    }
  }
  return resolved
}

function promptInvokeInputsIfNeeded(action: 'invoke' | 'bookmark' | 'compare') {
  if (!invokeTarget.value) return false
  const fields = collectInvokeInputFields(invokeTarget.value)
  if (fields.length === 0) return false
  pendingInvokeAction.value = action
  invokeInputFields.value = fields
  showInvokeInputDialog.value = true
  return true
}

async function executeInvoke(inputs?: Record<string, string>) {
  if (!invokeTarget.value) return
  const id = invokeTarget.value.id
  invoking.value = true
  invokeResult.value = null
  try {
    const result = await httpApisApi.invoke(id, inputs)
    if (invokeTarget.value?.id !== id) return
    invokeResult.value = result
    activeTab.value = 'response'
    const defInStore = store.definitions.find(d => d.id === id)
    if (defInStore) {
      defInStore.lastStatusCode = result.statusCode
      defInStore.lastInvokedAt = new Date().toISOString()
    }
    await loadHistory(id)
  } catch (e: any) {
    if (invokeTarget.value?.id === id)
      toast.add({ severity: 'error', summary: 'Invocation failed', detail: e.message, life: 5000 })
  } finally {
    invoking.value = false
  }
}

async function doInvoke() {
  if (!invokeTarget.value) return
  if (promptInvokeInputsIfNeeded('invoke')) return
  await executeInvoke()
}

async function confirmInvokeWithInputs() {
  const action = pendingInvokeAction.value
  const inputs = resolveInvokeInputs()
  showInvokeInputDialog.value = false
  if (action === 'bookmark') {
    await doBookmark(inputs)
    return
  }
  if (action === 'compare') {
    await doCompare(inputs)
    return
  }
  await executeInvoke(inputs)
}

async function doBookmark(inputs?: Record<string, string>) {
  if (!invokeTarget.value) return
  if (!inputs && promptInvokeInputsIfNeeded('bookmark')) return
  try {
    const snap = await httpApisApi.bookmark(invokeTarget.value.id, undefined, inputs)
    toast.add({ severity: 'success', summary: 'Bookmarked', detail: `Snapshot saved at ${new Date(snap.capturedAt).toLocaleTimeString()}.`, life: 3000 })
    await Promise.all([loadSnapshots(invokeTarget.value.id), loadHistory(invokeTarget.value.id)])
  } catch (e: any) {
    toast.add({ severity: 'error', summary: 'Bookmark failed', detail: e.message, life: 5000 })
  }
}

// ── Comparison ────────────────────────────────────────────────────────────────
const comparing = ref(false)
const compareResult = ref<{ comparison: HttpSchemaComparisonResult; liveResponse: any } | null>(null)

async function doCompare(inputs?: Record<string, string>) {
  if (!invokeTarget.value) return
  if (!inputs && promptInvokeInputsIfNeeded('compare')) return
  comparing.value = true
  compareResult.value = null
  try {
    compareResult.value = await httpApisApi.compare(invokeTarget.value.id, undefined, inputs)
    activeTab.value = 'comparison'
    await loadHistory(invokeTarget.value.id)
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
  try {
    const snaps = await httpApisApi.getSnapshots(id)
    if (invokeTarget.value?.id === id) snapshots.value = snaps
  }
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
const expandedHistoryRows = ref<HttpApiInvocationRecord[]>([])

async function loadHistory(id: string) {
  historyLoading.value = true
  try {
    const rows = await httpApisApi.getHistory(id, 50)
    if (invokeTarget.value?.id === id) {
      history.value = rows
      expandedHistoryRows.value = []
    }
  }
  finally { historyLoading.value = false }
}

// ── Open invoke panel + lazy load ─────────────────────────────────────────────
function openAndLoad(def: HttpApiDefinition) {
  openInvokePanel(def)
  expandedId.value = def.id
  loadSnapshots(def.id)
  loadHistory(def.id)
}

function toggleExpand(def: HttpApiDefinition) {
  if (expandedId.value === def.id) {
    expandedId.value = null
  } else {
    openAndLoad(def)
  }
}

async function invokeAndExpand(def: HttpApiDefinition) {
  openAndLoad(def)
  activeTab.value = 'response'
  await doInvoke()
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

// ── Connections mode: expand + CLI copy ──────────────────────────────────────
const connExpandedRows = ref<Record<string, boolean>>({})
const copiedCliId = ref<string | null>(null)
const dataPath = ref<string | null>(null)

function groupColor(name: string | null | undefined): string {
  return store.groups.find(g => g.name === name)?.color ?? 'var(--surface-border)'
}

function cliInvokeCommand(def: HttpApiDefinition): string {
  const parts: string[] = ['mcp-http']
  if (dataPath.value) parts.push(`--data-path "${dataPath.value}"`)
  parts.push(`http api invoke --name "${def.name.replace(/"/g, '\\"')}"`)
  if (def.baseUrl?.includes('host.docker.internal')) parts.push('--use-localhost')
  return parts.join(' ')
}

async function copyCliCommand(def: HttpApiDefinition) {
  try {
    await navigator.clipboard.writeText(cliInvokeCommand(def))
    copiedCliId.value = def.id
    setTimeout(() => { if (copiedCliId.value === def.id) copiedCliId.value = null }, 2000)
    toast.add({ severity: 'info', summary: 'CLI command copied', life: 2000 })
  } catch {
    toast.add({ severity: 'error', summary: 'Copy failed — clipboard unavailable', life: 3000 })
  }
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

function methodSeverity(method: string) {
  if (method === 'GET') return 'info'
  if (method === 'DELETE') return 'danger'
  return 'secondary'
}

// ── Mount ─────────────────────────────────────────────────────────────────────
onMounted(async () => {
  loading.value = true
  try {
    const [, , info] = await Promise.all([
      store.loadAll(),
      store.loadGroups(),
      systemApi.getInfo().catch(() => ({ apiVersion: '', dotnetVersion: '', dataPath: null })),
    ])
    dataPath.value = info.dataPath ?? null
  } finally {
    loading.value = false
  }
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
      <div v-if="isConnectionsMode" class="toolbar-right">
        <Button label="Import" icon="pi pi-upload" severity="secondary" outlined size="small" @click="openImportDialog" />
        <Button label="Export" icon="pi pi-download" severity="secondary" outlined size="small" @click="openExportDialog" />
        <Button label="Group" icon="pi pi-tag" severity="secondary" outlined size="small" @click="openCreateGroup" />
        <Button label="New API" icon="pi pi-plus" size="small" @click="openCreate" />
      </div>
    </div>

    <div v-if="isConnectionsMode" class="table-wrap">
      <DataTable :value="filteredDefs" :loading="loading" stripedRows rowHover scrollable scrollHeight="flex"
                 v-model:expandedRows="connExpandedRows" dataKey="id">
        <template #empty>
          <div class="empty-state">
            <i class="pi pi-send empty-icon" />
            <p>No HTTP API definitions yet.</p>
            <Button label="Create one" icon="pi pi-plus" @click="openCreate" />
          </div>
        </template>
        <template #loading>
          <div class="p-4"><Skeleton v-for="i in 4" :key="i" height="40px" class="mb-2" /></div>
        </template>

        <Column expander style="width:2.5rem; padding-left:0.5rem; padding-right:0" />

        <Column field="method" header="Method" sortable style="min-width:100px">
          <template #body="{ data }">
            <Tag :value="data.method" :severity="methodSeverity(data.method)" />
          </template>
        </Column>

        <Column field="name" header="Name" sortable style="min-width:220px">
          <template #body="{ data }">
            <div class="table-name-cell">
              <Button
                :icon="store.isFavourite(data.id) ? 'pi pi-star-fill' : 'pi pi-star'"
                :severity="store.isFavourite(data.id) ? 'warning' : 'secondary'"
                text rounded size="small" class="fav-btn"
                @click.stop="store.toggleFavourite(data.id)"
              />
              <span class="def-name">{{ data.name }}</span>
            </div>
          </template>
        </Column>

        <Column header="Endpoint" style="width:220px; max-width:220px; min-width:120px">
          <template #body="{ data }">
            <span class="conn-url" :title="`${data.baseUrl}${data.path}`">{{ data.baseUrl }}{{ data.path }}</span>
          </template>
        </Column>

        <Column field="authenticationMode" header="Auth" style="min-width:160px">
          <template #body="{ data }">
            <Tag :value="data.authenticationMode" :severity="AUTH_SEVERITY[data.authenticationMode] ?? 'secondary'" />
          </template>
        </Column>

        <Column field="groupName" header="Group" sortable style="min-width:130px">
          <template #body="{ data }">
            <span v-if="data.groupName" class="conn-group-pill"
                  :style="{ backgroundColor: groupColor(data.groupName) + '22', color: groupColor(data.groupName), border: `1px solid ${groupColor(data.groupName)}55` }">
              {{ data.groupName }}
            </span>
            <span v-else class="text-muted">—</span>
          </template>
        </Column>

        <Column header="Actions" style="min-width:200px; text-align:right">
          <template #body="{ data }">
            <div class="row-actions">
              <Button
                :icon="copiedCliId === data.id ? 'pi pi-check' : 'pi pi-terminal'"
                text rounded size="small"
                v-tooltip.top="'Copy CLI command'"
                @click="copyCliCommand(data)"
              />
              <Button icon="pi pi-copy" text rounded size="small" v-tooltip.top="'Duplicate'" @click="copyDef(data)" />
              <Button icon="pi pi-pencil" text rounded size="small" v-tooltip.top="'Edit'" @click="openEdit(data)" />
              <Button icon="pi pi-trash" text rounded size="small" severity="danger" v-tooltip.top="'Delete'" @click="confirmDelete(data)" />
            </div>
          </template>
        </Column>

        <template #expansion="{ data }">
          <div class="conn-expand-panel">
            <div class="conn-expand-section">
              <span class="conn-expand-label">Full URL</span>
              <span class="conn-expand-mono">{{ data.baseUrl }}{{ data.path }}</span>
            </div>
            <div v-if="data.note" class="conn-expand-section">
              <span class="conn-expand-label">Note</span>
              <span class="conn-expand-note">{{ data.note }}</span>
            </div>
            <div v-if="data.headers?.length" class="conn-expand-section">
              <span class="conn-expand-label">Headers</span>
              <table class="conn-mini-table">
                <tr v-for="h in data.headers" :key="h.name">
                  <td class="conn-key">{{ h.name }}</td>
                  <td class="conn-val">{{ h.isAuthorization ? '••••••' : h.value }}</td>
                </tr>
              </table>
            </div>
            <div v-if="data.queryParams?.filter((q: any) => q.enabled).length" class="conn-expand-section">
              <span class="conn-expand-label">Params</span>
              <table class="conn-mini-table">
                <tr v-for="q in data.queryParams.filter((q: any) => q.enabled)" :key="q.name">
                  <td class="conn-key">{{ q.name }}</td>
                  <td class="conn-val">{{ q.value }}</td>
                </tr>
              </table>
            </div>
            <div class="conn-expand-section">
              <span class="conn-expand-label">CLI</span>
              <code class="conn-cli-code">{{ cliInvokeCommand(data) }}</code>
            </div>
          </div>
        </template>
      </DataTable>
    </div>

    <!-- ── Invoke mode: full-width table ─────────────────────────────────────── -->
    <div v-else class="api-table">
      <!-- header row -->
      <div class="api-table__header">
        <div class="col-expand"></div>
        <div class="col-method">Method</div>
        <div class="col-name">Name</div>
        <div class="col-url">URL</div>
        <div class="col-auth">Auth</div>
        <div class="col-status">Status</div>
        <div class="col-actions"></div>
      </div>

      <Skeleton v-if="loading" height="3rem" class="mb-2" v-for="i in 4" :key="i" />

      <template v-if="!loading">
        <div v-for="def in filteredDefs" :key="def.id" class="api-row-wrap">
          <!-- main row -->
          <div class="api-row" :class="{ 'api-row--expanded': expandedId === def.id }">
            <div class="col-expand">
              <Button
                text rounded size="small"
                :icon="expandedId === def.id ? 'pi pi-chevron-down' : 'pi pi-chevron-right'"
                @click="toggleExpand(def)"
              />
            </div>
            <div class="col-method">
              <Tag :value="def.method" :severity="methodSeverity(def.method)" class="method-tag" />
            </div>
            <div class="col-name">
              <span
                class="fav-star"
                :class="{ 'fav-star--inactive': !store.isFavourite(def.id) }"
                v-tooltip.top="store.isFavourite(def.id) ? 'Remove from favourites' : 'Add to favourites'"
                @click.stop="store.toggleFavourite(def.id)"
              >⭐</span>
              <span class="def-name-text">{{ def.name }}</span>
            </div>
            <div class="col-url">
              <span class="url-text" :title="`${def.baseUrl}${def.path}`">{{ def.baseUrl }}{{ def.path }}</span>
            </div>
            <div class="col-auth">
              <Tag :value="def.authenticationMode" :severity="AUTH_SEVERITY[def.authenticationMode] ?? 'secondary'" class="auth-tag" />
            </div>
            <div class="col-status">
              <Tag
                v-if="def.lastStatusCode"
                :value="`${def.lastStatusCode}`"
                :severity="statusSeverity(def.lastStatusCode)"
              />
            </div>
            <div class="col-actions">
              <Button
                :icon="copiedCliId === def.id ? 'pi pi-check' : 'pi pi-terminal'"
                text rounded size="small"
                v-tooltip.top="'Copy CLI command'"
                @click="copyCliCommand(def)"
              />
              <Button
                label="Invoke"
                icon="pi pi-play"
                size="small"
                :loading="invoking && invokeTarget?.id === def.id"
                @click="invokeAndExpand(def)"
              />
            </div>
          </div>

          <!-- expanded detail panel -->
          <div v-if="expandedId === def.id" class="api-detail-panel">
            <div class="api-detail-panel__header">
              <span class="api-detail-panel__title">{{ def.name }}</span>
              <div class="api-detail-panel__actions">
                <Button label="Invoke" icon="pi pi-play" size="small" :loading="invoking" @click="doInvoke" />
                <Button label="Bookmark" icon="pi pi-bookmark" severity="secondary" outlined size="small" @click="() => doBookmark()" />
                <Button label="Compare" icon="pi pi-sync" severity="info" outlined size="small" :loading="comparing" @click="() => doCompare()" />
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
              <TabPanels v-model:value="activeTab">
              <!-- Overview -->
              <TabPanel value="overview">
                <div class="overview-grid">
                  <div class="ov-row"><span class="ov-label">URL</span><code>{{ def.baseUrl }}{{ def.path }}</code></div>
                  <div class="ov-row"><span class="ov-label">Method</span><Tag :value="def.method" severity="info" /></div>
                  <div class="ov-row"><span class="ov-label">Auth</span><Tag :value="def.authenticationMode" :severity="AUTH_SEVERITY[def.authenticationMode]" /></div>
                  <div v-if="def.note" class="ov-row"><span class="ov-label">Note</span><span>{{ def.note }}</span></div>
                  <div v-if="def.lastInvokedAt" class="ov-row"><span class="ov-label">Last invoked</span><span>{{ new Date(def.lastInvokedAt).toLocaleString() }}</span></div>
                </div>

                <div v-if="def.headers.length" class="section-label">Request Headers</div>
                <DataTable v-if="def.headers.length" :value="def.headers" size="small" class="mt-1">
                  <Column field="name" header="Name" />
                  <Column field="value" header="Value" />
                </DataTable>

                <div v-if="def.queryParams.filter(q => q.enabled).length" class="section-label mt-2">Query Params</div>
                <DataTable v-if="def.queryParams.filter(q => q.enabled).length" :value="def.queryParams.filter(q => q.enabled)" size="small">
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
                    <Tag v-if="snap.isGolden || def.goldenSnapshotId === snap.id" value="Golden" severity="warn" />
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
                  v-model:expandedRows="expandedHistoryRows"
                  size="small"
                  :paginator="history.length > 20"
                  :rows="20"
                  class="history-table"
                >
                  <Column expander style="width: 2.5rem" />
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
                  <template #expansion="{ data }">
                    <div class="history-expansion">
                      <Tabs value="request" class="history-detail-tabs">
                        <TabList>
                          <Tab value="request">Request</Tab>
                          <Tab value="response-headers">Response Headers</Tab>
                          <Tab value="response-body">Response Body</Tab>
                        </TabList>
                        <TabPanels>
                          <TabPanel value="request">
                            <div class="overview-grid">
                              <div class="ov-row"><span class="ov-label">URL</span><code>{{ data.requestBaseUrl }}{{ data.requestPath }}</code></div>
                              <div class="ov-row"><span class="ov-label">Method</span><Tag :value="data.requestMethod || '—'" :severity="methodSeverity(data.requestMethod || '')" /></div>
                            </div>
                            <div class="section-label mt-2">Request Headers</div>
                            <DataTable :value="Object.entries(data.requestHeaders ?? {}).map(([name, value]) => ({ name, value }))" size="small">
                              <Column field="name" header="Name" />
                              <Column field="value" header="Value" />
                            </DataTable>
                            <div class="section-label mt-2">Query Strings</div>
                            <DataTable :value="Object.entries(data.requestQueryParams ?? {}).map(([name, value]) => ({ name, value }))" size="small">
                              <Column field="name" header="Name" />
                              <Column field="value" header="Value" />
                            </DataTable>
                          </TabPanel>
                          <TabPanel value="response-headers">
                            <div class="response-meta">
                              <Tag :value="`${data.statusCode || '—'}`" :severity="statusSeverity(data.statusCode)" />
                              <span class="latency">{{ data.latencyMs }} ms</span>
                              <span v-if="data.contentType" class="content-type">{{ data.contentType }}</span>
                            </div>
                            <DataTable
                              :value="Object.entries(data.responseHeaders ?? {}).map(([name, value]) => ({ name, value }))"
                              size="small"
                              class="mt-2"
                            >
                              <Column field="name" header="Name" />
                              <Column field="value" header="Value" />
                            </DataTable>
                          </TabPanel>
                          <TabPanel value="response-body">
                            <JsonViewer v-if="data.body" :data="data.body" />
                            <span v-else class="text-muted">No response body captured.</span>
                          </TabPanel>
                        </TabPanels>
                      </Tabs>
                    </div>
                  </template>
                </DataTable>
              </TabPanel>
              </TabPanels>
            </Tabs>
          </div>
        </div>
      </template>

      <div v-if="!loading && filteredDefs.length === 0" class="empty-state">
        <i class="pi pi-send empty-icon" />
        <p>No HTTP API definitions found.</p>
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
            <Select
              v-model="form.authenticationMode"
              :options="AUTH_MODES"
              optionLabel="label"
              optionValue="value"
              class="w-full"
              @update:modelValue="onAuthModeChanged"
            />
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
              <InputText v-model="form.apiKeyOptions!.headerName" placeholder="X-Api-Key" class="w-full" />
            </div>
            <div>
              <label>Prefix (optional)</label>
              <InputText v-model="form.apiKeyOptions!.prefix" placeholder="Bearer " class="w-full" />
            </div>
          </div>
          <div class="form-row">
            <label>API Key *</label>
            <Password v-model="form.apiKeyOptions!.apiKey" :feedback="false" toggleMask class="w-full" />
          </div>
        </template>

        <!-- Bearer options -->
        <template v-if="form.authenticationMode === 'Bearer'">
          <div class="form-section-label">Bearer Token</div>
          <div class="form-row">
            <label>Token *</label>
            <Password v-model="form.bearerOptions!.token" :feedback="false" toggleMask class="w-full" />
          </div>
        </template>

        <!-- Azure Client Credentials -->
        <template v-if="form.authenticationMode === 'AzureClientCredentials'">
          <div class="form-section-label">Azure Client Credentials</div>
          <div class="full-width">
            <AzureContextBanner
              :subscriptionId="selectedSubscriptionId"
              @account-loaded="onAzureAccountLoaded"
              @subscription-changed="onSubscriptionChanged"
            />
          </div>
          <div class="form-row-two">
            <div>
              <label>Tenant ID</label>
              <InputText v-model="form.azureCredentials!.tenantId" class="w-full" />
            </div>
            <div>
              <label>Client ID</label>
              <InputText v-model="form.azureCredentials!.clientId" class="w-full" />
              <AppRegistrationPicker :clientId="form.azureCredentials!.clientId" @selected="onAppRegistrationSelected" />
            </div>
          </div>
          <div class="form-row-two">
            <div>
              <label>Client Secret</label>
              <Password
                v-if="!form.azureCredentials!.keyVaultSecretRef"
                v-model="form.azureCredentials!.clientSecret"
                :feedback="false"
                toggleMask
                class="w-full"
              />
              <KeyVaultSecretPicker
                v-model="form.azureCredentials!.keyVaultSecretRef"
                :subscriptionId="selectedSubscriptionId"
              />
            </div>
            <div>
              <label>Scope</label>
              <InputText v-model="form.azureCredentials!.scope" placeholder="https://..." class="w-full" />
            </div>
          </div>
          <div class="form-row">
            <label>Authority Host <span class="text-muted">(optional)</span></label>
            <InputText v-model="form.azureCredentials!.authorityHost" placeholder="https://login.microsoftonline.com" class="w-full" />
          </div>
        </template>

        <!-- Custom Headers section -->
        <div class="form-section-label">
          Request Headers
          <Button icon="pi pi-plus" text rounded size="small" @click="addHeader" />
        </div>
        <div v-for="(h, i) in formHeaders" :key="i">
          <div v-if="isAuthorizationHeader(h)" class="form-row-header">
            <InputText v-model="h.name" placeholder="Header" class="w-full" @update:modelValue="normalizeAuthorizationHeaders([h])" />
            <Select
              v-model="h.authorizationType"
              :options="authorizationTypeOptions"
              optionLabel="label"
              optionValue="value"
              style="width: 8rem"
            />
            <InputText v-model="h.value" :placeholder="headerValuePlaceholder(h)" class="w-full" />
            <Button icon="pi pi-times" text rounded size="small" severity="danger" @click="removeHeader(i)" />
          </div>
          <div v-else class="form-row-header--plain">
            <InputText v-model="h.name" placeholder="Header" class="w-full" @update:modelValue="normalizeAuthorizationHeaders([h])" />
            <InputText v-model="h.value" :placeholder="headerValuePlaceholder(h)" class="w-full" />
            <Button icon="pi pi-times" text rounded size="small" severity="danger" @click="removeHeader(i)" />
          </div>
        </div>

        <!-- Query params -->
        <div class="form-section-label">
          Query Params
          <Button icon="pi pi-plus" text rounded size="small" @click="addQueryParam" />
        </div>
        <div v-for="(q, i) in form.queryParams" :key="i" class="form-row-query">
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

    <Dialog
      v-model:visible="showInvokeInputDialog"
      header="Invoke HTTP API"
      modal
      :style="{ width: '520px' }"
    >
      <div class="form-grid">
        <div class="form-row">
          <label>
            This API requires runtime inputs from placeholders (for example:
            <code>{{ '{upn}' }}</code> or <code>{{ '{upn:user@contoso.com}' }}</code>).
          </label>
        </div>
        <div v-for="field in invokeInputFields" :key="field.name" class="form-row">
          <label>{{ field.name }}</label>
          <InputText v-model="field.value" :placeholder="field.defaultValue || 'Required if no default'" class="w-full" />
          <small v-if="field.defaultValue" class="text-muted">Default: {{ field.defaultValue }}</small>
        </div>
      </div>
      <template #footer>
        <Button label="Cancel" severity="secondary" outlined @click="showInvokeInputDialog = false" />
        <Button label="Invoke" icon="pi pi-play" :loading="invoking" @click="confirmInvokeWithInputs" />
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

.api-table { flex: 1; min-height: 0; overflow-y: auto; border: 1px solid var(--surface-border); border-radius: 8px; background: var(--surface-card); }
.api-table__header { display: grid; grid-template-columns: 2.5rem 84px minmax(0, 1.5fr) minmax(0, 2.5fr) 168px 80px 104px; align-items: center; gap: 0.5rem; padding: 0.35rem 0.75rem; font-size: 0.72rem; font-weight: 600; color: var(--text-color-secondary); text-transform: uppercase; letter-spacing: 0.04em; border-bottom: 1px solid var(--surface-border); position: sticky; top: 0; background: var(--surface-card); z-index: 1; }
.api-row-wrap { border-bottom: 1px solid var(--surface-border); }
.api-row-wrap:last-child { border-bottom: none; }
.api-row { display: grid; grid-template-columns: 2.5rem 84px minmax(0, 1.5fr) minmax(0, 2.5fr) 168px 80px 104px; align-items: center; gap: 0.5rem; padding: 0.4rem 0.75rem; transition: background 0.12s; }
.api-row:hover { background: var(--surface-hover); }
.api-row--expanded { background: var(--surface-hover); }
.col-expand { display: flex; align-items: center; justify-content: center; }
.col-method, .col-auth, .col-status, .col-actions { display: flex; align-items: center; }
.col-actions { justify-content: flex-end; }
.col-name { display: flex; align-items: center; gap: 0.3rem; min-width: 0; font-weight: 600; font-size: 0.9rem; overflow: hidden; }
.col-url { min-width: 0; display: flex; align-items: center; }
.url-text { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-size: 0.75rem; color: var(--text-color-secondary); }
.def-name-text { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.fav-star { font-size: 0.85rem; flex-shrink: 0; cursor: pointer; transition: opacity 0.15s; }
.fav-star--inactive { opacity: 0.2; }
.fav-star:hover { opacity: 1; }
.method-tag, .auth-tag { font-size: 0.7rem !important; }

.table-name-cell { display: flex; align-items: center; gap: 0.35rem; }
.row-actions { display: flex; justify-content: flex-end; gap: 0.2rem; }

/* ── Connections table: URL, group pill, expand panel ──────────────────── */
.conn-url {
  display: block; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
  max-width: 100%;
  font-size: 0.75rem; color: var(--text-color-secondary); font-family: monospace;
}
.conn-group-pill {
  display: inline-block; padding: 0.15rem 0.55rem; border-radius: 999px;
  font-size: 0.72rem; font-weight: 600; white-space: nowrap;
}
.conn-expand-panel {
  display: flex; flex-direction: column; gap: 0.45rem;
  padding: 0.6rem 1rem; background: var(--surface-ground); font-size: 0.82rem;
}
.conn-expand-section { display: flex; align-items: flex-start; gap: 0.75rem; }
.conn-expand-label {
  min-width: 80px; flex-shrink: 0; font-size: 0.7rem; font-weight: 700;
  text-transform: uppercase; letter-spacing: 0.04em;
  color: var(--text-color-secondary); padding-top: 0.15rem;
}
.conn-expand-mono { font-family: monospace; font-size: 0.78rem; word-break: break-all; }
.conn-expand-note { color: var(--text-color-secondary); font-style: italic; }
.conn-mini-table { border-collapse: collapse; font-size: 0.78rem; }
.conn-key { font-family: monospace; color: var(--text-color-secondary); padding-right: 0.75rem; white-space: nowrap; vertical-align: top; }
.conn-val { font-family: monospace; word-break: break-all; vertical-align: top; }
.conn-cli-code {
  font-family: monospace; font-size: 0.78rem;
  background: var(--surface-section, var(--surface-b, var(--surface-ground)));
  padding: 0.2rem 0.5rem; border-radius: 4px;
  color: var(--primary-color); border: 1px solid var(--surface-border);
}

.empty-state { text-align: center; padding: 2rem; color: var(--text-color-secondary); }
.empty-icon { font-size: 2rem; margin-bottom: 0.5rem; display: block; }

.api-detail-panel { border-top: 1px solid var(--surface-border); padding: 0.75rem 1rem; background: var(--surface-ground); }
.api-detail-panel__header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 0.75rem; flex-wrap: wrap; gap: 0.5rem; }
.api-detail-panel__title { font-weight: 700; font-size: 1rem; }
.api-detail-panel__actions { display: flex; gap: 0.4rem; }
.invoke-tabs { }

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
.history-expansion {
  border-top: 1px solid var(--surface-border);
  padding-top: 0.5rem;
}

/* Form dialog */
.form-grid { display: flex; flex-direction: column; gap: 0.6rem; }
.form-row { display: flex; flex-direction: column; gap: 0.2rem; }
.form-row label { font-size: 0.8rem; font-weight: 600; color: var(--text-color-secondary); }
.form-row-two { display: grid; grid-template-columns: 1fr 1fr; gap: 0.5rem; }
.form-row-two label { font-size: 0.8rem; font-weight: 600; color: var(--text-color-secondary); }
.form-row-header { display: grid; grid-template-columns: minmax(0, 1fr) auto minmax(0, 1fr) auto; gap: 0.4rem; align-items: center; }
.form-row-header--plain { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 1fr) auto; gap: 0.4rem; align-items: center; }
.form-row-query { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 1fr) auto; gap: 0.4rem; align-items: center; }
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
