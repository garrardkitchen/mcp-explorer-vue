<script setup lang="ts">
import { computed, watch } from 'vue'
import SelectButton from 'primevue/selectbutton'
import Password from 'primevue/password'
import KeyVaultSecretPicker from '@/components/connections/KeyVaultSecretPicker.vue'
import CertificateCredentialPanel from '@/components/certificates/CertificateCredentialPanel.vue'
import type { CertificateReference, KeyVaultSecretReference } from '@/api/types'
import type { AzureCredentialSource } from '@/components/connections/azureCredentialSource'

const props = withDefaults(defineProps<{
  /** Active credential source — owned by parent so empty KV/cert mid-edit stays selected. */
  source: AzureCredentialSource
  clientSecret?: string | null
  keyVaultSecretRef?: KeyVaultSecretReference | null
  certificateRef?: CertificateReference | null
  subscriptionId?: string
  /** When false, only Paste | Key Vault (OAuth optional secret). Default true. */
  allowCertificate?: boolean
  /** Soften copy when the secret itself is optional (OAuth). */
  secretOptional?: boolean
  clientId?: string
  tenantId?: string
  scope?: string
  defaultName?: string
}>(), {
  allowCertificate: true,
  secretOptional: false,
  clientSecret: '',
})

const emit = defineEmits<{
  'update:source': [value: AzureCredentialSource]
  'update:clientSecret': [value: string]
  'update:keyVaultSecretRef': [value: KeyVaultSecretReference | null | undefined]
  'update:certificateRef': [value: CertificateReference | null]
}>()

const sourceOptions = computed(() => {
  const options: { label: string; value: AzureCredentialSource }[] = [
    { label: 'Paste secret', value: 'paste' },
    { label: 'Key Vault', value: 'keyVault' },
  ]
  if (props.allowCertificate)
    options.push({ label: 'Certificate', value: 'certificate' })
  return options
})

const sourceModel = computed({
  get: () => props.source,
  set: (value: AzureCredentialSource) => emit('update:source', value),
})

const certificateModel = computed<CertificateReference | null>({
  get: () => props.certificateRef ?? null,
  set: (value) => emit('update:certificateRef', value),
})

const keyVaultModel = computed<KeyVaultSecretReference | null | undefined>({
  get: () => props.keyVaultSecretRef,
  set: (value) => emit('update:keyVaultSecretRef', value ?? undefined),
})

const clientSecretModel = computed({
  get: () => props.clientSecret ?? '',
  set: (value: string) => emit('update:clientSecret', value),
})

/** Clear inactive sources whenever the user switches the SelectButton. */
watch(
  () => props.source,
  (next, prev) => {
    if (prev === undefined || next === prev)
      return
    if (next === 'paste') {
      emit('update:keyVaultSecretRef', undefined)
      if (props.allowCertificate)
        emit('update:certificateRef', null)
    }
    else if (next === 'keyVault') {
      emit('update:clientSecret', '')
      if (props.allowCertificate)
        emit('update:certificateRef', null)
    }
    else if (next === 'certificate') {
      emit('update:clientSecret', '')
      emit('update:keyVaultSecretRef', undefined)
    }
  },
)

</script>

<template>
  <div class="credential-source-panel">
    <div class="csp-field">
      <label>Credential source</label>
      <SelectButton
        v-model="sourceModel"
        :options="sourceOptions"
        optionLabel="label"
        optionValue="value"
        :allowEmpty="false"
        class="csp-select"
      />
    </div>

    <div v-if="source === 'paste'" class="csp-field">
      <label>
        Client Secret
        <span v-if="secretOptional" class="optional">(optional)</span>
      </label>
      <Password
        v-model="clientSecretModel"
        :feedback="false"
        toggleMask
        class="w-full"
        inputClass="w-full"
      />
      <small class="csp-helper">
        <template v-if="secretOptional">
          Stored encrypted with the connection when provided. Prefer Key Vault for production.
        </template>
        <template v-else>
          Stored encrypted with the connection. Prefer Key Vault or a certificate for production.
        </template>
      </small>
    </div>

    <div v-else-if="source === 'keyVault'" class="csp-field">
      <label>Key Vault secret</label>
      <KeyVaultSecretPicker
        v-model="keyVaultModel"
        :subscriptionId="subscriptionId"
      />
      <small class="csp-helper">
        Only the vault and secret names are stored. The secret value is resolved at connect time via DefaultAzureCredential.
      </small>
    </div>

    <div v-else-if="source === 'certificate' && allowCertificate" class="csp-field csp-cert">
      <CertificateCredentialPanel
        v-model="certificateModel"
        :client-id="clientId"
        :tenant-id="tenantId"
        :scope="scope"
        :default-name="defaultName"
      />
    </div>
  </div>
</template>

<style scoped>
.credential-source-panel {
  display: flex;
  flex-direction: column;
  gap: 12px;
  width: 100%;
}
.csp-field {
  display: flex;
  flex-direction: column;
  gap: 6px;
  min-width: 0;
}
.csp-field > label {
  font-size: 12px;
  font-weight: 500;
  color: var(--text-secondary);
  text-transform: uppercase;
  letter-spacing: .04em;
}
.optional {
  text-transform: none;
  font-weight: 400;
  color: var(--text-muted);
}
.csp-helper {
  color: var(--text-muted);
  font-size: 11px;
  line-height: 1.4;
}
.csp-select {
  flex-wrap: wrap;
}
.csp-cert {
  width: 100%;
}
.w-full {
  width: 100%;
}
</style>
