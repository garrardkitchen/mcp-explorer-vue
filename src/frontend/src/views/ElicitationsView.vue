<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useToast } from 'primevue/usetoast'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'
import Select from 'primevue/select'
import Tag from 'primevue/tag'
import Skeleton from 'primevue/skeleton'
import Tabs from 'primevue/tabs'
import TabList from 'primevue/tablist'
import Tab from 'primevue/tab'
import TabPanels from 'primevue/tabpanels'
import TabPanel from 'primevue/tabpanel'
import { elicitationApi } from '@/api/elicitation'
import type { ElicitationRequest, ElicitationHistoryEntry } from '@/api/types'

const toast = useToast()

const pending = ref<ElicitationRequest[]>([])
const history = ref<ElicitationHistoryEntry[]>([])
const loadingPending = ref(false)
const loadingHistory = ref(false)
const activeTab = ref('0')

// SSE
let eventSource: EventSource | null = null
const sseStatus = ref<'connecting' | 'connected' | 'disconnected'>('disconnected')

// Respond dialog
const showRespondDialog = ref(false)
const respondingRequest = ref<ElicitationRequest | null>(null)
const responseAction = ref<'Accept' | 'Decline'>('Accept')
const responseContent = ref<Record<string, string | number | boolean>>({})
const submitting = ref(false)

const actionOptions = [
  { label: 'Accept', value: 'Accept' },
  { label: 'Decline', value: 'Decline' },
]

async function loadPending() {
  loadingPending.value = true
  try { pending.value = await elicitationApi.getPending() }
  catch { pending.value = [] }
  finally { loadingPending.value = false }
}

async function loadHistory() {
  loadingHistory.value = true
  try { history.value = await elicitationApi.getHistory() }
  catch { history.value = [] }
  finally { loadingHistory.value = false }
}

function connectSse() {
  if (eventSource) { eventSource.close() }
  sseStatus.value = 'connecting'
  eventSource = elicitationApi.createStream(
    (req: ElicitationRequest) => {
      // Add to pending list if not already there
      if (!pending.value.find(p => p.id === req.id)) {
        pending.value.unshift(req)
        toast.add({ severity: 'info', summary: 'Elicitation Request', detail: `From: ${req.connectionName}`, life: 8000 })
      }
    },
    () => { sseStatus.value = 'disconnected' }
  )
  eventSource.onopen = () => { sseStatus.value = 'connected' }
}

function disconnectSse() {
  eventSource?.close()
  eventSource = null
  sseStatus.value = 'disconnected'
}

function openRespond(req: ElicitationRequest) {
  respondingRequest.value = req
  responseAction.value = 'Accept'
  responseContent.value = {}
  // Pre-populate fields from schema
  const schema = req.schema as any
  if (schema?.properties) {
    for (const [key, prop] of Object.entries(schema.properties as Record<string, any>)) {
      responseContent.value[key] = prop.type === 'boolean' ? false : prop.type === 'number' ? 0 : ''
    }
  }
  showRespondDialog.value = true
}

async function submitResponse() {
  if (!respondingRequest.value) return
  submitting.value = true
  try {
    await elicitationApi.respond(
      respondingRequest.value.id,
      responseAction.value,
      responseAction.value === 'Accept' ? responseContent.value as Record<string, unknown> : undefined
    )
    pending.value = pending.value.filter(p => p.id !== respondingRequest.value?.id)
    showRespondDialog.value = false
    await loadHistory()
    toast.add({ severity: 'success', summary: `Response submitted: ${responseAction.value}`, life: 3000 })
  } catch (e: any) {
    toast.add({ severity: 'error', summary: 'Submit failed', detail: e.message, life: 5000 })
  } finally { submitting.value = false }
}

function getSchemaFields(schema: Record<string, unknown>) {
  const s = schema as any
  if (!s?.properties) return []
  const required: string[] = s.required ?? []
  return Object.entries(s.properties as Record<string, any>).map(([k, v]) => ({
    name: k, type: v.type ?? 'string', description: v.description ?? '',
    required: required.includes(k), enum: v.enum,
  }))
}

function formatTime(ts: string) {
  return new Date(ts).toLocaleString()
}

function statusSeverity(s: string) {
  return s === 'Accepted' ? 'success' : s === 'Rejected' ? 'danger' : 'warn'
}

onMounted(async () => {
  await Promise.all([loadPending(), loadHistory()])
  connectSse()
})

onUnmounted(() => disconnectSse())
</script>

<template>
  <div class="elicitations-view">
    <div class="toolbar">
      <div class="toolbar-left">
        <span class="toolbar-title">Elicitations</span>
        <div class="sse-indicator" :class="sseStatus">
          <span class="sse-dot" />
          <span class="sse-label">{{ sseStatus === 'connected' ? 'Live' : sseStatus === 'connecting' ? 'Connecting…' : 'Disconnected' }}</span>
        </div>
      </div>
      <div class="toolbar-actions">
        <Button v-if="sseStatus === 'disconnected'" label="Reconnect" icon="pi pi-refresh" size="small" @click="connectSse" />
        <Button v-else label="Disconnect" icon="pi pi-stop-circle" size="small" severity="secondary" @click="disconnectSse" />
        <Button icon="pi pi-refresh" text size="small" v-tooltip="'Refresh'" @click="loadPending" />
      </div>
    </div>

    <Tabs v-model:value="activeTab" class="tabs-container">
      <TabList>
        <Tab value="0">
          Pending
          <span v-if="pending.length" class="count-badge">{{ pending.length }}</span>
        </Tab>
        <Tab value="1">History</Tab>
      </TabList>

      <TabPanels class="tab-panels">
        <!-- Pending -->
        <TabPanel value="0">
          <div v-if="loadingPending" class="p-4"><Skeleton v-for="i in 3" :key="i" height="100px" class="mb-3" /></div>
          <div v-else-if="pending.length === 0" class="empty-state">
            <i class="pi pi-bell" />
            <p>No pending elicitation requests</p>
            <p class="text-muted-sm">The SSE stream will notify you when MCP servers request user input.</p>
          </div>
          <div v-else class="pending-list">
            <div v-for="req in pending" :key="req.id" class="request-card">
              <div class="request-header">
                <div class="request-meta">
                  <Tag :value="req.connectionName" severity="secondary" />
                  <span class="request-time">{{ formatTime(req.timestampUtc) }}</span>
                </div>
                <Tag :value="req.status" :severity="statusSeverity(req.status)" />
              </div>
              <div v-if="req.message" class="request-message">{{ req.message }}</div>
              <div v-if="getSchemaFields(req.schema).length" class="schema-preview">
                <span class="schema-label">Requested fields: </span>
                <span v-for="f in getSchemaFields(req.schema)" :key="f.name" class="schema-field">{{ f.name }}</span>
              </div>
              <div class="request-actions">
                <Button label="Respond" icon="pi pi-reply" size="small" @click="openRespond(req)" :disabled="req.status !== 'Pending'" />
              </div>
            </div>
          </div>
        </TabPanel>

        <!-- History -->
        <TabPanel value="1">
          <div v-if="loadingHistory" class="p-4"><Skeleton v-for="i in 5" :key="i" height="80px" class="mb-3" /></div>
          <div v-else-if="history.length === 0" class="empty-state">
            <i class="pi pi-history" /><p>No elicitation history</p>
          </div>
          <div v-else class="history-list">
            <div v-for="entry in history" :key="entry.request.id" class="history-card">
              <div class="history-header">
                <Tag :value="entry.request.connectionName" severity="secondary" />
                <span class="request-time">{{ formatTime(entry.request.timestampUtc) }}</span>
                <Tag v-if="entry.response" :value="entry.response.action" :severity="entry.response.action === 'Accept' ? 'success' : 'danger'" />
                <Tag v-else :value="entry.request.status" :severity="statusSeverity(entry.request.status)" />
              </div>
              <div v-if="entry.request.message" class="request-message">{{ entry.request.message }}</div>
              <div v-if="entry.response?.content" class="response-content">
                <span class="schema-label">Response: </span>
                <code class="response-code">{{ JSON.stringify(entry.response.content) }}</code>
              </div>
              <div v-if="entry.response?.timestampUtc" class="responded-at">
                Responded: {{ formatTime(entry.response.timestampUtc) }}
              </div>
            </div>
          </div>
        </TabPanel>
      </TabPanels>
    </Tabs>

    <!-- Respond dialog -->
    <Dialog v-model:visible="showRespondDialog" header="Respond to Request" modal :style="{ width: '520px' }">
      <div v-if="respondingRequest" class="respond-form">
        <div class="request-context">
          <div class="context-row">
            <span class="context-label">Connection:</span>
            <Tag :value="respondingRequest.connectionName" severity="secondary" />
          </div>
          <div v-if="respondingRequest.message" class="context-message">
            <span class="context-label">Message:</span>
            <p>{{ respondingRequest.message }}</p>
          </div>
        </div>

        <div class="form-field">
          <label>Action</label>
          <Select v-model="responseAction" :options="actionOptions" optionLabel="label" optionValue="value" style="width:100%" />
        </div>

        <template v-if="responseAction === 'Accept'">
          <div v-for="f in getSchemaFields(respondingRequest.schema)" :key="f.name" class="form-field">
            <label>
              {{ f.name }}
              <span v-if="f.required" class="required">*</span>
              <span class="field-type">{{ f.type }}</span>
            </label>
            <p v-if="f.description" class="field-desc">{{ f.description }}</p>
            <select v-if="f.enum" v-model="responseContent[f.name]" class="native-select">
              <option v-for="opt in f.enum" :key="opt" :value="opt">{{ opt }}</option>
            </select>
            <div v-else-if="f.type === 'boolean'" class="bool-field">
              <label class="bool-label">
                <input type="checkbox" v-model="responseContent[f.name]" />
                {{ responseContent[f.name] ? 'True' : 'False' }}
              </label>
            </div>
            <InputText v-else v-model="(responseContent as any)[f.name]" :placeholder="f.name" style="width:100%" />
          </div>
        </template>
      </div>
      <template #footer>
        <Button label="Cancel" severity="secondary" text @click="showRespondDialog = false" />
        <Button :label="`Submit ${responseAction}`" :icon="responseAction === 'Accept' ? 'pi pi-check' : 'pi pi-times'"
                :severity="responseAction === 'Accept' ? 'success' : 'danger'"
                :loading="submitting" @click="submitResponse" />
      </template>
    </Dialog>
  </div>
</template>

<style scoped>
.elicitations-view { display:flex; flex-direction:column; height:100%; background:var(--bg-base); color:var(--text-primary); overflow:hidden; }
.toolbar { display:flex; align-items:center; justify-content:space-between; padding:12px 20px; background:var(--bg-surface); border-bottom:1px solid var(--border); flex-shrink:0; }
.toolbar-left { display:flex; align-items:center; gap:14px; }
.toolbar-title { font-weight:600; font-size:15px; }
.toolbar-actions { display:flex; gap:8px; }
.sse-indicator { display:flex; align-items:center; gap:6px; font-size:12px; }
.sse-dot { width:8px; height:8px; border-radius:50%; background:var(--text-muted); flex-shrink:0; }
.sse-indicator.connected .sse-dot { background:var(--success); animation:pulse 2s ease-in-out infinite; }
.sse-indicator.connecting .sse-dot { background:var(--warning); animation:pulse 1s ease-in-out infinite; }
.sse-indicator.disconnected .sse-dot { background:var(--text-muted); }
.sse-indicator.connected .sse-label { color:var(--success); }
.sse-indicator.connecting .sse-label { color:var(--warning); }
.sse-indicator.disconnected .sse-label { color:var(--text-muted); }
@keyframes pulse { 0%,100%{opacity:1} 50%{opacity:0.4} }
.tabs-container { flex:1; display:flex; flex-direction:column; overflow:hidden; }
.tab-panels { flex:1; overflow-y:auto; }
.count-badge { display:inline-flex; align-items:center; justify-content:center; min-width:18px; height:18px; background:var(--danger); color:#fff; border-radius:999px; font-size:10px; font-weight:600; margin-left:6px; padding:0 4px; }
.empty-state { display:flex; flex-direction:column; align-items:center; justify-content:center; gap:10px; min-height:300px; color:var(--text-muted); text-align:center; padding:24px; }
.empty-state i { font-size:48px; }
.empty-state p { margin:0; }
.text-muted-sm { font-size:12px; }
.pending-list, .history-list { display:flex; flex-direction:column; gap:12px; padding:16px 20px; }
.request-card, .history-card { background:var(--bg-surface); border:1px solid var(--border); border-radius:var(--border-radius-md); padding:14px 16px; border-left:3px solid var(--warning); }
.history-card { border-left-color:var(--text-muted); }
.request-header, .history-header { display:flex; align-items:center; gap:8px; margin-bottom:8px; flex-wrap:wrap; }
.request-time { font-size:12px; color:var(--text-muted); margin-left:auto; }
.request-message { font-size:14px; color:var(--text-primary); margin-bottom:8px; line-height:1.5; }
.schema-preview { display:flex; align-items:center; gap:6px; flex-wrap:wrap; margin-bottom:10px; }
.schema-label { font-size:11px; color:var(--text-muted); text-transform:uppercase; letter-spacing:.04em; }
.schema-field { background:var(--bg-raised); color:var(--text-secondary); border-radius:999px; padding:2px 8px; font-size:11px; font-family:var(--font-family-mono); }
.request-actions { display:flex; gap:8px; }
.response-content { display:flex; align-items:center; gap:8px; margin-top:6px; font-size:12px; }
.response-code { font-family:var(--font-family-mono); color:var(--text-secondary); }
.responded-at { font-size:11px; color:var(--text-muted); margin-top:4px; }
/* Respond dialog */
.respond-form { display:flex; flex-direction:column; gap:14px; }
.request-context { background:var(--bg-raised); border:1px solid var(--border); border-radius:var(--border-radius-sm); padding:12px; }
.context-row { display:flex; align-items:center; gap:8px; margin-bottom:6px; }
.context-label { font-size:12px; color:var(--text-muted); text-transform:uppercase; font-weight:500; }
.context-message p { margin:4px 0 0; font-size:14px; color:var(--text-primary); }
.form-field { display:flex; flex-direction:column; gap:6px; }
.form-field label { display:flex; align-items:center; gap:6px; font-size:12px; font-weight:500; color:var(--text-secondary); text-transform:uppercase; letter-spacing:.04em; }
.required { color:var(--danger); }
.field-type { font-size:10px; color:var(--text-muted); background:var(--bg-raised); padding:1px 5px; border-radius:3px; margin-left:auto; }
.field-desc { margin:0 0 4px; font-size:12px; color:var(--text-muted); }
.native-select { width:100%; background:var(--bg-raised); border:1px solid var(--border); color:var(--text-primary); padding:8px; border-radius:var(--border-radius-sm); }
.bool-field { display:flex; align-items:center; }
.bool-label { display:flex; align-items:center; gap:8px; cursor:pointer; font-size:14px; color:var(--text-primary); font-weight:normal; text-transform:none; }
</style>
