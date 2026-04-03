<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useToast } from 'primevue/usetoast'
import { useRouter } from 'vue-router'
import Splitter from 'primevue/splitter'
import SplitterPanel from 'primevue/splitterpanel'
import Button from 'primevue/button'
import InputText from 'primevue/inputtext'
import Skeleton from 'primevue/skeleton'
import Tag from 'primevue/tag'
import Dialog from 'primevue/dialog'
import Select from 'primevue/select'
import JsonViewer from '@/components/common/JsonViewer.vue'
import { useConnectionsStore } from '@/stores/connections'
import { connectionsApi } from '@/api/connections'
import { llmModelsApi } from '@/api/llmModels'
import { chatApi } from '@/api/chat'
import { useChatStore } from '@/stores/chat'
import type { ActivePrompt, LlmModelDefinition } from '@/api/types'

const toast = useToast()
const router = useRouter()
const store = useConnectionsStore()
const chatStore = useChatStore()

const selectedConnName = ref<string | null>(null)
const prompts = ref<ActivePrompt[]>([])
const promptsLoading = ref(false)
const promptSearch = ref('')
const selectedPrompt = ref<ActivePrompt | null>(null)
const args = ref<Record<string, string>>({})
const executing = ref(false)
const result = ref<unknown>(null)
const resultError = ref<string | null>(null)

// Send to LLM dialog
const showLlmDialog = ref(false)
const llmModels = ref<LlmModelDefinition[]>([])
const selectedModel = ref<string | null>(null)
const sendingToLlm = ref(false)

const connectedConnections = computed(() => store.activeConnections.filter(c => c.isConnected))

const filteredPrompts = computed(() => {
  const q = promptSearch.value.toLowerCase()
  if (!q) return prompts.value
  return prompts.value.filter(p => p.name.toLowerCase().includes(q) || p.description?.toLowerCase().includes(q))
})

watch(selectedConnName, async (name) => {
  selectedPrompt.value = null; prompts.value = []; result.value = null
  if (!name) return
  promptsLoading.value = true
  try { prompts.value = await store.getPrompts(name) }
  catch (e: any) { toast.add({ severity: 'error', summary: 'Failed to load prompts', detail: e.message, life: 5000 }) }
  finally { promptsLoading.value = false }
})

watch(selectedPrompt, (p) => { args.value = {}; result.value = null; resultError.value = null })

function selectPrompt(p: ActivePrompt) { selectedPrompt.value = p }

async function execute() {
  if (!selectedConnName.value || !selectedPrompt.value) return
  executing.value = true; result.value = null; resultError.value = null
  try {
    const r = await connectionsApi.executePrompt(selectedConnName.value, selectedPrompt.value.name, args.value)
    result.value = r
    toast.add({ severity: 'success', summary: 'Prompt executed', life: 2000 })
  } catch (e: any) {
    resultError.value = e.message
    toast.add({ severity: 'error', summary: 'Execution failed', detail: e.message, life: 5000 })
  } finally { executing.value = false }
}

async function openSendToLlm() {
  llmModels.value = await llmModelsApi.getAll()
  const sel = await llmModelsApi.getSelected()
  selectedModel.value = sel.selectedModelName
  showLlmDialog.value = true
}

async function sendToLlm() {
  if (!selectedModel.value || !result.value) return
  sendingToLlm.value = true
  try {
    const session = await chatApi.createSession()
    await chatStore.loadSessions()
    await chatStore.selectSession(session.id)
    // Send the prompt result as the first user message
    const content = typeof result.value === 'string' ? result.value : JSON.stringify(result.value, null, 2)
    await chatStore.sendMessage(`Prompt result:\n\n${content}`, selectedModel.value, [])
    showLlmDialog.value = false
    toast.add({ severity: 'success', summary: 'Sent to LLM', detail: 'Navigating to Chat…', life: 3000 })
    await router.push('/chat')
  } catch (e: any) {
    toast.add({ severity: 'error', summary: 'Failed', detail: e.message, life: 5000 })
  } finally { sendingToLlm.value = false }
}

// Load active connections when store initialises (covers both: view mounts after init,
// and view mounts before init completes). immediate:true fires on mount if already ready.
watch(() => store.initialized, (ready, wasReady) => { if (ready && !wasReady) store.loadActive() }, { immediate: true })
</script>

<template>
  <div class="prompts-view">
    <Splitter class="splitter">
      <SplitterPanel :size="20" :minSize="15">
        <div class="panel">
          <div class="panel-header">Connections</div>
          <div v-if="connectedConnections.length === 0" class="empty-panel">
            <i class="pi pi-server" /><p>No active connections</p>
          </div>
          <div v-else class="item-list">
            <div v-for="c in connectedConnections" :key="c.name" class="list-item"
                 :class="{ active: selectedConnName === c.name }" @click="selectedConnName = c.name">
              <i class="pi pi-circle-fill dot" /><span>{{ c.name }}</span>
            </div>
          </div>
        </div>
      </SplitterPanel>

      <SplitterPanel :size="35" :minSize="25">
        <div class="panel">
          <div class="panel-header">
            Prompts
            <Tag v-if="prompts.length" :value="String(prompts.length)" severity="secondary" />
          </div>
          <div class="panel-search">
            <InputText v-model="promptSearch" placeholder="Filter prompts…" style="width:100%" />
          </div>
          <div v-if="promptsLoading" class="p-3"><Skeleton v-for="i in 4" :key="i" height="40px" class="mb-2" /></div>
          <div v-else-if="!selectedConnName" class="empty-panel"><p>Select a connection</p></div>
          <div v-else-if="filteredPrompts.length === 0" class="empty-panel"><p>No prompts found</p></div>
          <div v-else class="item-list">
            <div v-for="p in filteredPrompts" :key="p.name" class="list-item prompt-item"
                 :class="{ active: selectedPrompt?.name === p.name }" @click="selectPrompt(p)">
              <div class="item-body-icon">
                <img v-if="p.iconUrl" :src="p.iconUrl" :alt="p.name" class="item-icon-img" />
                <span v-else class="item-icon-badge">{{ p.name.charAt(0).toUpperCase() }}</span>
              </div>
              <div class="item-body-text">
                <span class="item-name">{{ p.name }}</span>
                <span class="item-desc">{{ p.description }}</span>
              </div>
              <Tag v-if="p.arguments?.length" :value="`${p.arguments.length} args`" severity="secondary" />
            </div>
          </div>
        </div>
      </SplitterPanel>

      <SplitterPanel :size="45" :minSize="30">
        <div class="panel detail-panel">
          <div v-if="!selectedPrompt" class="empty-panel">
            <i class="pi pi-file-edit" /><p>Select a prompt to execute</p>
          </div>
          <template v-else>
            <div class="detail-header">
              <h3 class="item-title">{{ selectedPrompt.name }}</h3>
              <p class="item-desc-full">{{ selectedPrompt.description }}</p>
            </div>
            <div class="args-section">
              <div class="section-label">Arguments</div>
              <div v-if="!selectedPrompt.arguments?.length" class="muted-sm">No arguments required</div>
              <div v-for="arg in selectedPrompt.arguments" :key="arg.name" class="arg-field">
                <label>{{ arg.name }}<span v-if="arg.required" class="required">*</span></label>
                <p v-if="arg.description" class="arg-desc">{{ arg.description }}</p>
                <InputText v-model="args[arg.name]" :placeholder="arg.description || arg.name" class="w-full" />
              </div>
            </div>
            <div class="action-bar">
              <Button label="Execute" icon="pi pi-play" :loading="executing" @click="execute" />
              <Button v-if="result !== null" label="Send to LLM" icon="pi pi-send" severity="secondary" @click="openSendToLlm" />
            </div>
            <div v-if="resultError" class="error-box"><i class="pi pi-times-circle" /> {{ resultError }}</div>
            <div v-if="result !== null" class="result-section">
              <JsonViewer :data="result" title="Prompt Result" :initially-expanded="true" />
            </div>
          </template>
        </div>
      </SplitterPanel>
    </Splitter>

    <Dialog v-model:visible="showLlmDialog" header="Send to LLM" modal :style="{ width: '400px' }">
      <div class="form-field">
        <label>Select Model</label>
        <Select v-model="selectedModel" :options="llmModels" optionLabel="name" optionValue="name" class="w-full" placeholder="Choose a model" />
      </div>
      <template #footer>
        <Button label="Cancel" severity="secondary" text @click="showLlmDialog = false" />
        <Button label="Send" icon="pi pi-send" :loading="sendingToLlm" :disabled="!selectedModel" @click="sendToLlm" />
      </template>
    </Dialog>
  </div>
</template>

<style scoped>
.prompts-view { height:100%; display:flex; flex-direction:column; background:var(--bg-base); }
.splitter { flex:1; height:100%; }
.panel { display:flex; flex-direction:column; height:100%; border-right:1px solid var(--border); overflow:hidden; }
.detail-panel { border-right:none; overflow-y:auto; }
.panel-header { display:flex; align-items:center; gap:8px; padding:12px 16px; font-size:12px; font-weight:600; text-transform:uppercase; letter-spacing:.05em; color:var(--text-muted); border-bottom:1px solid var(--border); flex-shrink:0; }
.panel-search { padding:8px 12px; border-bottom:1px solid var(--border); flex-shrink:0; }
.empty-panel { display:flex; flex-direction:column; align-items:center; justify-content:center; gap:10px; flex:1; color:var(--text-muted); font-size:13px; text-align:center; padding:20px; }
.empty-panel i { font-size:32px; }
.item-list { overflow-y:auto; flex:1; }
.list-item { display:flex; align-items:center; gap:8px; padding:10px 16px; cursor:pointer; font-size:13px; color:var(--text-secondary); border-left:2px solid transparent; transition:var(--transition-fast); }
.list-item:hover { background:var(--nav-item-hover); color:var(--text-primary); }
.list-item.active { background:var(--nav-item-active); color:var(--accent); border-left-color:var(--accent); }
.prompt-item { justify-content:space-between; flex-wrap:nowrap; gap:8px; }
.item-body-icon { flex-shrink:0; width:28px; height:28px; display:flex; align-items:center; justify-content:center; }
.item-icon-img { width:24px; height:24px; border-radius:4px; object-fit:contain; }
.item-icon-badge { width:26px; height:26px; border-radius:6px; background:var(--accent); color:#fff; font-size:12px; font-weight:700; display:flex; align-items:center; justify-content:center; flex-shrink:0; }
.item-body-text { flex:1; min-width:0; }
.item-name { display:block; font-size:13px; font-weight:500; font-family:var(--font-family-mono); color:var(--text-primary); }
.item-desc { display:block; font-size:11px; color:var(--text-muted); white-space:nowrap; overflow:hidden; text-overflow:ellipsis; }
.dot { font-size:8px; color:var(--success); flex-shrink:0; }
.detail-header { padding:16px 20px; border-bottom:1px solid var(--border); }
.item-title { margin:0 0 4px; font-size:16px; font-weight:600; color:var(--text-primary); }
.item-desc-full { margin:0; font-size:13px; color:var(--text-secondary); }
.args-section { padding:16px 20px; border-bottom:1px solid var(--border); }
.section-label { font-size:11px; font-weight:600; text-transform:uppercase; letter-spacing:.05em; color:var(--text-muted); margin-bottom:12px; }
.arg-field { margin-bottom:12px; }
.arg-field label { display:flex; align-items:center; gap:4px; font-size:12px; color:var(--text-secondary); margin-bottom:4px; font-weight:500; }
.required { color:var(--danger); }
.arg-desc { margin:0 0 4px; font-size:11px; color:var(--text-muted); }
.w-full { width:100%; }
.muted-sm { color:var(--text-muted); font-size:12px; }
.action-bar { padding:12px 20px; display:flex; gap:8px; border-bottom:1px solid var(--border); }
.error-box { margin:12px 20px; padding:10px 14px; background:rgba(239,68,68,.1); border:1px solid var(--danger); border-radius:var(--border-radius-sm); color:var(--danger); font-size:13px; display:flex; gap:8px; }
.result-section { flex:1; padding:12px 20px; min-height:200px; }
.form-field { display:flex; flex-direction:column; gap:6px; }
.form-field label { font-size:12px; font-weight:500; color:var(--text-secondary); text-transform:uppercase; letter-spacing:.04em; }
</style>
