<script setup lang="ts">
import { ref, computed, watch, onMounted, onBeforeUnmount } from 'vue'
import { useToast } from 'primevue/usetoast'
import Splitter from 'primevue/splitter'
import SplitterPanel from 'primevue/splitterpanel'
import Button from 'primevue/button'
import InputText from 'primevue/inputtext'
import Skeleton from 'primevue/skeleton'
import Tag from 'primevue/tag'
import Select from 'primevue/select'
import Popover from 'primevue/popover'
import Tabs from 'primevue/tabs'
import TabList from 'primevue/tablist'
import Tab from 'primevue/tab'
import TabPanels from 'primevue/tabpanels'
import TabPanel from 'primevue/tabpanel'
import JsonViewer from '@/components/common/JsonViewer.vue'
import ToolDocsDialog from '@/components/common/ToolDocsDialog.vue'
import { useConnectionsStore } from '@/stores/connections'
import { connectionsApi } from '@/api/connections'
import { llmModelsApi } from '@/api/llmModels'
import { chatApi } from '@/api/chat'
import { preferencesApi } from '@/api/preferences'
import { generatePromptMarkdown, generatePromptsListMarkdown } from '@/composables/useToolDocs'
import { marked } from 'marked'
import DOMPurify from 'dompurify'
import type { ActivePrompt, LlmModelDefinition } from '@/api/types'

const toast = useToast()
const store = useConnectionsStore()

const selectedConnName = ref<string | null>(null)
const prompts = ref<ActivePrompt[]>([])
const promptsLoading = ref(false)
const promptSearch = ref('')
const selectedPrompt = ref<ActivePrompt | null>(null)
const args = ref<Record<string, string>>({})
const executing = ref(false)
const result = ref<unknown>(null)
const resultError = ref<string | null>(null)

// ── Result tabs ───────────────────────────────────────────────────────
const activeResultTab = ref<string>('json')

// ── Inline LLM ───────────────────────────────────────────────────────
const llmResponse = ref('')
const llmStreaming = ref(false)
const llmError = ref<string | null>(null)
const llmModelName = ref<string | null>(null)
const llmAbortController = ref<AbortController | null>(null)

// ── Model picker popover ──────────────────────────────────────────────
const modelPickerRef = ref<InstanceType<typeof Popover> | null>(null)
const llmModels = ref<LlmModelDefinition[]>([])
const selectedModel = ref<string | null>(null)
const loadingModels = ref(false)

// Docs dialog
const docsVisible = ref(false)
const listDocsVisible = ref(false)

// ── Favourites ────────────────────────────────────────────────────────
const favorites = ref<Set<string>>(new Set())
const showFavoritesFirst = ref(false)

async function toggleFav(name: string) {
  if (favorites.value.has(name)) favorites.value.delete(name)
  else favorites.value.add(name)
  favorites.value = new Set(favorites.value)
  try {
    await preferencesApi.patch({ favoritePrompts: [...favorites.value] })
  } catch (e: any) {
    toast.add({ severity: 'error', summary: 'Failed to save favourite', detail: e.message, life: 3000 })
  }
}

async function toggleShowFavoritesFirst() {
  showFavoritesFirst.value = !showFavoritesFirst.value
  try {
    await preferencesApi.patch({ showPromptFavoritesFirst: showFavoritesFirst.value })
  } catch (e: any) {
    toast.add({ severity: 'error', summary: 'Failed to save preference', detail: e.message, life: 3000 })
  }
}

const connectedConnections = computed(() => store.activeConnections.filter(c => c.isConnected))

type PromptListItem = ActivePrompt | { isSeparator: true; label: string }

const filteredPrompts = computed<PromptListItem[]>(() => {
  let list = prompts.value
  const q = promptSearch.value.toLowerCase()
  if (q) list = list.filter(p => p.name.toLowerCase().includes(q) || p.description?.toLowerCase().includes(q))

  if (!showFavoritesFirst.value || favorites.value.size === 0) return list

  const favs = list.filter(p => favorites.value.has(p.name))
  const rest = list.filter(p => !favorites.value.has(p.name))
  const items: PromptListItem[] = []
  if (favs.length) { items.push({ isSeparator: true, label: '⭐ Favourites' }); items.push(...favs) }
  if (rest.length) { items.push({ isSeparator: true, label: 'All Prompts' }); items.push(...rest) }
  return items
})

const filteredPromptsOnly = computed(() =>
  filteredPrompts.value.filter((p): p is ActivePrompt => !('isSeparator' in p))
)

const llmResponseHtml = computed(() =>
  llmResponse.value
    ? DOMPurify.sanitize(marked.parse(llmResponse.value) as string)
    : ''
)
const hasLlmOutput = computed(() => llmResponse.value.length > 0 || llmStreaming.value)

watch(selectedConnName, async (name) => {
  selectedPrompt.value = null; prompts.value = []; result.value = null
  if (!name) return
  promptsLoading.value = true
  try { prompts.value = await store.getPrompts(name) }
  catch (e: any) { toast.add({ severity: 'error', summary: 'Failed to load prompts', detail: e.message, life: 5000 }) }
  finally { promptsLoading.value = false }
})

watch(selectedPrompt, () => {
  args.value = {}
  result.value = null
  resultError.value = null
  llmResponse.value = ''
  llmError.value = null
  llmModelName.value = null
  activeResultTab.value = 'json'
  llmAbortController.value?.abort()
})

function selectPrompt(p: ActivePrompt) { selectedPrompt.value = p }

async function execute() {
  if (!selectedConnName.value || !selectedPrompt.value) return
  llmAbortController.value?.abort()
  executing.value = true; result.value = null; resultError.value = null
  try {
    const r = await connectionsApi.executePrompt(selectedConnName.value, selectedPrompt.value.name, args.value)
    result.value = r
    activeResultTab.value = 'json'
    toast.add({ severity: 'success', summary: 'Prompt executed', life: 2000 })
  } catch (e: any) {
    resultError.value = e.message
    toast.add({ severity: 'error', summary: 'Execution failed', detail: e.message, life: 5000 })
  } finally { executing.value = false }
}

async function openModelPicker(event: Event) {
  // Toggle immediately — event.currentTarget becomes null after any await
  modelPickerRef.value?.toggle(event)
  if (llmModels.value.length === 0) {
    loadingModels.value = true
    try {
      llmModels.value = await llmModelsApi.getAll()
      const sel = await llmModelsApi.getSelected()
      selectedModel.value = sel.selectedModelName
    } finally { loadingModels.value = false }
  }
}

async function confirmSendToLlm() {
  if (!selectedModel.value || !result.value) return
  modelPickerRef.value?.hide()
  llmAbortController.value?.abort()
  llmResponse.value = ''
  llmError.value = null
  llmStreaming.value = true
  llmModelName.value = selectedModel.value
  activeResultTab.value = 'llm'
  try {
    const session = await chatApi.createSession()
    const content = typeof result.value === 'string'
      ? result.value
      : JSON.stringify(result.value, null, 2)
    const abort = new AbortController()
    llmAbortController.value = abort
    for await (const evt of chatApi.streamMessage(
      session.id,
      `Analyse and explain this prompt result:\n\n\`\`\`json\n${content}\n\`\`\``,
      selectedModel.value, [], abort.signal,
    )) {
      if (evt.type === 'token' && evt.text) llmResponse.value += evt.text
      else if (evt.type === 'error') { llmError.value = evt.errorMessage ?? 'LLM error'; break }
    }
  } catch (e: any) {
    if (e.name !== 'AbortError') llmError.value = e.message
  } finally {
    llmStreaming.value = false
    llmAbortController.value = null
  }
}

function cancelLlm() { llmAbortController.value?.abort(); llmStreaming.value = false }

watch(() => store.initialized, (ready, wasReady) => { if (ready && !wasReady) store.loadActive() }, { immediate: true })

onMounted(async () => {
  try {
    const prefs = await preferencesApi.getAll()
    favorites.value = new Set(prefs.favoritePrompts ?? [])
    showFavoritesFirst.value = prefs.showPromptFavoritesFirst ?? false
  } catch { /* non-fatal */ }
})

onBeforeUnmount(() => { llmAbortController.value?.abort() })
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
            <Tag v-if="prompts.length" :value="filteredPromptsOnly.length < prompts.length ? `${filteredPromptsOnly.length} / ${prompts.length}` : String(prompts.length)" :severity="filteredPromptsOnly.length < prompts.length ? 'warn' : 'secondary'" />
            <button
              v-if="filteredPromptsOnly.length > 0"
              class="fav-btn"
              :style="showFavoritesFirst ? 'color:var(--warning)' : ''"
              @click="toggleShowFavoritesFirst"
              :title="showFavoritesFirst ? 'Show all prompts' : 'Show favourites first'"
            ><i :class="showFavoritesFirst ? 'pi pi-star-fill' : 'pi pi-star'" /></button>
            <button
              v-if="filteredPromptsOnly.length > 0"
              class="fav-btn"
              @click="listDocsVisible = true"
              :title="`View docs for ${filteredPromptsOnly.length} visible prompt${filteredPromptsOnly.length === 1 ? '' : 's'}`"
            ><i class="pi pi-book" /></button>
          </div>
          <div class="panel-search">
            <InputText v-model="promptSearch" placeholder="Filter prompts…" style="width:100%" />
          </div>
          <div v-if="promptsLoading" class="p-3"><Skeleton v-for="i in 4" :key="i" height="40px" class="mb-2" /></div>
          <div v-else-if="!selectedConnName" class="empty-panel"><p>Select a connection</p></div>
          <div v-else-if="filteredPrompts.length === 0" class="empty-panel"><p>No prompts found</p></div>
          <div v-else class="item-list">
            <template v-for="item in filteredPrompts" :key="'isSeparator' in item ? item.label : item.name">
              <div v-if="'isSeparator' in item" class="list-separator">{{ item.label }}</div>
              <div v-else class="list-item prompt-item"
                   :class="{ active: selectedPrompt?.name === item.name }" @click="selectPrompt(item)">
                <div class="item-body-icon">
                  <img v-if="item.iconUrl" :src="item.iconUrl" :alt="item.name" class="item-icon-img" />
                  <span v-else class="item-icon-badge">{{ item.name.charAt(0).toUpperCase() }}</span>
                </div>
                <div class="item-body-text">
                  <span class="item-name">{{ item.name }}</span>
                  <span class="item-desc">{{ item.description }}</span>
                </div>
                <Tag v-if="item.arguments?.length" :value="`${item.arguments.length} args`" severity="secondary" />
                <button class="fav-btn" @click.stop="toggleFav(item.name)" :title="favorites.has(item.name) ? 'Unfavourite' : 'Favourite'">
                  <i :class="favorites.has(item.name) ? 'pi pi-star-fill' : 'pi pi-star'" :style="favorites.has(item.name) ? 'color:var(--warning)' : ''" />
                </button>
              </div>
            </template>
          </div>
        </div>
      </SplitterPanel>

      <SplitterPanel :size="45" :minSize="30">
        <div class="panel detail-panel">
          <div v-if="!selectedPrompt" class="empty-panel">
            <i class="pi pi-file-edit" /><p>Select a prompt to execute</p>
          </div>
          <template v-else>
            <!-- Fixed top: header + args + action bar -->
            <div class="detail-header flex-shrink-0">
              <div class="detail-header-row">
                <div>
                  <h3 class="item-title">{{ selectedPrompt.name }}</h3>
                  <p class="item-desc-full">{{ selectedPrompt.description }}</p>
                </div>
                <button class="fav-btn" @click="docsVisible = true" title="View documentation"><i class="pi pi-book" /></button>
              </div>
            </div>
            <div class="args-section flex-shrink-0">
              <div class="section-label">Arguments</div>
              <div v-if="!selectedPrompt.arguments?.length" class="muted-sm">No arguments required</div>
              <div v-for="arg in selectedPrompt.arguments" :key="arg.name" class="arg-field">
                <label>{{ arg.name }}<span v-if="arg.required" class="required">*</span></label>
                <p v-if="arg.description" class="arg-desc">{{ arg.description }}</p>
                <InputText v-model="args[arg.name]" :placeholder="arg.description || arg.name" class="w-full" />
              </div>
            </div>
            <div class="action-bar flex-shrink-0">
              <Button label="Execute" icon="pi pi-play" :loading="executing" @click="execute" />
              <Button
                v-if="result !== null"
                :label="llmStreaming ? 'Streaming…' : 'Send to LLM'"
                :icon="llmStreaming ? 'pi pi-spin pi-spinner' : 'pi pi-sparkles'"
                severity="secondary"
                @click="openModelPicker"
              />
              <Button v-if="llmStreaming" label="Cancel" icon="pi pi-times" severity="danger" text @click="cancelLlm" />
            </div>
            <div v-if="resultError" class="error-box flex-shrink-0"><i class="pi pi-times-circle" /> {{ resultError }}</div>

            <!-- Result tabs — takes all remaining space -->
            <div v-if="result !== null" class="result-tabs-area">
              <Tabs :value="activeResultTab" @update:value="v => activeResultTab = v as string" class="result-tabs">
                <TabList>
                  <Tab value="json"><i class="pi pi-code mr-1" />JSON Result</Tab>
                  <Tab value="llm" :disabled="!hasLlmOutput">
                    <i class="pi pi-sparkles mr-1" />LLM Response
                    <span v-if="llmStreaming" class="stream-dot" />
                    <Tag v-else-if="llmModelName" :value="llmModelName" severity="secondary" class="ml-1 text-xs" />
                  </Tab>
                </TabList>
                <TabPanels class="result-tab-panels">
                  <TabPanel value="json" class="result-tab-panel json-tab-panel">
                    <JsonViewer :data="result" title="" :initially-expanded="true" />
                  </TabPanel>
                  <TabPanel value="llm" class="result-tab-panel llm-tab-panel">
                    <div class="llm-output-wrapper">
                      <div v-if="llmError" class="error-box"><i class="pi pi-times-circle" /> {{ llmError }}</div>
                      <div v-else-if="!hasLlmOutput" class="empty-panel"><p>LLM response will appear here</p></div>
                      <template v-else>
                        <div class="llm-output" v-html="llmResponseHtml" />
                        <span v-if="llmStreaming" class="cursor-blink">▋</span>
                      </template>
                    </div>
                  </TabPanel>
                </TabPanels>
              </Tabs>
            </div>
          </template>
        </div>
      </SplitterPanel>
    </Splitter>

    <!-- Model picker popover (anchored to the Send to LLM button) -->
    <Popover ref="modelPickerRef" class="model-picker-popover">
      <div class="model-picker-content">
        <div class="model-picker-title">Select Model</div>
        <div v-if="loadingModels" class="model-picker-loading"><i class="pi pi-spin pi-spinner" /> Loading…</div>
        <Select
          v-else
          v-model="selectedModel"
          :options="llmModels"
          optionLabel="name"
          optionValue="name"
          class="w-full"
          placeholder="Choose a model"
        />
        <div class="model-picker-actions">
          <Button label="Cancel" severity="secondary" text size="small" @click="modelPickerRef?.hide()" />
          <Button label="Send" icon="pi pi-sparkles" size="small" :disabled="!selectedModel || loadingModels" @click="confirmSendToLlm" />
        </div>
      </div>
    </Popover>

    <ToolDocsDialog
      v-model:visible="docsVisible"
      :raw-markdown="selectedPrompt ? generatePromptMarkdown(selectedPrompt) : ''"
      :title="selectedPrompt ? `Documentation: ${selectedPrompt.name}` : 'Documentation'"
    />
    <ToolDocsDialog
      v-model:visible="listDocsVisible"
      :raw-markdown="generatePromptsListMarkdown(filteredPromptsOnly)"
      :title="`Documentation: ${filteredPromptsOnly.length} prompt${filteredPromptsOnly.length === 1 ? '' : 's'}`"
    />
  </div>
</template>

<style scoped>
.prompts-view { height:100%; display:flex; flex-direction:column; background:var(--bg-base); }
.splitter { flex:1; height:100%; }
.panel { display:flex; flex-direction:column; height:100%; border-right:1px solid var(--border); overflow:hidden; }
/* detail-panel is a flex column; top sections are fixed, result-tabs-area takes remaining space */
.detail-panel { border-right:none; overflow:hidden; }
.flex-shrink-0 { flex-shrink:0; }
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
.list-separator { padding:6px 16px; font-size:10px; font-weight:700; text-transform:uppercase; letter-spacing:.08em; color:var(--text-muted); background:var(--bg-raised); border-bottom:1px solid var(--border); flex-shrink:0; }
.dot { font-size:8px; color:var(--success); flex-shrink:0; }
.detail-header { padding:16px 20px; border-bottom:1px solid var(--border); }
.detail-header-row { display:flex; align-items:flex-start; justify-content:space-between; gap:8px; }
.fav-btn { background:none; border:none; cursor:pointer; color:var(--text-muted); padding:4px 6px; border-radius:var(--border-radius-sm); transition:var(--transition-fast); flex-shrink:0; }
.fav-btn:hover { color:var(--accent); background:var(--nav-item-hover); }
.item-title { margin:0 0 4px; font-size:16px; font-weight:600; color:var(--text-primary); }
.item-desc-full { margin:0; font-size:13px; color:var(--text-secondary); }
.args-section { padding:16px 20px; border-bottom:1px solid var(--border); overflow-y:auto; max-height:40%; }
.section-label { font-size:11px; font-weight:600; text-transform:uppercase; letter-spacing:.05em; color:var(--text-muted); margin-bottom:12px; }
.arg-field { margin-bottom:12px; }
.arg-field label { display:flex; align-items:center; gap:4px; font-size:12px; color:var(--text-secondary); margin-bottom:4px; font-weight:500; }
.required { color:var(--danger); }
.arg-desc { margin:0 0 4px; font-size:11px; color:var(--text-muted); }
.w-full { width:100%; }
.muted-sm { color:var(--text-muted); font-size:12px; }
.action-bar { padding:12px 20px; display:flex; gap:8px; border-bottom:1px solid var(--border); }
.error-box { margin:12px 20px; padding:10px 14px; background:rgba(239,68,68,.1); border:1px solid var(--danger); border-radius:var(--border-radius-sm); color:var(--danger); font-size:13px; display:flex; gap:8px; }
.mr-1 { margin-right:4px; }
.ml-1 { margin-left:4px; }
.text-xs { font-size:10px !important; }

/* ── Result tabs ─────────────────────────────────────────────────── */
.result-tabs-area { flex:1; min-height:0; display:flex; flex-direction:column; overflow:hidden; }
.result-tabs { display:flex; flex-direction:column; flex:1; min-height:0; height:100%; }

/* LLM streaming indicator dot */
.stream-dot { display:inline-block; width:7px; height:7px; border-radius:50%; background:var(--accent); margin-left:6px; animation:pulse 1s ease-in-out infinite; vertical-align:middle; }
@keyframes pulse { 0%,100%{opacity:1} 50%{opacity:.3} }
:global(.cursor-blink) { animation:blink .7s step-end infinite; }
@keyframes blink { 0%,100%{opacity:1} 50%{opacity:0} }

/* LLM response content */
.llm-output-wrapper { display:flex; flex-direction:column; flex:1; min-height:0; overflow-y:auto; padding:16px 20px; }
.llm-tab-panel { flex:1; min-height:0; overflow-y:auto; }
.llm-output { font-size:13px; line-height:1.7; color:var(--text-primary); }
.llm-output :deep(h1),.llm-output :deep(h2),.llm-output :deep(h3) { margin:.75em 0 .4em; font-weight:600; color:var(--text-primary); }
.llm-output :deep(p) { margin:0 0 .75em; }
.llm-output :deep(code) { background:var(--bg-raised); padding:1px 5px; border-radius:4px; font-family:var(--font-family-mono); font-size:12px; }
.llm-output :deep(pre) { background:var(--bg-raised); padding:12px; border-radius:var(--border-radius); overflow-x:auto; }
.llm-output :deep(ul),.llm-output :deep(ol) { padding-left:1.5em; margin:0 0 .75em; }
.llm-output :deep(li) { margin-bottom:.25em; }

/* Model picker popover */
.model-picker-content { display:flex; flex-direction:column; gap:12px; min-width:280px; padding:4px 0; }
.model-picker-title { font-size:12px; font-weight:600; text-transform:uppercase; letter-spacing:.05em; color:var(--text-muted); }
.model-picker-loading { display:flex; align-items:center; gap:8px; font-size:13px; color:var(--text-muted); }
.model-picker-actions { display:flex; justify-content:flex-end; gap:8px; padding-top:4px; border-top:1px solid var(--border); }
</style>

<style>
/* PrimeVue Tabs internals — must be global to override component styles */
.result-tabs .p-tabs { display:flex; flex-direction:column; flex:1; min-height:0; height:100%; }
.result-tabs .p-tabpanels { flex:1; min-height:0; display:flex; flex-direction:column; padding:0 !important; }
.result-tabs .p-tabpanel[data-p-active="true"] { display:flex; flex-direction:column; flex:1; min-height:0; padding:0 !important; }
/* JSON viewer fills the json tab panel */
.result-tabs .json-tab-panel .json-viewer { flex:1; min-height:0; height:auto !important; }
.result-tabs .json-tab-panel { padding:0 !important; }
/* LLM tab scrolls its own content */
.result-tabs .llm-tab-panel { flex:1; min-height:0; overflow-y:auto; }
</style>
