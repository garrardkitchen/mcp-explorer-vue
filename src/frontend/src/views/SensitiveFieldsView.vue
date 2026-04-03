<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useToast } from 'primevue/usetoast'
import Button from 'primevue/button'
import InputText from 'primevue/inputtext'
import Select from 'primevue/select'
import ToggleSwitch from 'primevue/toggleswitch'
import Tag from 'primevue/tag'
import Skeleton from 'primevue/skeleton'
import { preferencesApi } from '@/api/preferences'
import type { SensitiveFieldConfiguration, AiDetectionStrictness } from '@/api/types'

const toast = useToast()

const loading = ref(false)
const saving = ref(false)
const config = ref<SensitiveFieldConfiguration>({
  additionalSensitiveFields: [],
  allowedFields: [],
  useAiDetection: false,
  aiStrictness: 'Balanced',
  showDetectionDebug: false,
})

const newPattern = ref('')
const newAllowedField = ref('')

const strictnessOptions: { label: string; value: AiDetectionStrictness }[] = [
  { label: 'Conservative', value: 'Conservative' },
  { label: 'Balanced', value: 'Balanced' },
  { label: 'Aggressive', value: 'Aggressive' },
]

async function load() {
  loading.value = true
  try {
    config.value = await preferencesApi.getSensitiveFields()
  } catch (e: any) {
    toast.add({ severity: 'error', summary: 'Failed to load config', detail: e.message, life: 5000 })
  } finally { loading.value = false }
}

async function save() {
  saving.value = true
  try {
    await preferencesApi.updateSensitiveFields(config.value)
    toast.add({ severity: 'success', summary: 'Saved', detail: 'Sensitive field configuration updated', life: 3000 })
  } catch (e: any) {
    toast.add({ severity: 'error', summary: 'Save failed', detail: e.message, life: 5000 })
  } finally { saving.value = false }
}

function addPattern() {
  const p = newPattern.value.trim()
  if (!p) return
  if (!config.value.additionalSensitiveFields.includes(p)) {
    config.value.additionalSensitiveFields.push(p)
  }
  newPattern.value = ''
}

function removePattern(idx: number) {
  config.value.additionalSensitiveFields.splice(idx, 1)
}

function addAllowedField() {
  const f = newAllowedField.value.trim()
  if (!f) return
  if (!config.value.allowedFields.includes(f)) {
    config.value.allowedFields.push(f)
  }
  newAllowedField.value = ''
}

function removeAllowedField(idx: number) {
  config.value.allowedFields.splice(idx, 1)
}

onMounted(load)
</script>

<template>
  <div class="sf-view">
    <div class="toolbar">
      <div>
        <h2 class="page-title">Sensitive Field Detection</h2>
        <p class="page-subtitle">Configure how sensitive data is detected and protected in MCP communications.</p>
      </div>
      <Button label="Save Configuration" icon="pi pi-save" :loading="saving" @click="save" />
    </div>

    <div v-if="loading" class="p-6"><Skeleton v-for="i in 5" :key="i" height="48px" class="mb-3" /></div>

    <div v-else class="content">
      <!-- Detection Toggles -->
      <div class="config-section">
        <h3 class="section-title"><i class="pi pi-shield" /> Detection Modes</h3>
        <div class="toggle-grid">
          <div class="toggle-row">
            <div class="toggle-info">
              <span class="toggle-label">AI-Powered Detection</span>
              <span class="toggle-desc">Uses an LLM to detect sensitive data in context. Adds latency.</span>
            </div>
            <ToggleSwitch v-model="config.useAiDetection" />
          </div>
          <div class="toggle-row">
            <div class="toggle-info">
              <span class="toggle-label">Debug Logging</span>
              <span class="toggle-desc">Log detection decisions to the browser console.</span>
            </div>
            <ToggleSwitch v-model="config.showDetectionDebug" />
          </div>
        </div>
      </div>

      <!-- AI Strictness -->
      <div class="config-section" v-if="config.useAiDetection">
        <h3 class="section-title"><i class="pi pi-sliders-h" /> AI Detection Strictness</h3>
        <div class="strictness-selector">
          <div
            v-for="opt in strictnessOptions"
            :key="opt.value"
            class="strictness-card"
            :class="{ active: config.aiStrictness === opt.value }"
            @click="config.aiStrictness = opt.value"
          >
            <span class="strictness-label">{{ opt.label }}</span>
            <span class="strictness-desc">
              <template v-if="opt.value === 'Conservative'">Only flag obvious secrets (API keys, passwords with labels)</template>
              <template v-else-if="opt.value === 'Balanced'">Standard detection — good balance of precision and recall</template>
              <template v-else>Maximum sensitivity — may have false positives</template>
            </span>
          </div>
        </div>
      </div>

      <!-- Additional Sensitive Fields -->
      <div class="config-section">
        <h3 class="section-title"><i class="pi pi-exclamation-triangle" /> Additional Sensitive Fields</h3>
        <p class="section-desc">Field names or patterns that should always be treated as sensitive (in addition to built-in detection).</p>
        <div class="add-row">
          <InputText v-model="newPattern" placeholder="e.g. creditCardNumber or /\btoken_\w+/i" style="flex:1"
                     @keydown.enter="addPattern" />
          <Button label="Add" icon="pi pi-plus" @click="addPattern" />
        </div>
        <div v-if="config.additionalSensitiveFields.length === 0" class="empty-list">No custom patterns added.</div>
        <div v-else class="tag-list">
          <div v-for="(p, idx) in config.additionalSensitiveFields" :key="idx" class="tag-item">
            <code class="pattern-code">{{ p }}</code>
            <button class="remove-btn" @click="removePattern(idx)"><i class="pi pi-times" /></button>
          </div>
        </div>
      </div>

      <!-- Allowed Fields (bypass detection) -->
      <div class="config-section">
        <h3 class="section-title"><i class="pi pi-check-circle" /> Allowed Fields (Bypass Detection)</h3>
        <p class="section-desc">Field names that should never be flagged as sensitive, even if they match patterns.</p>
        <div class="add-row">
          <InputText v-model="newAllowedField" placeholder="e.g. username, publicKey" style="flex:1"
                     @keydown.enter="addAllowedField" />
          <Button label="Add" icon="pi pi-plus" @click="addAllowedField" />
        </div>
        <div v-if="config.allowedFields.length === 0" class="empty-list">No allowed fields configured.</div>
        <div v-else class="tag-list">
          <div v-for="(f, idx) in config.allowedFields" :key="idx" class="tag-item allowed">
            <code class="pattern-code">{{ f }}</code>
            <button class="remove-btn" @click="removeAllowedField(idx)"><i class="pi pi-times" /></button>
          </div>
        </div>
      </div>

      <!-- Info box -->
      <div class="info-box">
        <i class="pi pi-info-circle" />
        <div>
          <strong>How detection works:</strong> Regex pattern matching is always active and detects common secret formats
          (API keys, passwords, tokens). Heuristic scanning detects high-entropy tokens. AI detection adds context-aware
          analysis when enabled. Detected values are masked in the UI and encrypted at rest.
        </div>
      </div>

      <div class="save-bar">
        <Button label="Save Configuration" icon="pi pi-save" :loading="saving" @click="save" />
      </div>
    </div>
  </div>
</template>

<style scoped>
.sf-view { display:flex; flex-direction:column; height:100%; background:var(--bg-base); color:var(--text-primary); overflow-y:auto; }
.toolbar { display:flex; align-items:flex-start; justify-content:space-between; padding:20px 28px; background:var(--bg-surface); border-bottom:1px solid var(--border); flex-shrink:0; }
.page-title { margin:0 0 4px; font-size:18px; font-weight:600; }
.page-subtitle { margin:0; font-size:13px; color:var(--text-secondary); }
.content { padding:24px 28px; display:flex; flex-direction:column; gap:24px; max-width:800px; }
.config-section { background:var(--bg-surface); border:1px solid var(--border); border-radius:var(--border-radius-md); padding:20px; }
.section-title { display:flex; align-items:center; gap:8px; margin:0 0 8px; font-size:14px; font-weight:600; color:var(--text-primary); }
.section-desc { margin:0 0 14px; font-size:13px; color:var(--text-secondary); }
.toggle-grid { display:flex; flex-direction:column; gap:12px; }
.toggle-row { display:flex; align-items:center; justify-content:space-between; padding:10px 0; border-bottom:1px solid var(--border); }
.toggle-row:last-child { border-bottom:none; }
.toggle-info { flex:1; }
.toggle-label { display:block; font-size:14px; font-weight:500; color:var(--text-primary); }
.toggle-desc { display:block; font-size:12px; color:var(--text-muted); margin-top:2px; }
.strictness-selector { display:grid; grid-template-columns:repeat(3,1fr); gap:12px; }
.strictness-card { background:var(--bg-raised); border:2px solid var(--border); border-radius:var(--border-radius-sm); padding:14px; cursor:pointer; transition:var(--transition-fast); }
.strictness-card:hover { border-color:var(--accent-hover); }
.strictness-card.active { border-color:var(--accent); background:var(--accent-muted); }
.strictness-label { display:block; font-weight:600; font-size:14px; color:var(--text-primary); margin-bottom:4px; }
.strictness-desc { display:block; font-size:12px; color:var(--text-secondary); }
.add-row { display:flex; gap:8px; margin-bottom:10px; }
.empty-list { font-size:12px; color:var(--text-muted); font-style:italic; }
.tag-list { display:flex; flex-direction:column; gap:6px; }
.tag-item { display:flex; align-items:center; gap:8px; padding:6px 10px; background:var(--bg-raised); border:1px solid var(--border); border-radius:var(--border-radius-sm); }
.tag-item.allowed { border-left:3px solid var(--success); }
.pattern-code { font-family:var(--font-family-mono); font-size:13px; color:var(--text-primary); flex:1; }
.remove-btn { background:none; border:none; cursor:pointer; color:var(--text-muted); padding:2px 4px; border-radius:3px; font-size:11px; }
.remove-btn:hover { color:var(--danger); background:rgba(239,68,68,.1); }
.info-box { display:flex; gap:12px; padding:14px 16px; background:rgba(6,182,212,.08); border:1px solid rgba(6,182,212,.3); border-radius:var(--border-radius-sm); font-size:13px; color:var(--text-secondary); }
.info-box i { color:var(--info); font-size:16px; flex-shrink:0; margin-top:1px; }
.save-bar { padding-bottom:24px; }
</style>
