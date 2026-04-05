<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useToast } from 'primevue/usetoast'
import Splitter from 'primevue/splitter'
import SplitterPanel from 'primevue/splitterpanel'
import Button from 'primevue/button'
import InputText from 'primevue/inputtext'
import Skeleton from 'primevue/skeleton'
import Tag from 'primevue/tag'
import JsonViewer from '@/components/common/JsonViewer.vue'
import ToolDocsDialog from '@/components/common/ToolDocsDialog.vue'
import { useConnectionsStore } from '@/stores/connections'
import { connectionsApi } from '@/api/connections'
import { generateResourceTemplateMarkdown, generateResourceTemplatesListMarkdown } from '@/composables/useToolDocs'
import type { ActiveResourceTemplate } from '@/api/types'

const toast = useToast()
const store = useConnectionsStore()

const selectedConnName = ref<string | null>(null)
const templates = ref<ActiveResourceTemplate[]>([])
const loading = ref(false)
const templateSearch = ref('')
const selectedTemplate = ref<ActiveResourceTemplate | null>(null)
const templateParams = ref<Record<string, string>>({})
const reading = ref(false)
const content = ref<unknown>(null)
const readError = ref<string | null>(null)

const connectedConnections = computed(() => store.activeConnections.filter(c => c.isConnected))
const filteredTemplates = computed(() => {
  const q = templateSearch.value.toLowerCase()
  return q ? templates.value.filter(t => t.name.toLowerCase().includes(q) || t.uriTemplate?.toLowerCase().includes(q)) : templates.value
})

const docsVisible = ref(false)
const listDocsVisible = ref(false)

// Extract {param} placeholders from URI template
const templateVars = computed(() => {
  if (!selectedTemplate.value) return []
  const matches = selectedTemplate.value.uriTemplate.matchAll(/\{([^}]+)\}/g)
  return [...matches].map(m => m[1])
})

const resolvedUri = computed(() => {
  if (!selectedTemplate.value) return ''
  let uri = selectedTemplate.value.uriTemplate
  for (const [k, v] of Object.entries(templateParams.value)) {
    uri = uri.replace(`{${k}}`, v ? encodeURIComponent(v) : `{${k}}`)
  }
  return uri
})

watch(selectedConnName, async (name) => {
  selectedTemplate.value = null; templates.value = []; content.value = null
  if (!name) return
  loading.value = true
  try { templates.value = await store.getResourceTemplates(name) }
  catch (e: any) { toast.add({ severity: 'error', summary: 'Failed to load templates', detail: e.message, life: 5000 }) }
  finally { loading.value = false }
})

watch(selectedTemplate, () => { templateParams.value = {}; content.value = null; readError.value = null })

function selectTemplate(t: ActiveResourceTemplate) { selectedTemplate.value = t }

async function readTemplate() {
  if (!selectedConnName.value || !selectedTemplate.value) return
  reading.value = true; content.value = null; readError.value = null
  try {
    const r = await connectionsApi.readResource(selectedConnName.value, resolvedUri.value)
    try { content.value = JSON.parse(typeof r === 'string' ? r : (r as any).result ?? r) }
    catch { content.value = (r as any).result ?? r }
    toast.add({ severity: 'success', summary: 'Resource read', life: 2000 })
  } catch (e: any) {
    readError.value = e.message
    toast.add({ severity: 'error', summary: 'Read failed', detail: e.message, life: 5000 })
  } finally { reading.value = false }
}

watch(() => store.initialized, (ready, wasReady) => { if (ready && !wasReady) store.loadActive() }, { immediate: true })
</script>

<template>
  <div class="rt-view">
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
            Templates
            <Tag v-if="templates.length" :value="String(templates.length)" severity="secondary" />
            <button
              v-if="filteredTemplates.length > 0"
              class="fav-btn"
              @click="listDocsVisible = true"
              :title="`View docs for ${filteredTemplates.length} visible template${filteredTemplates.length === 1 ? '' : 's'}`"
            ><i class="pi pi-book" /></button>
          </div>
          <div class="panel-search">
            <InputText v-model="templateSearch" placeholder="Filter templates…" style="width:100%" />
          </div>
          <div v-if="loading" class="p-3"><Skeleton v-for="i in 4" :key="i" height="48px" class="mb-2" /></div>
          <div v-else-if="!selectedConnName" class="empty-panel"><p>Select a connection</p></div>
          <div v-else-if="filteredTemplates.length === 0" class="empty-panel"><p>No templates found</p></div>
          <div v-else class="item-list">
            <div v-for="t in filteredTemplates" :key="t.uriTemplate" class="list-item template-item"
                 :class="{ active: selectedTemplate?.uriTemplate === t.uriTemplate }" @click="selectTemplate(t)">
              <div class="item-body-icon">
                <img v-if="t.iconUrl" :src="t.iconUrl" :alt="t.name" class="item-icon-img" />
                <span v-else class="item-icon-badge">{{ t.name.charAt(0).toUpperCase() }}</span>
              </div>
              <div>
                <span class="item-name">{{ t.name }}</span>
                <span class="item-uri">{{ t.uriTemplate }}</span>
                <span v-if="t.description" class="item-desc">{{ t.description }}</span>
              </div>
            </div>
          </div>
        </div>
      </SplitterPanel>

      <SplitterPanel :size="45" :minSize="30">
        <div class="panel detail-panel">
          <div v-if="!selectedTemplate" class="empty-panel">
            <i class="pi pi-copy" /><p>Select a template to expand and read</p>
          </div>
          <template v-else>
            <div class="detail-header">
              <div class="detail-header-row">
                <div>
                  <h3 class="item-title">{{ selectedTemplate.name }}</h3>
                  <p class="item-template">{{ selectedTemplate.uriTemplate }}</p>
                  <p v-if="selectedTemplate.description" class="item-desc-full">{{ selectedTemplate.description }}</p>
                </div>
                <button class="fav-btn" @click="docsVisible = true" title="View documentation"><i class="pi pi-book" /></button>
              </div>
            </div>

            <div class="params-section">
              <div class="section-label">URI Parameters</div>
              <div v-if="templateVars.length === 0" class="muted-sm">No parameters</div>
              <div v-for="v in templateVars" :key="v" class="param-field">
                <label>{{ v }}</label>
                <InputText v-model="templateParams[v]" :placeholder="`Value for {${v}}`" class="w-full" />
              </div>
              <div class="resolved-uri">
                <span class="section-label" style="margin-bottom:4px;display:block">Resolved URI</span>
                <code class="uri-code">{{ resolvedUri }}</code>
              </div>
            </div>

            <div class="action-bar">
              <Button label="Read" icon="pi pi-eye" :loading="reading" @click="readTemplate" />
            </div>
            <div v-if="readError" class="error-box"><i class="pi pi-times-circle" /> {{ readError }}</div>
            <div v-if="content !== null" class="result-section">
              <JsonViewer :data="content" title="Template Content" :initially-expanded="true" />
            </div>
          </template>
        </div>
      </SplitterPanel>
    </Splitter>

    <ToolDocsDialog
      v-model:visible="docsVisible"
      :raw-markdown="selectedTemplate ? generateResourceTemplateMarkdown(selectedTemplate) : ''"
      :title="selectedTemplate ? `Documentation: ${selectedTemplate.name}` : 'Documentation'"
    />
    <ToolDocsDialog
      v-model:visible="listDocsVisible"
      :raw-markdown="generateResourceTemplatesListMarkdown(filteredTemplates)"
      :title="`Documentation: ${filteredTemplates.length} template${filteredTemplates.length === 1 ? '' : 's'}`"
    />
  </div>
</template>

<style scoped>
.rt-view { height:100%; display:flex; flex-direction:column; background:var(--bg-base); }
.splitter { flex:1; height:100%; }
.panel { display:flex; flex-direction:column; height:100%; border-right:1px solid var(--border); overflow:hidden; }
.detail-panel { border-right:none; overflow-y:auto; }
.panel-header { display:flex; align-items:center; gap:8px; padding:12px 16px; font-size:12px; font-weight:600; text-transform:uppercase; letter-spacing:.05em; color:var(--text-muted); border-bottom:1px solid var(--border); flex-shrink:0; }
.panel-search { padding:8px 12px; border-bottom:1px solid var(--border); flex-shrink:0; }
.empty-panel { display:flex; flex-direction:column; align-items:center; justify-content:center; gap:10px; flex:1; color:var(--text-muted); font-size:13px; text-align:center; padding:20px; }
.empty-panel i { font-size:32px; }
.item-list { overflow-y:auto; flex:1; }
.list-item { display:flex; align-items:flex-start; gap:8px; padding:10px 16px; cursor:pointer; border-left:2px solid transparent; border-bottom:1px solid var(--border); transition:var(--transition-fast); }
.list-item:hover { background:var(--nav-item-hover); }
.list-item.active { background:var(--nav-item-active); border-left-color:var(--accent); }
.item-body-icon { flex-shrink:0; width:28px; height:28px; display:flex; align-items:center; justify-content:center; margin-top:2px; }
.item-icon-img { width:24px; height:24px; border-radius:4px; object-fit:contain; }
.item-icon-badge { width:26px; height:26px; border-radius:6px; background:var(--accent); color:#fff; font-size:12px; font-weight:700; display:flex; align-items:center; justify-content:center; flex-shrink:0; }
.item-name { display:block; font-size:13px; font-weight:500; color:var(--text-primary); }
.item-uri { display:block; font-size:11px; font-family:var(--font-family-mono); color:var(--text-muted); }
.item-desc { display:block; font-size:11px; color:var(--text-muted); }
.dot { font-size:8px; color:var(--success); flex-shrink:0; margin-top:4px; }
.detail-header { padding:16px 20px; border-bottom:1px solid var(--border); }
.detail-header-row { display:flex; align-items:flex-start; justify-content:space-between; gap:8px; }
.fav-btn { background:none; border:none; cursor:pointer; color:var(--text-muted); padding:4px 6px; border-radius:var(--border-radius-sm); transition:var(--transition-fast); flex-shrink:0; }
.fav-btn:hover { color:var(--accent); background:var(--nav-item-hover); }
.item-title { margin:0 0 4px; font-size:16px; font-weight:600; color:var(--text-primary); }
.item-template { margin:0 0 4px; font-family:var(--font-family-mono); font-size:12px; color:var(--accent); }
.item-desc-full { margin:0; font-size:13px; color:var(--text-secondary); }
.params-section { padding:16px 20px; border-bottom:1px solid var(--border); }
.section-label { font-size:11px; font-weight:600; text-transform:uppercase; letter-spacing:.05em; color:var(--text-muted); margin-bottom:12px; }
.param-field { margin-bottom:10px; }
.param-field label { display:block; font-size:12px; color:var(--text-secondary); margin-bottom:4px; font-weight:500; font-family:var(--font-family-mono); }
.w-full { width:100%; }
.muted-sm { color:var(--text-muted); font-size:12px; }
.resolved-uri { margin-top:12px; padding:10px; background:var(--code-bg); border-radius:var(--border-radius-sm); border:1px solid var(--border); }
.uri-code { font-family:var(--font-family-mono); font-size:12px; color:var(--accent); word-break:break-all; }
.action-bar { padding:12px 20px; border-bottom:1px solid var(--border); }
.error-box { margin:12px 20px; padding:10px 14px; background:rgba(239,68,68,.1); border:1px solid var(--danger); border-radius:var(--border-radius-sm); color:var(--danger); font-size:13px; display:flex; gap:8px; }
.result-section { flex:1; padding:12px 20px; min-height:200px; }
</style>
