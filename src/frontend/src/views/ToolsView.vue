<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useToast } from 'primevue/usetoast'
import Splitter from 'primevue/splitter'
import SplitterPanel from 'primevue/splitterpanel'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Button from 'primevue/button'
import InputText from 'primevue/inputtext'
import Skeleton from 'primevue/skeleton'
import Tag from 'primevue/tag'
import OverlayPanel from 'primevue/overlaypanel'
import JsonViewer from '@/components/common/JsonViewer.vue'
import ToolDocsDialog from '@/components/common/ToolDocsDialog.vue'
import { useConnectionsStore } from '@/stores/connections'
import { connectionsApi } from '@/api/connections'
import { preferencesApi } from '@/api/preferences'
import type { ActiveTool } from '@/api/types'

const toast = useToast()
const store = useConnectionsStore()

// ── Connection selection ───────────────────────────────────────────────
const selectedConnName = ref<string | null>(null)
const tools = ref<ActiveTool[]>([])
const toolsLoading = ref(false)
const toolSearch = ref('')

// ── Tool selection + execution ─────────────────────────────────────────
const selectedTool = ref<ActiveTool | null>(null)
const params = ref<Record<string, unknown>>({})
const invoking = ref(false)
const result = ref<unknown>(null)
const resultError = ref<string | null>(null)
const paramErrors = ref(new Set<string>())

// ── Per-field history ──────────────────────────────────────────────────
const fieldHistoryPanel = ref<InstanceType<typeof OverlayPanel> | null>(null)
const fieldHistoryField = ref('')
const fieldHistoryValues = ref<string[]>([])

// ── Docs dialog ───────────────────────────────────────────────────────
const docsVisible = ref(false)
const docsTool = ref<ActiveTool | null>(null)
const listDocsVisible = ref(false)
const iconLoadError = ref<Record<string, boolean>>({})

function openDocs(tool: ActiveTool) {
  docsTool.value = tool
  docsVisible.value = true
}

const filteredToolsOnly = computed(() =>
  filteredTools.value.filter((item): item is ActiveTool => !('isSeparator' in item))
)

// ── Favorites (backend) ────────────────────────────────────────────────
const favorites = ref<Set<string>>(new Set())
const showFavoritesFirst = ref(false)

async function toggleFav(toolName: string) {
  if (favorites.value.has(toolName)) favorites.value.delete(toolName)
  else favorites.value.add(toolName)
  favorites.value = new Set(favorites.value) // trigger reactivity
  try {
    await preferencesApi.patch({ favoriteTools: [...favorites.value] })
  } catch (e: any) {
    toast.add({ severity: 'error', summary: 'Failed to save favourite', detail: e.message, life: 3000 })
  }
}

// ── Parameter history (backend) ────────────────────────────────────────
const allParamHistory = ref<Record<string, string[]>>({})

const paramHistory = computed<Record<string, unknown>[]>(() =>
  (allParamHistory.value[selectedTool.value?.name ?? ''] ?? []).map(s => {
    try { return JSON.parse(s) } catch { return {} }
  })
)

// ── Computed ───────────────────────────────────────────────────────────
const connectedConnections = computed(() => store.activeConnections.filter(c => c.isConnected))

type ToolListItem = ActiveTool | { isSeparator: true; label: string }

const filteredTools = computed<ToolListItem[]>(() => {
  let list = tools.value
  const q = toolSearch.value.toLowerCase()
  if (q) list = list.filter(t => t.name.toLowerCase().includes(q) || t.description?.toLowerCase().includes(q))

  if (!showFavoritesFirst.value || favorites.value.size === 0) return list

  const favs = list.filter(t => favorites.value.has(t.name))
  const rest = list.filter(t => !favorites.value.has(t.name))
  const items: ToolListItem[] = []
  if (favs.length) {
    items.push({ isSeparator: true, label: '⭐ Favourites' })
    items.push(...favs)
  }
  if (rest.length) {
    items.push({ isSeparator: true, label: 'All Tools' })
    items.push(...rest)
  }
  return items
})

const inputProps = computed(() => {
  if (!selectedTool.value?.inputSchema) return []
  const schema = selectedTool.value.inputSchema as any
  const props = schema.properties ?? {}
  const required: string[] = schema.required ?? []
  return Object.entries(props).map(([k, v]: [string, any]) => ({
    name: k, description: v.description ?? '', type: v.type ?? 'string',
    required: required.includes(k), enum: v.enum,
  }))
})

// ── Watchers ───────────────────────────────────────────────────────────
watch(selectedConnName, async (name) => {
  selectedTool.value = null; tools.value = []; result.value = null
  if (!name) return
  toolsLoading.value = true
  try { tools.value = await store.getTools(name) }
  catch (e: any) { toast.add({ severity: 'error', summary: 'Failed to load tools', detail: e.message, life: 5000 }) }
  finally { toolsLoading.value = false }
})

watch(selectedTool, (t) => {
  params.value = {}; result.value = null; resultError.value = null; paramErrors.value = new Set()
})

// ── Methods ────────────────────────────────────────────────────────────
function selectTool(tool: ActiveTool) { selectedTool.value = tool }

async function invoke() {
  if (!selectedConnName.value || !selectedTool.value) return

  // Validate required parameters
  const errors = new Set<string>()
  for (const p of inputProps.value) {
    if (p.required && !getParamValue(p.name).trim()) {
      errors.add(p.name)
    }
  }
  if (errors.size > 0) {
    paramErrors.value = errors
    toast.add({ severity: 'warn', summary: 'Required fields missing', detail: `Please fill in: ${[...errors].join(', ')}`, life: 4000 })
    return
  }
  paramErrors.value = new Set()

  invoking.value = true; result.value = null; resultError.value = null
  try {
    const r = await connectionsApi.invokeTool(selectedConnName.value, selectedTool.value.name, params.value)
    result.value = r

    // Save history to backend
    const toolName = selectedTool.value.name
    const histJson = JSON.stringify(params.value)
    const existing = allParamHistory.value[toolName] ?? []
    const updated = [histJson, ...existing.filter(h => h !== histJson)].slice(0, 5)
    allParamHistory.value = { ...allParamHistory.value, [toolName]: updated }
    await preferencesApi.patch({ parameterHistory: allParamHistory.value })

    toast.add({ severity: 'success', summary: 'Tool executed', life: 2000 })
  } catch (e: any) {
    resultError.value = e.message
    toast.add({ severity: 'error', summary: 'Invocation failed', detail: e.message, life: 5000 })
  } finally { invoking.value = false }
}

function loadHistoryParams(h: Record<string, unknown>) {
  params.value = JSON.parse(JSON.stringify(h))
}

function getParamValue(key: string) { return params.value[key] !== undefined ? String(params.value[key]) : '' }
function setParamValue(key: string, val: string) {
  params.value[key] = val
  if (paramErrors.value.has(key) && val.trim()) {
    const s = new Set(paramErrors.value)
    s.delete(key)
    paramErrors.value = s
  }
}

function openFieldHistory(event: Event, fieldName: string) {
  const runs = paramHistory.value
  const seen = new Set<string>()
  fieldHistoryValues.value = []
  for (const run of runs) {
    const val = run[fieldName]
    if (val !== undefined && val !== null && val !== '') {
      const str = String(val)
      if (!seen.has(str)) {
        seen.add(str)
        fieldHistoryValues.value.push(str)
      }
    }
  }
  if (fieldHistoryValues.value.length === 0) return
  fieldHistoryField.value = fieldName
  fieldHistoryPanel.value?.toggle(event)
}

function selectFieldHistoryValue(val: string) {
  setParamValue(fieldHistoryField.value, val)
  fieldHistoryPanel.value?.hide()
}

onMounted(async () => {
  try {
    const prefs = await preferencesApi.getAll()
    favorites.value = new Set(prefs.favoriteTools ?? [])
    showFavoritesFirst.value = prefs.showFavoritesFirst ?? false
    allParamHistory.value = (prefs as any).parameterHistory ?? {}
  } catch (e: any) {
    toast.add({ severity: 'warn', summary: 'Could not load preferences', detail: e.message, life: 3000 })
  }
})

// Single watcher handles both: view mounts after init (immediate fires) and
// view mounts before init completes (fires when initialized transitions false→true).
watch(() => store.initialized, async (ready, wasReady) => {
  if (ready && !wasReady) await store.loadActive()
}, { immediate: true })
</script>

<template>
  <div class="tools-view">
    <Splitter class="tools-splitter">
      <!-- Connection list -->
      <SplitterPanel :size="20" :minSize="15">
        <div class="panel connections-panel">
          <div class="panel-header">Connections</div>
          <div v-if="connectedConnections.length === 0" class="empty-panel">
            <i class="pi pi-server" />
            <p>No active connections.<br />Connect one from the Connections page.</p>
          </div>
          <div v-else class="conn-list">
            <div
              v-for="c in connectedConnections"
              :key="c.name"
              class="conn-item"
              :class="{ active: selectedConnName === c.name }"
              @click="selectedConnName = c.name"
            >
              <i class="pi pi-circle-fill dot" />
              <span>{{ c.name }}</span>
            </div>
          </div>
        </div>
      </SplitterPanel>

      <!-- Tool list -->
      <SplitterPanel :size="35" :minSize="25">
        <div class="panel tools-panel">
          <div class="panel-header">
            Tools
            <Tag
              v-if="tools.length"
              :value="toolSearch ? `${filteredToolsOnly.length} / ${tools.length}` : String(tools.length)"
              :severity="toolSearch && filteredToolsOnly.length < tools.length ? 'warn' : 'secondary'"
            />
            <button
              class="fav-btn"
              :style="showFavoritesFirst ? 'color:var(--warning)' : ''"
              @click="showFavoritesFirst = !showFavoritesFirst"
              :title="showFavoritesFirst ? 'Show all tools' : 'Show favourites first'"
            >
              <i :class="showFavoritesFirst ? 'pi pi-star-fill' : 'pi pi-star'" />
            </button>
            <button
              v-if="filteredToolsOnly.length > 0"
              class="fav-btn"
              @click="listDocsVisible = true"
              :title="`View docs for ${filteredToolsOnly.length} visible tool${filteredToolsOnly.length === 1 ? '' : 's'}`"
            >
              <i class="pi pi-book" />
            </button>
          </div>
          <div class="panel-search">
            <InputText v-model="toolSearch" placeholder="Filter tools…" class="search-input" />
          </div>
          <div v-if="toolsLoading" class="p-3">
            <Skeleton v-for="i in 5" :key="i" height="36px" class="mb-2" />
          </div>
          <div v-else-if="!selectedConnName" class="empty-panel"><p>Select a connection</p></div>
          <div v-else-if="filteredTools.length === 0" class="empty-panel"><p>No tools found</p></div>
          <div v-else class="tool-list">
            <template v-for="item in filteredTools" :key="'isSeparator' in item ? item.label : item.name">
              <div v-if="'isSeparator' in item" class="tool-section-header">{{ item.label }}</div>
              <div
                v-else
                class="tool-item"
                :class="{ active: selectedTool?.name === item.name }"
                @click="selectTool(item)"
              >
                <div class="tool-item-body">
                  <div class="tool-item-icon">
                    <img v-if="item.iconUrl && !iconLoadError[item.name]" :src="item.iconUrl" :alt="item.name" class="tool-icon-img" @error="iconLoadError[item.name] = true" />
                    <span v-else class="tool-icon-badge">{{ item.name.charAt(0).toUpperCase() }}</span>
                  </div>
                  <div class="tool-item-text">
                    <span class="tool-name">{{ item.name }}</span>
                    <span class="tool-desc">{{ item.description }}</span>
                  </div>
                </div>
                <button class="fav-btn" @click.stop="toggleFav(item.name)" :title="favorites.has(item.name) ? 'Unfavorite' : 'Favorite'">
                  <i :class="favorites.has(item.name) ? 'pi pi-star-fill' : 'pi pi-star'" :style="favorites.has(item.name) ? 'color:var(--warning)' : ''" />
                </button>
              </div>
            </template>
          </div>
        </div>
      </SplitterPanel>

      <!-- Detail + output -->
      <SplitterPanel :size="45" :minSize="30">
        <div class="panel detail-panel">
          <div v-if="!selectedTool" class="empty-panel">
            <i class="pi pi-wrench" />
            <p>Select a tool to inspect and invoke it</p>
          </div>
          <template v-else>
            <div class="detail-header">
              <div class="detail-header-info">
                <div class="detail-icon">
                  <img v-if="selectedTool.iconUrl && !iconLoadError[selectedTool.name]" :src="selectedTool.iconUrl" :alt="selectedTool.name" class="detail-icon-img" @error="iconLoadError[selectedTool.name] = true" />
                  <span v-else class="detail-icon-badge">{{ selectedTool.name.charAt(0).toUpperCase() }}</span>
                </div>
                <div>
                  <h3 class="tool-title">{{ selectedTool.name }}</h3>
                  <p class="tool-desc-full">{{ selectedTool.description }}</p>
                </div>
              </div>
              <Button
                icon="pi pi-book"
                text
                size="small"
                v-tooltip="'View documentation'"
                @click="openDocs(selectedTool!)"
              />
            </div>

            <div class="params-section">
              <div class="section-label">Parameters</div>
              <div v-if="inputProps.length === 0" class="muted-sm">No parameters</div>
              <div v-for="p in inputProps" :key="p.name" class="param-field" :class="{ 'param-error': paramErrors.has(p.name) }">
                <div class="param-label-row">
                  <label>
                    {{ p.name }}
                    <span v-if="p.required" class="required">*</span>
                    <span class="param-type">{{ p.type }}</span>
                  </label>
                  <button
                    v-if="paramHistory.length"
                    class="field-history-btn"
                    title="Show previous values for this field"
                    @click="openFieldHistory($event, p.name)"
                  ><i class="pi pi-history" /></button>
                </div>
                <select v-if="p.enum" :value="getParamValue(p.name)" @change="setParamValue(p.name, ($event.target as HTMLSelectElement).value)" class="param-select" :class="{ 'input-error': paramErrors.has(p.name) }">
                  <option value="">— select —</option>
                  <option v-for="opt in p.enum" :key="opt" :value="opt">{{ opt }}</option>
                </select>
                <InputText v-else :modelValue="getParamValue(p.name)" @update:modelValue="setParamValue(p.name, String($event))"
                           :placeholder="p.description || p.name" class="w-full" :class="{ 'p-invalid': paramErrors.has(p.name) }" />
                <p v-if="p.description" class="param-desc">{{ p.description }}</p>
              </div>
            </div>

            <!-- History -->
            <div v-if="paramHistory.length" class="history-section">
              <div class="section-label">Recent Parameters</div>
              <Button
                v-for="(h, idx) in paramHistory"
                :key="idx"
                text
                size="small"
                class="history-btn"
                :label="`Run ${paramHistory.length - idx}`"
                icon="pi pi-history"
                @click="loadHistoryParams(h)"
                v-tooltip.right="JSON.stringify(h).slice(0, 120)"
              />
            </div>

            <div class="invoke-bar">
              <Button label="Execute" icon="pi pi-play" :loading="invoking" @click="invoke" />
            </div>

            <OverlayPanel ref="fieldHistoryPanel">
              <div class="field-history-popup">
                <div class="field-history-header">Previous values for <strong>{{ fieldHistoryField }}</strong></div>
                <div v-for="val in fieldHistoryValues" :key="val" class="field-history-item" @click="selectFieldHistoryValue(val)">
                  {{ val }}
                </div>
              </div>
            </OverlayPanel>

            <div v-if="resultError" class="error-box">
              <i class="pi pi-times-circle" /> {{ resultError }}
            </div>

            <div v-if="result !== null" class="result-section">
              <JsonViewer :data="result" title="Result" :initially-expanded="true" />
            </div>
          </template>
        </div>
      </SplitterPanel>
    </Splitter>
  </div>

  <ToolDocsDialog v-model:visible="docsVisible" :tool="docsTool" />
  <ToolDocsDialog v-model:visible="listDocsVisible" :tools="filteredToolsOnly" />
</template>

<style scoped>
.tools-view { height:100%; display:flex; flex-direction:column; background:var(--bg-base); }
.tools-splitter { flex:1; height:100%; }
.panel { display:flex; flex-direction:column; height:100%; border-right:1px solid var(--border); overflow:hidden; }
.detail-panel { border-right:none; overflow-y:auto; }
.panel-header { display:flex; align-items:center; gap:8px; padding:12px 16px; font-size:12px; font-weight:600; text-transform:uppercase; letter-spacing:.05em; color:var(--text-muted); border-bottom:1px solid var(--border); flex-shrink:0; }
.panel-search { padding:8px 12px; border-bottom:1px solid var(--border); flex-shrink:0; }
.search-input { width:100%; }
.empty-panel { display:flex; flex-direction:column; align-items:center; justify-content:center; gap:10px; flex:1; color:var(--text-muted); font-size:13px; text-align:center; padding:20px; }
.empty-panel i { font-size:32px; }
.conn-list { overflow-y:auto; flex:1; }
.conn-item { display:flex; align-items:center; gap:8px; padding:10px 16px; cursor:pointer; font-size:13px; color:var(--text-secondary); transition:var(--transition-fast); border-left:2px solid transparent; }
.conn-item:hover { background:var(--nav-item-hover); color:var(--text-primary); }
.conn-item.active { background:var(--nav-item-active); color:var(--accent); border-left-color:var(--accent); }
.dot { font-size:8px; color:var(--success); }
.tool-list { overflow-y:auto; flex:1; }
.tool-item { display:flex; align-items:center; gap:8px; padding:10px 14px; cursor:pointer; border-bottom:1px solid var(--border); border-left:2px solid transparent; transition:var(--transition-fast); }
.tool-item:hover { background:var(--nav-item-hover); }
.tool-item.active { background:var(--nav-item-active); border-left-color:var(--accent); }
.tool-item-body { flex:1; min-width:0; display:flex; align-items:center; gap:10px; }
.tool-item-icon { flex-shrink:0; width:28px; height:28px; display:flex; align-items:center; justify-content:center; }
.tool-icon-img { width:24px; height:24px; border-radius:4px; object-fit:contain; }
.tool-icon-badge { width:26px; height:26px; border-radius:6px; background:var(--accent); color:#fff; font-size:12px; font-weight:700; display:flex; align-items:center; justify-content:center; flex-shrink:0; }
.tool-item-text { flex:1; min-width:0; }
.tool-name { display:block; font-size:13px; font-weight:500; color:var(--text-primary); font-family:var(--font-family-mono); }
.tool-desc { display:block; font-size:11px; color:var(--text-muted); white-space:nowrap; overflow:hidden; text-overflow:ellipsis; }
.fav-btn { background:none; border:none; cursor:pointer; color:var(--text-muted); padding:4px; flex-shrink:0; }
.fav-btn:hover { color:var(--warning); }
.detail-header { padding:16px 20px; border-bottom:1px solid var(--border); flex-shrink:0; display:flex; align-items:flex-start; justify-content:space-between; gap:12px; }
.detail-header-info { display:flex; align-items:center; gap:12px; }
.detail-icon { flex-shrink:0; width:36px; height:36px; display:flex; align-items:center; justify-content:center; }
.detail-icon-img { width:32px; height:32px; border-radius:6px; object-fit:contain; }
.detail-icon-badge { width:36px; height:36px; border-radius:8px; background:var(--accent); color:#fff; font-size:16px; font-weight:700; display:flex; align-items:center; justify-content:center; flex-shrink:0; }
.tool-title { margin:0 0 4px; font-size:16px; font-weight:600; color:var(--text-primary); }
.tool-desc-full { margin:0; font-size:13px; color:var(--text-secondary); }
.params-section, .history-section { padding:16px 20px; border-bottom:1px solid var(--border); }
.section-label { font-size:11px; font-weight:600; text-transform:uppercase; letter-spacing:.05em; color:var(--text-muted); margin-bottom:12px; }
.param-field { margin-bottom:12px; }
.param-field label { display:flex; align-items:center; gap:6px; font-size:12px; color:var(--text-secondary); margin-bottom:4px; }
.param-label-row { display:flex; align-items:center; justify-content:space-between; margin-bottom:4px; }
.param-label-row label { margin-bottom:0; }
.field-history-btn { background:none; border:none; cursor:pointer; color:var(--text-muted); padding:2px 4px; border-radius:3px; font-size:11px; transition:color .15s; }
.field-history-btn:hover { color:var(--accent); }
.field-history-popup { min-width:200px; max-width:320px; }
.field-history-header { font-size:11px; color:var(--text-muted); padding:4px 0 8px; border-bottom:1px solid var(--border); margin-bottom:6px; }
.field-history-item { padding:6px 8px; border-radius:4px; cursor:pointer; font-family:var(--font-family-mono); font-size:12px; white-space:nowrap; overflow:hidden; text-overflow:ellipsis; }
.field-history-item:hover { background:var(--bg-raised); color:var(--accent); }
.input-error { border-color: var(--danger) !important; }
.required { color:var(--danger); }
.param-type { font-size:10px; color:var(--text-muted); background:var(--bg-raised); padding:1px 5px; border-radius:3px; margin-left:auto; }
.param-desc { font-size:11px; color:var(--text-muted); margin:3px 0 0; }
.param-select { width:100%; background:var(--bg-raised); border:1px solid var(--border); color:var(--text-primary); padding:8px; border-radius:var(--border-radius-sm); }
.w-full { width:100%; }
.muted-sm { color:var(--text-muted); font-size:12px; }
.history-item { display:flex; align-items:center; gap:8px; padding:6px 10px; border:1px solid var(--border); border-radius:var(--border-radius-sm); cursor:pointer; margin-bottom:4px; font-size:12px; color:var(--text-secondary); }
.history-item:hover { background:var(--bg-raised); border-color:var(--accent); }
.history-preview { font-family:var(--font-family-mono); overflow:hidden; text-overflow:ellipsis; white-space:nowrap; }
.invoke-bar { padding:12px 20px; border-bottom:1px solid var(--border); }
.error-box { margin:12px 20px; padding:10px 14px; background:rgba(239,68,68,.1); border:1px solid var(--danger); border-radius:var(--border-radius-sm); color:var(--danger); font-size:13px; display:flex; gap:8px; }
.result-section { flex:1; padding:12px 20px; min-height:200px; }
.tool-section-header { padding:4px 14px; font-size:10px; font-weight:600; text-transform:uppercase; letter-spacing:.06em; color:var(--text-muted); background:var(--bg-raised); border-bottom:1px solid var(--border); }
.history-btn { display:flex; width:100%; justify-content:flex-start; margin-bottom:4px; }
</style>
