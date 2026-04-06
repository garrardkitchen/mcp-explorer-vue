<script setup lang="ts">
import { ref, onMounted } from 'vue'
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
import type { LlmModelDefinition } from '@/api/types'

const toast = useToast()
const confirm = useConfirm()

const models = ref<LlmModelDefinition[]>([])
const loading = ref(false)
const selectedModelName = ref<string | null>(null)
const showDialog = ref(false)
const editMode = ref(false)
const saving = ref(false)
const showApiKey = ref<Record<string, boolean>>({})

const blankForm = (): LlmModelDefinition => ({
  name: '', providerType: 'OpenAI', endpoint: '', apiKey: '',
  modelName: '', systemPrompt: '', deploymentName: '', note: '',
})
const form = ref<LlmModelDefinition>(blankForm())
const originalName = ref('')

const providerOptions = [
  { label: 'OpenAI', value: 'OpenAI' },
  { label: 'Azure OpenAI', value: 'AzureOpenAI' },
  { label: 'Azure AI Foundry', value: 'AzureAIFoundry' },
  { label: 'Ollama', value: 'Ollama' },
  { label: 'Custom', value: 'Custom' },
]

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
  form.value = blankForm(); showDialog.value = true
}

function openEdit(m: LlmModelDefinition) {
  editMode.value = true; originalName.value = m.name
  form.value = JSON.parse(JSON.stringify(m)); showDialog.value = true
}

async function save() {
  if (!form.value.name?.trim()) {
    toast.add({ severity: 'warn', summary: 'Validation', detail: 'Name is required', life: 3000 }); return
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

function toggleKey(name: string) {
  showApiKey.value[name] = !showApiKey.value[name]
}

function maskKey(k: string) {
  if (!k) return '—'
  return k.length > 8 ? `${k.slice(0, 4)}${'•'.repeat(Math.min(k.length - 8, 12))}${k.slice(-4)}` : '••••••••'
}

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
            <span class="model-id">{{ m.modelName || m.deploymentName }}</span>
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
          <div v-if="m.note" class="card-field">
            <span class="field-label">Note</span>
            <span class="field-value">{{ m.note }}</span>
          </div>
        </div>
        <div class="card-actions">
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
        <div class="form-field">
          <label>Model Name</label>
          <InputText v-model="form.modelName" placeholder="gpt-4o" class="w-full" />
        </div>
        <div v-if="form.providerType === 'AzureOpenAI' || form.providerType === 'AzureAIFoundry'" class="form-field">
          <label>Deployment Name</label>
          <InputText v-model="form.deploymentName" placeholder="my-deployment" class="w-full" />
        </div>
        <div class="form-field">
          <label>Endpoint / Base URL</label>
          <InputText v-model="form.endpoint" placeholder="https://api.openai.com/v1" class="w-full" />
        </div>
        <div class="form-field">
          <label>API Key</label>
          <Password v-model="form.apiKey" placeholder="sk-…" :feedback="false" toggleMask class="w-full" inputClass="w-full" />
        </div>
        <div class="form-field full-width">
          <label>System Prompt</label>
          <Textarea v-model="form.systemPrompt" rows="3" class="w-full" autoResize placeholder="You are a helpful assistant…" />
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
.w-full { width:100%; }
</style>
