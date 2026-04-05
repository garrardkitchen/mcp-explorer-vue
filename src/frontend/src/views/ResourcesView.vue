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
import { generateResourceMarkdown, generateResourcesListMarkdown } from '@/composables/useToolDocs'
import type { ActiveResource } from '@/api/types'

const toast = useToast()
const store = useConnectionsStore()

const selectedConnName = ref<string | null>(null)
const resources = ref<ActiveResource[]>([])
const loading = ref(false)
const resourceSearch = ref('')
const selectedResource = ref<ActiveResource | null>(null)
const reading = ref(false)
const content = ref<unknown>(null)
const readError = ref<string | null>(null)

const connectedConnections = computed(() => store.activeConnections.filter(c => c.isConnected))
const filteredResources = computed(() => {
  const q = resourceSearch.value.toLowerCase()
  if (!q) return resources.value
  return resources.value.filter(r => r.name.toLowerCase().includes(q) || r.uri?.toLowerCase().includes(q))
})

const docsVisible = ref(false)
const listDocsVisible = ref(false)

watch(selectedConnName, async (name) => {
  selectedResource.value = null; resources.value = []; content.value = null
  if (!name) return
  loading.value = true
  try { resources.value = await store.getResources(name) }
  catch (e: any) { toast.add({ severity: 'error', summary: 'Failed to load resources', detail: e.message, life: 5000 }) }
  finally { loading.value = false }
})

watch(selectedResource, () => { content.value = null; readError.value = null })

function selectResource(r: ActiveResource) { selectedResource.value = r }

async function readResource() {
  if (!selectedConnName.value || !selectedResource.value) return
  reading.value = true; content.value = null; readError.value = null
  try {
    const r = await connectionsApi.readResource(selectedConnName.value, selectedResource.value.uri)
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
  <div class="resources-view">
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

      <SplitterPanel :size="40" :minSize="25">
        <div class="panel">
          <div class="panel-header">
            Resources
            <Tag v-if="resources.length" :value="String(resources.length)" severity="secondary" />
            <button
              v-if="filteredResources.length > 0"
              class="fav-btn"
              @click="listDocsVisible = true"
              :title="`View docs for ${filteredResources.length} visible resource${filteredResources.length === 1 ? '' : 's'}`"
            ><i class="pi pi-book" /></button>
          </div>
          <div class="panel-search">
            <InputText v-model="resourceSearch" placeholder="Filter resources…" style="width:100%" />
          </div>
          <div v-if="loading" class="p-3"><Skeleton v-for="i in 4" :key="i" height="48px" class="mb-2" /></div>
          <div v-else-if="!selectedConnName" class="empty-panel"><p>Select a connection</p></div>
          <div v-else-if="filteredResources.length === 0" class="empty-panel"><p>No resources found</p></div>
          <div v-else class="item-list">
            <div v-for="r in filteredResources" :key="r.uri" class="list-item resource-item"
                 :class="{ active: selectedResource?.uri === r.uri }" @click="selectResource(r)">
              <div class="item-body-icon">
                <img v-if="r.iconUrl" :src="r.iconUrl" :alt="r.name" class="item-icon-img" />
                <span v-else class="item-icon-badge">{{ r.name.charAt(0).toUpperCase() }}</span>
              </div>
              <div class="resource-body">
                <span class="item-name">{{ r.name }}</span>
                <span class="item-uri">{{ r.uri }}</span>
                <span v-if="r.description" class="item-desc">{{ r.description }}</span>
              </div>
              <Tag v-if="r.mimeType" :value="r.mimeType" severity="secondary" style="font-size:10px;flex-shrink:0" />
            </div>
          </div>
        </div>
      </SplitterPanel>

      <SplitterPanel :size="40" :minSize="30">
        <div class="panel detail-panel">
          <div v-if="!selectedResource" class="empty-panel">
            <i class="pi pi-database" /><p>Select a resource to read it</p>
          </div>
          <template v-else>
            <div class="detail-header">
              <div class="detail-header-row">
                <div>
                  <h3 class="item-title">{{ selectedResource.name }}</h3>
                  <p class="item-uri-full">{{ selectedResource.uri }}</p>
                  <p v-if="selectedResource.description" class="item-desc-full">{{ selectedResource.description }}</p>
                  <div class="meta-row">
                    <Tag v-if="selectedResource.mimeType" :value="selectedResource.mimeType" severity="secondary" />
                  </div>
                </div>
                <button class="fav-btn" @click="docsVisible = true" title="View documentation"><i class="pi pi-book" /></button>
              </div>
            </div>
            <div class="action-bar">
              <Button label="Read Resource" icon="pi pi-eye" :loading="reading" @click="readResource" />
            </div>
            <div v-if="readError" class="error-box"><i class="pi pi-times-circle" /> {{ readError }}</div>
            <div v-if="content !== null" class="result-section">
              <JsonViewer :data="content" title="Resource Content" :initially-expanded="true" />
            </div>
          </template>
        </div>
      </SplitterPanel>
    </Splitter>

    <ToolDocsDialog
      v-model:visible="docsVisible"
      :raw-markdown="selectedResource ? generateResourceMarkdown(selectedResource) : ''"
      :title="selectedResource ? `Documentation: ${selectedResource.name}` : 'Documentation'"
    />
    <ToolDocsDialog
      v-model:visible="listDocsVisible"
      :raw-markdown="generateResourcesListMarkdown(filteredResources)"
      :title="`Documentation: ${filteredResources.length} resource${filteredResources.length === 1 ? '' : 's'}`"
    />
  </div>
</template>

<style scoped>
.resources-view { height:100%; display:flex; flex-direction:column; background:var(--bg-base); }
.splitter { flex:1; height:100%; }
.panel { display:flex; flex-direction:column; height:100%; border-right:1px solid var(--border); overflow:hidden; }
.detail-panel { border-right:none; overflow-y:auto; }
.panel-header { display:flex; align-items:center; gap:8px; padding:12px 16px; font-size:12px; font-weight:600; text-transform:uppercase; letter-spacing:.05em; color:var(--text-muted); border-bottom:1px solid var(--border); flex-shrink:0; }
.panel-search { padding:8px 12px; border-bottom:1px solid var(--border); flex-shrink:0; }
.empty-panel { display:flex; flex-direction:column; align-items:center; justify-content:center; gap:10px; flex:1; color:var(--text-muted); font-size:13px; text-align:center; padding:20px; }
.empty-panel i { font-size:32px; }
.item-list { overflow-y:auto; flex:1; }
.list-item { display:flex; align-items:center; gap:8px; padding:10px 16px; cursor:pointer; border-left:2px solid transparent; border-bottom:1px solid var(--border); transition:var(--transition-fast); }
.list-item:hover { background:var(--nav-item-hover); }
.list-item.active { background:var(--nav-item-active); border-left-color:var(--accent); }
.resource-item { align-items:flex-start; }
.item-body-icon { flex-shrink:0; width:28px; height:28px; display:flex; align-items:center; justify-content:center; margin-top:2px; }
.item-icon-img { width:24px; height:24px; border-radius:4px; object-fit:contain; }
.item-icon-badge { width:26px; height:26px; border-radius:6px; background:var(--accent); color:#fff; font-size:12px; font-weight:700; display:flex; align-items:center; justify-content:center; flex-shrink:0; }
.resource-body { flex:1; min-width:0; }
.item-name { display:block; font-size:13px; font-weight:500; color:var(--text-primary); }
.item-uri { display:block; font-size:11px; font-family:var(--font-family-mono); color:var(--text-muted); overflow:hidden; text-overflow:ellipsis; white-space:nowrap; }
.item-desc { display:block; font-size:11px; color:var(--text-muted); }
.dot { font-size:8px; color:var(--success); flex-shrink:0; }
.detail-header { padding:16px 20px; border-bottom:1px solid var(--border); }
.detail-header-row { display:flex; align-items:flex-start; justify-content:space-between; gap:8px; }
.fav-btn { background:none; border:none; cursor:pointer; color:var(--text-muted); padding:4px 6px; border-radius:var(--border-radius-sm); transition:var(--transition-fast); flex-shrink:0; }
.fav-btn:hover { color:var(--accent); background:var(--nav-item-hover); }
.item-title { margin:0 0 4px; font-size:16px; font-weight:600; color:var(--text-primary); }
.item-uri-full { margin:0 0 4px; font-family:var(--font-family-mono); font-size:12px; color:var(--text-muted); word-break:break-all; }
.item-desc-full { margin:0 0 8px; font-size:13px; color:var(--text-secondary); }
.meta-row { display:flex; gap:8px; flex-wrap:wrap; }
.action-bar { padding:12px 20px; border-bottom:1px solid var(--border); }
.error-box { margin:12px 20px; padding:10px 14px; background:rgba(239,68,68,.1); border:1px solid var(--danger); border-radius:var(--border-radius-sm); color:var(--danger); font-size:13px; display:flex; gap:8px; }
.result-section { flex:1; padding:12px 20px; min-height:200px; }
</style>
