<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { marked } from 'marked'
import DOMPurify from 'dompurify'
import Dialog from 'primevue/dialog'
import Tabs from 'primevue/tabs'
import Tab from 'primevue/tab'
import TabList from 'primevue/tablist'
import TabPanels from 'primevue/tabpanels'
import TabPanel from 'primevue/tabpanel'
import Button from 'primevue/button'
import { useToast } from 'primevue/usetoast'
import type { ChatMessage } from '@/api/types'

marked.setOptions({ gfm: true, breaks: true })

const props = defineProps<{
  visible: boolean
  messages: ChatMessage[]
  sessionName: string
}>()
const emit = defineEmits<{ (e: 'update:visible', v: boolean): void }>()

const toast = useToast()
const activeTab = ref('raw')

function escapeMarkdown(text: string): string {
  return text
    .replace(/\\/g, '\\\\').replace(/`/g, '\\`').replace(/\*/g, '\\*')
    .replace(/_/g, '\\_').replace(/\[/g, '\\[').replace(/]/g, '\\]')
    .replace(/#/g, '\\#').replace(/\|/g, '\\|')
}

function formatMs(ms: number): string {
  const s = ms / 1000
  if (s < 60) return `${s.toFixed(1)}s`
  const m = Math.floor(s / 60); const sec = Math.floor(s % 60)
  return `${m}m ${sec}s`
}

function formatDuration(ms: number): string {
  const s = Math.floor(ms / 1000)
  if (s < 60) return `${s}s`
  const m = Math.floor(s / 60); const rem = s % 60
  if (m < 60) return `${m}m ${rem}s`
  const h = Math.floor(m / 60); const rm = m % 60
  return `${h}h ${rm}m`
}

const reportMarkdown = computed(() => {
  const msgs = props.messages
  if (!msgs.length) return ''

  const now = new Date()
  const sessionName = props.sessionName || 'Chat Session'

  const userMsgs = msgs.filter(m => m.role.toLowerCase() === 'user')
  const assistantMsgs = msgs.filter(m => m.role.toLowerCase() === 'assistant')
  const toolMsgs = msgs.filter(m => m.role.toLowerCase() === 'system' && m.toolCallName)

  const totalInput = assistantMsgs.reduce((s, m) => s + (m.tokenUsage?.inputTokens ?? 0), 0)
  const totalOutput = assistantMsgs.reduce((s, m) => s + (m.tokenUsage?.outputTokens ?? 0), 0)
  const totalTokens = assistantMsgs.reduce((s, m) => s + (m.tokenUsage?.totalTokens ?? 0), 0)

  const thinkingTimes = assistantMsgs
    .filter(m => m.thinkingMilliseconds != null)
    .map(m => m.thinkingMilliseconds!)
  const avgThinking = thinkingTimes.length
    ? thinkingTimes.reduce((a, b) => a + b, 0) / thinkingTimes.length
    : 0

  const firstTs = msgs[0] ? new Date(msgs[0].timestampUtc).getTime() : 0
  const lastTs = msgs[msgs.length - 1] ? new Date(msgs[msgs.length - 1].timestampUtc).getTime() : 0
  const durationMs = lastTs - firstTs

  const lines: string[] = []

  lines.push('# Chat Session Report')
  lines.push(`**${escapeMarkdown(sessionName)}** • ${now.toISOString().slice(0, 16).replace('T', ' ')}`)
  lines.push('')
  lines.push('---')
  lines.push('')
  lines.push('## 📋 Executive Summary')
  lines.push('')

  if (userMsgs.length > 0) {
    const preview = userMsgs[0].content
    const topic = preview.length > 100 ? preview.slice(0, 100) + '...' : preview
    lines.push(`**Topic:** ${escapeMarkdown(topic)}`)
  }

  if (toolMsgs.length > 0) {
    const uniqueTools = [...new Set(toolMsgs.map(m => m.toolCallName).filter(Boolean))]
    lines.push(`**Tools Used:** ${uniqueTools.map(t => `\`${t}\``).join(', ')}`)
  }

  lines.push('')
  lines.push('### Metrics')
  lines.push(`- **Messages:** ${msgs.length} total (${userMsgs.length} user, ${assistantMsgs.length} assistant, ${toolMsgs.length} tool calls)`)

  if (totalTokens > 0) {
    lines.push(`- **Tokens:** ${totalTokens.toLocaleString()} total (${totalInput.toLocaleString()} input, ${totalOutput.toLocaleString()} output)`)
  }
  if (avgThinking > 0) {
    lines.push(`- **Avg Response Time:** ${formatMs(avgThinking)}`)
  }
  if (durationMs > 0) {
    lines.push(`- **Duration:** ${formatDuration(durationMs)}`)
  }

  const models = [...new Set(assistantMsgs.map(m => m.modelName).filter(Boolean))]
  if (models.length > 0) {
    lines.push(`- **Model:** ${models.join(', ')}`)
  }

  lines.push('')
  lines.push('---')
  lines.push('')
  lines.push('## 💬 Full Transcript')
  lines.push('')

  for (const msg of msgs) {
    const ts = new Date(msg.timestampUtc).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
    const role = msg.role.toLowerCase()

    if (role === 'system' && msg.toolCallName) {
      lines.push(`#### [${ts}] 🔧 Tool: [\`${msg.toolCallName}\`]`)
      if (msg.connectionName) lines.push(`*Connection: ${escapeMarkdown(msg.connectionName)}*`)
      if (msg.toolCallParameters) {
        lines.push('')
        lines.push('<details>')
        lines.push('<summary>Parameters</summary>')
        lines.push('')
        lines.push('```json')
        try {
          lines.push(JSON.stringify(JSON.parse(msg.toolCallParameters), null, 2))
        } catch {
          lines.push(msg.toolCallParameters)
        }
        lines.push('```')
        lines.push('')
        lines.push('</details>')
      }
      lines.push('')
    } else if (role === 'user') {
      lines.push(`#### [${ts}] 👤 User`)
      lines.push(msg.content)
      lines.push('')
    } else if (role === 'assistant') {
      const meta: string[] = []
      if (msg.tokenUsage) meta.push(`${msg.tokenUsage.inputTokens} → ${msg.tokenUsage.outputTokens} tokens`)
      if (msg.thinkingMilliseconds != null) meta.push(formatMs(msg.thinkingMilliseconds))
      const modelSuffix = msg.modelName ? ` (${escapeMarkdown(msg.modelName)})` : ''
      lines.push(`#### [${ts}] 🤖 Assistant${modelSuffix}`)
      if (meta.length > 0) {
        lines.push(`> *${meta.join(' | ')}*`)
        lines.push('')
      }
      lines.push(msg.content)
      lines.push('')
    }
  }

  lines.push('---')
  lines.push('')
  lines.push(`*Report generated on ${now.toISOString().slice(0, 19).replace('T', ' ')}*`)

  return lines.join('\n')
})

const reportHtml = computed(() => {
  if (!reportMarkdown.value) return ''
  const raw = marked(reportMarkdown.value) as string
  return DOMPurify.sanitize(raw)
})

const fileName = computed(() => {
  const safe = (props.sessionName || 'chat')
    .toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '') || 'chat-report'
  const ts = new Date().toISOString().slice(0, 16).replace(/[-T:]/g, '').slice(0, 12)
  return `${safe}-${ts}.md`
})

async function copyReport() {
  try {
    await navigator.clipboard.writeText(reportMarkdown.value)
    toast.add({ severity: 'success', summary: 'Copied!', detail: 'Report copied to clipboard', life: 2000 })
  } catch {
    toast.add({ severity: 'error', summary: 'Copy failed', life: 3000 })
  }
}

function downloadReport() {
  const blob = new Blob([reportMarkdown.value], { type: 'text/markdown' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url; a.download = fileName.value; a.click()
  URL.revokeObjectURL(url)
}

// Reset to raw tab when opened
watch(() => props.visible, v => { if (v) activeTab.value = 'raw' })
</script>

<template>
  <Dialog
    :visible="visible"
    @update:visible="emit('update:visible', $event)"
    header="Chat Session Report"
    modal
    :style="{ width: '860px', maxWidth: '95vw' }"
    :pt="{ content: { style: 'padding: 0; overflow: hidden;' } }"
  >
    <template #header>
      <div class="report-dialog-header">
        <span><i class="pi pi-file-edit" style="margin-right:6px" />Chat Session Report</span>
        <div class="report-actions">
          <Button icon="pi pi-copy" label="Copy" size="small" text @click="copyReport" />
          <Button icon="pi pi-download" label="Download" size="small" text @click="downloadReport" />
        </div>
      </div>
    </template>

    <Tabs v-model:value="activeTab" class="report-tabs">
      <TabList>
        <Tab value="raw">📝 Raw Markdown</Tab>
        <Tab value="preview">👁 Preview</Tab>
      </TabList>
      <TabPanels>
        <TabPanel value="raw">
          <textarea class="report-raw" readonly :value="reportMarkdown" />
        </TabPanel>
        <TabPanel value="preview">
          <div class="report-preview markdown-body" v-html="reportHtml" />
        </TabPanel>
      </TabPanels>
    </Tabs>
  </Dialog>
</template>

<style scoped>
.report-dialog-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  width: 100%;
  font-weight: 600;
  font-size: 15px;
}
.report-actions {
  display: flex;
  gap: 4px;
  margin-right: 8px;
}
.report-tabs {
  height: 520px;
  display: flex;
  flex-direction: column;
}
.report-raw {
  width: 100%;
  height: 440px;
  resize: none;
  background: var(--code-bg, #0d1117);
  color: var(--text-primary, #e6edf3);
  font-family: var(--font-family-mono, monospace);
  font-size: 12px;
  border: none;
  padding: 12px 16px;
  outline: none;
  box-sizing: border-box;
}
.report-preview {
  height: 440px;
  overflow-y: auto;
  padding: 16px 20px;
  font-size: 13px;
  line-height: 1.6;
}
</style>

<style>
/* Markdown body styles in report preview */
.report-preview.markdown-body h1,
.report-preview.markdown-body h2,
.report-preview.markdown-body h3,
.report-preview.markdown-body h4 {
  margin-top: 1em;
  margin-bottom: 0.4em;
  color: var(--text-primary);
  font-weight: 600;
}
.report-preview.markdown-body h1 { font-size: 1.4em; border-bottom: 1px solid var(--border); padding-bottom: 6px; }
.report-preview.markdown-body h2 { font-size: 1.2em; }
.report-preview.markdown-body h3 { font-size: 1.05em; }
.report-preview.markdown-body h4 { font-size: 0.95em; color: var(--text-muted); }
.report-preview.markdown-body p { margin: 0.5em 0; color: var(--text-primary); }
.report-preview.markdown-body code { background: var(--code-bg); font-family: var(--font-family-mono); font-size: 11px; padding: 1px 4px; border-radius: 3px; }
.report-preview.markdown-body pre { background: var(--code-bg); border: 1px solid var(--border); border-radius: var(--border-radius-sm); padding: 10px 14px; overflow-x: auto; }
.report-preview.markdown-body pre code { padding: 0; background: none; }
.report-preview.markdown-body blockquote { border-left: 3px solid var(--accent-muted); padding-left: 12px; color: var(--text-muted); margin: 0.5em 0; }
.report-preview.markdown-body table { border-collapse: collapse; width: 100%; margin: 0.8em 0; }
.report-preview.markdown-body th, .report-preview.markdown-body td { border: 1px solid var(--border); padding: 6px 10px; font-size: 12px; }
.report-preview.markdown-body th { background: var(--bg-raised); font-weight: 600; }
.report-preview.markdown-body ul, .report-preview.markdown-body ol { padding-left: 1.4em; margin: 0.4em 0; }
.report-preview.markdown-body li { margin: 0.2em 0; }
.report-preview.markdown-body hr { border: none; border-top: 1px solid var(--border); margin: 1em 0; }
.report-preview.markdown-body strong { color: var(--text-primary); font-weight: 700; }
.report-preview.markdown-body details { margin: 0.4em 0; }
.report-preview.markdown-body summary { cursor: pointer; color: var(--accent); }
</style>
