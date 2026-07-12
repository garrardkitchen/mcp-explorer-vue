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
      <div class="header-actions">
        <Button label="Import from Key Vault" icon="pi pi-lock" severity="secondary" outlined @click="openKvImport" />
        <Button label="Create CSR" icon="pi pi-file-edit" severity="secondary" outlined
                v-tooltip="'For CA-issued certificates: generate a key + signing request here, import the issued cert later'"
                @click="openCsr" />
        <Button label="New Certificate" icon="pi pi-plus" @click="openCreate" />
      </div>
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
              <template v-if="data.certificate.state === 'CsrPending'">
                <Button icon="pi pi-file-export" text size="small" v-tooltip="'Download CSR (send to your CA)'"
                        @click="downloadCsr(data.certificate)" />
                <Button icon="pi pi-file-import" text size="small" v-tooltip="'Import the CA-issued certificate'"
                        @click="openImportIssued(data.certificate)" />
              </template>
              <template v-else>
                <Button icon="pi pi-cloud-upload" text size="small" v-tooltip="'Upload to app registration'"
                        @click="openUpload(data.certificate)" />
                <Button icon="pi pi-download" text size="small" v-tooltip="'Download public cert (PEM)'"
                        @click="downloadPem(data.certificate)" />
                <Button icon="pi pi-refresh" text size="small" v-tooltip="'Renew & re-upload'"
                        :disabled="data.certificate.state !== 'Active'" @click="openRenew(data.certificate)" />
              </template>
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
                    <Button label="Manage" icon="pi pi-list" text size="small"
                            v-tooltip="'List and clean up key credentials on this app registration'"
                            @click="openKeyCredentials(u.appId, u.displayName)" />
                  </div>
                </div>
                <p v-else class="muted-sm">Not uploaded to any app registration yet.</p>

                <div class="detail-actions">
                  <Button label="Renew & re-upload" icon="pi pi-refresh" size="small" severity="secondary" outlined
                          :disabled="data.certificate.state !== 'Active'" @click="openRenew(data.certificate)" />
                  <Button label="Export PFX" icon="pi pi-download" size="small" severity="secondary" outlined
                          :disabled="data.certificate.state === 'CsrPending'" @click="openPfxExport(data.certificate)" />
                  <Button label="Export to Key Vault" icon="pi pi-lock" size="small" severity="secondary" outlined
                          :disabled="data.certificate.state === 'CsrPending'" @click="openKvExport(data.certificate)" />
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

    <!-- ── CSR dialog ── -->
    <Dialog v-model:visible="csrDialog" header="Create Certificate Signing Request" modal :style="{ width: '540px' }">
      <div class="create-grid">
        <p class="muted-sm span-2" style="margin:0">
          Generates a private key and CSR locally. Send the CSR to your certificate authority,
          then import the issued certificate from the row's <i class="pi pi-file-import" /> action.
          The private key never leaves this machine.
        </p>
        <div class="field span-2">
          <label>Name <span class="req">*</span></label>
          <InputText v-model="csrForm.name" class="w-full mono" placeholder="my-ca-cert" autofocus />
          <small v-if="csrForm.name && !isValidName(csrForm.name)" class="field-error">
            Lowercase letters, digits, and hyphens only (no leading/trailing hyphen).
          </small>
        </div>
        <div class="field">
          <label>Key size</label>
          <Select v-model="csrForm.keySize" :options="[2048, 4096]" class="w-full" />
        </div>
        <div class="field">
          <label>Subject (CN) <span class="opt">(optional)</span></label>
          <InputText v-model="csrForm.subjectCn" class="w-full mono" :placeholder="csrForm.name ? `mcp-explorer-${csrForm.name}` : 'mcp-explorer-…'" />
        </div>
        <div class="span-2">
          <StepProgress v-if="csrBusy || csrSteps" :steps="csrSteps" :busy="csrBusy"
                        busy-label="Creating CSR…" @retry="runCreateCsr" />
        </div>
      </div>
      <template #footer>
        <Button label="Close" severity="secondary" text @click="csrDialog = false" />
        <Button label="Create CSR" icon="pi pi-file-edit" :disabled="!isValidName(csrForm.name) || csrBusy"
                :loading="csrBusy" @click="runCreateCsr" />
      </template>
    </Dialog>

    <!-- ── Import issued certificate dialog ── -->
    <Dialog v-model:visible="issuedDialog" :header="`Import issued certificate for ${issuedTarget?.name}`" modal :style="{ width: '600px' }">
      <div class="kc-body">
        <p class="muted-sm">
          Paste the certificate your CA issued for this CSR (PEM, <code>-----BEGIN CERTIFICATE-----</code>).
          It must match the private key generated with the CSR.
        </p>
        <Textarea v-model="issuedPem" rows="9" class="w-full mono" placeholder="-----BEGIN CERTIFICATE-----" />
        <StepProgress v-if="issuedBusy || issuedSteps" :steps="issuedSteps" :busy="issuedBusy"
                      busy-label="Importing issued certificate…" @retry="runImportIssued" />
      </div>
      <template #footer>
        <Button label="Close" severity="secondary" text @click="issuedDialog = false" />
        <Button label="Import" icon="pi pi-file-import" :disabled="!issuedPem.trim() || issuedBusy"
                :loading="issuedBusy" @click="runImportIssued" />
      </template>
    </Dialog>

    <!-- ── Key Vault import dialog ── -->
    <Dialog v-model:visible="kvImportDialog" header="Import certificate from Key Vault" modal :style="{ width: '540px' }">
      <div class="create-grid">
        <div class="field span-2">
          <label>Key Vault <span class="req">*</span></label>
          <Select v-model="kvImportVault" :options="kvVaults" optionLabel="name" optionValue="name"
                  class="w-full" placeholder="Select a vault" :loading="kvVaultsLoading" filter
                  @change="loadKvCertificates" />
        </div>
        <div class="field span-2">
          <label>Certificate <span class="req">*</span></label>
          <Select v-model="kvImportCert" :options="kvCertNames" class="w-full"
                  placeholder="Select a certificate" :loading="kvCertsLoading" :disabled="!kvImportVault" filter />
          <small class="muted-sm">The certificate's policy must allow an exportable private key.</small>
        </div>
        <div class="field span-2">
          <label>Local name <span class="req">*</span></label>
          <InputText v-model="kvImportLocalName" class="w-full mono" placeholder="imported-cert" />
          <small v-if="kvImportLocalName && !isValidName(kvImportLocalName)" class="field-error">
            Lowercase letters, digits, and hyphens only (no leading/trailing hyphen).
          </small>
        </div>
        <div class="span-2">
          <StepProgress v-if="kvImportBusy || kvImportSteps" :steps="kvImportSteps" :busy="kvImportBusy"
                        busy-label="Importing from Key Vault…" @retry="runKvImport" />
        </div>
      </div>
      <template #footer>
        <Button label="Close" severity="secondary" text @click="kvImportDialog = false" />
        <Button label="Import" icon="pi pi-lock" :disabled="!kvImportVault || !kvImportCert || !isValidName(kvImportLocalName) || kvImportBusy"
                :loading="kvImportBusy" @click="runKvImport" />
      </template>
    </Dialog>

    <!-- ── Key Vault export dialog ── -->
    <Dialog v-model:visible="kvExportDialog" :header="`Export ${kvExportTarget?.name} to Key Vault`" modal :style="{ width: '480px' }">
      <div class="kc-body">
        <p class="muted-sm">Imports this certificate (with its private key) into the vault so teammates can use it.</p>
        <div class="field">
          <label>Key Vault <span class="req">*</span></label>
          <Select v-model="kvExportVault" :options="kvVaults" optionLabel="name" optionValue="name"
                  class="w-full" placeholder="Select a vault" :loading="kvVaultsLoading" filter />
        </div>
        <StepProgress v-if="kvExportBusy || kvExportSteps" :steps="kvExportSteps" :busy="kvExportBusy"
                      busy-label="Exporting to Key Vault…" @retry="runKvExport" />
      </div>
      <template #footer>
        <Button label="Close" severity="secondary" text @click="kvExportDialog = false" />
        <Button label="Export" icon="pi pi-lock" :disabled="!kvExportVault || kvExportBusy"
                :loading="kvExportBusy" @click="runKvExport" />
      </template>
    </Dialog>

    <!-- ── Renew dialog ── -->
    <Dialog v-model:visible="renewDialog" :header="`Renew ${renewTarget?.name}`" modal :style="{ width: '560px' }">
      <div class="renew-body">
        <p class="muted-sm">
          Renewing generates a fresh certificate with the same subject and key size, uploads it to
          every app registration this one was uploaded to, and repoints the
          {{ renewUsageLabel }} to the new certificate. The old certificate keeps working until
          everything has succeeded, then is marked superseded.
        </p>
        <label class="cleanup-check">
          <Checkbox v-model="renewRemoveOld" binary />
          <span>Also remove the old key credential(s) from Azure after the upload succeeds</span>
        </label>
        <StepProgress v-if="renewBusy || renewSteps" :steps="renewSteps" :busy="renewBusy"
                      busy-label="Renewing certificate…" @retry="runRenew" />
      </div>
      <template #footer>
        <Button label="Close" severity="secondary" text @click="renewDialog = false" />
        <Button label="Renew" icon="pi pi-refresh" :disabled="renewBusy" :loading="renewBusy" @click="runRenew" />
      </template>
    </Dialog>

    <!-- ── Key credentials dialog (stale cleanup) ── -->
    <Dialog v-model:visible="kcDialog" :header="`Key credentials on ${kcAppLabel}`" modal :style="{ width: '640px' }">
      <div class="kc-body">
        <p class="muted-sm">
          All certificate key credentials on this app registration. An entry is flagged
          <b>stale</b> when nothing uses it any more — its local certificate was replaced by a
          renewal, or it has expired in Azure. Its expiry date may still be in the future;
          stale means unused, and unused credentials are safe (and recommended) to remove.
        </p>
        <div v-if="kcLoading" class="muted-sm"><i class="pi pi-spin pi-spinner" /> Loading key credentials…</div>
        <p v-else-if="kcError" class="kc-error"><i class="pi pi-exclamation-triangle" /> {{ kcError }}</p>
        <p v-else-if="!kcItems.length" class="muted-sm">No certificate key credentials on this app registration.</p>
        <div v-else class="kc-list">
          <div v-for="k in kcItems" :key="k.keyId" class="kc-row" :class="{ stale: k.isStale }">
            <div class="kc-main">
              <span class="kc-name">{{ k.displayName || '(unnamed)' }}</span>
              <span class="mono kc-meta">
                {{ k.customKeyIdentifierHex ? shortThumb(k.customKeyIdentifierHex) : 'no thumbprint' }}
                · expires {{ formatDate(k.endDateTime) }}
                <template v-if="k.localCertificateName"> · local: {{ k.localCertificateName }}</template>
              </span>
            </div>
            <Tag v-if="k.isStale" :value="k.staleReason ? `Stale · ${k.staleReason}` : 'Stale ⚠'" severity="warn" />
            <Tag v-else-if="k.localCertificateName" value="Current ✓" severity="success" />
            <Button icon="pi pi-trash" text size="small" severity="danger" v-tooltip="'Remove this key credential from Azure'"
                    :loading="kcRemoving === k.keyId" @click="removeKeyCredential(k.keyId)" />
          </div>
        </div>
      </div>
      <template #footer>
        <Button label="Close" severity="secondary" text @click="kcDialog = false" />
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
import { computed, onMounted, reactive, ref } from 'vue'
import Button from 'primevue/button'
import Checkbox from 'primevue/checkbox'
import Column from 'primevue/column'
import DataTable from 'primevue/datatable'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'
import Password from 'primevue/password'
import Select from 'primevue/select'
import Tag from 'primevue/tag'
import Textarea from 'primevue/textarea'
import { useConfirm } from 'primevue/useconfirm'
import { useToast } from 'primevue/usetoast'
import AppRegistrationPicker from '@/components/connections/AppRegistrationPicker.vue'
import StepProgress from '@/components/certificates/StepProgress.vue'
import { azureApi } from '@/api/azure'
import { certificatesApi } from '@/api/certificates'
import { extractApiError } from '@/api/client'
import { useCertificatesStore } from '@/stores/certificates'
import type { AzureAppRegistration, AzureKeyVaultInfo, CertUploadStatus, CertificateInfo, CertificateWithUsage, GraphKeyCredentialInfo, StepResult } from '@/api/types'

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

// ── CSR flow ────────────────────────────────────────────────────────────────

const csrDialog = ref(false)
const csrBusy = ref(false)
const csrSteps = ref<StepResult[] | null>(null)
const csrForm = reactive({ name: '', subjectCn: '', keySize: 2048 })

function openCsr() {
  Object.assign(csrForm, { name: '', subjectCn: '', keySize: 2048 })
  csrSteps.value = null
  csrDialog.value = true
}

async function runCreateCsr() {
  csrBusy.value = true
  csrSteps.value = null
  try {
    const result = await certificatesApi.createCsr(csrForm.name, csrForm.subjectCn || undefined, csrForm.keySize)
    csrSteps.value = result.steps
    if (result.success) {
      toast.add({ severity: 'success', summary: 'CSR created', detail: `Download it from the ${csrForm.name} row and send it to your CA.`, life: 6000 })
      await store.load()
    }
  }
  catch (err) {
    toast.add({ severity: 'error', summary: 'CSR creation failed', detail: extractApiError(err), life: 6000 })
  }
  finally {
    csrBusy.value = false
  }
}

async function downloadCsr(cert: CertificateInfo) {
  try {
    const blob = await certificatesApi.downloadCsr(cert.name)
    triggerDownload(blob, `${cert.name}.csr`)
  }
  catch (err) {
    toast.add({ severity: 'error', summary: 'Download failed', detail: extractApiError(err), life: 5000 })
  }
}

const issuedDialog = ref(false)
const issuedTarget = ref<CertificateInfo | null>(null)
const issuedPem = ref('')
const issuedBusy = ref(false)
const issuedSteps = ref<StepResult[] | null>(null)

function openImportIssued(cert: CertificateInfo) {
  issuedTarget.value = cert
  issuedPem.value = ''
  issuedSteps.value = null
  issuedDialog.value = true
}

async function runImportIssued() {
  if (!issuedTarget.value) return
  issuedBusy.value = true
  issuedSteps.value = null
  try {
    const result = await certificatesApi.importIssued(issuedTarget.value.name, issuedPem.value)
    issuedSteps.value = result.steps
    if (result.success) {
      toast.add({ severity: 'success', summary: 'Certificate issued & imported', detail: issuedTarget.value.name, life: 4200 })
      await store.load()
    }
  }
  catch (err) {
    toast.add({ severity: 'error', summary: 'Import failed', detail: extractApiError(err), life: 6000 })
  }
  finally {
    issuedBusy.value = false
  }
}

// ── Key Vault import/export ─────────────────────────────────────────────────

const kvVaults = ref<AzureKeyVaultInfo[]>([])
const kvVaultsLoading = ref(false)

async function loadKvVaults() {
  if (kvVaults.value.length) return
  kvVaultsLoading.value = true
  try {
    kvVaults.value = await azureApi.getKeyVaults()
  }
  catch (err) {
    toast.add({ severity: 'error', summary: 'Could not list Key Vaults', detail: extractApiError(err), life: 5000 })
  }
  finally {
    kvVaultsLoading.value = false
  }
}

const kvImportDialog = ref(false)
const kvImportVault = ref('')
const kvImportCert = ref('')
const kvImportLocalName = ref('')
const kvCertNames = ref<string[]>([])
const kvCertsLoading = ref(false)
const kvImportBusy = ref(false)
const kvImportSteps = ref<StepResult[] | null>(null)

function openKvImport() {
  kvImportVault.value = ''
  kvImportCert.value = ''
  kvImportLocalName.value = ''
  kvCertNames.value = []
  kvImportSteps.value = null
  kvImportDialog.value = true
  void loadKvVaults()
}

async function loadKvCertificates() {
  if (!kvImportVault.value) return
  kvCertsLoading.value = true
  kvImportCert.value = ''
  try {
    kvCertNames.value = await azureApi.getKeyVaultCertificates(kvImportVault.value)
  }
  catch (err) {
    toast.add({ severity: 'error', summary: 'Could not list certificates', detail: extractApiError(err), life: 5000 })
    kvCertNames.value = []
  }
  finally {
    kvCertsLoading.value = false
  }
}

async function runKvImport() {
  kvImportBusy.value = true
  kvImportSteps.value = null
  try {
    const result = await certificatesApi.importFromKeyVault(kvImportVault.value, kvImportCert.value, kvImportLocalName.value)
    kvImportSteps.value = result.steps
    if (result.success) {
      toast.add({ severity: 'success', summary: 'Imported from Key Vault', detail: kvImportLocalName.value, life: 4200 })
      await store.load()
    }
  }
  catch (err) {
    toast.add({ severity: 'error', summary: 'Import failed', detail: extractApiError(err), life: 6000 })
  }
  finally {
    kvImportBusy.value = false
  }
}

const kvExportDialog = ref(false)
const kvExportTarget = ref<CertificateInfo | null>(null)
const kvExportVault = ref('')
const kvExportBusy = ref(false)
const kvExportSteps = ref<StepResult[] | null>(null)

function openKvExport(cert: CertificateInfo) {
  kvExportTarget.value = cert
  kvExportVault.value = ''
  kvExportSteps.value = null
  kvExportDialog.value = true
  void loadKvVaults()
}

async function runKvExport() {
  if (!kvExportTarget.value) return
  kvExportBusy.value = true
  kvExportSteps.value = null
  try {
    const result = await certificatesApi.exportToKeyVault(kvExportTarget.value.name, kvExportVault.value)
    kvExportSteps.value = result.steps
    if (result.success)
      toast.add({ severity: 'success', summary: 'Exported to Key Vault', detail: `${kvExportTarget.value.name} → ${kvExportVault.value}`, life: 4200 })
  }
  catch (err) {
    toast.add({ severity: 'error', summary: 'Export failed', detail: extractApiError(err), life: 6000 })
  }
  finally {
    kvExportBusy.value = false
  }
}

// ── Renew ───────────────────────────────────────────────────────────────────

const renewDialog = ref(false)
const renewTarget = ref<CertificateInfo | null>(null)
const renewRemoveOld = ref(false)
const renewBusy = ref(false)
const renewSteps = ref<StepResult[] | null>(null)

const renewUsageLabel = computed(() => {
  const item = store.items.find(i => i.certificate.name === renewTarget.value?.name)
  const total = item ? item.usedByConnections.length + item.usedByHttpApis.length : 0
  return total === 1 ? '1 referencing connection' : `${total} referencing connections`
})

function openRenew(cert: CertificateInfo) {
  renewTarget.value = cert
  renewRemoveOld.value = false
  renewSteps.value = null
  renewDialog.value = true
}

async function runRenew() {
  if (!renewTarget.value) return
  renewBusy.value = true
  renewSteps.value = null
  try {
    const result = await certificatesApi.renew(renewTarget.value.name, renewRemoveOld.value)
    renewSteps.value = result.steps
    if (result.success) {
      toast.add({ severity: 'success', summary: 'Certificate renewed', detail: `${renewTarget.value.name} → ${result.certificate?.name}`, life: 5000 })
      await store.load()
    }
  }
  catch (err) {
    toast.add({ severity: 'error', summary: 'Renewal failed', detail: extractApiError(err), life: 6000 })
  }
  finally {
    renewBusy.value = false
  }
}

// ── Key credentials (stale cleanup) ─────────────────────────────────────────

const kcDialog = ref(false)
const kcAppId = ref('')
const kcAppLabel = ref('')
const kcItems = ref<GraphKeyCredentialInfo[]>([])
const kcLoading = ref(false)
const kcError = ref<string | null>(null)
const kcRemoving = ref<string | null>(null)

async function openKeyCredentials(appId: string, displayName?: string) {
  kcAppId.value = appId
  kcAppLabel.value = displayName || appId
  kcDialog.value = true
  await loadKeyCredentials()
}

async function loadKeyCredentials() {
  kcLoading.value = true
  kcError.value = null
  try {
    kcItems.value = await certificatesApi.listKeyCredentials(kcAppId.value)
  }
  catch (err) {
    kcError.value = extractApiError(err)
  }
  finally {
    kcLoading.value = false
  }
}

function removeKeyCredential(keyId: string) {
  confirm.require({
    message: 'Remove this key credential from the app registration? Anything still authenticating with it will stop working.',
    header: 'Remove Key Credential',
    icon: 'pi pi-exclamation-triangle',
    rejectProps: { label: 'Cancel', severity: 'secondary', outlined: true },
    acceptProps: { label: 'Remove', severity: 'danger' },
    accept: async () => {
      kcRemoving.value = keyId
      try {
        await certificatesApi.removeKeyCredential(kcAppId.value, keyId)
        toast.add({ severity: 'success', summary: 'Key credential removed', life: 3000 })
        await Promise.all([loadKeyCredentials(), store.load()])
      }
      catch (err) {
        toast.add({ severity: 'error', summary: 'Removal failed', detail: extractApiError(err), life: 6000 })
      }
      finally {
        kcRemoving.value = null
      }
    },
  })
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

.header-actions {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
  justify-content: flex-end;
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
.pfx-body,
.renew-body,
.kc-body {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.cleanup-check {
  display: flex;
  align-items: center;
  gap: 9px;
  font-size: 12.5px;
  color: var(--text-secondary);
  cursor: pointer;
}

.kc-list {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.kc-row {
  display: flex;
  align-items: center;
  gap: 10px;
  border: 1px solid var(--border);
  border-radius: var(--border-radius-sm);
  padding: 8px 10px;
}

.kc-row.stale {
  border-color: color-mix(in srgb, var(--warning) 45%, transparent);
  background: color-mix(in srgb, var(--warning) 6%, transparent);
}

.kc-main {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 1px;
}

.kc-name {
  font-size: 12.5px;
  font-weight: 600;
  color: var(--text-primary);
}

.kc-meta {
  font-size: 10.5px;
  color: var(--text-muted);
}

.kc-error {
  color: var(--danger);
  font-size: 12.5px;
  margin: 0;
  display: flex;
  align-items: center;
  gap: 7px;
}

.selected-app { font-size: 11.5px; color: var(--text-secondary); }

.w-full { width: 100%; }
</style>
