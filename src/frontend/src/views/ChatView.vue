<script setup lang="ts">
import { ref, computed, watch, onMounted, onUnmounted, nextTick } from 'vue'
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
import SlashCommandMenu from '@/components/chat/SlashCommandMenu.vue'
import type { SlashCommand } from '@/components/chat/SlashCommandMenu.vue'
import { useChatStore } from '@/stores/chat'
import { useConnectionsStore } from '@/stores/connections'
import { llmModelsApi } from '@/api/llmModels'
import { connectionsApi } from '@/api/connections'
import { apiClient } from '@/api/client'
import { preferencesApi } from '@/api/preferences'
import type { LlmModelDefinition, ActivePrompt, PromptArgument, SensitiveFieldConfiguration } from '@/api/types'

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

// ── Slash command state ─────────────────────────────────────────────
const slashMenuRef = ref<InstanceType<typeof SlashCommandMenu>>()
const slashVisible = ref(false)
const slashQuery = ref('')

// Prompt picker state
const promptPickerVisible = ref(false)
const promptPickerSearch = ref('')
const promptPickerLoading = ref(false)
const promptsByConnection = ref<Array<{ connectionName: string; prompts: ActivePrompt[] }>>([])
const selectedPromptConnection = ref('')
const selectedPromptName = ref('')
const promptParams = ref<PromptArgument[]>([])
const promptParamValues = ref<Record<string, string>>({})
const promptPickerError = ref('')
const promptPickerConfirming = ref(false)

// Stats overlay state
const statsVisible = ref(false)

const sessionTokenStats = computed(() => {
  const msgs = chatStore.messages.filter(m => m.tokenUsage)
  const input = msgs.reduce((s, m) => s + (m.tokenUsage?.inputTokens ?? 0), 0)
  const output = msgs.reduce((s, m) => s + (m.tokenUsage?.outputTokens ?? 0), 0)
  return { input, output, total: input + output, turns: msgs.length }
})

const sessionToolCalls = computed(() => {
  const calls = chatStore.messages.filter(m => m.role === 'system' && m.toolCallName)
  // Aggregate by tool name + connection
  const map = new Map<string, { toolName: string; connectionName?: string; count: number }>()
  for (const m of calls) {
    const key = `${m.connectionName ?? ''}::${m.toolCallName ?? ''}`
    if (map.has(key)) {
      map.get(key)!.count++
    } else {
      map.set(key, { toolName: m.toolCallName!, connectionName: m.connectionName, count: 1 })
    }
  }
  return [...map.values()].sort((a, b) => b.count - a.count)
})

// Model picker state (for /model)
const modelPickerVisible = ref(false)

const SLASH_COMMANDS: SlashCommand[] = [
  {
    id: 'prompt', name: '/prompt', label: 'Prompt Picker',
    description: 'Browse & inject an MCP prompt template from connected servers',
    icon: '📋', group: 'MCP Prompts', groupIcon: '🔌',
  },
  {
    id: 'stats', name: '/stats', label: 'Token Stats',
    description: 'Show token usage: input / output / total for this session',
    icon: '📈', group: 'Chat Controls', groupIcon: '📊',
  },
  {
    id: 'report', name: '/report', label: 'Export Report',
    description: 'Copy entire conversation as formatted Markdown to clipboard',
    icon: '📄', group: 'Chat Controls', groupIcon: '📊',
  },
  {
    id: 'system', name: '/system', label: 'System Prompt',
    description: 'Open the system prompt editor for the active model',
    icon: '⚙️', group: 'Chat Controls', groupIcon: '📊',
  },
  {
    id: 'model', name: '/model', label: 'Switch Model',
    description: 'Quick-switch the active AI model',
    icon: '🤖', group: 'Chat Controls', groupIcon: '📊',
  },
  {
    id: 'clear', name: '/clear', label: 'Clear Chat',
    description: 'Clear all messages in the current session',
    icon: '🗑️', group: 'Chat Controls', groupIcon: '📊',
    isNew: true,
  },
]

const slashCommands = computed<SlashCommand[]>(() => {
  const cmds: SlashCommand[] = [...SLASH_COMMANDS]
  // Inject last 3 user messages as /recent entries
  const userMsgs = [...chatStore.messages]
    .filter(m => m.role === 'user')
    .reverse()
    .slice(0, 3)
  userMsgs.forEach((m, i) => {
    const preview = m.content.length > 55 ? m.content.slice(0, 55) + '…' : m.content
    cmds.unshift({
      id: `recent${i + 1}`,
      name: `/recent${i + 1}`,
      label: `Recent ${i + 1}`,
      description: preview,
      icon: ['1️⃣', '2️⃣', '3️⃣'][i],
      group: 'Recent Messages',
      groupIcon: '🕐',
    })
  })
  return cmds
})

watch(messageText, (val) => {
  if (val.startsWith('/')) {
    slashQuery.value = val.slice(1)
    slashVisible.value = true
  } else {
    slashVisible.value = false
    slashQuery.value = ''
  }
})

async function executeSlashCommand(cmd: SlashCommand) {
  slashVisible.value = false
  messageText.value = ''
  if (cmd.id === 'prompt') {
    await openPromptPicker()
  } else if (cmd.id.startsWith('recent')) {
    const idx = parseInt(cmd.id.replace('recent', '')) - 1
    const userMsgs = [...chatStore.messages].filter(m => m.role === 'user').reverse()
    if (userMsgs[idx]) messageText.value = userMsgs[idx].content
  } else if (cmd.id === 'stats') {
    statsVisible.value = true
  } else if (cmd.id === 'report') {
    reportVisible.value = true
  } else if (cmd.id === 'system') {
    openSystemPrompt()
  } else if (cmd.id === 'model') {
    modelPickerVisible.value = true
  } else if (cmd.id === 'clear') {
    confirmClearChat()
  }
}

function confirmClearChat() {
  confirm.require({
    message: 'Clear all messages in this session?',
    header: 'Clear Chat',
    icon: 'pi pi-exclamation-triangle',
    rejectProps: { label: 'Cancel', severity: 'secondary', outlined: true },
    acceptProps: { label: 'Clear', severity: 'danger' },
    accept: () => chatStore.clearMessages?.(),
  })
}

// ── Prompt Picker ───────────────────────────────────────────────────
async function openPromptPicker() {
  promptPickerVisible.value = true
  promptPickerSearch.value = ''
  promptPickerError.value = ''
  selectedPromptConnection.value = ''
  selectedPromptName.value = ''
  promptParams.value = []
  promptParamValues.value = {}
  promptPickerLoading.value = true
  try {
    const connected = connStore.activeConnections.filter(c => c.isConnected)
    const results = await Promise.allSettled(
      connected.map(async (c: { name: string }) => ({
        connectionName: c.name,
        prompts: await connectionsApi.getPrompts(c.name),
      }))
    )
    promptsByConnection.value = results
      .filter((r): r is PromiseFulfilledResult<{ connectionName: string; prompts: ActivePrompt[] }> => r.status === 'fulfilled')
      .map((r: PromiseFulfilledResult<{ connectionName: string; prompts: ActivePrompt[] }>) => r.value)
      .filter((r: { connectionName: string; prompts: ActivePrompt[] }) => r.prompts.length > 0)
  } catch (e: any) {
    promptPickerError.value = e.message
  } finally {
    promptPickerLoading.value = false
  }
}

const filteredPromptGroups = computed(() => {
  const q = promptPickerSearch.value.toLowerCase()
  return promptsByConnection.value.map(g => ({
    ...g,
    prompts: g.prompts.filter(p =>
      !q || p.name.toLowerCase().includes(q) || (p.description ?? '').toLowerCase().includes(q)
    ),
  })).filter(g => g.prompts.length > 0)
})

function selectPrompt(connectionName: string, promptName: string) {
  selectedPromptConnection.value = connectionName
  selectedPromptName.value = promptName
  promptPickerError.value = ''
  const group = promptsByConnection.value.find(g => g.connectionName === connectionName)
  const prompt = group?.prompts.find(p => p.name === promptName)
  promptParams.value = prompt?.arguments ?? []
  promptParamValues.value = {}
}

const canConfirmPrompt = computed(() =>
  !!selectedPromptConnection.value && !!selectedPromptName.value &&
  promptParams.value.filter(p => p.required).every(p => !!promptParamValues.value[p.name])
)

async function confirmPrompt() {
  if (!canConfirmPrompt.value) return
  promptPickerConfirming.value = true
  promptPickerError.value = ''
  try {
    const args: Record<string, string> = {}
    for (const p of promptParams.value) {
      if (promptParamValues.value[p.name]) args[p.name] = promptParamValues.value[p.name]
    }
    const result = await connectionsApi.executePrompt(
      selectedPromptConnection.value, selectedPromptName.value, args
    )
    promptPickerVisible.value = false

    // Ensure session exists
    if (!chatStore.activeSessionId) await createSession()

    // Send to AI — pass prompt metadata so user message is persisted with promptName/params
    await chatStore.sendMessage(
      result.result,
      selectedModelName.value ?? undefined,
      selectedConnectionNames.value,
      selectedPromptName.value,
      Object.keys(args).length ? args : undefined,
    )
    await nextTick()
    scrollToBottom()
  } catch (e: any) {
    promptPickerError.value = `Failed to load prompt: ${e.message}`
  } finally {
    promptPickerConfirming.value = false
  }
}

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

// ── Sensitive data masking ─────────────────────────────────────────────────
// Loaded from Data Guard page; merged with built-in patterns at runtime
const sensitiveConfig = ref<SensitiveFieldConfiguration>({
  additionalSensitiveFields: [],
  allowedFields: [],
  useAiDetection: false,
  aiStrictness: 'Balanced',
  showDetectionDebug: false,
})

// Built-in key name pattern (JSON tool params)
const BUILTIN_KEY_TERMS = 'key|password|token|secret|auth|credential|api[_\\-]?key|access[_\\-]?token|client[_\\-]?secret|private[_\\-]?key|bearer|passphrase|pass|pwd|apikey|clientsecret|accesstoken'

function buildKeyPattern(): RegExp {
  const extra = sensitiveConfig.value.additionalSensitiveFields
    .map(f => f.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'))
    .join('|')
  const src = extra ? `^(${BUILTIN_KEY_TERMS}|${extra})$` : `^(${BUILTIN_KEY_TERMS})$`
  return new RegExp(src, 'i')
}

// Built-in free-text trigger words
const BUILTIN_TEXT_TERMS = 'password|secret|token|api[_\\-]?key|credential|auth(?:orization)?|bearer|passphrase|pwd|pass|private[_\\-]?key|access[_\\-]?token|client[_\\-]?secret'

function buildTextPattern(): RegExp {
  const extra = sensitiveConfig.value.additionalSensitiveFields
    .map(f => f.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'))
    .join('|')
  const src = extra
    ? `\\b(${BUILTIN_TEXT_TERMS}|${extra})\\b\\s*(?:of\\s+|is\\s+|[:=]\\s*)([^\\s,;]+)`
    : `\\b(${BUILTIN_TEXT_TERMS})\\b\\s*(?:of\\s+|is\\s+|[:=]\\s*)([^\\s,;]+)`
  return new RegExp(src, 'gi')
}

function isAllowed(key: string): boolean {
  return sensitiveConfig.value.allowedFields
    .some(f => f.toLowerCase() === key.toLowerCase())
}

function hasSensitiveContent(text: string | undefined): boolean {
  if (!text) return false
  const pattern = buildTextPattern()
  pattern.lastIndex = 0
  let match: RegExpExecArray | null
  while ((match = pattern.exec(text)) !== null) {
    if (!isAllowed(match[1])) return true
  }
  return false
}

function getMaskedContent(text: string, msgId: string): string {
  if (!text) return ''
  if (revealedParams.value.has(msgId)) return escapeHtml(text).replace(/\n/g, '<br />')
  const pattern = buildTextPattern()
  pattern.lastIndex = 0
  const masked = text.replace(pattern, (_, keyword, _value) => {
    if (isAllowed(keyword)) return _
    return `${keyword} <SMASK>`
  })
  return escapeHtml(masked)
    .replace(/&lt;SMASK&gt;/g, '<span class="sensitive-mask" title="Sensitive value hidden">████████</span>')
    .replace(/\n/g, '<br />')
}

function hasSensitiveParams(params: string | undefined): boolean {
  if (!params) return false
  try {
    const obj = JSON.parse(params)
    if (typeof obj !== 'object' || obj === null) return false
    const pattern = buildKeyPattern()
    return Object.keys(obj).some(k => !isAllowed(k) && pattern.test(k))
  } catch { return false }
}

function getMaskedParams(params: string, msgId: string): string {
  if (!params) return ''
  try {
    const obj = JSON.parse(params)
    const revealed = revealedParams.value.has(msgId)
    if (revealed || typeof obj !== 'object' || obj === null) return JSON.stringify(obj, null, 2)
    const pattern = buildKeyPattern()
    const masked = Object.fromEntries(
      Object.entries(obj).map(([k, v]) => [
        k,
        !isAllowed(k) && pattern.test(k) && typeof v === 'string' ? '████████' : v
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

function isPromptInvocation(msg: { role: string; promptName?: string | null }) {
  return msg.role.toLowerCase() === 'user' && !!msg.promptName
}

// Parse the JSON-string prompt params into a key/value object for rendering
function parsePromptParams(params: string | null | undefined): Record<string, string> {
  if (!params) return {}
  try { return JSON.parse(params) ?? {} } catch { return {} }
}

// Track which prompt invocation blocks have their content expanded
const expandedPromptContent = ref<Set<string>>(new Set())
function togglePromptContent(msgId: string) {
  const s = new Set(expandedPromptContent.value)
  if (s.has(msgId)) s.delete(msgId); else s.add(msgId)
  expandedPromptContent.value = s
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
  if (slashVisible.value) {
    if (e.key === 'ArrowDown') { e.preventDefault(); slashMenuRef.value?.navigate(1) }
    else if (e.key === 'ArrowUp') { e.preventDefault(); slashMenuRef.value?.navigate(-1) }
    else if (e.key === 'Enter' || e.key === 'Tab') { e.preventDefault(); slashMenuRef.value?.confirm() }
    else if (e.key === 'Escape') { e.preventDefault(); slashVisible.value = false; messageText.value = '' }
    return
  }
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
  try {
    sensitiveConfig.value = await preferencesApi.getSensitiveFields()
  } catch { /* ignore — fall back to built-in patterns */ }
  window.addEventListener('slash-command', onExternalSlashCommand)
})

onUnmounted(() => {
  window.removeEventListener('slash-command', onExternalSlashCommand)
})

function onExternalSlashCommand(e: Event) {
  const cmd = (e as CustomEvent<string>).detail
  messageText.value = cmd
  // Slash watcher fires synchronously and sets slashVisible/slashQuery
}

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

            <!-- Prompt invocation block (Design A) -->
            <div v-if="isPromptInvocation(msg)" class="prompt-block">
              <div class="prompt-block-header">
                <div class="prompt-avatar">📋</div>
                <span class="prompt-label">Prompt:&nbsp;<strong>{{ msg.promptName }}</strong></span>
                <span v-if="msg.connectionName" class="prompt-conn-badge">🔌 {{ msg.connectionName }}</span>
                <span v-if="msg.modelName" class="model-badge prompt-model-badge">{{ msg.modelName }}</span>
                <span class="prompt-time">{{ formatTimestamp(msg.timestampUtc) }}</span>
                <button
                  class="params-toggle prompt-content-toggle"
                  :title="expandedPromptContent.has(msg.id) ? 'Hide prompt content' : 'Show prompt content'"
                  @click="togglePromptContent(msg.id)"
                >
                  <i :class="expandedPromptContent.has(msg.id) ? 'pi pi-chevron-up' : 'pi pi-chevron-down'" />
                </button>
              </div>
              <div class="prompt-block-body">
                <template v-if="Object.keys(parsePromptParams(msg.promptInvocationParams)).length">
                  <div v-for="(val, key) in parsePromptParams(msg.promptInvocationParams)" :key="key" class="prompt-param">
                    <span class="prompt-param-key">{{ key }}</span>
                    <span class="prompt-param-val">{{ val }}</span>
                  </div>
                </template>
                <span v-else class="prompt-no-params-hint">no parameters</span>
                <span class="prompt-sent-hint">↳ sent to AI</span>
              </div>
              <div v-if="expandedPromptContent.has(msg.id)" class="prompt-content-expanded">
                <pre class="prompt-content-pre">{{ msg.content }}</pre>
              </div>
            </div>

            <!-- Tool call message -->
            <div v-else-if="isToolCall(msg)" class="tool-call-block">
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
                <div class="message-content" v-html="getMaskedContent(msg.content, msg.id)" />
                <div v-if="hasSensitiveContent(msg.content)" class="sensitive-banner user-sensitive-banner">
                  <span>🔒 Contains sensitive values</span>
                  <button class="reveal-btn" @click="toggleReveal(msg.id)">
                    {{ revealedParams.has(msg.id) ? '🙈 Hide values' : '👁 Reveal values' }}
                  </button>
                </div>
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
        <div class="input-row-wrap">
          <SlashCommandMenu
            ref="slashMenuRef"
            :visible="slashVisible"
            :query="slashQuery"
            :commands="slashCommands"
            @select="executeSlashCommand"
            @dismiss="slashVisible = false; messageText = ''"
          />
          <div class="input-row">
            <Textarea
              v-model="messageText"
              placeholder="Type a message or / for commands… (Ctrl+Enter to send)"
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

    <!-- Token Stats Dialog -->
    <Dialog v-model:visible="statsVisible" header="Session Analytics" modal :style="{ width: '480px' }" :pt="{ header: { class: 'stats-dialog-header' } }">
      <!-- Hero banner -->
      <div class="stats-hero">
        <div class="stats-hero-glow" />
        <div class="stats-hero-total">
          <span class="stats-hero-number">{{ sessionTokenStats.total.toLocaleString() }}</span>
          <span class="stats-hero-label">total tokens</span>
        </div>
        <div class="stats-hero-bar">
          <div
            class="stats-bar-input"
            :style="{ width: sessionTokenStats.total ? (sessionTokenStats.input / sessionTokenStats.total * 100) + '%' : '50%' }"
            v-tooltip.top="'Input: ' + sessionTokenStats.input.toLocaleString()"
          />
          <div
            class="stats-bar-output"
            :style="{ width: sessionTokenStats.total ? (sessionTokenStats.output / sessionTokenStats.total * 100) + '%' : '50%' }"
            v-tooltip.top="'Output: ' + sessionTokenStats.output.toLocaleString()"
          />
        </div>
        <div class="stats-hero-legend">
          <span class="stats-legend-in">● Input</span>
          <span class="stats-legend-out">● Output</span>
        </div>
      </div>

      <!-- Metric cards -->
      <div class="stats-cards">
        <div class="stat-card">
          <div class="stat-icon">📥</div>
          <div class="stat-value">{{ sessionTokenStats.input.toLocaleString() }}</div>
          <div class="stat-label">Input tokens</div>
        </div>
        <div class="stat-card">
          <div class="stat-icon">📤</div>
          <div class="stat-value">{{ sessionTokenStats.output.toLocaleString() }}</div>
          <div class="stat-label">Output tokens</div>
        </div>
        <div class="stat-card">
          <div class="stat-icon">💬</div>
          <div class="stat-value">{{ sessionTokenStats.turns }}</div>
          <div class="stat-label">AI turns</div>
        </div>
        <div class="stat-card">
          <div class="stat-icon">🔧</div>
          <div class="stat-value">{{ sessionToolCalls.length }}</div>
          <div class="stat-label">Unique tools</div>
        </div>
      </div>

      <!-- Tool call breakdown -->
      <template v-if="sessionToolCalls.length">
        <div class="stats-section-title">🔧 Tools Called This Session</div>
        <div class="stats-tools-list">
          <div v-for="tc in sessionToolCalls" :key="tc.toolName + tc.connectionName" class="stats-tool-row">
            <div class="stats-tool-icon">⚡</div>
            <div class="stats-tool-info">
              <span class="stats-tool-name">{{ tc.toolName }}</span>
              <span v-if="tc.connectionName" class="stats-tool-conn">{{ tc.connectionName }}</span>
            </div>
            <div class="stats-tool-count">
              <span class="stats-tool-badge">×{{ tc.count }}</span>
            </div>
          </div>
        </div>
      </template>
      <div v-else class="stats-tools-empty">No tools called yet in this session</div>

      <template #footer>
        <Button label="Close" severity="secondary" @click="statsVisible = false" />
      </template>
    </Dialog>

    <!-- Model Picker Dialog -->
    <Dialog v-model:visible="modelPickerVisible" header="🤖 Switch Model" modal :style="{ width: '380px' }">
      <div class="model-picker-list">
        <div
          v-for="m in llmModels"
          :key="m.name"
          class="model-picker-item"
          :class="{ active: m.name === selectedModelName }"
          @click="selectedModelName = m.name; modelPickerVisible = false"
        >
          <span class="model-picker-name">{{ m.name }}</span>
          <span v-if="m.name === selectedModelName" class="model-active-badge">active</span>
        </div>
        <div v-if="!llmModels.length" class="model-picker-empty">No models configured</div>
      </div>
      <template #footer>
        <Button label="Cancel" severity="secondary" @click="modelPickerVisible = false" />
      </template>
    </Dialog>

    <!-- Prompt Picker Dialog -->
    <Dialog
      v-model:visible="promptPickerVisible"
      header="📋 Prompt Picker"
      modal
      :style="{ width: '640px', maxHeight: '80vh' }"
      :breakpoints="{ '768px': '95vw' }"
    >
      <div class="prompt-picker">
        <div v-if="promptPickerLoading" class="prompt-picker-loading">
          <i class="pi pi-spin pi-spinner" style="font-size:24px" />
          <span>Loading prompts…</span>
        </div>
        <template v-else>
          <div v-if="promptPickerError" class="prompt-picker-error">{{ promptPickerError }}</div>
          <InputText
            v-model="promptPickerSearch"
            placeholder="Search prompts…"
            class="w-full prompt-picker-search"
          />
          <div v-if="!filteredPromptGroups.length" class="prompt-picker-empty">
            <i class="pi pi-inbox" />
            <span>No prompts found. Make sure servers are connected and have prompts.</span>
          </div>
          <div v-else class="prompt-picker-cols">
            <!-- Left: prompt browser -->
            <div class="prompt-browser">
              <div v-for="group in filteredPromptGroups" :key="group.connectionName" class="prompt-group">
                <div class="prompt-group-header">🔌 {{ group.connectionName }}</div>
                <div
                  v-for="p in group.prompts"
                  :key="p.name"
                  class="prompt-item"
                  :class="{ active: selectedPromptConnection === group.connectionName && selectedPromptName === p.name }"
                  @click="selectPrompt(group.connectionName, p.name)"
                >
                  <div class="prompt-item-name">{{ p.name }}</div>
                  <div v-if="p.description" class="prompt-item-desc">{{ p.description }}</div>
                </div>
              </div>
            </div>

            <!-- Right: parameter form -->
            <div class="prompt-params-panel">
              <template v-if="selectedPromptName">
                <div class="params-title">Parameters for <strong>{{ selectedPromptName }}</strong></div>
                <div v-if="!promptParams.length" class="params-none">No parameters required</div>
                <div v-for="param in promptParams" :key="param.name" class="param-field">
                  <label class="param-label">
                    {{ param.name }}
                    <span v-if="param.required" class="param-required">*</span>
                  </label>
                  <div v-if="param.description" class="param-hint">{{ param.description }}</div>
                  <InputText
                    v-model="promptParamValues[param.name]"
                    :placeholder="param.required ? 'Required' : 'Optional'"
                    class="w-full"
                  />
                </div>
              </template>
              <div v-else class="params-empty-hint">← Select a prompt</div>
            </div>
          </div>
        </template>
      </div>
      <template #footer>
        <Button label="Cancel" severity="secondary" outlined @click="promptPickerVisible = false" />
        <Button
          label="Run in Chat"
          icon="pi pi-send"
          :disabled="!canConfirmPrompt"
          :loading="promptPickerConfirming"
          @click="confirmPrompt"
        />
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

/* Prompt invocation block (Design A — violet) */
.prompt-block {
  width: 100%;
  display: flex;
  flex-direction: column;
  border-radius: 8px;
  overflow: hidden;
  border: 1px solid rgba(139,92,246,.4);
  background: rgba(139,92,246,.05);
  font-size: 12px;
  transition: border-color .15s;
  flex-shrink: 0;
  box-sizing: border-box;
}
.prompt-block:hover { border-color: rgba(139,92,246,.6); }
.prompt-block-header {
  display: flex; align-items: center; gap: 8px;
  padding: 8px 12px;
  background: linear-gradient(90deg, rgba(139,92,246,.2), rgba(139,92,246,.08));
  border-bottom: 1px solid rgba(139,92,246,.25);
  flex-wrap: wrap;
  flex-shrink: 0;
  min-height: 38px;
}
.prompt-avatar {
  width: 24px; height: 24px; border-radius: 6px; flex-shrink: 0;
  background: rgba(139,92,246,.25); border: 1px solid rgba(139,92,246,.5);
  display: flex; align-items: center; justify-content: center; font-size: 12px;
}
.prompt-label { color: #a78bfa; font-weight: 600; flex: 1; min-width: 0; }
.prompt-label strong { font-weight: 700; }
.prompt-conn-badge {
  background: rgba(139,92,246,.14); color: #c4b5fd;
  border: 1px solid rgba(139,92,246,.4); padding: 1px 8px;
  border-radius: 10px; font-size: 11px; font-weight: 500; white-space: nowrap;
}
.prompt-model-badge {
  background: rgba(139,92,246,.12); color: #a78bfa;
  border: 1px solid rgba(139,92,246,.35) !important;
}
.prompt-time { font-size: 11px; color: var(--text-muted); white-space: nowrap; }
.prompt-block-body {
  padding: 8px 12px;
  background: rgba(139,92,246,.04);
  display: flex; align-items: center; gap: 10px; flex-wrap: wrap;
  flex-shrink: 0;
  min-height: 32px;
}
.prompt-param {
  display: flex; align-items: center; gap: 4px;
  background: rgba(139,92,246,.1); border: 1px solid rgba(139,92,246,.25);
  border-radius: 6px; padding: 3px 8px;
}
.prompt-param-key {
  font-size: 10px; text-transform: uppercase; letter-spacing: .05em;
  color: #c4b5fd; font-weight: 600;
}
.prompt-param-val {
  font-size: 12px; color: var(--text-primary);
  font-family: var(--font-family-mono);
  max-width: 180px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis;
}
.prompt-no-params { padding: 6px 12px; }
.prompt-no-params-hint {
  font-size: 11px; color: rgba(167,139,250,.45); font-style: italic;
}
.prompt-sent-hint {
  font-size: 11px; color: rgba(167,139,250,.6); margin-left: auto; white-space: nowrap;
}
.prompt-content-toggle {
  background: rgba(139,92,246,.12);
  border: 1px solid rgba(139,92,246,.3);
  color: #a78bfa;
}
.prompt-content-toggle:hover { background: rgba(139,92,246,.22); }
.prompt-content-expanded {
  border-top: 1px solid rgba(139,92,246,.2);
  background: rgba(139,92,246,.03);
  padding: 10px 12px;
}
.prompt-content-pre {
  margin: 0; white-space: pre-wrap; word-break: break-word;
  font-family: var(--font-family-mono); font-size: 11.5px;
  color: var(--text-secondary); line-height: 1.6;
}

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
.user-sensitive-banner { margin-top:6px; }
.sensitive-mask { background:rgba(239,68,68,.15); color:rgba(239,68,68,.9); border-radius:3px; padding:0 3px; font-family:var(--font-family-mono); letter-spacing:.05em; font-size:.95em; }
.reveal-btn { background:rgba(239,68,68,.1); border:1px solid rgba(239,68,68,.3); color:rgba(239,68,68,.9); border-radius:3px; padding:2px 8px; cursor:pointer; font-size:11px; white-space:nowrap; }
.reveal-btn:hover { background:rgba(239,68,68,.2); }

/* Overrides for sensitive elements inside the blue user bubble */
.user-bubble .sensitive-mask { background:rgba(0,0,0,0.3); color:#fff; border-radius:3px; padding:1px 5px; }
.user-bubble .sensitive-banner { background:rgba(0,0,0,0.2); border-color:rgba(255,255,255,0.3); color:rgba(255,255,255,0.9); }
.user-bubble .reveal-btn { background:rgba(0,0,0,0.15); border-color:rgba(255,255,255,0.35); color:rgba(255,255,255,0.9); }
.user-bubble .reveal-btn:hover { background:rgba(0,0,0,0.3); }

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
.input-row-wrap { position:relative; }
.input-row { display:flex; gap:8px; align-items:flex-end; }
.message-input { flex:1; resize:none; }
.input-btns { display:flex; flex-direction:column; gap:4px; }
.w-full { width:100%; }

/* Stats dialog — hero + cards */
.stats-hero { position:relative; background:linear-gradient(135deg, color-mix(in srgb, var(--accent) 18%, var(--bg-raised)), var(--bg-raised)); border:1px solid color-mix(in srgb, var(--accent) 30%, var(--border)); border-radius:12px; padding:20px; margin-bottom:16px; text-align:center; overflow:hidden; }
.stats-hero-glow { position:absolute; top:-40px; left:50%; transform:translateX(-50%); width:200px; height:200px; background:radial-gradient(circle, color-mix(in srgb, var(--accent) 25%, transparent), transparent 70%); pointer-events:none; }
.stats-hero-number { display:block; font-size:42px; font-weight:800; font-family:var(--font-family-mono); color:var(--text-primary); line-height:1.1; letter-spacing:-.02em; }
.stats-hero-label { display:block; font-size:12px; text-transform:uppercase; letter-spacing:.1em; color:var(--text-muted); margin-top:2px; }
.stats-hero-total { position:relative; }
.stats-hero-bar { display:flex; height:6px; border-radius:3px; overflow:hidden; background:var(--bg-overlay); margin:14px 0 6px; }
.stats-bar-input { background:var(--accent); transition:width .6s cubic-bezier(.4,0,.2,1); }
.stats-bar-output { background:color-mix(in srgb, var(--accent) 50%, #a3e635); transition:width .6s cubic-bezier(.4,0,.2,1); }
.stats-hero-legend { display:flex; justify-content:center; gap:16px; font-size:11px; }
.stats-legend-in { color:var(--accent); }
.stats-legend-out { color:color-mix(in srgb, var(--accent) 50%, #a3e635); }
.stats-cards { display:grid; grid-template-columns:repeat(4, 1fr); gap:10px; margin-bottom:16px; }
.stat-card { background:var(--bg-raised); border:1px solid var(--border); border-radius:10px; padding:12px 8px; text-align:center; transition:border-color .15s; }
.stat-card:hover { border-color:var(--accent); }
.stat-icon { font-size:18px; margin-bottom:4px; }
.stat-value { font-size:20px; font-weight:700; color:var(--text-primary); font-family:var(--font-family-mono); }
.stat-label { font-size:11px; color:var(--text-muted); margin-top:2px; white-space:nowrap; }
.stats-section-title { font-size:12px; font-weight:700; text-transform:uppercase; letter-spacing:.06em; color:var(--text-muted); margin-bottom:8px; }
.stats-tools-list { display:flex; flex-direction:column; gap:4px; max-height:200px; overflow-y:auto; }
.stats-tool-row { display:flex; align-items:center; gap:10px; padding:8px 10px; background:var(--bg-raised); border:1px solid var(--border); border-radius:7px; transition:border-color .15s; }
.stats-tool-row:hover { border-color:rgba(251,191,36,.4); }
.stats-tool-icon { font-size:14px; flex-shrink:0; }
.stats-tool-info { flex:1; min-width:0; }
.stats-tool-name { font-size:13px; font-weight:600; color:var(--text-primary); display:block; white-space:nowrap; overflow:hidden; text-overflow:ellipsis; }
.stats-tool-conn { font-size:11px; color:var(--text-muted); }
.stats-tool-count { flex-shrink:0; }
.stats-tool-badge { background:rgba(251,191,36,.15); color:#f59e0b; border:1px solid rgba(251,191,36,.35); padding:2px 8px; border-radius:10px; font-size:12px; font-weight:700; font-family:var(--font-family-mono); }
.stats-tools-empty { font-size:13px; color:var(--text-muted); text-align:center; padding:16px; background:var(--bg-raised); border-radius:8px; }

/* Model picker */
.model-picker-list { display:flex; flex-direction:column; gap:4px; max-height:320px; overflow-y:auto; }
.model-picker-item { display:flex; align-items:center; justify-content:space-between; padding:10px 14px; border-radius:6px; cursor:pointer; border:1px solid transparent; transition:var(--transition-fast); }
.model-picker-item:hover { background:var(--nav-item-hover); border-color:var(--border); }
.model-picker-item.active { background:color-mix(in srgb, var(--accent) 12%, transparent); border-color:var(--accent); }
.model-picker-name { font-size:14px; color:var(--text-primary); }
.model-active-badge { font-size:11px; background:var(--accent); color:#fff; padding:1px 7px; border-radius:8px; }
.model-picker-empty { text-align:center; padding:24px; color:var(--text-muted); }

/* Prompt picker dialog */
.prompt-picker { display:flex; flex-direction:column; gap:12px; }
.prompt-picker-loading { display:flex; flex-direction:column; align-items:center; gap:12px; padding:40px; color:var(--text-muted); }
.prompt-picker-error { color:var(--danger); font-size:13px; padding:8px 12px; background:rgba(239,68,68,.08); border:1px solid rgba(239,68,68,.25); border-radius:4px; }
.prompt-picker-search { font-size:13px; }
.prompt-picker-empty { display:flex; align-items:center; gap:8px; padding:24px; color:var(--text-muted); justify-content:center; }
.prompt-picker-cols { display:grid; grid-template-columns:1fr 1fr; gap:12px; max-height:360px; }
.prompt-browser { overflow-y:auto; display:flex; flex-direction:column; gap:6px; border:1px solid var(--border); border-radius:6px; padding:8px; }
.prompt-group-header { font-size:11px; font-weight:600; text-transform:uppercase; letter-spacing:.04em; color:var(--text-muted); padding:4px 6px 2px; }
.prompt-item { padding:7px 10px; border-radius:5px; cursor:pointer; border:1px solid transparent; transition:var(--transition-fast); }
.prompt-item:hover { background:var(--nav-item-hover); border-color:var(--border); }
.prompt-item.active { background:color-mix(in srgb, var(--accent) 12%, transparent); border-color:var(--accent); }
.prompt-item-name { font-size:13px; font-weight:500; color:var(--text-primary); }
.prompt-item-desc { font-size:11px; color:var(--text-muted); margin-top:2px; white-space:nowrap; overflow:hidden; text-overflow:ellipsis; }
.prompt-params-panel { overflow-y:auto; display:flex; flex-direction:column; gap:10px; border:1px solid var(--border); border-radius:6px; padding:12px; }
.params-title { font-size:12px; font-weight:600; color:var(--text-muted); }
.params-none { font-size:13px; color:var(--text-muted); text-align:center; padding:12px; }
.params-empty-hint { font-size:13px; color:var(--text-muted); text-align:center; padding:24px; }
.param-field { display:flex; flex-direction:column; gap:4px; }
.param-label { font-size:12px; font-weight:600; color:var(--text-primary); }
.param-required { color:var(--danger); margin-left:2px; }
.param-hint { font-size:11px; color:var(--text-muted); }
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
