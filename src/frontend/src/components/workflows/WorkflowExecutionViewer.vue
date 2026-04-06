<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import type { WorkflowExecution, WorkflowStepResult, StepExecutionStatus } from '@/api/types'
import JsonViewer from '@/components/common/JsonViewer.vue'
import Tag from 'primevue/tag'

const props = defineProps<{ execution: WorkflowExecution }>()

const activeStep = ref(0)

watch(() => props.execution, () => {
  if (props.execution?.stepResults?.length) {
    const sorted = [...props.execution.stepResults].sort((a, b) => a.stepNumber - b.stepNumber)
    activeStep.value = sorted[0].stepNumber
  }
}, { immediate: true })

const sortedSteps = computed(() =>
  [...(props.execution.stepResults ?? [])].sort((a, b) => a.stepNumber - b.stepNumber)
)

const activeStepResult = computed(() =>
  sortedSteps.value.find(s => s.stepNumber === activeStep.value)
)

const completedCount = computed(() =>
  sortedSteps.value.filter(s => s.status === 'Completed' || s.success === true).length
)

function stepSeverity(s: WorkflowStepResult) {
  const status = s.status ?? (s.success ? 'Completed' : 'Failed')
  return status === 'Completed' ? 'success'
       : status === 'Failed'    ? 'danger'
       : status === 'Running'   ? 'warn'
       : 'secondary'
}

function stepIcon(s: WorkflowStepResult) {
  const status = s.status ?? (s.success ? 'Completed' : 'Failed')
  return status === 'Completed' ? '✓'
       : status === 'Failed'    ? '✗'
       : status === 'Skipped'   ? '⊘'
       : status === 'Running'   ? '⏱'
       : '○'
}

function execSeverity(status: string) {
  return status === 'Completed' ? 'success'
       : status === 'Failed'    ? 'danger'
       : status === 'PartiallyCompleted' ? 'warn'
       : 'secondary'
}

function formatDuration(dur?: string) {
  if (!dur) return ''
  const m = dur.match(/(\d+):(\d+):(\d+)\.(\d+)/)
  if (!m) return dur
  const h = parseInt(m[1]), min = parseInt(m[2]), sec = parseInt(m[3])
  if (h > 0) return `${h}h ${min}m ${sec}s`
  if (min > 0) return `${min}m ${sec}s`
  const ms = Math.round(parseInt(m[4].substring(0, 3)))
  return sec > 0 ? `${sec}.${ms.toString().padStart(3,'0')}s` : `${ms}ms`
}

function parseJson(json?: string) {
  if (!json) return null
  try { return JSON.parse(json) } catch { return null }
}

function isMcpError(json?: string) {
  const parsed = parseJson(json)
  return parsed?.isError === true
}

async function copyToClipboard(text?: string) {
  if (!text) return
  try { await navigator.clipboard.writeText(text) } catch {}
}
</script>

<template>
  <div class="exec-viewer">
    <!-- Summary bar -->
    <div class="exec-summary">
      <div class="exec-summary-top">
        <span class="exec-name">{{ execution.workflowName }}</span>
        <Tag :value="execution.status" :severity="execSeverity(execution.status)" />
      </div>
      <div class="exec-summary-meta">
        <span class="meta-pill">🔌 {{ execution.connectionName }}</span>
        <span class="meta-item">🕐 {{ new Date(execution.startedUtc).toLocaleString() }}</span>
        <span class="meta-item">⏱ {{ formatDuration(execution.duration) }}</span>
        <span class="meta-item">📋 {{ completedCount }}/{{ sortedSteps.length }} steps</span>
      </div>
      <div v-if="execution.errorMessage" class="exec-error">
        <i class="pi pi-times-circle" /> {{ execution.errorMessage }}
      </div>
    </div>

    <!-- Step tabs -->
    <div v-if="sortedSteps.length" class="step-tabs">
      <div class="step-tab-list">
        <button
          v-for="s in sortedSteps" :key="s.stepNumber"
          class="step-tab-btn"
          :class="[`status-${(s.status ?? (s.success ? 'Completed' : 'Failed')).toLowerCase()}`, { active: activeStep === s.stepNumber }]"
          @click="activeStep = s.stepNumber"
        >
          <span class="step-tab-num">{{ s.stepNumber + 1 }}</span>
          <span class="step-tab-tool">{{ s.toolName }}</span>
          <span class="step-tab-icon">{{ stepIcon(s) }}</span>
        </button>
      </div>

      <div v-if="activeStepResult" class="step-content">
        <!-- Step meta row -->
        <div class="step-meta-row">
          <Tag :value="`${stepIcon(activeStepResult)} ${activeStepResult.status ?? (activeStepResult.success ? 'Completed' : 'Failed')}`"
               :severity="stepSeverity(activeStepResult)" />
          <span class="muted">{{ formatDuration(activeStepResult.duration) }}</span>
        </div>

        <!-- Error -->
        <div v-if="activeStepResult.errorMessage" class="step-error">
          <i class="pi pi-times-circle" /> {{ activeStepResult.errorMessage }}
        </div>

        <!-- Input Parameters -->
        <div v-if="activeStepResult.inputJson" class="step-section">
          <div class="step-section-header">
            <span class="step-section-title">↘ Input Parameters</span>
            <button class="copy-btn" title="Copy" @click="copyToClipboard(activeStepResult.inputJson)">📋</button>
          </div>
          <JsonViewer :data="parseJson(activeStepResult.inputJson)" title="" />
        </div>

        <!-- Output Result -->
        <div v-if="activeStepResult.outputJson != null" class="step-section">
          <div class="step-section-header">
            <span class="step-section-title">↗ Output Result</span>
            <button class="copy-btn" title="Copy" @click="copyToClipboard(activeStepResult.outputJson)">📋</button>
          </div>
          <div v-if="isMcpError(activeStepResult.outputJson)" class="mcp-error-banner">
            <i class="pi pi-exclamation-triangle" /> MCP tool returned an error
          </div>
          <JsonViewer :data="parseJson(activeStepResult.outputJson)" title="" />
        </div>

        <!-- Legacy result fallback (old history entries) -->
        <div v-else-if="activeStepResult.result !== undefined && activeStepResult.outputJson == null" class="step-section">
          <div class="step-section-header">
            <span class="step-section-title">↗ Output Result</span>
          </div>
          <JsonViewer :data="activeStepResult.result" title="" />
        </div>
      </div>
    </div>
    <div v-else class="no-steps">No step results recorded.</div>
  </div>
</template>

<style scoped>
.exec-viewer { display:flex; flex-direction:column; gap:12px; }
.exec-summary { background:var(--bg-raised); border:1px solid var(--border); border-radius:var(--border-radius-sm); padding:12px 14px; display:flex; flex-direction:column; gap:6px; }
.exec-summary-top { display:flex; align-items:center; gap:10px; }
.exec-name { font-size:14px; font-weight:600; color:var(--text-primary); flex:1; }
.exec-summary-meta { display:flex; flex-wrap:wrap; gap:10px; font-size:12px; color:var(--text-secondary); }
.meta-pill { font-family:var(--font-family-mono); background:var(--bg-surface); border:1px solid var(--border); border-radius:999px; padding:1px 8px; }
.exec-error { background:rgba(239,68,68,.1); border:1px solid var(--danger); border-radius:var(--border-radius-sm); padding:6px 10px; color:var(--danger); font-size:12px; display:flex; gap:6px; align-items:center; }
.step-tabs { display:flex; flex-direction:column; gap:0; border:1px solid var(--border); border-radius:var(--border-radius-sm); overflow:hidden; }
.step-tab-list { display:flex; flex-wrap:wrap; border-bottom:1px solid var(--border); background:var(--bg-surface); }
.step-tab-btn { display:flex; align-items:center; gap:6px; padding:8px 14px; border:none; border-right:1px solid var(--border); background:transparent; color:var(--text-secondary); cursor:pointer; font-size:12px; transition:var(--transition-fast); border-bottom:2px solid transparent; }
.step-tab-btn:hover { background:var(--nav-item-hover); color:var(--text-primary); }
.step-tab-btn.active { background:var(--bg-raised); color:var(--text-primary); font-weight:600; border-bottom-color:var(--accent); }
.step-tab-btn.status-completed.active { border-bottom-color:var(--success); }
.step-tab-btn.status-failed.active { border-bottom-color:var(--danger); }
.step-tab-btn.status-failed .step-tab-icon { color:var(--danger); }
.step-tab-btn.status-completed .step-tab-icon { color:var(--success); }
.step-tab-btn.status-running .step-tab-icon { color:var(--warning); }
.step-tab-num { width:20px; height:20px; border-radius:50%; background:var(--accent-muted); color:var(--accent); font-size:10px; font-weight:700; display:flex; align-items:center; justify-content:center; flex-shrink:0; }
.step-tab-tool { font-family:var(--font-family-mono); font-size:12px; max-width:160px; overflow:hidden; text-overflow:ellipsis; white-space:nowrap; }
.step-tab-icon { font-size:13px; }
.step-content { padding:14px 16px; display:flex; flex-direction:column; gap:12px; }
.step-meta-row { display:flex; align-items:center; gap:10px; }
.step-error { background:rgba(239,68,68,.08); border:1px solid var(--danger); border-radius:var(--border-radius-sm); padding:8px 12px; color:var(--danger); font-size:12px; display:flex; gap:6px; align-items:center; }
.step-section { display:flex; flex-direction:column; gap:6px; }
.step-section-header { display:flex; align-items:center; justify-content:space-between; }
.step-section-title { font-size:11px; font-weight:600; text-transform:uppercase; letter-spacing:.05em; color:var(--text-muted); }
.copy-btn { background:none; border:none; cursor:pointer; font-size:14px; padding:2px; opacity:.6; transition:opacity .15s; }
.copy-btn:hover { opacity:1; }
.mcp-error-banner { background:rgba(245,158,11,.1); border:1px solid var(--warning,#f59e0b); border-radius:var(--border-radius-sm); padding:6px 10px; color:var(--warning,#f59e0b); font-size:12px; display:flex; gap:6px; align-items:center; }
.muted { color:var(--text-muted); font-size:12px; }
.no-steps { color:var(--text-muted); font-size:13px; padding:16px; text-align:center; }
</style>
