<script setup lang="ts">
import { computed, ref, onMounted, watch } from 'vue'
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
import ConfirmDialog from 'primevue/confirmdialog'
import { llmModelsApi } from '@/api/llmModels'
import { extractApiError } from '@/api/client'
import type { FoundryAgentCatalogItem, LlmModelDefinition } from '@/api/types'

const toast = useToast()
const confirm = useConfirm()

const models = ref<LlmModelDefinition[]>([])
const loading = ref(false)
const selectedModelName = ref<string | null>(null)
const showDialog = ref(false)
const editMode = ref(false)
const saving = ref(false)
const testing = ref<Record<string, boolean>>({})
const showApiKey = ref<Record<string, boolean>>({})
const discoveringAgents = ref(false)
const discoveredAgents = ref<FoundryAgentCatalogItem[]>([])
const discoveryAttempted = ref(false)

const blankForm = (): LlmModelDefinition => ({
  name: '', providerType: 'OpenAI', endpoint: '', apiKey: '',
  modelName: '', systemPrompt: '', deploymentName: '',
  authenticationMode: 'DefaultAzureCredential', agentInvocationMode: 'VersionedAgent',
  agentName: '', agentVersion: '', note: '',
})
const form = ref<LlmModelDefinition>(blankForm())
const originalName = ref('')

const foundryAgentOptions = computed(() => discoveredAgents.value.map(agent => ({
  label: agent.name,
  value: agent.name,
})))

const selectedFoundryAgent = computed(() => discoveredAgents.value.find(agent =>
  agent.name === form.value.agentName))

const foundryVersionOptions = computed(() => (selectedFoundryAgent.value?.versions ?? []).map(version => ({
  label: version.description ? `v${version.version} — ${version.description}` : `v${version.version}`,
  value: version.version,
})))

const providerOptions = [
  { label: 'OpenAI', value: 'OpenAI' },
  { label: 'Azure OpenAI', value: 'AzureOpenAI' },
  { label: 'Azure AI Foundry Model', value: 'AzureAIFoundry' },
  { label: 'Azure AI Foundry Project Agent', value: 'AzureAIFoundryProject' },
  { label: 'Ollama', value: 'Ollama' },
  { label: 'Custom', value: 'Custom' },
]

const authenticationOptions = [
  { label: 'Default Azure Credential', value: 'DefaultAzureCredential' },
  { label: 'API Key', value: 'ApiKey' },
]

const agentInvocationOptions = [
  { label: 'Versioned agent', value: 'VersionedAgent' },
  { label: 'Hosted agent endpoint', value: 'HostedAgentEndpoint' },
]

function isFoundryProject(model: LlmModelDefinition) {
  return model.providerType === 'AzureAIFoundryProject'
}

function isHostedAgent(model: LlmModelDefinition) {
  return isFoundryProject(model) && model.agentInvocationMode === 'HostedAgentEndpoint'
}

async function load() {
  loading.value = true
  try {
    models.value = await llmModelsApi.getAll()
    const sel = await llmModelsApi.getSelected()
    selectedModelName.value = sel.selectedModelName
  } catch (e: any) {
    toast.add({ severity: 'error', summary: 'Failed to load models', detail: e.message, life: 5000 })
  } finally { loading.value = false }
}

function openCreate() {
  editMode.value = false; originalName.value = ''
  form.value = blankForm(); resetFoundryDiscovery(); showDialog.value = true
}

function openEdit(m: LlmModelDefinition) {
  editMode.value = true; originalName.value = m.name
  form.value = JSON.parse(JSON.stringify(m)); resetFoundryDiscovery(); showDialog.value = true
}

function resetFoundryDiscovery() {
  discoveredAgents.value = []
  discoveryAttempted.value = false
}

function applyFoundryVersion(version: string | null | undefined) {
  if (isHostedAgent(form.value)) {
    form.value.agentVersion = ''
    form.value.systemPrompt = ''
    return
  }

  form.value.agentVersion = version ?? ''
  const selectedVersion = selectedFoundryAgent.value?.versions.find(item => item.version === version)
  form.value.systemPrompt = selectedVersion?.systemPrompt ?? ''
}

function selectFoundryAgent(agentName: string | null | undefined) {
  form.value.agentName = agentName ?? ''
  const agent = discoveredAgents.value.find(item => item.name === agentName)
  applyFoundryVersion(isHostedAgent(form.value) ? '' : agent?.versions[0]?.version)
}

async function discoverFoundryAgents() {
  if (!form.value.endpoint?.trim()) {
    toast.add({ severity: 'warn', summary: 'Project endpoint required', detail: 'Enter the Foundry project endpoint before refreshing agents.', life: 4000 })
    return
  }
  if (form.value.authenticationMode === 'ApiKey' && !form.value.apiKey?.trim()) {
    toast.add({ severity: 'warn', summary: 'API key required', detail: 'Enter the project API key before refreshing agents.', life: 4000 })
    return
  }

  discoveringAgents.value = true
  try {
    const currentAgentName = form.value.agentName
    const currentVersion = form.value.agentVersion
    discoveredAgents.value = await llmModelsApi.discoverFoundryAgents(form.value)
    discoveryAttempted.value = true

    if (discoveredAgents.value.length === 0) {
      form.value.agentName = ''
      applyFoundryVersion('')
      toast.add({ severity: 'info', summary: 'No agents found', detail: 'The project returned no available agents.', life: 4000 })
      return
    }

    const selectedAgent = discoveredAgents.value.find(agent => agent.name === currentAgentName)
      ?? discoveredAgents.value[0]
    form.value.agentName = selectedAgent.name
    if (isHostedAgent(form.value)) {
      applyFoundryVersion('')
    } else {
      const selectedVersion = selectedAgent.versions.find(version => version.version === currentVersion)
        ?? selectedAgent.versions[0]
      applyFoundryVersion(selectedVersion?.version)
    }

    toast.add({
      severity: 'success',
      summary: 'Agents refreshed',
      detail: `Discovered ${discoveredAgents.value.length} agent${discoveredAgents.value.length === 1 ? '' : 's'}.`,
      life: 2500,
    })
  } catch (e: unknown) {
    toast.add({ severity: 'error', summary: 'Agent discovery failed', detail: extractApiError(e), life: 7000 })
  } finally {
    discoveringAgents.value = false
  }
}

async function save() {
  if (!form.value.name?.trim()) {
    toast.add({ severity: 'warn', summary: 'Validation', detail: 'Name is required', life: 3000 }); return
  }
  if (isFoundryProject(form.value)) {
    const missingRequiredField = !form.value.endpoint?.trim()
      || !form.value.agentName?.trim()
      || (!isHostedAgent(form.value) && !form.value.agentVersion?.trim())
    if (missingRequiredField) {
      const required = isHostedAgent(form.value)
        ? 'Project endpoint and agent name are required'
        : 'Project endpoint, agent name, and agent version are required'
      toast.add({ severity: 'warn', summary: 'Validation', detail: required, life: 4000 }); return
    }
    if (form.value.authenticationMode === 'ApiKey' && !form.value.apiKey?.trim()) {
      toast.add({ severity: 'warn', summary: 'Validation', detail: 'API key is required for API key authentication', life: 4000 }); return
    }
    if (form.value.authenticationMode === 'DefaultAzureCredential') form.value.apiKey = ''
  }
  saving.value = true
  try {
    if (editMode.value) {
      await llmModelsApi.update(originalName.value, form.value)
      toast.add({ severity: 'success', summary: 'Updated', life: 2000 })
    } else {
      await llmModelsApi.create(form.value)
      toast.add({ severity: 'success', summary: 'Created', life: 2000 })
    }
    showDialog.value = false; await load()
  } catch (e: any) {
    toast.add({ severity: 'error', summary: 'Save failed', detail: e.message, life: 5000 })
  } finally { saving.value = false }
}

function confirmDelete(m: LlmModelDefinition) {
  confirm.require({
    message: `Delete model "${m.name}"?`,
    header: 'Confirm Delete', icon: 'pi pi-trash',
    rejectProps: { label: 'Cancel', severity: 'secondary' },
    acceptProps: { label: 'Delete', severity: 'danger' },
    accept: async () => {
      try { await llmModelsApi.delete(m.name); await load(); toast.add({ severity: 'success', summary: 'Deleted', life: 2000 }) }
      catch (e: any) { toast.add({ severity: 'error', summary: 'Delete failed', detail: e.message, life: 5000 }) }
    },
  })
}

async function setDefault(m: LlmModelDefinition) {
  try {
    await llmModelsApi.setSelected(m.name)
    selectedModelName.value = m.name
    toast.add({ severity: 'success', summary: `"${m.name}" set as default`, life: 2000 })
  } catch (e: any) {
    toast.add({ severity: 'error', summary: 'Failed', detail: e.message, life: 5000 })
  }
}

async function testModel(m: LlmModelDefinition) {
  testing.value[m.name] = true
  try {
    const result = await llmModelsApi.test(m.name)
    toast.add({ severity: 'success', summary: 'Connection succeeded', detail: result.message, life: 5000 })
  } catch (e: unknown) {
    toast.add({ severity: 'error', summary: 'Connection failed', detail: extractApiError(e), life: 7000 })
  } finally {
    testing.value[m.name] = false
  }
}

function toggleKey(name: string) {
  showApiKey.value[name] = !showApiKey.value[name]
}

function maskKey(k: string) {
  if (!k) return '—'
  return k.length > 8 ? `${k.slice(0, 4)}${'•'.repeat(Math.min(k.length - 8, 12))}${k.slice(-4)}` : '••••••••'
}

watch(
  () => [form.value.providerType, form.value.endpoint, form.value.authenticationMode, form.value.apiKey],
  () => resetFoundryDiscovery(),
)

watch(
  () => form.value.agentInvocationMode,
  mode => {
    resetFoundryDiscovery()
    if (mode === 'HostedAgentEndpoint') {
      form.value.agentVersion = ''
      form.value.systemPrompt = ''
    }
  },
)

onMounted(load)
</script>

<template>
  <div class="ai-models-view">
    <div class="toolbar">
      <span class="toolbar-title">AI Models</span>
      <Button label="Add Model" icon="pi pi-plus" size="small" @click="openCreate" />
    </div>

    <div class="models-grid" v-if="!loading">
      <div v-if="models.length === 0" class="empty-state">
        <i class="pi pi-microchip-ai" /><p>No models configured yet</p>
        <Button label="Add your first model" @click="openCreate" />
      </div>
      <div v-for="m in models" :key="m.name" class="model-card" :class="{ selected: selectedModelName === m.name }">
        <div class="card-header">
          <div class="card-title-row">
            <span class="model-name">{{ m.name }}</span>
            <Tag v-if="selectedModelName === m.name" value="Default" severity="success" />
          </div>
          <div class="card-badges">
            <Tag :value="m.providerType" severity="secondary" />
            <span class="model-id">
              {{ isFoundryProject(m)
                ? (isHostedAgent(m) ? `${m.agentName} (hosted endpoint)` : `${m.agentName} v${m.agentVersion}`)
                : (m.modelName || m.deploymentName) }}
            </span>
          </div>
        </div>
        <div class="card-body">
          <div v-if="m.endpoint" class="card-field">
            <span class="field-label">Endpoint</span>
            <span class="field-value mono">{{ m.endpoint }}</span>
          </div>
          <div v-if="m.apiKey" class="card-field">
            <span class="field-label">API Key</span>
            <span class="field-value mono">
              {{ showApiKey[m.name] ? m.apiKey : maskKey(m.apiKey) }}
            </span>
            <button class="reveal-btn" @click="toggleKey(m.name)">
              <i :class="showApiKey[m.name] ? 'pi pi-eye-slash' : 'pi pi-eye'" />
            </button>
          </div>
          <div v-if="isFoundryProject(m)" class="card-field">
            <span class="field-label">Auth</span>
            <span class="field-value">{{ m.authenticationMode === 'ApiKey' ? 'API Key' : 'Default Azure Credential' }}</span>
          </div>
          <div v-if="m.note" class="card-field">
            <span class="field-label">Note</span>
            <span class="field-value">{{ m.note }}</span>
          </div>
        </div>
        <div class="card-actions">
          <Button label="Test" icon="pi pi-bolt" text size="small"
                  :loading="testing[m.name]" v-tooltip="'Sends a small live probe to this model or agent'"
                  @click="testModel(m)" />
          <Button label="Set Default" icon="pi pi-star" text size="small"
                  :severity="selectedModelName === m.name ? 'success' : 'secondary'"
                  @click="setDefault(m)" />
          <Button icon="pi pi-pencil" text size="small" v-tooltip="'Edit'" @click="openEdit(m)" />
          <Button icon="pi pi-trash" text size="small" severity="danger" v-tooltip="'Delete'" @click="confirmDelete(m)" />
        </div>
      </div>
    </div>
    <div v-else class="p-4"><Skeleton v-for="i in 3" :key="i" height="120px" class="mb-3" /></div>

    <Dialog v-model:visible="showDialog" :header="editMode ? 'Edit Model' : 'Add Model'" modal :style="{ width: '600px' }">
      <div class="form-grid">
        <div class="form-field">
          <label>Name *</label>
          <InputText v-model="form.name" placeholder="my-gpt4" class="w-full" />
        </div>
        <div class="form-field">
          <label>Provider</label>
          <Select v-model="form.providerType" :options="providerOptions" optionLabel="label" optionValue="value" class="w-full" />
        </div>
        <div v-if="!isFoundryProject(form)" class="form-field">
          <label>Model Name</label>
          <InputText v-model="form.modelName" placeholder="gpt-4o" class="w-full" />
        </div>
        <div v-if="form.providerType === 'AzureOpenAI' || form.providerType === 'AzureAIFoundry'" class="form-field">
          <label>Deployment Name</label>
          <InputText v-model="form.deploymentName" placeholder="my-deployment" class="w-full" />
        </div>
        <div v-if="isFoundryProject(form)" class="form-field">
          <label>Authentication</label>
          <Select v-model="form.authenticationMode" :options="authenticationOptions" optionLabel="label" optionValue="value" class="w-full" />
        </div>
        <div v-if="isFoundryProject(form)" class="form-field">
          <label>Agent Invocation</label>
          <Select v-model="form.agentInvocationMode" :options="agentInvocationOptions" optionLabel="label" optionValue="value" class="w-full" />
        </div>
        <div v-if="isHostedAgent(form)" class="credential-hint full-width">
          Calls the dedicated <code>/agents/{agentName}/endpoint/protocols/openai/responses</code> endpoint.
          Foundry's endpoint configuration controls which hosted agent version receives traffic.
        </div>
        <div class="form-field">
          <label>{{ isFoundryProject(form) ? 'Project Endpoint *' : 'Endpoint / Base URL' }}</label>
          <InputText v-model="form.endpoint"
                     :placeholder="isFoundryProject(form) ? 'https://resource.services.ai.azure.com/api/projects/project' : 'https://api.openai.com/v1'"
                     class="w-full" />
        </div>
        <div v-if="!isFoundryProject(form) || form.authenticationMode === 'ApiKey'" class="form-field">
          <label>API Key{{ isFoundryProject(form) ? ' *' : '' }}</label>
          <Password v-model="form.apiKey" placeholder="sk-…" :feedback="false" toggleMask class="w-full" inputClass="w-full" />
        </div>
        <div v-if="isFoundryProject(form)" class="form-field full-width">
          <label>Agent Name *</label>
          <div class="input-with-action">
            <Select
              :modelValue="form.agentName"
              :options="foundryAgentOptions"
              optionLabel="label"
              optionValue="value"
              :placeholder="discoveryAttempted ? 'Select an agent' : 'Refresh to discover agents'"
              editable
              :disabled="discoveringAgents"
              class="w-full"
              @update:modelValue="selectFoundryAgent"
            />
            <Button
              icon="pi pi-refresh"
              label="Refresh"
              severity="secondary"
              :loading="discoveringAgents"
              @click="discoverFoundryAgents"
            />
          </div>
        </div>
        <div v-if="isFoundryProject(form) && !isHostedAgent(form)" class="form-field">
          <label>Agent Version *</label>
          <Select
            :modelValue="form.agentVersion"
            :options="foundryVersionOptions"
            optionLabel="label"
            optionValue="value"
            placeholder="Select a version"
            editable
            :disabled="discoveringAgents"
            class="w-full"
            @update:modelValue="applyFoundryVersion"
          />
        </div>
        <div v-if="isFoundryProject(form) && form.authenticationMode === 'DefaultAzureCredential'" class="credential-hint full-width">
          Uses the process identity when hosted, or your Azure CLI/developer credential locally. No credential is stored.
        </div>
        <div class="form-field full-width">
          <label>System Prompt</label>
          <Textarea
            v-model="form.systemPrompt"
            rows="3"
            class="w-full"
            autoResize
            :readonly="isFoundryProject(form)"
            :placeholder="isHostedAgent(form)
              ? 'Instructions are managed by the hosted agent implementation.'
              : (isFoundryProject(form) ? 'No instructions are defined for this agent version.' : 'You are a helpful assistant…')"
          />
        </div>
        <div v-if="isFoundryProject(form)" class="credential-hint full-width">
          <template v-if="isHostedAgent(form)">
            Instructions and tools are controlled by the hosted agent implementation and its Foundry endpoint configuration.
          </template>
          <template v-else>
            The system prompt is loaded from the selected immutable Foundry agent version and is read-only here.
            Instructions and tools remain controlled by Foundry.
          </template>
        </div>
        <div class="form-field full-width">
          <label>Note</label>
          <InputText v-model="form.note" class="w-full" />
        </div>
      </div>
      <template #footer>
        <Button label="Cancel" severity="secondary" text @click="showDialog = false" />
        <Button :label="editMode ? 'Save' : 'Add'" icon="pi pi-check" :loading="saving" @click="save" />
      </template>
    </Dialog>
    <ConfirmDialog />
  </div>
</template>

<style scoped>
.ai-models-view { display:flex; flex-direction:column; height:100%; background:var(--bg-base); color:var(--text-primary); }
.toolbar { display:flex; align-items:center; justify-content:space-between; padding:12px 20px; background:var(--bg-surface); border-bottom:1px solid var(--border); flex-shrink:0; }
.toolbar-title { font-weight:600; font-size:15px; }
.models-grid { display:grid; grid-template-columns:repeat(auto-fill,minmax(320px,1fr)); gap:16px; padding:20px; overflow-y:auto; }
.empty-state { grid-column:1/-1; display:flex; flex-direction:column; align-items:center; gap:12px; padding:60px; color:var(--text-muted); }
.empty-state i { font-size:48px; }
.model-card { background:var(--bg-surface); border:1px solid var(--border); border-radius:var(--border-radius-md); overflow:hidden; transition:var(--transition-fast); display:flex; flex-direction:column; }
.model-card:hover { border-color:var(--accent); }
.model-card.selected { border-color:var(--success); }
.card-header { padding:14px 16px 10px; border-bottom:1px solid var(--border); }
.card-title-row { display:flex; align-items:center; justify-content:space-between; margin-bottom:6px; }
.model-name { font-size:14px; font-weight:600; color:var(--text-primary); }
.card-badges { display:flex; align-items:center; gap:8px; }
.model-id { font-family:var(--font-family-mono); font-size:12px; color:var(--text-muted); }
.card-body { padding:12px 16px; flex:1; display:flex; flex-direction:column; gap:6px; }
.card-field { display:flex; align-items:center; gap:6px; font-size:12px; flex-wrap:wrap; }
.field-label { color:var(--text-muted); min-width:60px; font-size:11px; text-transform:uppercase; }
.field-value { color:var(--text-secondary); flex:1; overflow:hidden; text-overflow:ellipsis; white-space:nowrap; }
.field-value.mono { font-family:var(--font-family-mono); }
.reveal-btn { background:none; border:none; cursor:pointer; color:var(--text-muted); padding:2px 4px; }
.reveal-btn:hover { color:var(--text-primary); }
.card-actions { display:flex; align-items:center; gap:4px; padding:8px 12px; border-top:1px solid var(--border); }
/* Dialog */
.form-grid { display:grid; grid-template-columns:1fr 1fr; gap:14px; }
.form-field { display:flex; flex-direction:column; gap:6px; }
.form-field label { font-size:12px; font-weight:500; color:var(--text-secondary); text-transform:uppercase; letter-spacing:.04em; }
.full-width { grid-column:1/-1; }
.credential-hint { font-size:12px; color:var(--text-muted); line-height:1.45; }
.input-with-action { display:flex; align-items:center; gap:8px; }
.w-full { width:100%; }
</style>
