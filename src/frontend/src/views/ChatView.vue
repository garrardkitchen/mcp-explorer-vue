<script setup lang="ts">
import { ref, computed, watch, onMounted, nextTick } from 'vue'
import { marked } from 'marked'
import DOMPurify from 'dompurify'
import { useToast } from 'primevue/usetoast'
import { useConfirm } from 'primevue/useconfirm'
import Button from 'primevue/button'
import InputText from 'primevue/inputtext'
import Textarea from 'primevue/textarea'
import Select from 'primevue/select'
import MultiSelect from 'primevue/multiselect'
import ToggleSwitch from 'primevue/toggleswitch'
import Dialog from 'primevue/dialog'
import Tooltip from 'primevue/tooltip'
import ChatReportDialog from '@/components/chat/ChatReportDialog.vue'
import { useChatStore } from '@/stores/chat'
import { useConnectionsStore } from '@/stores/connections'
import { llmModelsApi } from '@/api/llmModels'
import { apiClient } from '@/api/client'
import type { LlmModelDefinition } from '@/api/types'

marked.setOptions({ gfm: true, breaks: true })

const vTooltip = Tooltip

const toast = useToast()
const confirm = useConfirm()
const chatStore = useChatStore()
const connStore = useConnectionsStore()

const messageText = ref('')
const selectedModelName = ref<string | null>(null)
const selectedConnectionNames = ref<string[]>([])
const llmModels = ref<LlmModelDefinition[]>([])
const messagesEl = ref<HTMLElement>()
const renameDialog = ref(false)
const renameValue = ref('')
const renamingId = ref('')
const renderMarkdownEnabled = ref(true)
const reportVisible = ref(false)
const systemPromptVisible = ref(false)
const editSystemPrompt = ref('')
const savingSystemPrompt = ref(false)
const expandedParams = ref(new Set<string>())
const revealedParams = ref(new Set<string>())

const allParamsExpanded = computed(() => {
  const toolCalls = chatStore.messages.filter(m => m.role.toLowerCase() === 'system' && m.toolCallParameters)
  return toolCalls.length > 0 && toolCalls.every(m => expandedParams.value.has(m.id))
})
const hasAnyToolParams = computed(() =>
  chatStore.messages.some(m => m.role.toLowerCase() === 'system' && m.toolCallParameters)
)

function toggleAllParams() {
  const toolCalls = chatStore.messages.filter(m => m.role.toLowerCase() === 'system' && m.toolCallParameters)
  expandedParams.value = allParamsExpanded.value
    ? new Set<string>()
    : new Set(toolCalls.map(m => m.id))
}

// Sensitive property name patterns (same list as old Blazor ToolParameterProtectionService)
const SENSITIVE_PATTERNS = /^(key|password|token|secret|auth|credential|api[_\-]?key|access[_\-]?token|client[_\-]?secret|private[_\-]?key|bearer|passphrase|pass|pwd|apikey|clientsecret|accesstoken)$/i

function hasSensitiveParams(params: string | undefined): boolean {
  if (!params) return false
  try {
    const obj = JSON.parse(params)
    return typeof obj === 'object' && obj !== null &&
      Object.keys(obj).some(k => SENSITIVE_PATTERNS.test(k))
  } catch { return false }
}

function getMaskedParams(params: string, msgId: string): string {
  if (!params) return ''
  try {
    const obj = JSON.parse(params)
    const revealed = revealedParams.value.has(msgId)
    if (revealed || typeof obj !== 'object' || obj === null) return JSON.stringify(obj, null, 2)
    const masked = Object.fromEntries(
      Object.entries(obj).map(([k, v]) => [
        k,
        SENSITIVE_PATTERNS.test(k) && typeof v === 'string' ? '████████' : v
      ])
    )
    return JSON.stringify(masked, null, 2)
  } catch { return params }
}

function toggleReveal(msgId: string) {
  const s = new Set(revealedParams.value)
  if (s.has(msgId)) s.delete(msgId); else s.add(msgId)
  revealedParams.value = s
}

const connectedConnections = computed(() => connStore.activeConnections.filter(c => c.isConnected))
const sessionIndex = computed(() => chatStore.sessions.findIndex(s => s.id === chatStore.activeSessionId))
const currentSession = computed(() => chatStore.sessions.find(s => s.id === chatStore.activeSessionId))

// ── Markdown rendering ─────────────────────────────────────────────────
function renderContent(text: string, role: string): string {
  if (!text) return ''
  if (renderMarkdownEnabled.value && role.toLowerCase() === 'assistant') {
    const raw = marked(text) as string
    return DOMPurify.sanitize(raw, { FORBID_TAGS: ['script', 'style', 'iframe'], FORCE_BODY: true })
  }
  return escapeHtml(text).replace(/\n/g, '<br />')
}

function escapeHtml(t: string): string {
  return t
    .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;').replace(/'/g, '&#39;')
}

// ── Message helpers ────────────────────────────────────────────────────
function isToolCall(msg: { role: string; toolCallName?: string }) {
  return msg.role.toLowerCase() === 'system' && !!msg.toolCallName
}

function getAvatar(role: string): string {
  const map: Record<string, string> = { user: '👤', assistant: '🤖', system: '⚙️' }
  return map[role.toLowerCase()] ?? '💬'
}

function getName(role: string): string {
  const map: Record<string, string> = { user: 'You', assistant: 'Assistant', system: 'System' }
  return map[role.toLowerCase()] ?? role
}

function formatTimestamp(ts: string): string {
  return new Date(ts).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
}

function formatMs(ms: number): string {
  if (ms < 1000) return `${ms}ms`
  return `${(ms / 1000).toFixed(1)}s`
}

function formatThinking(ms: number): string {
  const s = Math.floor(ms / 1000)
  const m = Math.floor(s / 60)
  const sec = s % 60
  if (m > 0) return `${String(m).padStart(2, '0')}:${String(sec).padStart(2, '0')}`
  return `${(ms / 1000).toFixed(1)}s`
}

function prettyJson(raw: string): string {
  try { return JSON.stringify(JSON.parse(raw), null, 2) } catch { return raw }
}

function toggleParams(id: string) {
  const s = new Set(expandedParams.value)
  if (s.has(id)) s.delete(id); else s.add(id)
  expandedParams.value = s
}

// ── Copy message ───────────────────────────────────────────────────────
async function copyMessage(text: string) {
  try {
    await navigator.clipboard.writeText(text)
    toast.add({ severity: 'success', summary: 'Copied', life: 1500 })
  } catch {
    toast.add({ severity: 'error', summary: 'Copy failed', life: 3000 })
  }
}

// ── Session navigation with arrow keys ────────────────────────────────
function onSidebarKey(e: KeyboardEvent) {
  if (!['ArrowDown', 'ArrowUp'].includes(e.key)) return
  e.preventDefault()
  const idx = sessionIndex.value
  if (e.key === 'ArrowDown' && idx < chatStore.sessions.length - 1)
    chatStore.selectSession(chatStore.sessions[idx + 1].id)
  else if (e.key === 'ArrowUp' && idx > 0)
    chatStore.selectSession(chatStore.sessions[idx - 1].id)
}

// ── Send message ───────────────────────────────────────────────────────
async function sendMessage() {
  const text = messageText.value.trim()
  if (!text || chatStore.streaming) return
  if (!chatStore.activeSessionId) await createSession()
  messageText.value = ''
  try {
    await chatStore.sendMessage(text, selectedModelName.value ?? undefined, selectedConnectionNames.value)
    await nextTick()
    scrollToBottom()
  } catch (e: any) {
    toast.add({ severity: 'error', summary: 'Send failed', detail: e.message, life: 5000 })
  }
}

function onInputKeyDown(e: KeyboardEvent) {
  if (e.key === 'Enter' && e.ctrlKey) { e.preventDefault(); sendMessage() }
}

async function createSession() {
  try {
    const id = await chatStore.createSession()
    await chatStore.selectSession(id)
  } catch (e: any) {
    toast.add({ severity: 'error', summary: 'Failed to create session', detail: e.message, life: 5000 })
  }
}

function deleteSession(id: string) {
  const name = chatStore.sessions.find(s => s.id === id)?.name ?? 'this session'
  confirm.require({
    message: `Delete "${name}"? This cannot be undone.`,
    header: 'Delete Session',
    icon: 'pi pi-exclamation-triangle',
    rejectProps: { label: 'Cancel', severity: 'secondary', outlined: true },
    acceptProps: { label: 'Delete', severity: 'danger' },
    accept: () => chatStore.deleteSession(id).catch((e: any) =>
      toast.add({ severity: 'error', summary: 'Delete failed', detail: e.message, life: 5000 })
    ),
  })
}

function selectSession(id: string) {
  chatStore.selectSession(id).then(() => {
    expandedParams.value = new Set<string>()
    revealedParams.value = new Set<string>()
    // Pre-select the model used in this session (last assistant message wins)
    const lastModel = [...chatStore.messages].reverse().find(m => m.modelName)?.modelName
    if (lastModel) selectedModelName.value = lastModel
    nextTick().then(scrollToBottom)
  })
}

function openSystemPrompt() {
  const model = llmModels.value.find(m => m.name === selectedModelName.value)
  editSystemPrompt.value = model?.systemPrompt ?? ''
  systemPromptVisible.value = true
}

async function saveSystemPrompt() {
  if (!selectedModelName.value) return
  savingSystemPrompt.value = true
  try {
    await llmModelsApi.patchSystemPrompt(selectedModelName.value, editSystemPrompt.value)
    // Update local model so it's reflected immediately
    const model = llmModels.value.find(m => m.name === selectedModelName.value)
    if (model) model.systemPrompt = editSystemPrompt.value
    systemPromptVisible.value = false
    toast.add({ severity: 'success', summary: 'System prompt saved', life: 2000 })
  } catch (e: any) {
    toast.add({ severity: 'error', summary: 'Save failed', detail: e.message, life: 5000 })
  } finally {
    savingSystemPrompt.value = false
  }
}

function openRename(id: string, current: string) {  renamingId.value = id
  renameValue.value = current
  renameDialog.value = true
}

async function saveRename() {
  if (!renamingId.value || !renameValue.value.trim()) { renameDialog.value = false; return }
  try {
    await apiClient.patch(`/chat/sessions/${renamingId.value}`, { name: renameValue.value.trim() })
    await chatStore.loadSessions()
    toast.add({ severity: 'success', summary: 'Session renamed', life: 2000 })
  } catch {
    const session = chatStore.sessions.find(s => s.id === renamingId.value)
    if (session) session.name = renameValue.value.trim()
  }
  renameDialog.value = false
}

function scrollToBottom() {
  if (messagesEl.value) messagesEl.value.scrollTop = messagesEl.value.scrollHeight
}

watch(() => chatStore.messages.length, () => {
  nextTick().then(scrollToBottom)
})
watch(() => chatStore.streamingContent, () => nextTick().then(scrollToBottom))

onMounted(async () => {
  try {
    llmModels.value = await llmModelsApi.getAll()
    const sel = await llmModelsApi.getSelected()
    selectedModelName.value = sel.selectedModelName
  } catch { /* ignore */ }
})

watch(() => connStore.initialized, async (ready, wasReady) => {
  if (ready && !wasReady) {
    await Promise.all([chatStore.loadSessions(), connStore.loadActive()])
  }
}, { immediate: true })
</script>

<template>
  <div class="chat-view">

    <!-- ── Session sidebar ──────────────────────────────────── -->
    <aside class="session-sidebar" @keydown="onSidebarKey" tabindex="0">
      <div class="sidebar-header">
        <span>Sessions</span>
        <Button icon="pi pi-plus" text size="small" v-tooltip="'New session'" @click="createSession" />
      </div>
      <div class="session-list">
        <div v-if="chatStore.sessions.length === 0" class="empty-sessions">
          <p>No sessions yet</p>
        </div>
        <div
          v-for="s in chatStore.sessions"
          :key="s.id"
          class="session-item"
          :class="{ active: chatStore.activeSessionId === s.id }"
          @click="selectSession(s.id)"
        >
          <div class="session-info">
            <span class="session-name">{{ s.name }}</span>
            <span class="session-meta">{{ s.messageCount }} msgs</span>
          </div>
          <div class="session-actions">
            <button class="icon-btn" @click.stop="openRename(s.id, s.name)" title="Rename">
              <i class="pi pi-pencil" />
            </button>
            <button class="icon-btn danger" @click.stop="deleteSession(s.id)" title="Delete">
              <i class="pi pi-trash" />
            </button>
          </div>
        </div>
      </div>
    </aside>

    <!-- ── Main chat area ───────────────────────────────────── -->
    <div class="chat-main">

      <!-- Toolbar (shown when session active) -->
      <div v-if="chatStore.activeSessionId" class="chat-toolbar">
        <div class="toolbar-left">
          <span class="session-title">{{ currentSession?.name }}</span>
        </div>
        <div class="toolbar-right">
          <label class="md-toggle">
            <span class="md-toggle-label">Markdown</span>
            <ToggleSwitch v-model="renderMarkdownEnabled" />
          </label>
          <button
            class="params-expand-all-btn"
            :title="allParamsExpanded ? 'Collapse all parameters' : 'Expand all parameters'"
            :disabled="!hasAnyToolParams"
            @click="toggleAllParams"
          >{{ allParamsExpanded ? '📂' : '📁' }}</button>
          <Button
            label="System Prompt"
            icon="pi pi-sliders-h"
            size="small"
            text
            :disabled="!selectedModelName"
            :title="selectedModelName ? `Edit system prompt for ${selectedModelName}` : 'Select a model first'"
            @click="openSystemPrompt"
          />
          <Button
            label="Report"
            icon="pi pi-file-edit"
            size="small"
            text
            :disabled="chatStore.messages.length === 0"
            @click="reportVisible = true"
          />
        </div>
      </div>

      <!-- Messages -->
      <div ref="messagesEl" class="messages-area">

        <div v-if="!chatStore.activeSessionId" class="empty-chat">
          <i class="pi pi-comments" />
          <h3>MCP Chat</h3>
          <p>Create a new session to start chatting with your MCP servers.</p>
          <Button label="New Session" icon="pi pi-plus" @click="createSession" />
        </div>

        <template v-else>
          <div v-if="chatStore.messages.length === 0" class="empty-chat">
            <i class="pi pi-comments" />
            <p>Start a conversation…</p>
          </div>

          <template v-for="msg in chatStore.messages" :key="msg.id">

            <!-- Tool call message -->
            <div v-if="isToolCall(msg)" class="tool-call-block">
              <div class="tool-call-header">
                <div class="avatar-circle tool-avatar-circle">🔧</div>
                <span class="tool-label">Calling tool:&nbsp;<strong>{{ msg.toolCallName }}</strong></span>
                <span v-if="msg.connectionName" class="conn-badge">🔌 {{ msg.connectionName }}</span>
                <span v-if="msg.modelName" class="model-badge tool-model-badge">{{ msg.modelName }}</span>
                <span class="tool-time">{{ formatTimestamp(msg.timestampUtc) }}</span>
                <button
                  v-if="msg.toolCallParameters"
                  class="params-toggle"
                  :title="expandedParams.has(msg.id) ? 'Hide parameters' : 'Show parameters'"
                  @click="toggleParams(msg.id)"
                >
                  <i :class="expandedParams.has(msg.id) ? 'pi pi-chevron-up' : 'pi pi-chevron-down'" />
                </button>
              </div>
              <template v-if="msg.toolCallParameters && expandedParams.has(msg.id)">
                <pre class="tool-params-pre">{{ getMaskedParams(msg.toolCallParameters, msg.id) }}</pre>
                <div v-if="hasSensitiveParams(msg.toolCallParameters)" class="sensitive-banner">
                  <span>🔒 Contains sensitive values</span>
                  <button class="reveal-btn" @click="toggleReveal(msg.id)">
                    {{ revealedParams.has(msg.id) ? '🙈 Hide values' : '👁 Reveal values' }}
                  </button>
                </div>
              </template>
            </div>

            <!-- User message -->
            <div v-else-if="msg.role.toLowerCase() === 'user'" class="message-row user">
              <div class="message-bubble user-bubble">
                <div class="message-header user-header">
                  <button class="copy-btn" @click="copyMessage(msg.content)" title="Copy message">
                    <i class="pi pi-copy" />
                  </button>
                  <span v-if="msg.modelName" class="model-badge user-model-badge">{{ msg.modelName }}</span>
                  <span class="msg-time user-time">{{ formatTimestamp(msg.timestampUtc) }}</span>
                  <span class="msg-role-name">{{ getName(msg.role) }}</span>
                  <div class="avatar-circle user-avatar-circle">👤</div>
                </div>
                <div class="message-content" v-html="escapeHtml(msg.content).replace(/\n/g, '<br />')" />
              </div>
            </div>

            <!-- Assistant message -->
            <div v-else-if="msg.role.toLowerCase() === 'assistant'" class="message-row assistant">
              <div class="message-bubble assistant-bubble">
                <div class="message-header assistant-header">
                  <div class="avatar-circle asst-avatar-circle">🤖</div>
                  <span class="msg-role-name">Assistant</span>
                  <span class="msg-time asst-time">{{ formatTimestamp(msg.timestampUtc) }}</span>
                  <span v-if="msg.thinkingMilliseconds != null" class="thinking-badge" title="Time to first token">
                    🤔 {{ formatThinking(msg.thinkingMilliseconds) }}
                  </span>
                  <span v-if="msg.tokenUsage" class="token-badge" title="Token usage">
                    💰 {{ msg.tokenUsage.inputTokens }}↑ {{ msg.tokenUsage.outputTokens }}↓ {{ msg.tokenUsage.totalTokens }}
                  </span>
                  <span v-if="msg.modelName" class="model-badge">{{ msg.modelName }}</span>
                  <button class="copy-btn asst-copy" @click="copyMessage(msg.content)" title="Copy message">
                    <i class="pi pi-copy" />
                  </button>
                </div>
                <div class="message-content" v-html="renderContent(msg.content, 'assistant')" />
              </div>
            </div>

          </template>

          <!-- Streaming placeholder -->
          <div v-if="chatStore.streaming" class="message-row assistant">
            <div class="message-bubble assistant-bubble streaming">
              <div class="message-header assistant-header">
                <div class="avatar-circle asst-avatar-circle">🤖</div>
                <span class="msg-role-name">Assistant</span>
                <span v-if="!chatStore.streamingContent" class="thinking-live">
                  🤔 {{ formatMs(chatStore.thinkingMs) }}
                </span>
              </div>
              <div v-if="!chatStore.streamingContent" class="thinking-indicator">
                <span>Thinking</span><span class="thinking-dots">…</span>
              </div>
              <div v-else class="message-content" v-html="renderContent(chatStore.streamingContent, 'assistant')" />
            </div>
          </div>
        </template>
      </div>

      <!-- Input area -->
      <div class="input-area">
        <div class="input-controls">
          <MultiSelect
            v-model="selectedConnectionNames"
            :options="connectedConnections"
            optionLabel="name"
            optionValue="name"
            placeholder="Connections…"
            class="conn-select"
            display="chip"
          />
          <Select
            v-model="selectedModelName"
            :options="llmModels"
            optionLabel="name"
            optionValue="name"
            placeholder="Model…"
            class="model-select"
          />
        </div>
        <div class="input-row">
          <Textarea
            v-model="messageText"
            placeholder="Type a message… (Ctrl+Enter to send)"
            rows="3"
            autoResize
            class="message-input"
            @keydown="onInputKeyDown"
          />
          <div class="input-btns">
            <Button
              v-if="chatStore.streaming"
              icon="pi pi-stop-circle"
              severity="danger"
              v-tooltip="'Cancel'"
              @click="chatStore.cancelStream()"
            />
            <Button
              v-else
              icon="pi pi-send"
              :disabled="!messageText.trim() || !chatStore.activeSessionId"
              @click="sendMessage"
              v-tooltip="'Send (Ctrl+Enter)'"
            />
          </div>
        </div>
      </div>
    </div>

    <!-- Rename dialog -->
    <Dialog v-model:visible="renameDialog" header="Rename Session" modal :style="{ width: '320px' }">
      <InputText v-model="renameValue" class="w-full" @keydown.enter="saveRename" />
      <template #footer>
        <Button label="Cancel" severity="secondary" text @click="renameDialog = false" />
        <Button label="Save" icon="pi pi-check" @click="saveRename" />
      </template>
    </Dialog>

    <!-- Report dialog -->
    <ChatReportDialog
      v-model:visible="reportVisible"
      :messages="chatStore.messages"
      :session-name="currentSession?.name ?? ''"
    />

    <!-- System Prompt dialog -->
    <Dialog
      v-model:visible="systemPromptVisible"
      :header="`System Prompt — ${selectedModelName ?? 'No model selected'}`"
      modal
      :style="{ width: '600px' }"
      :breakpoints="{ '768px': '95vw' }"
    >
      <p class="system-prompt-hint">
        This prompt is prepended to every conversation with <strong>{{ selectedModelName }}</strong> as a hidden system instruction. Changes are saved to the model and take effect on the next message.
      </p>
      <Textarea
        v-model="editSystemPrompt"
        rows="10"
        class="w-full system-prompt-textarea"
        autoResize
        placeholder="You are a helpful assistant…"
      />
      <template #footer>
        <Button label="Cancel" severity="secondary" outlined @click="systemPromptVisible = false" />
        <Button label="Save" icon="pi pi-check" :loading="savingSystemPrompt" @click="saveSystemPrompt" />
      </template>
    </Dialog>

  </div>
</template>

<style scoped>
.chat-view { display:flex; height:100%; background:var(--bg-base); overflow:hidden; }

/* Session sidebar */
.session-sidebar { width:220px; flex-shrink:0; display:flex; flex-direction:column; background:var(--bg-surface); border-right:1px solid var(--border); outline:none; }
.sidebar-header { display:flex; align-items:center; justify-content:space-between; padding:12px 14px; font-size:12px; font-weight:600; text-transform:uppercase; letter-spacing:.05em; color:var(--text-muted); border-bottom:1px solid var(--border); flex-shrink:0; }
.session-list { flex:1; overflow-y:auto; }
.empty-sessions { display:flex; align-items:center; justify-content:center; padding:24px 12px; color:var(--text-muted); font-size:12px; }
.session-item { display:flex; align-items:center; gap:8px; padding:10px 12px; cursor:pointer; border-left:2px solid transparent; transition:var(--transition-fast); }
.session-item:hover { background:var(--nav-item-hover); }
.session-item.active { background:var(--nav-item-active); border-left-color:var(--accent); }
.session-info { flex:1; min-width:0; }
.session-name { display:block; font-size:13px; color:var(--text-primary); white-space:nowrap; overflow:hidden; text-overflow:ellipsis; }
.session-meta { display:block; font-size:11px; color:var(--text-muted); }
.session-actions { display:flex; gap:2px; opacity:0; transition:var(--transition-fast); }
.session-item:hover .session-actions { opacity:1; }
.icon-btn { background:none; border:none; cursor:pointer; color:var(--text-muted); padding:3px 5px; border-radius:3px; font-size:11px; }
.icon-btn:hover { color:var(--text-primary); background:var(--bg-raised); }
.icon-btn.danger:hover { color:var(--danger); }

/* Main area */
.chat-main { flex:1; display:flex; flex-direction:column; overflow:hidden; min-width:0; }

/* Toolbar */
.chat-toolbar { display:flex; align-items:center; justify-content:space-between; padding:8px 16px; background:var(--bg-surface); border-bottom:1px solid var(--border); flex-shrink:0; gap:12px; }
.toolbar-left { flex:1; min-width:0; }
.session-title { font-size:13px; font-weight:600; color:var(--text-primary); white-space:nowrap; overflow:hidden; text-overflow:ellipsis; }
.toolbar-right { display:flex; align-items:center; gap:12px; flex-shrink:0; }
.md-toggle { display:flex; align-items:center; gap:6px; cursor:pointer; user-select:none; }
.md-toggle-label { font-size:12px; color:var(--text-muted); }

/* Messages area */
.messages-area { flex:1; overflow-y:auto; padding:16px 20px; display:flex; flex-direction:column; gap:10px; }
.empty-chat { display:flex; flex-direction:column; align-items:center; gap:12px; margin:auto; text-align:center; color:var(--text-muted); }
.empty-chat i { font-size:48px; }
.empty-chat h3 { margin:0; color:var(--text-primary); }
.empty-chat p { margin:0; font-size:14px; }

/* Tool call block */
.tool-call-block { background:rgba(251,191,36,.08); border:1px solid rgba(251,191,36,.25); border-left:3px solid #f59e0b; border-radius:6px; padding:8px 12px; font-size:12px; align-self:stretch; }
.tool-call-header { display:flex; align-items:center; gap:8px; flex-wrap:wrap; }
.tool-label { color:#f59e0b; flex:1; min-width:0; }
.tool-label strong { font-weight:700; }
.conn-badge { background:rgba(20,184,166,.15); color:#14b8a6; border:1px solid rgba(20,184,166,.4); padding:1px 8px; border-radius:10px; font-size:11px; white-space:nowrap; font-weight:500; }
.tool-model-badge { background:rgba(251,191,36,.12); color:#f59e0b; border-color:rgba(251,191,36,.35) !important; }
.tool-time { color:var(--text-muted); font-size:11px; margin-left:auto; white-space:nowrap; }
.params-toggle { background:rgba(56,189,248,.1); border:1px solid rgba(56,189,248,.3); color:var(--accent); cursor:pointer; border-radius:3px; padding:2px 5px; font-size:10px; flex-shrink:0; }
.params-toggle:hover { background:rgba(56,189,248,.2); }
.params-expand-all-btn { background:none; border:none; cursor:pointer; font-size:16px; padding:2px 4px; opacity:.7; transition:opacity .15s; }
.params-expand-all-btn:hover { opacity:1; }
.params-expand-all-btn:disabled { opacity:.25; cursor:default; }
.system-prompt-hint { font-size:13px; color:var(--text-muted); margin:0 0 .75rem; line-height:1.5; }
.system-prompt-textarea { font-family: monospace; font-size:13px; }
.tool-params-pre { margin:8px 0 4px; background:var(--code-bg); border:1px solid var(--border); border-radius:4px; padding:8px 10px; font-family:var(--font-family-mono); font-size:11px; color:var(--text-primary); overflow-x:auto; white-space:pre; max-height:240px; overflow-y:auto; }
.sensitive-banner { display:flex; align-items:center; gap:8px; padding:4px 8px; background:rgba(239,68,68,.07); border:1px solid rgba(239,68,68,.2); border-radius:4px; font-size:11px; color:rgba(239,68,68,.9); margin-top:2px; }
.sensitive-banner span { flex:1; }
.reveal-btn { background:rgba(239,68,68,.1); border:1px solid rgba(239,68,68,.3); color:rgba(239,68,68,.9); border-radius:3px; padding:2px 8px; cursor:pointer; font-size:11px; white-space:nowrap; }
.reveal-btn:hover { background:rgba(239,68,68,.2); }

/* Avatar circles */
.avatar-circle { width:30px; height:30px; border-radius:50%; display:flex; align-items:center; justify-content:center; font-size:15px; flex-shrink:0; }
.user-avatar-circle { background:rgba(255,255,255,.18); }
.asst-avatar-circle { background:var(--bg-raised); border:1px solid var(--border); }
.tool-avatar-circle { background:rgba(251,191,36,.18); border:1px solid rgba(251,191,36,.35); font-size:13px; width:26px; height:26px; }

/* Message rows */
.message-row { display:flex; gap:8px; align-items:flex-start; }
.message-row.user { justify-content:flex-end; }
.message-row.assistant { justify-content:flex-start; }
.message-bubble { max-width:76%; min-width:80px; border-radius:10px; padding:10px 14px; }
.user-bubble { background:var(--accent); color:#fff; border-radius:10px 10px 4px 10px; }
.assistant-bubble { background:var(--bg-surface); border:1px solid var(--border); border-radius:10px 10px 10px 4px; color:var(--text-primary); }
.assistant-bubble.streaming { border-color:var(--accent); animation:pulse-border 1.5s ease-in-out infinite; }
@keyframes pulse-border { 0%,100%{border-color:var(--accent)}50%{border-color:var(--accent-muted)} }

/* Message header */
.message-header { display:flex; align-items:center; gap:6px; margin-bottom:6px; flex-wrap:wrap; }
.user-header { justify-content:flex-end; }
.assistant-header { justify-content:flex-start; }
.msg-role-name { font-size:11px; font-weight:600; text-transform:uppercase; letter-spacing:.04em; }
.user-bubble .msg-role-name { color:rgba(255,255,255,.7); }
.assistant-bubble .msg-role-name { color:var(--accent); }
.user-time { color:rgba(255,255,255,.55); font-size:11px; }
.asst-time { color:var(--text-muted); font-size:11px; }
.thinking-badge { font-size:11px; color:gold; background:rgba(255,215,0,.1); padding:1px 6px; border-radius:3px; white-space:nowrap; }
.token-badge { font-size:11px; color:#a3e635; background:rgba(163,230,53,.1); border:1px solid rgba(163,230,53,.25); padding:1px 7px; border-radius:3px; font-family:var(--font-family-mono); white-space:nowrap; letter-spacing:-.01em; }
.model-badge { font-size:11px; background:var(--bg-raised); color:var(--text-muted); border:1px solid var(--border); padding:1px 6px; border-radius:3px; white-space:nowrap; }
.user-model-badge { background:rgba(255,255,255,.12); color:rgba(255,255,255,.65); border-color:rgba(255,255,255,.2); }
.thinking-live { font-size:11px; color:gold; margin-left:auto; }
.copy-btn { background:none; border:none; cursor:pointer; padding:2px 4px; border-radius:3px; font-size:11px; opacity:.45; transition:opacity .15s; color:rgba(255,255,255,.8); }
.asst-copy { color:var(--text-muted); margin-left:auto; }
.copy-btn:hover { opacity:1; }

/* Message content */
.message-content { font-size:14px; line-height:1.65; word-break:break-word; }

/* Thinking indicator */
.thinking-indicator { display:flex; align-items:center; gap:4px; color:var(--text-muted); font-size:13px; padding:4px 0; }
.thinking-dots { animation:blink 1s step-end infinite; }
@keyframes blink { 0%,100%{opacity:1}50%{opacity:0} }

/* Input area */
.input-area { flex-shrink:0; border-top:1px solid var(--border); background:var(--bg-surface); padding:12px 16px; display:flex; flex-direction:column; gap:8px; }
.input-controls { display:flex; gap:8px; }
.conn-select { flex:1; }
.model-select { width:200px; }
.input-row { display:flex; gap:8px; align-items:flex-end; }
.message-input { flex:1; resize:none; }
.input-btns { display:flex; flex-direction:column; gap:4px; }
.w-full { width:100%; }
</style>

<style>
/* Global: markdown rendering inside assistant message bubbles */
.message-content table { border-collapse:collapse; width:100%; margin:.6em 0; font-size:13px; }
.message-content th, .message-content td { border:1px solid var(--border); padding:5px 10px; text-align:left; }
.message-content th { background:var(--bg-raised); font-weight:600; }
.message-content pre { background:var(--code-bg); border:1px solid var(--border); border-radius:4px; padding:10px 14px; margin:6px 0; font-family:var(--font-family-mono); font-size:12px; overflow-x:auto; white-space:pre; }
.message-content code { background:var(--code-bg); font-family:var(--font-family-mono); font-size:12px; padding:1px 4px; border-radius:3px; }
.message-content pre code { padding:0; background:none; }
.message-content blockquote { border-left:3px solid var(--accent-muted); padding-left:12px; margin:.5em 0; color:var(--text-muted); }
.message-content h1,.message-content h2,.message-content h3,.message-content h4 { margin:.6em 0 .3em; font-weight:600; color:var(--text-primary); }
.message-content ul,.message-content ol { padding-left:1.4em; margin:.4em 0; }
.message-content li { margin:.2em 0; }
.message-content p { margin:.4em 0; }
.message-content a { color:var(--accent); text-decoration:underline; }
.message-content strong { font-weight:700; }
</style>
