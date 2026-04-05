<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
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
import { preferencesApi } from '@/api/preferences'
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
type ResourceListItem = ActiveResource | { isSeparator: true; label: string }

const filteredResources = computed<ResourceListItem[]>(() => {
  let list = resources.value
  const q = resourceSearch.value.toLowerCase()
  if (q) list = list.filter(r => r.name.toLowerCase().includes(q) || r.uri?.toLowerCase().includes(q))

  if (!showFavoritesFirst.value || favorites.value.size === 0) return list

  const favs = list.filter(r => favorites.value.has(r.uri))
  const rest = list.filter(r => !favorites.value.has(r.uri))
  const items: ResourceListItem[] = []
  if (favs.length) { items.push({ isSeparator: true, label: '⭐ Favourites' }); items.push(...favs) }
  if (rest.length) { items.push({ isSeparator: true, label: 'All Resources' }); items.push(...rest) }
  return items
})

const filteredResourcesOnly = computed(() =>
  filteredResources.value.filter((r): r is ActiveResource => !('isSeparator' in r))
)

const docsVisible = ref(false)
const listDocsVisible = ref(false)

// ── Favourites ────────────────────────────────────────────────────────
const favorites = ref<Set<string>>(new Set())
const showFavoritesFirst = ref(false)

async function toggleFav(uri: string) {
  if (favorites.value.has(uri)) favorites.value.delete(uri)
  else favorites.value.add(uri)
  favorites.value = new Set(favorites.value)
  try {
    await preferencesApi.patch({ favoriteResources: [...favorites.value] })
  } catch (e: any) {
    toast.add({ severity: 'error', summary: 'Failed to save favourite', detail: e.message, life: 3000 })
  }
}

async function toggleShowFavoritesFirst() {
  showFavoritesFirst.value = !showFavoritesFirst.value
  try {
    await preferencesApi.patch({ showResourceFavoritesFirst: showFavoritesFirst.value })
  } catch (e: any) {
    toast.add({ severity: 'error', summary: 'Failed to save preference', detail: e.message, life: 3000 })
  }
}

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

onMounted(async () => {
  try {
    const prefs = await preferencesApi.getAll()
    favorites.value = new Set(prefs.favoriteResources ?? [])
    showFavoritesFirst.value = prefs.showResourceFavoritesFirst ?? false
  } catch { /* non-fatal */ }
})
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
            <Tag v-if="resources.length" :value="filteredResourcesOnly.length < resources.length ? `${filteredResourcesOnly.length} / ${resources.length}` : String(resources.length)" :severity="filteredResourcesOnly.length < resources.length ? 'warn' : 'secondary'" />
            <button
              v-if="filteredResourcesOnly.length > 0"
              class="fav-btn"
              :style="showFavoritesFirst ? 'color:var(--warning)' : ''"
              @click="toggleShowFavoritesFirst"
              :title="showFavoritesFirst ? 'Show all resources' : 'Show favourites first'"
            ><i :class="showFavoritesFirst ? 'pi pi-star-fill' : 'pi pi-star'" /></button>
            <button
              v-if="filteredResourcesOnly.length > 0"
              class="fav-btn"
              @click="listDocsVisible = true"
              :title="`View docs for ${filteredResourcesOnly.length} visible resource${filteredResourcesOnly.length === 1 ? '' : 's'}`"
            ><i class="pi pi-book" /></button>
          </div>
          <div class="panel-search">
            <InputText v-model="resourceSearch" placeholder="Filter resources…" style="width:100%" />
          </div>
          <div v-if="loading" class="p-3"><Skeleton v-for="i in 4" :key="i" height="48px" class="mb-2" /></div>
          <div v-else-if="!selectedConnName" class="empty-panel"><p>Select a connection</p></div>
          <div v-else-if="filteredResources.length === 0" class="empty-panel"><p>No resources found</p></div>
          <div v-else class="item-list">
            <template v-for="item in filteredResources" :key="'isSeparator' in item ? item.label : item.uri">
              <div v-if="'isSeparator' in item" class="list-separator">{{ item.label }}</div>
              <div v-else class="list-item resource-item"
                   :class="{ active: selectedResource?.uri === item.uri }" @click="selectResource(item)">
                <div class="item-body-icon">
                  <img v-if="item.iconUrl" :src="item.iconUrl" :alt="item.name" class="item-icon-img" />
                  <span v-else class="item-icon-badge">{{ item.name.charAt(0).toUpperCase() }}</span>
                </div>
                <div class="resource-body">
                  <span class="item-name">{{ item.name }}</span>
                  <span class="item-uri">{{ item.uri }}</span>
                  <span v-if="item.description" class="item-desc">{{ item.description }}</span>
                </div>
                <Tag v-if="item.mimeType" :value="item.mimeType" severity="secondary" style="font-size:10px;flex-shrink:0" />
                <button class="fav-btn" @click.stop="toggleFav(item.uri)" :title="favorites.has(item.uri) ? 'Unfavourite' : 'Favourite'">
                  <i :class="favorites.has(item.uri) ? 'pi pi-star-fill' : 'pi pi-star'" :style="favorites.has(item.uri) ? 'color:var(--warning)' : ''" />
                </button>
              </div>
            </template>
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
      :raw-markdown="generateResourcesListMarkdown(filteredResourcesOnly)"
      :title="`Documentation: ${filteredResourcesOnly.length} resource${filteredResourcesOnly.length === 1 ? '' : 's'}`"
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
.list-separator { padding:6px 16px; font-size:10px; font-weight:700; text-transform:uppercase; letter-spacing:.08em; color:var(--text-muted); background:var(--bg-raised); border-bottom:1px solid var(--border); flex-shrink:0; }
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
