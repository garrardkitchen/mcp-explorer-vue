<template>
  <div class="certificates-view">
    <!-- ── Header ── -->
    <div class="page-header">
      <div>
        <h2 class="page-title"><i class="pi pi-verified title-icon" /> Certificates</h2>
        <p class="page-sub">
          Client certificates stored in the local <code>certs/</code> store · used for Azure
          client-credential auth on MCP &amp; HTTP connections. Private keys never leave this machine.
        </p>
      </div>
      <Button label="New Certificate" icon="pi pi-plus" @click="openCreate" />
    </div>

    <!-- ── Stat tiles ── -->
    <div class="stat-row">
      <div class="stat">
        <span class="v">{{ store.activeCertificates.length }}</span>
        <span class="k">Certificates</span>
      </div>
      <div class="stat s-ok">
        <span class="v">{{ store.uploadedCount }}</span>
        <span class="k">Uploaded to app registrations</span>
      </div>
      <div class="stat s-warn">
        <span class="v">{{ store.expiringSoon.length }}</span>
        <span class="k">Expiring within 30 days</span>
      </div>
      <div class="stat s-bad">
        <span class="v">{{ store.expired.length }}</span>
        <span class="k">Expired</span>
      </div>
    </div>

    <!-- ── Table ── -->
    <div class="card">
      <DataTable
        :value="store.items"
        v-model:expandedRows="expandedRows"
        dataKey="certificate.name"
        :loading="store.loading"
        responsiveLayout="scroll"
      >
        <template #empty>
          <div class="empty-state">
            <i class="pi pi-verified" />
            <p>No certificates yet. Create one here, or inline while creating a connection.</p>
          </div>
        </template>

        <Column expander style="width: 2.5rem" />

        <Column header="Name">
          <template #body="{ data }">
            <div class="name-cell">
              <b>{{ data.certificate.name }}</b>
              <span class="mono sub">{{ data.certificate.subject }}</span>
            </div>
          </template>
        </Column>

        <Column header="Thumbprint">
          <template #body="{ data }">
            <span v-if="data.certificate.thumbprintSha1" class="thumb-chip mono" v-tooltip="data.certificate.thumbprintSha1"
                  @click="copyText(data.certificate.thumbprintSha1, 'Thumbprint')">
              {{ shortThumb(data.certificate.thumbprintSha1) }} <i class="pi pi-copy" />
            </span>
            <span v-else class="muted">—</span>
          </template>
        </Column>

        <Column header="Expires">
          <template #body="{ data }">
            <div class="expiry-cell">
              <span class="mono">{{ formatDate(data.certificate.notAfter) }}</span>
              <Tag v-bind="expiryTag(data.certificate)" />
            </div>
          </template>
        </Column>

        <Column header="App Registration">
          <template #body="{ data }">
            <template v-if="data.certificate.state === 'CsrPending'">
              <Tag value="CSR pending" severity="info" />
            </template>
            <template v-else-if="data.certificate.uploadedTo.length">
              <div class="uploaded-cell">
                <Tag v-for="u in data.certificate.uploadedTo" :key="u.keyId"
                     :value="`${u.displayName || u.appId} ✓`" severity="success" class="mono-tag" />
              </div>
            </template>
            <Tag v-else value="not uploaded" severity="secondary" />
          </template>
        </Column>

        <Column header="Used by">
          <template #body="{ data }">
            <span v-if="usageCount(data)" class="usage-chip">
              {{ usageCount(data) }} {{ usageCount(data) === 1 ? 'reference' : 'references' }}
            </span>
            <span v-else class="muted">—</span>
          </template>
        </Column>

        <Column header="Actions" style="text-align:right">
          <template #body="{ data }">
            <div class="row-actions">
              <Button icon="pi pi-cloud-upload" text size="small" v-tooltip="'Upload to app registration'"
                      :disabled="data.certificate.state === 'CsrPending'" @click="openUpload(data.certificate)" />
              <Button icon="pi pi-download" text size="small" v-tooltip="'Download public cert (PEM)'"
                      :disabled="data.certificate.state === 'CsrPending'" @click="downloadPem(data.certificate)" />
              <Button icon="pi pi-trash" text size="small" severity="danger"
                      v-tooltip="usageCount(data) ? 'In use — detach it from connections first' : 'Delete'"
                      :disabled="usageCount(data) > 0" @click="confirmDelete(data.certificate)" />
            </div>
          </template>
        </Column>

        <!-- ── Detail expansion ── -->
        <template #expansion="{ data }">
          <div class="detail">
            <div class="detail-grid">
              <div>
                <h4>Files on disk</h4>
                <div class="files">
                  <span class="file mono"><i class="pi pi-file" /> certs/{{ data.certificate.name }}/cert.pem</span>
                  <span class="file mono"><i class="pi pi-lock" /> certs/{{ data.certificate.name }}/key.pem <span class="perm">0600 · never leaves this machine</span></span>
                  <span v-if="data.certificate.hasPfx" class="file mono"><i class="pi pi-box" /> certs/{{ data.certificate.name }}/cert.pfx <span class="perm">password-protected</span></span>
                </div>

                <h4>Thumbprints</h4>
                <div class="files">
                  <span class="file mono" @click="copyText(data.certificate.thumbprintSha1, 'SHA-1 thumbprint')">
                    SHA-1&nbsp;&nbsp; {{ data.certificate.thumbprintSha1 || '—' }} <i v-if="data.certificate.thumbprintSha1" class="pi pi-copy" />
                  </span>
                  <span class="file mono" @click="copyText(data.certificate.thumbprintSha256, 'SHA-256 thumbprint')">
                    SHA-256 {{ data.certificate.thumbprintSha256 || '—' }} <i v-if="data.certificate.thumbprintSha256" class="pi pi-copy" />
                  </span>
                </div>
              </div>

              <div>
                <h4>Used by</h4>
                <div v-if="usageCount(data)" class="usedby">
                  <Tag v-for="name in data.usedByConnections" :key="`c-${name}`" :value="name" severity="info" icon="pi pi-server" />
                  <Tag v-for="name in data.usedByHttpApis" :key="`h-${name}`" :value="name" severity="info" icon="pi pi-link" />
                </div>
                <p v-else class="muted-sm">Not referenced by any connection.</p>

                <h4>Upload status</h4>
                <div v-if="data.certificate.uploadedTo.length" class="verify-list">
                  <div v-for="u in data.certificate.uploadedTo" :key="u.keyId" class="verify-row">
                    <span class="mono">{{ u.displayName || u.appId }}</span>
                    <Tag v-if="verifyResults[verifyKey(data.certificate.name, u.appId)]"
                         v-bind="statusTag(verifyResults[verifyKey(data.certificate.name, u.appId)])" />
                    <Button label="Verify" icon="pi pi-sync" text size="small"
                            :loading="verifying === verifyKey(data.certificate.name, u.appId)"
                            @click="verifyUpload(data.certificate.name, u.appId)" />
                  </div>
                </div>
                <p v-else class="muted-sm">Not uploaded to any app registration yet.</p>

                <div class="detail-actions">
                  <Button label="Export PFX" icon="pi pi-download" size="small" severity="secondary" outlined
                          :disabled="data.certificate.state === 'CsrPending'" @click="openPfxExport(data.certificate)" />
                </div>
              </div>
            </div>
          </div>
        </template>
      </DataTable>
    </div>

    <!-- ── Create dialog ── -->
    <Dialog v-model:visible="createDialog" header="New Certificate" modal :style="{ width: '540px' }">
      <div class="create-grid">
        <div class="field span-2">
          <label>Name <span class="req">*</span></label>
          <InputText v-model="createForm.name" class="w-full mono" placeholder="my-api-cert" autofocus />
          <small v-if="createForm.name && !isValidName(createForm.name)" class="field-error">
            Lowercase letters, digits, and hyphens only (no leading/trailing hyphen).
          </small>
        </div>
        <div class="field">
          <label>Key size</label>
          <Select v-model="createForm.keySize" :options="[2048, 4096]" class="w-full" />
        </div>
        <div class="field">
          <label>Valid for</label>
          <Select v-model="createForm.validityMonths" :options="validityOptions" optionLabel="label" optionValue="value" class="w-full" />
        </div>
        <div class="field span-2">
          <label>Subject (CN) <span class="opt">(optional)</span></label>
          <InputText v-model="createForm.subjectCn" class="w-full mono" :placeholder="createForm.name ? `mcp-explorer-${createForm.name}` : 'mcp-explorer-…'" />
        </div>
        <div class="field span-2">
          <label>PFX password <span class="opt">(optional — also writes a password-protected .pfx)</span></label>
          <Password v-model="createForm.pfxPassword" :feedback="false" toggleMask class="w-full" inputClass="w-full" />
        </div>
        <div class="span-2">
          <StepProgress v-if="creating || createSteps" :steps="createSteps" :busy="creating"
                        busy-label="Generating certificate…" @retry="create" />
        </div>
      </div>
      <template #footer>
        <Button label="Close" severity="secondary" text @click="createDialog = false" />
        <Button label="Generate" icon="pi pi-cog" :disabled="!isValidName(createForm.name) || creating"
                :loading="creating" @click="create" />
      </template>
    </Dialog>

    <!-- ── Upload dialog ── -->
    <Dialog v-model:visible="uploadDialog" :header="`Upload ${uploadTarget?.name} to App Registration`" modal :style="{ width: '540px' }">
      <div class="upload-dialog-body">
        <p class="muted-sm">
          Adds the public certificate to the app registration's key credentials via Microsoft Graph.
          The private key never leaves this machine.
        </p>
        <AppRegistrationPicker :clientId="uploadAppId" @selected="onUploadAppSelected" />
        <div v-if="uploadAppId" class="mono selected-app">App ID: {{ uploadAppId }}</div>
        <StepProgress v-if="uploadBusy || uploadStepsDialog" :steps="uploadStepsDialog" :busy="uploadBusy"
                      busy-label="Uploading to app registration…" @retry="runUpload" />
      </div>
      <template #footer>
        <Button label="Close" severity="secondary" text @click="uploadDialog = false" />
        <Button label="Upload" icon="pi pi-cloud-upload" :disabled="!uploadAppId || uploadBusy"
                :loading="uploadBusy" @click="runUpload" />
      </template>
    </Dialog>

    <!-- ── PFX export dialog ── -->
    <Dialog v-model:visible="pfxDialog" :header="`Export ${pfxTarget?.name}.pfx`" modal :style="{ width: '440px' }">
      <div class="pfx-body">
        <p class="muted-sm">The PFX bundle contains the private key, so a password is required. The export is audit-logged.</p>
        <div class="field">
          <label>Password <span class="req">*</span></label>
          <Password v-model="pfxPassword" :feedback="false" toggleMask class="w-full" inputClass="w-full" autofocus />
        </div>
      </div>
      <template #footer>
        <Button label="Cancel" severity="secondary" text @click="pfxDialog = false" />
        <Button label="Export" icon="pi pi-download" :disabled="!pfxPassword.trim() || pfxExporting"
                :loading="pfxExporting" @click="exportPfx" />
      </template>
    </Dialog>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import Button from 'primevue/button'
import Column from 'primevue/column'
import DataTable from 'primevue/datatable'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'
import Password from 'primevue/password'
import Select from 'primevue/select'
import Tag from 'primevue/tag'
import { useConfirm } from 'primevue/useconfirm'
import { useToast } from 'primevue/usetoast'
import AppRegistrationPicker from '@/components/connections/AppRegistrationPicker.vue'
import StepProgress from '@/components/certificates/StepProgress.vue'
import { certificatesApi } from '@/api/certificates'
import { extractApiError } from '@/api/client'
import { useCertificatesStore } from '@/stores/certificates'
import type { AzureAppRegistration, CertUploadStatus, CertificateInfo, CertificateWithUsage, StepResult } from '@/api/types'

const store = useCertificatesStore()
const toast = useToast()
const confirm = useConfirm()

const expandedRows = ref({})

onMounted(() => { void store.load() })

// ── Formatting helpers ──────────────────────────────────────────────────────

const validityOptions = [
  { label: '6 months', value: 6 },
  { label: '12 months', value: 12 },
  { label: '24 months', value: 24 },
]

function isValidName(name: string) {
  return /^[a-z0-9]([a-z0-9-]{0,62}[a-z0-9])?$/.test(name)
}

function formatDate(value?: string | null) {
  if (!value) return '—'
  return new Date(value).toLocaleDateString(undefined, { day: '2-digit', month: 'short', year: 'numeric' })
}

function shortThumb(thumb: string) {
  return `${thumb.slice(0, 4)}…${thumb.slice(-4)}`
}

function usageCount(item: CertificateWithUsage) {
  return item.usedByConnections.length + item.usedByHttpApis.length
}

function expiryTag(cert: CertificateInfo): { value: string, severity: string } {
  if (cert.state === 'Superseded') return { value: 'Superseded', severity: 'secondary' }
  if (!cert.notAfter) return { value: 'CSR pending', severity: 'info' }
  const days = Math.ceil((new Date(cert.notAfter).getTime() - Date.now()) / 86_400_000)
  if (days <= 0) return { value: `Expired · ${Math.abs(days)}d ago`, severity: 'danger' }
  if (days <= 30) return { value: `Expiring · ${days}d`, severity: 'warn' }
  return { value: `Valid · ${days}d`, severity: 'success' }
}

function statusTag(status: CertUploadStatus): { value: string, severity: string } {
  switch (status) {
    case 'Current': return { value: 'Current ✓', severity: 'success' }
    case 'Stale': return { value: 'Stale ⚠', severity: 'warn' }
    default: return { value: 'Missing', severity: 'danger' }
  }
}

async function copyText(value: string | null | undefined, label: string) {
  if (!value) return
  await navigator.clipboard.writeText(value)
  toast.add({ severity: 'info', summary: 'Copied', detail: `${label} copied to clipboard`, life: 2000 })
}

// ── Create ──────────────────────────────────────────────────────────────────

const createDialog = ref(false)
const creating = ref(false)
const createSteps = ref<StepResult[] | null>(null)
const createForm = reactive({ name: '', subjectCn: '', keySize: 2048, validityMonths: 12, pfxPassword: '' })

function openCreate() {
  Object.assign(createForm, { name: '', subjectCn: '', keySize: 2048, validityMonths: 12, pfxPassword: '' })
  createSteps.value = null
  createDialog.value = true
}

async function create() {
  creating.value = true
  createSteps.value = null
  try {
    const result = await certificatesApi.generate({
      name: createForm.name,
      subjectCn: createForm.subjectCn || undefined,
      keySize: createForm.keySize,
      validityMonths: createForm.validityMonths,
      pfxPassword: createForm.pfxPassword || undefined,
    })
    createSteps.value = result.steps
    if (result.success) {
      toast.add({ severity: 'success', summary: 'Certificate created', detail: result.certificate?.name, life: 4200 })
      await store.load()
    }
  }
  catch (err) {
    toast.add({ severity: 'error', summary: 'Generation failed', detail: extractApiError(err), life: 6000 })
  }
  finally {
    creating.value = false
  }
}

// ── Upload ──────────────────────────────────────────────────────────────────

const uploadDialog = ref(false)
const uploadTarget = ref<CertificateInfo | null>(null)
const uploadAppId = ref('')
const uploadBusy = ref(false)
const uploadStepsDialog = ref<StepResult[] | null>(null)

function openUpload(cert: CertificateInfo) {
  uploadTarget.value = cert
  uploadAppId.value = cert.uploadedTo[0]?.appId ?? ''
  uploadStepsDialog.value = null
  uploadDialog.value = true
}

function onUploadAppSelected(app: AzureAppRegistration) {
  uploadAppId.value = app.appId
}

async function runUpload() {
  if (!uploadTarget.value || !uploadAppId.value) return
  uploadBusy.value = true
  uploadStepsDialog.value = null
  try {
    const result = await certificatesApi.upload(uploadTarget.value.name, uploadAppId.value)
    uploadStepsDialog.value = result.steps
    if (result.success) {
      toast.add({ severity: 'success', summary: 'Certificate uploaded', detail: `${uploadTarget.value.name} → app registration`, life: 4200 })
      await store.load()
    }
  }
  catch (err) {
    toast.add({ severity: 'error', summary: 'Upload failed', detail: extractApiError(err), life: 6000 })
  }
  finally {
    uploadBusy.value = false
  }
}

// ── Verify upload status ────────────────────────────────────────────────────

const verifying = ref<string | null>(null)
const verifyResults = reactive<Record<string, CertUploadStatus>>({})

function verifyKey(name: string, appId: string) {
  return `${name}|${appId}`
}

async function verifyUpload(name: string, appId: string) {
  const key = verifyKey(name, appId)
  verifying.value = key
  try {
    verifyResults[key] = await certificatesApi.uploadStatus(name, appId)
  }
  catch (err) {
    toast.add({ severity: 'error', summary: 'Verification failed', detail: extractApiError(err), life: 5000 })
  }
  finally {
    verifying.value = null
  }
}

// ── Downloads ───────────────────────────────────────────────────────────────

async function downloadPem(cert: CertificateInfo) {
  try {
    const blob = await certificatesApi.downloadPem(cert.name)
    triggerDownload(blob, `${cert.name}.pem`)
  }
  catch (err) {
    toast.add({ severity: 'error', summary: 'Download failed', detail: extractApiError(err), life: 5000 })
  }
}

const pfxDialog = ref(false)
const pfxTarget = ref<CertificateInfo | null>(null)
const pfxPassword = ref('')
const pfxExporting = ref(false)

function openPfxExport(cert: CertificateInfo) {
  pfxTarget.value = cert
  pfxPassword.value = ''
  pfxDialog.value = true
}

async function exportPfx() {
  if (!pfxTarget.value) return
  pfxExporting.value = true
  try {
    const blob = await certificatesApi.exportPfx(pfxTarget.value.name, pfxPassword.value)
    triggerDownload(blob, `${pfxTarget.value.name}.pfx`)
    pfxDialog.value = false
  }
  catch (err) {
    toast.add({ severity: 'error', summary: 'Export failed', detail: extractApiError(err), life: 5000 })
  }
  finally {
    pfxExporting.value = false
  }
}

function triggerDownload(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = fileName
  anchor.click()
  URL.revokeObjectURL(url)
}

// ── Delete ──────────────────────────────────────────────────────────────────

function confirmDelete(cert: CertificateInfo) {
  confirm.require({
    message: `Delete certificate '${cert.name}'? This removes its files (including the private key) from the local store.`,
    header: 'Delete Certificate',
    icon: 'pi pi-exclamation-triangle',
    rejectProps: { label: 'Cancel', severity: 'secondary', outlined: true },
    acceptProps: { label: 'Delete', severity: 'danger' },
    accept: async () => {
      try {
        await certificatesApi.remove(cert.name)
        toast.add({ severity: 'success', summary: 'Certificate deleted', detail: cert.name, life: 3000 })
        await store.load()
      }
      catch (err) {
        toast.add({ severity: 'error', summary: 'Delete blocked', detail: extractApiError(err), life: 6000 })
      }
    },
  })
}
</script>

<style scoped>
.certificates-view {
  padding: 20px 24px 32px;
  overflow-y: auto;
}

.page-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 18px;
}

.page-title {
  display: flex;
  align-items: center;
  gap: 9px;
  margin: 0 0 4px;
  font-size: 18px;
  font-weight: 600;
  color: var(--text-primary);
}

.title-icon { color: var(--accent); }

.page-sub {
  margin: 0;
  font-size: 12.5px;
  color: var(--text-muted);
}

.page-sub code {
  font-family: var(--font-family-mono);
  font-size: 11px;
  background: var(--code-bg);
  border: 1px solid var(--border);
  border-radius: 4px;
  padding: 0 4px;
}

/* Stat tiles */
.stat-row {
  display: flex;
  gap: 12px;
  margin-bottom: 16px;
  flex-wrap: wrap;
}

.stat {
  flex: 1;
  min-width: 150px;
  background: var(--bg-surface);
  border: 1px solid var(--border);
  border-radius: var(--border-radius-md);
  padding: 12px 14px;
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.stat .v {
  font-size: 22px;
  font-weight: 650;
  font-variant-numeric: tabular-nums;
  color: var(--text-primary);
}

.stat .k { font-size: 11px; color: var(--text-muted); }
.stat.s-ok .v { color: var(--success); }
.stat.s-warn .v { color: var(--warning); }
.stat.s-bad .v { color: var(--danger); }

.card {
  background: var(--bg-surface);
  border: 1px solid var(--border);
  border-radius: var(--border-radius-md);
  overflow: hidden;
}

.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
  padding: 28px;
  color: var(--text-muted);
}

.empty-state .pi { font-size: 24px; }

.name-cell { display: flex; flex-direction: column; gap: 1px; }
.name-cell b { color: var(--text-primary); font-weight: 600; }
.name-cell .sub { font-size: 10.5px; color: var(--text-muted); }

.mono { font-family: var(--font-family-mono); }

.thumb-chip {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  font-size: 10.5px;
  background: var(--code-bg);
  border: 1px solid var(--border);
  border-radius: 5px;
  padding: 3px 8px;
  color: var(--text-secondary);
  cursor: pointer;
}

.thumb-chip:hover { color: var(--text-primary); border-color: var(--text-muted); }
.thumb-chip .pi { font-size: 9px; }

.expiry-cell { display: flex; flex-direction: column; gap: 3px; align-items: flex-start; }
.expiry-cell .mono { font-size: 12px; }

.uploaded-cell { display: flex; flex-wrap: wrap; gap: 4px; }
.mono-tag { font-family: var(--font-family-mono); font-size: 10px; }

.usage-chip {
  display: inline-flex;
  align-items: center;
  font-size: 11px;
  padding: 2px 10px;
  border-radius: 12px;
  background: var(--accent-muted);
  color: var(--accent);
  font-weight: 600;
}

.muted { color: var(--text-muted); }
.muted-sm { color: var(--text-muted); font-size: 12px; margin: 0; }

.row-actions { display: flex; gap: 2px; justify-content: flex-end; }

/* Detail expansion */
.detail { padding: 6px 10px 12px; }

.detail-grid {
  display: grid;
  grid-template-columns: 1.2fr 1fr;
  gap: 18px;
}

@media (max-width: 900px) {
  .detail-grid { grid-template-columns: 1fr; }
}

.detail h4 {
  margin: 12px 0 6px;
  font-size: 11px;
  text-transform: uppercase;
  letter-spacing: .06em;
  color: var(--text-muted);
}

.detail h4:first-child { margin-top: 0; }

.files { display: flex; flex-direction: column; gap: 5px; }

.file {
  display: inline-flex;
  align-items: center;
  gap: 7px;
  font-size: 11.5px;
  color: var(--text-secondary);
  cursor: default;
  overflow-wrap: anywhere;
}

.file .pi { font-size: 11px; }
.file .pi-copy { cursor: pointer; }
.file .perm { color: var(--text-muted); font-size: 10px; }

.usedby { display: flex; flex-wrap: wrap; gap: 6px; }

.verify-list { display: flex; flex-direction: column; gap: 6px; }
.verify-row { display: flex; align-items: center; gap: 10px; font-size: 12px; }

.detail-actions { margin-top: 14px; display: flex; gap: 8px; }

/* Dialogs */
.create-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px 14px;
}

.span-2 { grid-column: 1 / -1; }

.field label {
  display: block;
  font-size: 11.5px;
  font-weight: 600;
  color: var(--text-secondary);
  margin-bottom: 4px;
}

.field .opt { font-weight: 400; color: var(--text-muted); }
.field .req { color: var(--danger); }
.field-error { color: var(--danger); font-size: 11px; }

.upload-dialog-body,
.pfx-body {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.selected-app { font-size: 11.5px; color: var(--text-secondary); }

.w-full { width: 100%; }
</style>
