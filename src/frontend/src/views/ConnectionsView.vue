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
import { useConnectionsStore } from '@/stores/connections'
import { connectionsApi } from '@/api/connections'
import { apiClient } from '@/api/client'
import type { ConnectionDefinition, ConnectionGroup, AzureAccountInfo, AzureAppRegistration } from '@/api/types'
import AzureContextBanner from '@/components/connections/AzureContextBanner.vue'
import AppRegistrationPicker from '@/components/connections/AppRegistrationPicker.vue'
import KeyVaultSecretPicker from '@/components/connections/KeyVaultSecretPicker.vue'

const toast = useToast()
const confirm = useConfirm()
const store = useConnectionsStore()

const loading = ref(false)
const groups = ref<ConnectionGroup[]>([])
const selectedGroup = ref<string | null>(null)
const searchQuery = ref('')
const showDialog = ref(false)
const editMode = ref(false)
const saving = ref(false)

// Group management state
const groupDialog = ref(false)
const groupEditMode = ref(false)
const groupForm = ref<ConnectionGroup>({ name: '', color: '#6366f1', description: '' })
const groupFormOriginalName = ref('')

// ── Export dialog ────────────────────────────────────────────────────────────
const exportDialogVisible   = ref(false)
const exportFilter          = ref('')
const exportSelected        = ref<Set<string>>(new Set())
const exportPassword        = ref('')
const exportPasswordConfirm = ref('')
const exportPasswordCopied    = ref(false)
const exporting             = ref(false)

const exportableConnections = computed(() => {
  const q = exportFilter.value.toLowerCase()
  return store.savedConnections.filter(c =>
    !q || c.name.toLowerCase().includes(q) || c.endpoint?.toLowerCase().includes(q)
  )
})
const exportAllChecked = computed(() =>
  exportableConnections.value.length > 0 &&
  exportableConnections.value.every(c => exportSelected.value.has(c.name))
)

function generateExportPassword() {
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

function openExportDialog() {
  exportFilter.value = ''
  exportSelected.value = new Set(store.savedConnections.map(c => c.name))
  exportPassword.value = ''
  exportPasswordConfirm.value = ''
  exportPasswordCopied.value = false
  exportDialogVisible.value = true
}
function toggleExportItem(name: string) {
  const s = new Set(exportSelected.value)
  s.has(name) ? s.delete(name) : s.add(name)
  exportSelected.value = s
}
function toggleExportAll() {
  if (exportAllChecked.value) {
    exportSelected.value = new Set()
  } else {
    exportSelected.value = new Set(exportableConnections.value.map(c => c.name))
  }
}

async function doExport() {
  if (exportSelected.value.size === 0) {
    toast.add({ severity: 'warn', summary: 'Select connections', detail: 'Choose at least one connection to export.', life: 3000 }); return
  }
  if (!exportPassword.value) {
    toast.add({ severity: 'warn', summary: 'Password required', detail: 'Enter a password to protect the export file.', life: 3000 }); return
  }
  if (exportPassword.value !== exportPasswordConfirm.value) {
    toast.add({ severity: 'warn', summary: 'Passwords do not match', detail: 'Re-enter the same password in both fields.', life: 3000 }); return
  }
  exporting.value = true
  try {
    const res = await apiClient.post('/connections/export',
      { names: [...exportSelected.value], password: exportPassword.value },
      { responseType: 'blob' }
    )
    const url = URL.createObjectURL(new Blob([res.data], { type: 'application/json' }))
    const a = document.createElement('a'); a.href = url; a.download = 'connections-export.json'; a.click(); URL.revokeObjectURL(url)
    exportDialogVisible.value = false
    toast.add({ severity: 'success', summary: 'Exported', detail: `${exportSelected.value.size} connection(s) exported.`, life: 3000 })
  } catch (e: any) {
    toast.add({ severity: 'error', summary: 'Export failed', detail: e.message, life: 5000 })
  } finally { exporting.value = false }
}

// ── Import dialog ────────────────────────────────────────────────────────────
const importDialogVisible = ref(false)
const importFile          = ref<File | null>(null)
const importPassword      = ref('')
const importing           = ref(false)
const importDropHover     = ref(false)
const importFileInput     = ref<HTMLInputElement>()

function openImportDialog() {
  importFile.value = null
  importPassword.value = ''
  importDialogVisible.value = true
}
function onImportFileDrop(e: DragEvent) {
  importDropHover.value = false
  const file = e.dataTransfer?.files?.[0]
  if (file?.name.endsWith('.json')) importFile.value = file
  else toast.add({ severity: 'warn', summary: 'Invalid file', detail: 'Please drop a .json export file.', life: 3000 })
}
function onImportFileInput(e: Event) {
  const file = (e.target as HTMLInputElement).files?.[0]
  if (file) importFile.value = file
}

async function doImport() {
  if (!importFile.value) {
    toast.add({ severity: 'warn', summary: 'No file', detail: 'Select an export file first.', life: 3000 }); return
  }
  if (!importPassword.value) {
    toast.add({ severity: 'warn', summary: 'Password required', detail: 'Enter the password used when exporting.', life: 3000 }); return
  }
  importing.value = true
  try {
    const text    = await importFile.value.text()
    const payload = JSON.parse(text)
    const res = await apiClient.post<{ imported: number; total: number }>(
      '/connections/import', { payload, password: importPassword.value }
    )
    await load()
    importDialogVisible.value = false
    const { imported, total } = res.data
    const skipped = total - imported
    const msg = skipped > 0
      ? `${imported} imported, ${skipped} renamed (version suffix added for name conflicts).`
      : `${imported} connection(s) imported.`
    toast.add({ severity: 'success', summary: 'Import complete', detail: msg, life: 5000 })
  } catch (e: any) {
    const detail = e.response?.data?.error ?? e.message
    toast.add({ severity: 'error', summary: 'Import failed', detail, life: 6000 })
  } finally { importing.value = false; if (importFileInput.value) importFileInput.value.value = '' }
}

const blankForm = (): Partial<ConnectionDefinition> => ({
  name: '', endpoint: '', authenticationMode: 'CustomHeaders',
  headers: [], note: '', groupName: '',
})
const form = ref<Partial<ConnectionDefinition>>(blankForm())
const originalName = ref('')

const AUTH_MODE_LABELS: Record<string, string> = {
  CustomHeaders: 'Custom Headers',
  AzureClientCredentials: 'Azure Client Credentials',
  OAuth: 'OAuth',
  // v1 integer fallbacks (should not appear after backend fix, but just in case)
  '0': 'Custom Headers',
  '1': 'Azure Client Credentials',
  '2': 'OAuth',
}

const AUTH_MODE_SEVERITY: Record<string, string> = {
  CustomHeaders: 'secondary',
  AzureClientCredentials: 'info',
  OAuth: 'success',
  // v1 integer fallbacks
  '0': 'secondary',
  '1': 'info',
  '2': 'success',
}

function authModeLabel(mode: unknown) {
  return AUTH_MODE_LABELS[String(mode)] ?? String(mode)
}
function authModeSeverity(mode: unknown): 'secondary' | 'info' | 'success' {
  return (AUTH_MODE_SEVERITY[String(mode)] ?? 'secondary') as 'secondary' | 'info' | 'success'
}

const authModeOptions = [
  { label: 'Custom Headers', value: 'CustomHeaders' },
  { label: 'Azure Client Credentials', value: 'AzureClientCredentials' },
  { label: 'OAuth', value: 'OAuth' },
]

// Options for the group dropdown in the form — existing groups + ability to clear
const groupOptions = computed(() => groups.value.map(g => ({ label: g.name, value: g.name })))
const groupColorMap = computed(() => Object.fromEntries(groups.value.map(g => [g.name, g.color])))

const connectedNames = computed(() => new Set(store.activeConnections.map(c => c.name)))
const connectedCount = computed(() => store.activeConnections.filter(c => c.isConnected).length)

const filteredConnections = computed(() => {
  let list = store.savedConnections
  if (selectedGroup.value) list = list.filter(c => c.groupName === selectedGroup.value)
  if (searchQuery.value) {
    const q = searchQuery.value.toLowerCase()
    list = list.filter(c => c.name.toLowerCase().includes(q) || c.endpoint?.toLowerCase().includes(q))
  }
  return list
})

async function load() {
  loading.value = true
  try {
    await Promise.all([store.loadSaved(), store.loadActive()])
    try {
      const res = await apiClient.get<ConnectionGroup[]>('/connections/groups')
      groups.value = res.data ?? []
    } catch { /* groups are optional */ }
  } finally { loading.value = false }
}

function openCreate() {
  editMode.value = false; originalName.value = ''
  form.value = blankForm(); showDialog.value = true
}

function openEdit(conn: ConnectionDefinition) {
  editMode.value = true; originalName.value = conn.name
  // Deep clone so edits don't mutate the store until saved
  form.value = JSON.parse(JSON.stringify(conn))
  // Ensure nested credential objects exist so v-model doesn't crash
  if (form.value.authenticationMode === 'AzureClientCredentials' && !form.value.azureCredentials) {
    form.value.azureCredentials = { tenantId: '', clientId: '', clientSecret: '', scope: '' }
  }
  if (form.value.authenticationMode === 'OAuth' && !form.value.oAuthOptions) {
    form.value.oAuthOptions = { clientId: '', redirectUri: '', scopes: '' }
  }
  showDialog.value = true
}

function onAuthModeChange() {
  // Initialise credential objects when the user switches mode so v-model bindings are safe
  if (form.value.authenticationMode === 'AzureClientCredentials' && !form.value.azureCredentials) {
    form.value.azureCredentials = { tenantId: '', clientId: '', clientSecret: '', scope: '' }
  }
  if (form.value.authenticationMode === 'OAuth' && !form.value.oAuthOptions) {
    form.value.oAuthOptions = { clientId: '', redirectUri: '', scopes: '' }
  }
}

// ── Azure Assist ─────────────────────────────────────────────────────────────

const selectedSubscriptionId = ref<string | undefined>(undefined)

function onAzureAccountLoaded(account: AzureAccountInfo | null) {
  if (!account) return
  // Auto-populate Tenant ID if blank
  if (form.value.azureCredentials && !form.value.azureCredentials.tenantId.trim()) {
    form.value.azureCredentials = { ...form.value.azureCredentials, tenantId: account.tenantId }
  }
  if (!selectedSubscriptionId.value) {
    selectedSubscriptionId.value = account.subscriptionId
  }
}

function onSubscriptionChanged(subscriptionId: string) {
  selectedSubscriptionId.value = subscriptionId
}

function onAppRegistrationSelected(app: AzureAppRegistration) {
  if (form.value.azureCredentials) {
    const scope = app.firstApiResourceId ? `api://${app.firstApiResourceId}/.default` : ''
    form.value.azureCredentials = {
      ...form.value.azureCredentials,
      clientId: app.appId,
      scope: scope || form.value.azureCredentials.scope
    }
  }
}

function onOAuthAppRegistrationSelected(app: AzureAppRegistration) {
  if (form.value.oAuthOptions) {
    form.value.oAuthOptions = { ...form.value.oAuthOptions, clientId: app.appId }
  }
}

async function save() {
  if (!form.value.name?.trim() || !form.value.endpoint?.trim()) {
    toast.add({ severity: 'warn', summary: 'Validation', detail: 'Name and endpoint are required', life: 3000 }); return
  }
  if (form.value.authenticationMode === 'AzureClientCredentials') {
    const az = form.value.azureCredentials
    const hasSecret = !!az?.clientSecret?.trim() || !!az?.keyVaultSecretRef
    if (!az?.tenantId?.trim() || !az?.clientId?.trim() || !hasSecret || !az?.scope?.trim()) {
      toast.add({ severity: 'warn', summary: 'Validation', detail: 'Tenant ID, Client ID, Client Secret (or Key Vault reference) and Scope are required for Azure Client Credentials', life: 4000 }); return
    }
  }
  if (form.value.authenticationMode === 'OAuth') {
    const oa = form.value.oAuthOptions
    if (!oa?.clientId?.trim() || !oa?.redirectUri?.trim() || !oa?.scopes?.trim()) {
      toast.add({ severity: 'warn', summary: 'Validation', detail: 'Client ID, Redirect URI and Scopes are required for OAuth', life: 4000 }); return
    }
  }
  saving.value = true
  try {
    if (editMode.value) {
      await connectionsApi.update(originalName.value, form.value)
      toast.add({ severity: 'success', summary: 'Updated', detail: `"${form.value.name}" updated`, life: 3000 })
    } else {
      await store.createConnection(form.value)
      toast.add({ severity: 'success', summary: 'Created', detail: `"${form.value.name}" created`, life: 3000 })
    }
    showDialog.value = false
    await load() // refresh groups + connections
  } catch (e: any) {
    toast.add({ severity: 'error', summary: 'Error', detail: e.message, life: 5000 })
  } finally { saving.value = false }
}

function confirmDelete(conn: ConnectionDefinition) {
  confirm.require({
    message: `Delete connection "${conn.name}"?`,
    header: 'Confirm Delete', icon: 'pi pi-trash',
    rejectProps: { label: 'Cancel', severity: 'secondary' },
    acceptProps: { label: 'Delete', severity: 'danger' },
    accept: async () => {
      try { await store.deleteConnection(conn.name); toast.add({ severity: 'success', summary: 'Deleted', life: 2000 }) }
      catch (e: any) { toast.add({ severity: 'error', summary: 'Delete failed', detail: e.message, life: 5000 }) }
    },
  })
}

async function toggleConnect(conn: ConnectionDefinition) {
  try {
    if (connectedNames.value.has(conn.name)) {
      await store.disconnect(conn.name); toast.add({ severity: 'info', summary: 'Disconnected', detail: conn.name, life: 2000 })
    } else {
      await store.connect(conn.name); toast.add({ severity: 'success', summary: 'Connected', detail: conn.name, life: 2000 })
    }
  } catch (e: any) { toast.add({ severity: 'error', summary: 'Failed', detail: e.message, life: 5000 }) }
}

function addHeader() {
  if (!form.value.headers) form.value.headers = []
  form.value.headers.push({ name: '', value: '', authorizationType: 'None', isAuthorization: false })
}
function removeHeader(idx: number) { form.value.headers?.splice(idx, 1) }

function openCreateGroup() {
  groupForm.value = { name: '', color: '#6366f1', description: '' }
  groupEditMode.value = false
  groupDialog.value = true
}

function openEditGroup(g: ConnectionGroup) {
  groupForm.value = { ...g }
  groupFormOriginalName.value = g.name
  groupEditMode.value = true
  groupDialog.value = true
}

async function saveGroup() {
  try {
    if (groupEditMode.value) {
      await apiClient.put(`/connections/groups/${encodeURIComponent(groupFormOriginalName.value)}`, groupForm.value)
    } else {
      await apiClient.post('/connections/groups', groupForm.value)
    }
    groupDialog.value = false
    await load()
    toast.add({ severity: 'success', summary: groupEditMode.value ? 'Group updated' : 'Group created', life: 2000 })
  } catch (e: any) {
    toast.add({ severity: 'error', summary: 'Save failed', detail: e.message, life: 5000 })
  }
}

function confirmDeleteGroup(g: ConnectionGroup) {
  confirm.require({
    message: `Delete group "${g.name}"? Connections in this group will become ungrouped.`,
    header: 'Delete Group',
    icon: 'pi pi-exclamation-triangle',
    rejectProps: { label: 'Cancel', severity: 'secondary', outlined: true },
    acceptProps: { label: 'Delete', severity: 'danger' },
    accept: async () => {
      try {
        await apiClient.delete(`/connections/groups/${encodeURIComponent(g.name)}`)
        if (selectedGroup.value === g.name) selectedGroup.value = null
        await load()
        toast.add({ severity: 'success', summary: 'Group deleted', life: 2000 })
      } catch (e: any) {
        toast.add({ severity: 'error', summary: 'Delete failed', detail: e.message, life: 5000 })
      }
    }
  })
}

onMounted(load)
</script>

<template>
  <div class="connections-view">
    <div class="summary-bar">
      <div class="summary-info">
        <span class="stat"><i class="pi pi-server" /> {{ store.savedConnections.length }} saved</span>
        <span class="stat success"><i class="pi pi-circle-fill dot" /> {{ connectedCount }} connected</span>
      </div>
      <div class="bar-actions">
        <Button label="Import" icon="pi pi-upload" text size="small" @click="openImportDialog" />
        <Button label="Export" icon="pi pi-download" text size="small" @click="openExportDialog" />
        <Button label="New Connection" icon="pi pi-plus" size="small" @click="openCreate" />
      </div>
    </div>

    <div class="filters-row">
      <InputText v-model="searchQuery" placeholder="Search connections…" style="width:240px" />
      <div class="group-tabs">
        <button class="group-tab" :class="{ active: !selectedGroup }" @click="selectedGroup = null">All</button>
        <span v-for="g in groups" :key="g.name" class="group-tab-wrap">
          <button class="group-tab" :class="{ active: selectedGroup === g.name }" @click="selectedGroup = g.name">
            <span class="group-color-dot" :style="{ background: g.color }" />
            {{ g.name }}
          </button>
          <button class="group-tab-edit" @click.stop="openEditGroup(g)" title="Edit group"><i class="pi pi-pencil" /></button>
          <button class="group-tab-edit danger" @click.stop="confirmDeleteGroup(g)" title="Delete group"><i class="pi pi-trash" /></button>
        </span>
        <Button icon="pi pi-plus" text size="small" @click="openCreateGroup" v-tooltip="'Add group'" class="group-add-btn" />
      </div>
    </div>

    <div class="table-wrap">
      <DataTable :value="filteredConnections" :loading="loading" stripedRows rowHover scrollable scrollHeight="flex">
        <template #empty>
          <div class="empty-state">
            <i class="pi pi-server" />
            <p>No connections yet.</p>
            <Button label="New Connection" icon="pi pi-plus" @click="openCreate" />
          </div>
        </template>
        <template #loading>
          <div class="p-4"><Skeleton v-for="i in 4" :key="i" height="40px" class="mb-2" /></div>
        </template>
        <Column field="name" header="Name" sortable style="min-width:180px">
          <template #body="{ data }"><span class="conn-name">{{ data.name }}</span></template>
        </Column>
        <Column field="endpoint" header="Endpoint" sortable style="min-width:220px">
          <template #body="{ data }"><span class="mono">{{ data.endpoint }}</span></template>
        </Column>
        <Column field="authenticationMode" header="Auth" style="min-width:180px">
          <template #body="{ data }">
            <Tag :value="authModeLabel(data.authenticationMode)" :severity="authModeSeverity(data.authenticationMode)" />
          </template>
        </Column>
        <Column field="groupName" header="Group" sortable style="min-width:120px">
          <template #body="{ data }">
            <span v-if="data.groupName" class="group-badge" :style="groupColorMap[data.groupName] ? { background: groupColorMap[data.groupName] + '22', color: groupColorMap[data.groupName], borderColor: groupColorMap[data.groupName] + '66' } : {}">{{ data.groupName }}</span>
            <span v-else class="muted">—</span>
          </template>
        </Column>
        <Column header="Status" style="min-width:120px">
          <template #body="{ data }">
            <Tag :value="connectedNames.has(data.name) ? 'Connected' : 'Disconnected'"
                 :severity="connectedNames.has(data.name) ? 'success' : 'secondary'" />
          </template>
        </Column>
        <Column header="Actions" style="min-width:200px;text-align:right">
          <template #body="{ data }">
            <div class="row-actions">
              <Button :label="connectedNames.has(data.name) ? 'Disconnect' : 'Connect'"
                      :icon="connectedNames.has(data.name) ? 'pi pi-times' : 'pi pi-link'"
                      :severity="connectedNames.has(data.name) ? 'secondary' : 'success'"
                      size="small" text @click="toggleConnect(data)" />
              <Button icon="pi pi-pencil" text size="small" v-tooltip="'Edit'" @click="openEdit(data)" />
              <Button icon="pi pi-trash" text size="small" severity="danger" v-tooltip="'Delete'" @click="confirmDelete(data)" />
            </div>
          </template>
        </Column>
      </DataTable>
    </div>

    <Dialog v-model:visible="showDialog" :header="editMode ? 'Edit Connection' : 'New Connection'" modal :style="{ width: '700px' }">
      <div class="form-grid">
        <!-- Row 1: Name + Endpoint -->
        <div class="form-field">
          <label>Name *</label>
          <InputText v-model="form.name" placeholder="my-mcp-server" class="w-full" />
        </div>
        <div class="form-field">
          <label>Endpoint *</label>
          <InputText v-model="form.endpoint" placeholder="http://localhost:3000" class="w-full" />
        </div>

        <!-- Row 2: Auth Mode + Group -->
        <div class="form-field">
          <label>Auth Mode</label>
          <Select v-model="form.authenticationMode" :options="authModeOptions"
                  optionLabel="label" optionValue="value" class="w-full"
                  @change="onAuthModeChange" />
        </div>
        <div class="form-field">
          <label>Group</label>
          <!-- Show a Select if groups exist, letting the user pick or clear; otherwise plain text -->
          <Select v-if="groups.length"
                  v-model="form.groupName"
                  :options="[{ label: '— None —', value: '' }, ...groupOptions]"
                  optionLabel="label" optionValue="value"
                  editable class="w-full"
                  placeholder="Select or type a group" />
          <InputText v-else v-model="form.groupName" placeholder="Optional group" class="w-full" />
        </div>

        <!-- Note full-width -->
        <div class="form-field full-width">
          <label>Note</label>
          <Textarea v-model="form.note" rows="2" class="w-full" autoResize />
        </div>

        <!-- Azure Client Credentials section -->
        <template v-if="form.authenticationMode === 'AzureClientCredentials'">
          <div class="section-divider full-width">Azure Client Credentials</div>
          <!-- Azure Assist Banner (Design 2) -->
          <div class="full-width">
            <AzureContextBanner @account-loaded="onAzureAccountLoaded" @subscription-changed="onSubscriptionChanged" />
          </div>
          <div class="form-field">
            <div class="field-label-row">
              <label>Tenant ID</label>
              <span class="auto-populated-hint" v-if="form.azureCredentials!.tenantId">Auto-detected</span>
            </div>
            <InputText v-model="form.azureCredentials!.tenantId" class="w-full" />
          </div>
          <div class="form-field">
            <label>Client ID</label>
            <InputText v-model="form.azureCredentials!.clientId" class="w-full" />
            <AppRegistrationPicker @selected="onAppRegistrationSelected" />
          </div>
          <div class="form-field">
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
          <div class="form-field">
            <div class="field-label-row">
              <label>Scope</label>
              <span class="auto-populated-hint" v-if="form.azureCredentials!.scope">Auto-filled from App Reg</span>
            </div>
            <InputText v-model="form.azureCredentials!.scope" class="w-full" />
          </div>
          <div class="form-field full-width">
            <label>Authority Host <span class="optional">(optional)</span></label>
            <InputText v-model="form.azureCredentials!.authorityHost" placeholder="https://login.microsoftonline.com" class="w-full" />
          </div>
        </template>

        <!-- OAuth section -->
        <template v-if="form.authenticationMode === 'OAuth'">
          <div class="section-divider full-width">OAuth Settings</div>
          <!-- Azure Assist Banner for OAuth too -->
          <div class="full-width">
            <AzureContextBanner @account-loaded="() => {}" @subscription-changed="onSubscriptionChanged" />
          </div>
          <div class="form-field">
            <label>Client ID</label>
            <InputText v-model="form.oAuthOptions!.clientId" class="w-full" />
            <AppRegistrationPicker @selected="onOAuthAppRegistrationSelected" />
          </div>
          <div class="form-field">
            <label>Client Secret <span class="optional">(optional)</span></label>
            <Password
              v-if="!form.oAuthOptions!.keyVaultSecretRef"
              v-model="form.oAuthOptions!.clientSecret!"
              :feedback="false"
              toggleMask
              class="w-full"
            />
            <KeyVaultSecretPicker
              v-model="form.oAuthOptions!.keyVaultSecretRef"
              :subscriptionId="selectedSubscriptionId"
            />
          </div>
          <div class="form-field">
            <label>Redirect URI</label>
            <InputText v-model="form.oAuthOptions!.redirectUri" class="w-full" />
          </div>
          <div class="form-field">
            <label>Scopes</label>
            <InputText v-model="form.oAuthOptions!.scopes" placeholder="openid profile" class="w-full" />
          </div>
        </template>

        <!-- Headers section -->
        <div class="form-section full-width">
          <div class="section-header">
            <span class="section-label">Headers</span>
            <Button label="Add Header" icon="pi pi-plus" text size="small" @click="addHeader" />
          </div>
          <p v-if="!form.headers?.length" class="muted-sm">No custom headers.</p>
          <div v-for="(h, idx) in form.headers" :key="idx" class="header-row">
            <InputText v-model="h.name" placeholder="Header name" style="flex:1" />
            <InputText v-model="h.value" placeholder="Value" style="flex:2" />
            <Button icon="pi pi-times" text size="small" severity="danger" @click="removeHeader(idx)" />
          </div>
        </div>
      </div>

      <template #footer>
        <Button label="Cancel" severity="secondary" text @click="showDialog = false" />
        <Button :label="editMode ? 'Save Changes' : 'Create'" icon="pi pi-check" :loading="saving" @click="save" />
      </template>
    </Dialog>

    <!-- Group management dialog -->
    <Dialog v-model:visible="groupDialog" :header="groupEditMode ? 'Edit Group' : 'New Group'" modal style="width:420px">
      <div class="form-grid" style="gap:12px">
        <div class="form-field">
          <label>Name <span class="req">*</span></label>
          <InputText v-model="groupForm.name" class="w-full" placeholder="e.g. Production" />
          <small v-if="groupEditMode" class="field-hint">Renaming a group will update all connections in it.</small>
        </div>
        <div class="form-field">
          <label>Colour</label>
          <div style="display:flex;align-items:center;gap:8px">
            <input type="color" v-model="groupForm.color" style="width:40px;height:32px;border:none;background:none;cursor:pointer;padding:0" />
            <InputText v-model="groupForm.color" class="w-full" placeholder="#6366f1" />
          </div>
        </div>
        <div class="form-field">
          <label>Description</label>
          <InputText v-model="groupForm.description" class="w-full" placeholder="Optional description" />
        </div>
      </div>
      <template #footer>
        <Button label="Cancel" severity="secondary" outlined @click="groupDialog = false" />
        <Button :label="groupEditMode ? 'Save' : 'Create'" icon="pi pi-check" :disabled="!groupForm.name.trim()" @click="saveGroup" />
      </template>
    </Dialog>

    <!-- ── Export Dialog ───────────────────────────────────────────────────── -->
    <Dialog v-model:visible="exportDialogVisible" header="Export Connections" modal style="width:520px">
      <div class="export-dialog">
        <p class="dialog-hint">Select the connections to export and set a password to encrypt the file.</p>

        <div class="dialog-section">
          <div class="dialog-section-header">
            <span class="section-label">Connections</span>
            <button class="select-all-btn" @click="toggleExportAll">
              {{ exportAllChecked ? 'Deselect all' : 'Select all' }}
            </button>
          </div>
          <InputText v-model="exportFilter" placeholder="Filter…" class="w-full mb-2" />
          <div class="export-list">
            <div v-if="exportableConnections.length === 0" class="muted-sm">No connections match the filter.</div>
            <label v-for="c in exportableConnections" :key="c.name" class="export-item">
              <Checkbox :modelValue="exportSelected.has(c.name)" binary @change="toggleExportItem(c.name)" />
              <div class="export-item-info">
                <span class="conn-name">{{ c.name }}</span>
                <span class="mono muted-sm">{{ c.endpoint }}</span>
              </div>
            </label>
          </div>
          <p class="selected-count">{{ exportSelected.size }} of {{ store.savedConnections.length }} selected</p>
        </div>

        <div class="dialog-section">
          <div class="section-label mb-2">Password</div>
          <div class="form-field mb-2">
            <div class="password-field-row">
              <label>Password *</label>
              <div class="password-actions">
                <Button
                  label="Generate"
                  icon="pi pi-bolt"
                  size="small"
                  severity="secondary"
                  text
                  @click="generateExportPassword"
                />
                <Button
                  :icon="exportPasswordCopied ? 'pi pi-check' : 'pi pi-copy'"
                  :label="exportPasswordCopied ? 'Copied!' : 'Copy'"
                  size="small"
                  severity="secondary"
                  text
                  :disabled="!exportPassword"
                  @click="copyExportPassword"
                />
              </div>
            </div>
            <Password v-model="exportPassword" placeholder="Enter or generate a password" :feedback="true" toggleMask class="w-full" inputClass="w-full" />
          </div>
          <div class="form-field">
            <label>Confirm Password *</label>
            <Password v-model="exportPasswordConfirm" placeholder="Repeat password" :feedback="false" toggleMask class="w-full" inputClass="w-full" />
            <small v-if="exportPasswordConfirm && exportPassword !== exportPasswordConfirm" class="pw-mismatch">Passwords do not match</small>
          </div>
        </div>
      </div>
      <template #footer>
        <Button label="Cancel" severity="secondary" text @click="exportDialogVisible = false" />
        <Button
          label="Export & Download"
          icon="pi pi-download"
          :loading="exporting"
          :disabled="exportSelected.size === 0 || !exportPassword || exportPassword !== exportPasswordConfirm"
          @click="doExport"
        />
      </template>
    </Dialog>

    <!-- ── Import Dialog ───────────────────────────────────────────────────── -->
    <Dialog v-model:visible="importDialogVisible" header="Import Connections" modal style="width:460px">
      <div class="import-dialog">
        <p class="dialog-hint">Upload an encrypted export file and enter the password used when it was created.</p>

        <div class="dialog-section">
          <div class="section-label mb-2">Export File</div>
          <div
            class="drop-zone"
            :class="{ hover: importDropHover, 'has-file': !!importFile }"
            @dragover.prevent="importDropHover = true"
            @dragleave="importDropHover = false"
            @drop.prevent="onImportFileDrop"
            @click="importFileInput?.click()"
          >
            <i :class="importFile ? 'pi pi-file-check' : 'pi pi-upload'" class="drop-icon" />
            <span v-if="importFile">{{ importFile.name }}</span>
            <span v-else>Drop a .json file here or click to browse</span>
          </div>
          <input ref="importFileInput" type="file" accept=".json" hidden @change="onImportFileInput" />
        </div>

        <div class="dialog-section">
          <div class="form-field">
            <label>Password *</label>
            <Password v-model="importPassword" placeholder="Enter export password" :feedback="false" toggleMask class="w-full" inputClass="w-full" />
          </div>
        </div>
      </div>
      <template #footer>
        <Button label="Cancel" severity="secondary" text @click="importDialogVisible = false" />
        <Button
          label="Import"
          icon="pi pi-upload"
          :loading="importing"
          :disabled="!importFile || !importPassword"
          @click="doImport"
        />
      </template>
    </Dialog>

    <ConfirmDialog />
  </div>
</template>

<style scoped>
.connections-view { display:flex; flex-direction:column; height:100%; background:var(--bg-base); color:var(--text-primary); }

/* ── Summary bar ── */
.summary-bar { display:flex; align-items:center; justify-content:space-between; padding:12px 20px; background:var(--bg-surface); border-bottom:1px solid var(--border); flex-shrink:0; }
.summary-info { display:flex; gap:20px; }
.stat { display:flex; align-items:center; gap:6px; font-size:13px; color:var(--text-secondary); }
.stat.success { color:var(--success); }
.dot { font-size:8px; }
.bar-actions { display:flex; gap:8px; }

/* ── Filters ── */
.filters-row { display:flex; align-items:center; gap:16px; padding:10px 20px; border-bottom:1px solid var(--border); background:var(--bg-surface); flex-shrink:0; flex-wrap:wrap; }
.group-tabs { display:flex; gap:4px; flex-wrap:wrap; }
.group-tab { background:none; border:none; border-bottom:2px solid transparent; color:var(--text-secondary); cursor:pointer; font-size:13px; padding:4px 12px; border-radius:var(--border-radius-sm); transition:var(--transition-fast); }
.group-tab:hover { color:var(--text-primary); background:var(--bg-raised); }
.group-tab.active { color:var(--accent); border-bottom-color:var(--accent); font-weight:500; }
.group-tab-wrap { display:inline-flex; align-items:center; gap:1px; }
.group-color-dot { display:inline-block; width:8px; height:8px; border-radius:50%; margin-right:4px; flex-shrink:0; }
.group-tab-edit { background:none; border:none; cursor:pointer; color:var(--text-muted); padding:2px 3px; border-radius:3px; font-size:10px; opacity:0; transition:opacity .15s; }
.group-tab-wrap:hover .group-tab-edit { opacity:1; }
.group-tab-edit:hover { background:var(--bg-raised); }
.group-tab-edit.danger:hover { color:var(--danger); }
.group-add-btn { margin-left:4px; }

/* ── Table ── */
.table-wrap { flex:1; overflow:hidden; display:flex; flex-direction:column; }
.conn-name { font-weight:500; }
.mono { font-family:var(--font-family-mono); font-size:12px; color:var(--text-secondary); }
.group-badge { background:var(--accent-muted); color:var(--accent); border: 1px solid transparent; border-radius:999px; padding:2px 8px; font-size:11px; font-weight:500; }
.muted { color:var(--text-muted); }
.muted-sm { color:var(--text-muted); font-size:12px; margin:4px 0; }
.row-actions { display:flex; gap:4px; justify-content:flex-end; }
.empty-state { display:flex; flex-direction:column; align-items:center; gap:12px; padding:48px 24px; color:var(--text-muted); }
.empty-state i { font-size:40px; }

/* ── Dialog form ── */
.form-grid { display:grid; grid-template-columns:1fr 1fr; gap:16px; }
.form-field { display:flex; flex-direction:column; gap:6px; }
.form-field label { font-size:12px; font-weight:500; color:var(--text-secondary); text-transform:uppercase; letter-spacing:.04em; }
.optional { text-transform:none; font-weight:400; color:var(--text-muted); }
.field-hint { color:var(--text-muted); font-size:11px; margin-top:4px; display:block; }
.full-width { grid-column:1/-1; }
.field-label-row { display:flex; align-items:center; justify-content:space-between; }
.field-label-row label { margin-bottom:0 !important; }
.auto-populated-hint { font-size:0.65rem; font-weight:600; background:#dcfce7; color:#166534; border:1px solid #bbf7d0; border-radius:10px; padding:1px 7px; text-transform:none; letter-spacing:0; }
.w-full { width:100%; }

.section-divider {
  grid-column:1/-1;
  font-size:11px; font-weight:600; color:var(--text-muted);
  text-transform:uppercase; letter-spacing:.05em;
  border-bottom:1px solid var(--border); padding-bottom:4px;
  margin-top:4px;
}
.section-label { font-size:11px; font-weight:600; color:var(--text-muted); text-transform:uppercase; letter-spacing:.05em; }
.form-section { grid-column:1/-1; }
.section-header { display:flex; align-items:center; justify-content:space-between; margin-bottom:8px; }
.header-row { display:flex; gap:8px; align-items:center; margin-bottom:6px; }

/* ── Export / Import dialogs ── */
.dialog-hint { margin:0 0 16px; font-size:13px; color:var(--text-secondary); }
.dialog-section { margin-bottom:20px; }
.dialog-section:last-child { margin-bottom:0; }
.dialog-section-header { display:flex; align-items:center; justify-content:space-between; margin-bottom:8px; }
.select-all-btn { background:none; border:none; cursor:pointer; font-size:12px; color:var(--accent); padding:2px 4px; border-radius:4px; }
.select-all-btn:hover { background:var(--nav-item-hover); }
.export-list { max-height:220px; overflow-y:auto; border:1px solid var(--border); border-radius:var(--border-radius-sm); padding:4px 0; background:var(--bg-surface); }
.export-item { display:flex; align-items:center; gap:10px; padding:8px 12px; cursor:pointer; transition:background .12s; }
.export-item:hover { background:var(--nav-item-hover); }
.export-item-info { display:flex; flex-direction:column; min-width:0; }
.selected-count { margin:6px 0 0; font-size:11px; color:var(--text-muted); text-align:right; }
.mb-2 { margin-bottom:10px; }
.pw-mismatch { color:var(--danger); font-size:11px; margin-top:4px; }
.password-field-row { display:flex; align-items:center; justify-content:space-between; margin-bottom:4px; }
.password-field-row label { margin-bottom:0; }
.password-actions { display:flex; gap:2px; }

/* Drop zone */
.drop-zone { display:flex; flex-direction:column; align-items:center; justify-content:center; gap:8px; padding:28px 20px; border:2px dashed var(--border); border-radius:var(--border-radius); cursor:pointer; transition:all .15s; color:var(--text-muted); font-size:13px; text-align:center; }
.drop-zone:hover, .drop-zone.hover { border-color:var(--accent); color:var(--accent); background:var(--nav-item-hover); }
.drop-zone.has-file { border-color:var(--success); color:var(--success); background:rgba(34,197,94,.05); }
.drop-icon { font-size:28px; }
.req { color:var(--danger); }</style>

