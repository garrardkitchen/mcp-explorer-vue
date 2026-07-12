<template>
  <div class="cert-panel">
    <!-- ── Certificate picker ── -->
    <div class="picker-row">
      <div class="field grow">
        <label>Certificate</label>
        <Select
          v-model="selection"
          :options="certificateOptions"
          optionLabel="label"
          optionValue="value"
          class="w-full"
          placeholder="Select a certificate"
          @change="onSelectionChange"
        />
      </div>
      <RouterLink :to="{ name: 'certificates' }" class="open-link">
        Open Certificates <i class="pi pi-external-link" />
      </RouterLink>
    </div>

    <!-- ── Inline generator ── -->
    <div v-if="selection === NEW_CERT && !generatedCert" class="gen-form">
      <div class="gen-grid">
        <div class="field span-2">
          <label>Name <span class="req">*</span></label>
          <InputText v-model="genForm.name" class="w-full mono" placeholder="my-api-cert" />
          <small v-if="genForm.name && !isValidName(genForm.name)" class="field-error">
            Lowercase letters, digits, and hyphens only (no leading/trailing hyphen).
          </small>
        </div>
        <div class="field">
          <label>Key size</label>
          <Select v-model="genForm.keySize" :options="[2048, 4096]" class="w-full" />
        </div>
        <div class="field">
          <label>Valid for</label>
          <Select v-model="genForm.validityMonths" :options="validityOptions" optionLabel="label" optionValue="value" class="w-full" />
        </div>
        <div class="field span-2">
          <label>Subject (CN) <span class="opt">(optional — defaults to mcp-explorer-{name})</span></label>
          <InputText v-model="genForm.subjectCn" class="w-full mono" :placeholder="genForm.name ? `mcp-explorer-${genForm.name}` : 'mcp-explorer-…'" />
        </div>
        <div class="field span-2">
          <label>PFX password <span class="opt">(optional — also writes a password-protected .pfx)</span></label>
          <Password v-model="genForm.pfxPassword" :feedback="false" toggleMask class="w-full" inputClass="w-full" />
        </div>
      </div>

      <div class="gen-actions">
        <Button
          label="Generate certificate"
          icon="pi pi-cog"
          size="small"
          :disabled="!isValidName(genForm.name) || generating"
          :loading="generating"
          @click="generate"
        />
        <span class="hint">Self-signed X.509 · SHA-256 · saved under the local <code>certs/</code> store</span>
      </div>

      <StepProgress
        v-if="generating || genSteps"
        :steps="genSteps"
        :busy="generating"
        busy-label="Generating certificate…"
        @retry="generate"
      />
    </div>

    <!-- ── Selected certificate summary ── -->
    <div v-if="selectedCert" class="cert-summary" :class="{ ok: true }">
      <div class="summary-head">
        <i class="pi pi-verified" />
        <span class="summary-name">{{ selectedCert.name }}</span>
        <Tag v-if="expiryTag" :value="expiryTag.label" :severity="expiryTag.severity" />
      </div>
      <div class="summary-grid">
        <span class="k">Thumbprint</span>
        <span class="v mono">
          {{ selectedCert.thumbprintSha1 }}
          <Button icon="pi pi-copy" text size="small" v-tooltip="'Copy thumbprint'" @click="copyThumbprint" />
        </span>
        <span class="k">Expires</span>
        <span class="v">{{ formatDate(selectedCert.notAfter) }}</span>
        <template v-if="selectedCert.uploadedTo.length">
          <span class="k">Uploaded to</span>
          <span class="v">{{ selectedCert.uploadedTo.map(u => u.displayName || u.appId).join(', ') }}</span>
        </template>
      </div>

      <!-- ── Upload to App Registration ── -->
      <div class="upload-block">
        <div v-if="!uploadDone" class="upload-cta">
          <Button
            :label="uploading ? 'Uploading…' : 'Upload to App Registration'"
            icon="pi pi-cloud-upload"
            size="small"
            :disabled="!clientId || uploading"
            :loading="uploading"
            @click="upload"
          />
          <span class="hint">
            <template v-if="clientId">Adds the public key to the app registration via Microsoft Graph — the private key never leaves this machine.</template>
            <template v-else>Select a Client ID above to enable upload.</template>
          </span>
        </div>
        <StepProgress
          v-if="uploading || uploadSteps"
          :steps="uploadSteps"
          :busy="uploading"
          busy-label="Uploading to app registration…"
          @retry="upload"
        />

        <!-- ── Test token ── -->
        <div v-if="uploadDone || alreadyUploadedToClient" class="test-row">
          <Button
            :label="testing ? 'Testing…' : 'Test token acquisition'"
            icon="pi pi-bolt"
            size="small"
            severity="secondary"
            outlined
            :disabled="!canTestToken || testing"
            :loading="testing"
            @click="testToken"
          />
          <span class="hint">Dry-run with ClientCertificateCredential. New uploads can take 30–60s to propagate.</span>
        </div>
        <StepProgress
          v-if="testing || testSteps"
          :steps="testSteps"
          :busy="testing"
          busy-label="Acquiring token…"
          @retry="testToken"
        />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import Button from 'primevue/button'
import InputText from 'primevue/inputtext'
import Password from 'primevue/password'
import Select from 'primevue/select'
import Tag from 'primevue/tag'
import { useToast } from 'primevue/usetoast'
import StepProgress from './StepProgress.vue'
import { certificatesApi } from '@/api/certificates'
import { extractApiError } from '@/api/client'
import { useCertificatesStore } from '@/stores/certificates'
import type { CertificateInfo, CertificateReference, StepResult } from '@/api/types'

const NEW_CERT = '__new__'

const props = defineProps<{
  modelValue?: CertificateReference | null
  /** App registration (client) id — enables the upload button. */
  clientId?: string
  tenantId?: string
  scope?: string
  /** Used to derive default certificate name / subject CN. */
  defaultName?: string
}>()

const emit = defineEmits<{
  'update:modelValue': [value: CertificateReference | null]
}>()

const toast = useToast()
const store = useCertificatesStore()

const selection = ref<string>(props.modelValue?.certificateName || NEW_CERT)
const generatedCert = ref<CertificateInfo | null>(null)

const generating = ref(false)
const genSteps = ref<StepResult[] | null>(null)
const uploading = ref(false)
const uploadSteps = ref<StepResult[] | null>(null)
const uploadDone = ref(false)
const testing = ref(false)
const testSteps = ref<StepResult[] | null>(null)

const validityOptions = [
  { label: '6 months', value: 6 },
  { label: '12 months', value: 12 },
  { label: '24 months', value: 24 },
]

const genForm = reactive({
  name: slugify(props.defaultName || ''),
  subjectCn: '',
  keySize: 2048,
  validityMonths: 12,
  pfxPassword: '',
})

onMounted(() => {
  if (!store.initialized && !store.loading) void store.load()
})

watch(() => props.defaultName, (name) => {
  if (!genForm.name && name) genForm.name = slugify(name)
})

watch(() => props.modelValue, (value) => {
  selection.value = value?.certificateName || (generatedCert.value ? generatedCert.value.name : NEW_CERT)
})

const certificateOptions = computed(() => [
  { label: '＋ Create new certificate…', value: NEW_CERT },
  ...store.activeCertificates
    .filter(i => i.certificate.state === 'Active')
    .map(i => ({
      label: `${i.certificate.name} — expires ${formatDate(i.certificate.notAfter)}${expirySuffix(i.certificate)}`,
      value: i.certificate.name,
    })),
])

const selectedCert = computed<CertificateInfo | null>(() => {
  const name = props.modelValue?.certificateName
  if (!name) return null
  return store.items.find(i => i.certificate.name === name)?.certificate
    ?? (generatedCert.value?.name === name ? generatedCert.value : null)
})

const alreadyUploadedToClient = computed(() =>
  !!props.clientId && !!selectedCert.value?.uploadedTo.some(u => u.appId === props.clientId))

const canTestToken = computed(() =>
  !!props.tenantId?.trim() && !!props.clientId?.trim() && !!props.scope?.trim())

const expiryTag = computed(() => {
  const notAfter = selectedCert.value?.notAfter
  if (!notAfter) return null
  const days = Math.ceil((new Date(notAfter).getTime() - Date.now()) / 86_400_000)
  if (days <= 0) return { label: 'Expired', severity: 'danger' as const }
  if (days <= 30) return { label: `Expiring · ${days}d`, severity: 'warn' as const }
  return { label: `Valid · ${days}d`, severity: 'success' as const }
})

function slugify(value: string) {
  return value.toLowerCase().replace(/[^a-z0-9-]+/g, '-').replace(/-{2,}/g, '-').replace(/^-+|-+$/g, '')
}

function isValidName(name: string) {
  return /^[a-z0-9]([a-z0-9-]{0,62}[a-z0-9])?$/.test(name)
}

function formatDate(value?: string | null) {
  if (!value) return '—'
  return new Date(value).toLocaleDateString(undefined, { day: '2-digit', month: 'short', year: 'numeric' })
}

function expirySuffix(cert: CertificateInfo) {
  if (!cert.notAfter) return ''
  const days = Math.ceil((new Date(cert.notAfter).getTime() - Date.now()) / 86_400_000)
  return days <= 30 ? ` ⚠ ${days}d` : ' ✓'
}

function onSelectionChange() {
  resetOperationState()
  if (selection.value === NEW_CERT) {
    generatedCert.value = null
    emit('update:modelValue', null)
  }
  else {
    emit('update:modelValue', { certificateName: selection.value })
  }
}

function resetOperationState() {
  genSteps.value = null
  uploadSteps.value = null
  uploadDone.value = false
  testSteps.value = null
}

async function generate() {
  generating.value = true
  genSteps.value = null
  try {
    const result = await certificatesApi.generate({
      name: genForm.name,
      subjectCn: genForm.subjectCn || undefined,
      keySize: genForm.keySize,
      validityMonths: genForm.validityMonths,
      pfxPassword: genForm.pfxPassword || undefined,
    })
    genSteps.value = result.steps
    if (result.success && result.certificate) {
      generatedCert.value = result.certificate
      selection.value = result.certificate.name
      emit('update:modelValue', { certificateName: result.certificate.name })
      toast.add({ severity: 'success', summary: 'Certificate created', detail: `${result.certificate.name} · expires ${formatDate(result.certificate.notAfter)}`, life: 4200 })
      void store.load()
    }
  }
  catch (err) {
    toast.add({ severity: 'error', summary: 'Certificate generation failed', detail: extractApiError(err), life: 6000 })
  }
  finally {
    generating.value = false
  }
}

async function upload() {
  if (!props.clientId || !props.modelValue) return
  uploading.value = true
  uploadSteps.value = null
  try {
    const result = await certificatesApi.upload(props.modelValue.certificateName, props.clientId)
    uploadSteps.value = result.steps
    if (result.success) {
      uploadDone.value = true
      toast.add({ severity: 'success', summary: 'Certificate uploaded', detail: 'The app registration now trusts this certificate.', life: 4200 })
      void store.load()
    }
  }
  catch (err) {
    toast.add({ severity: 'error', summary: 'Upload failed', detail: extractApiError(err), life: 6000 })
  }
  finally {
    uploading.value = false
  }
}

async function testToken() {
  if (!props.modelValue || !canTestToken.value) return
  testing.value = true
  testSteps.value = null
  try {
    const result = await certificatesApi.testToken(
      props.modelValue.certificateName, props.tenantId!, props.clientId!, props.scope!)
    testSteps.value = result.steps
  }
  catch (err) {
    toast.add({ severity: 'error', summary: 'Token test failed', detail: extractApiError(err), life: 6000 })
  }
  finally {
    testing.value = false
  }
}

async function copyThumbprint() {
  if (!selectedCert.value?.thumbprintSha1) return
  await navigator.clipboard.writeText(selectedCert.value.thumbprintSha1)
  toast.add({ severity: 'info', summary: 'Copied', detail: 'Thumbprint copied to clipboard', life: 2000 })
}
</script>

<style scoped>
.cert-panel {
  display: flex;
  flex-direction: column;
  gap: 12px;
  border: 1px solid var(--border);
  border-radius: var(--border-radius-md);
  background: var(--bg-overlay);
  padding: 12px;
}

.picker-row {
  display: flex;
  align-items: flex-end;
  gap: 10px;
}

.grow { flex: 1; }

.open-link {
  font-size: 12px;
  color: var(--accent);
  text-decoration: none;
  white-space: nowrap;
  padding-bottom: 8px;
  display: inline-flex;
  align-items: center;
  gap: 5px;
}
.open-link:hover { text-decoration: underline; }
.open-link .pi { font-size: 10px; }

.field label {
  display: block;
  font-size: 11.5px;
  font-weight: 600;
  color: var(--text-secondary);
  margin-bottom: 4px;
}

.field .opt { font-weight: 400; color: var(--text-muted); }
.field .req { color: var(--danger); }

.field-error {
  color: var(--danger);
  font-size: 11px;
}

.gen-form {
  border-top: 1px dashed var(--border);
  padding-top: 12px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.gen-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 10px 12px;
}

.span-2 { grid-column: 1 / -1; }

.gen-actions {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
}

.hint {
  font-size: 11.5px;
  color: var(--text-muted);
}

.mono { font-family: var(--font-family-mono); }

.cert-summary {
  border: 1px solid color-mix(in srgb, var(--success) 35%, transparent);
  background: color-mix(in srgb, var(--success) 6%, transparent);
  border-radius: var(--border-radius-md);
  padding: 10px 12px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.summary-head {
  display: flex;
  align-items: center;
  gap: 8px;
  font-weight: 600;
  font-size: 12.5px;
}

.summary-head .pi-verified { color: var(--success); }

.summary-grid {
  display: grid;
  grid-template-columns: 92px 1fr;
  gap: 3px 10px;
  font-size: 12px;
}

.summary-grid .k { color: var(--text-muted); }
.summary-grid .v { color: var(--text-primary); overflow-wrap: anywhere; display: inline-flex; align-items: center; gap: 2px; }
.summary-grid .v.mono { font-size: 11px; }

.upload-block {
  border-top: 1px dashed var(--border);
  padding-top: 10px;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.upload-cta,
.test-row {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
}
</style>
