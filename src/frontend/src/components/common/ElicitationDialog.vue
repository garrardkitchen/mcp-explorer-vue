<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import Dialog from 'primevue/dialog'
import Button from 'primevue/button'
import InputText from 'primevue/inputtext'
import Select from 'primevue/select'
import MultiSelect from 'primevue/multiselect'
import Tag from 'primevue/tag'
import type { ElicitationRequest } from '@/api/types'

const props = defineProps<{
  request: ElicitationRequest | null
  stepNumber: number
}>()

const emit = defineEmits<{
  respond: [action: string, content?: Record<string, unknown>]
}>()

// ── Internal visibility — managed by the request prop ─────────────────
const visible = ref(false)
watch(() => props.request, (r) => { visible.value = !!r }, { immediate: true })

// ── Form state ─────────────────────────────────────────────────────────
// Values can be string, number, boolean, or string[] (for multi-select)
const content = ref<Record<string, string | number | boolean | string[]>>({})
const submitting = ref(false)
const declining = ref(false)

/** Resolve labelled options for a schema property, plus whether it's multi-select. */
function resolveOptions(p: any): { options?: Array<{value: string, label: string}>, isMulti: boolean } {
  if (Array.isArray(p.enum)) {
    // UntitledSingleSelectEnumSchema: { type:"string", enum:[...] }
    return { options: (p.enum as string[]).map(v => ({ value: v, label: v })), isMulti: false }
  }
  if (Array.isArray(p.oneOf)) {
    // TitledSingleSelectEnumSchema: { type:"string", oneOf:[{const,title}] }
    return { options: (p.oneOf as any[]).map(o => ({ value: o.const, label: o.title || o.const })), isMulti: false }
  }
  if (p.type === 'array' && Array.isArray(p.items?.enum)) {
    // UntitledMultiSelectEnumSchema: { type:"array", items:{ type:"string", enum:[...] } }
    return { options: (p.items.enum as string[]).map(v => ({ value: v, label: v })), isMulti: true }
  }
  if (p.type === 'array' && Array.isArray(p.items?.anyOf)) {
    // TitledMultiSelectEnumSchema: { type:"array", items:{ anyOf:[{const,title}] } }
    return { options: (p.items.anyOf as any[]).map(o => ({ value: o.const, label: o.title || o.const })), isMulti: true }
  }
  return { isMulti: false }
}

// Seed form fields from schema defaults when the request changes.
// The backend sends schema as a flat { fieldName: schemaDef } object — not wrapped
// in a JSON Schema { type:"object", properties:{} } envelope.
watch(() => props.request, (req) => {
  content.value = {}
  if (!req?.schema) return
  for (const [key, prop] of Object.entries(req.schema as Record<string, any>)) {
    const p = prop as any
    const { isMulti } = resolveOptions(p)
    if (isMulti) {
      content.value[key] = (p.default as string[]) ?? []
    } else if (p.type === 'boolean') {
      content.value[key] = p.default ?? false
    } else if (p.type === 'integer' || p.type === 'number') {
      content.value[key] = p.default ?? 0
    } else {
      content.value[key] = p.default ?? ''
    }
  }
}, { immediate: true })

// ── Schema helpers ─────────────────────────────────────────────────────
const fields = computed(() => {
  // schema is a flat { fieldName: schemaDef } map (backend does NOT wrap in properties:{})
  const schemaMap = props.request?.schema as Record<string, any> | undefined
  if (!schemaMap || Object.keys(schemaMap).length === 0) return []
  return Object.entries(schemaMap).map(([key, prop]: [string, any]) => {
    const { options, isMulti } = resolveOptions(prop)
    return {
      name: key,
      label: prop.title ?? key,
      type: (prop.type ?? 'string') as string,
      description: prop.description ?? '',
      required: false,
      options,
      isMulti,
      minimum: prop.minimum as number | undefined,
      maximum: prop.maximum as number | undefined,
      minLength: prop.minLength as number | undefined,
      maxLength: prop.maxLength as number | undefined,
      format: prop.format as string | undefined,
    }
  })
})

// ── Actions ────────────────────────────────────────────────────────────
async function accept() {
  submitting.value = true
  try {
    // Coerce number/integer fields: HTML inputs return strings; MCP server expects numbers.
    const coerced: Record<string, unknown> = {}
    for (const f of fields.value) {
      const val = content.value[f.name]
      if (f.type === 'number' || f.type === 'integer') {
        coerced[f.name] = typeof val === 'string' ? parseFloat(val) : val
      } else {
        coerced[f.name] = val
      }
    }
    emit('respond', 'Accept', coerced)
  } finally {
    submitting.value = false
  }
}

async function decline() {
  declining.value = true
  try {
    emit('respond', 'Decline')
  } finally {
    declining.value = false
  }
}

function formatTime(ts: string) {
  return new Date(ts).toLocaleTimeString()
}

function iconForType(type: string, format?: string, isMulti?: boolean, hasOptions?: boolean) {
  if (type === 'boolean') return 'pi-check-square'
  if (type === 'integer' || type === 'number') return 'pi-hash'
  if (format === 'date' || format === 'datetime') return 'pi-calendar'
  if (format === 'uri') return 'pi-link'
  if (format === 'password') return 'pi-lock'
  if (hasOptions && isMulti) return 'pi-list-check'
  if (hasOptions) return 'pi-circle'
  return 'pi-pencil'
}
</script>

<template>
  <Dialog
    v-model:visible="visible"
    :closable="false"
    :modal="true"
    :draggable="false"
    :style="{ width: '540px', maxWidth: '95vw' }"
    :pt="{ header: { class: 'elicit-dialog-header' }, content: { class: 'elicit-dialog-content' }, footer: { class: 'elicit-dialog-footer' } }"
  >
    <template #header>
      <div class="header-inner">
        <div class="header-left">
          <span class="header-icon">⚡</span>
          <div>
            <div class="header-title">Input Required</div>
            <div class="header-sub">MCP server is requesting additional information</div>
          </div>
        </div>
        <div class="header-right">
          <Tag v-if="request" :value="request.connectionName" severity="secondary" class="conn-tag" />
          <div v-if="stepNumber > 1" class="step-badge">Step {{ stepNumber }}</div>
        </div>
      </div>
    </template>

    <div v-if="request" class="elicit-body">
      <!-- Server message -->
      <div v-if="request.message" class="server-message">
        <i class="pi pi-comment message-icon" />
        <p class="message-text">{{ request.message }}</p>
      </div>

      <!-- Fields count indicator when there are multiple -->
      <div v-if="fields.length > 1" class="fields-hint">
        {{ fields.length }} fields requested · <span class="required-hint">* required</span>
      </div>

      <!-- Form fields -->
      <div class="fields-list">
        <div v-for="f in fields" :key="f.name" class="field-row">
          <div class="field-label-row">
            <i :class="['pi', iconForType(f.type, f.format, f.isMulti, !!f.options), 'field-type-icon']" />
            <label>
              {{ f.label }}
              <span v-if="f.required" class="req-mark">*</span>
            </label>
            <span v-if="f.options" class="type-pill">{{ f.isMulti ? 'multi-select' : 'single-select' }}</span>
            <span v-else class="type-pill">{{ f.format ? `${f.type} (${f.format})` : f.type }}</span>
          </div>
          <p v-if="f.description" class="field-desc">{{ f.description }}</p>

          <!-- Boolean toggle -->
          <div v-if="f.type === 'boolean'" class="bool-row">
            <button
              class="bool-toggle"
              :class="{ active: content[f.name] === true }"
              @click="content[f.name] = !content[f.name]"
            >
              <span class="bool-knob" />
            </button>
            <span class="bool-label-text">{{ content[f.name] ? 'True' : 'False' }}</span>
          </div>

          <!-- Radio buttons — single-select, ≤3 options -->
          <div v-else-if="f.options && !f.isMulti && f.options.length <= 3" class="options-group" role="radiogroup">
            <label
              v-for="opt in f.options"
              :key="opt.value"
              class="option-card"
              :class="{ selected: content[f.name] === opt.value }"
            >
              <input
                type="radio"
                :name="f.name"
                :value="opt.value"
                v-model="(content as any)[f.name]"
                class="option-input"
              />
              <span class="option-indicator radio-indicator" />
              <span class="option-label">{{ opt.label }}</span>
            </label>
          </div>

          <!-- Dropdown — single-select, >3 options -->
          <Select
            v-else-if="f.options && !f.isMulti && f.options.length > 3"
            v-model="(content as any)[f.name]"
            :options="f.options"
            optionLabel="label"
            optionValue="value"
            placeholder="Select an option…"
            class="field-input w-full"
          />

          <!-- Checkboxes — multi-select, ≤3 options -->
          <div v-else-if="f.options && f.isMulti && f.options.length <= 3" class="options-group" role="group">
            <label
              v-for="opt in f.options"
              :key="opt.value"
              class="option-card"
              :class="{ selected: Array.isArray(content[f.name]) && (content[f.name] as string[]).includes(opt.value) }"
            >
              <input
                type="checkbox"
                :value="opt.value"
                v-model="(content as any)[f.name]"
                class="option-input"
              />
              <span class="option-indicator check-indicator" />
              <span class="option-label">{{ opt.label }}</span>
            </label>
          </div>

          <!-- Multi-select dropdown — multi-select, >3 options -->
          <MultiSelect
            v-else-if="f.options && f.isMulti && f.options.length > 3"
            v-model="(content as any)[f.name]"
            :options="f.options"
            optionLabel="label"
            optionValue="value"
            placeholder="Select options…"
            display="chip"
            class="field-input w-full"
          />

          <!-- Integer/Number -->
          <InputText
            v-else-if="f.type === 'integer' || f.type === 'number'"
            v-model="(content as any)[f.name]"
            type="number"
            :min="f.minimum"
            :max="f.maximum"
            :placeholder="f.description || String(f.minimum ?? '') + '–' + String(f.maximum ?? '')"
            class="field-input"
          />

          <!-- Date format -->
          <input
            v-else-if="f.format === 'date'"
            v-model="(content as any)[f.name]"
            type="date"
            class="native-date-input field-input"
          />

          <!-- DateTime format -->
          <input
            v-else-if="f.format === 'datetime' || f.format === 'date-time'"
            v-model="(content as any)[f.name]"
            type="datetime-local"
            class="native-date-input field-input"
          />

          <!-- Password format -->
          <InputText
            v-else-if="f.format === 'password'"
            v-model="(content as any)[f.name]"
            type="password"
            :placeholder="f.description || f.label"
            class="field-input"
          />

          <!-- URI format -->
          <InputText
            v-else-if="f.format === 'uri'"
            v-model="(content as any)[f.name]"
            type="url"
            :placeholder="f.description || 'https://…'"
            class="field-input"
          />

          <!-- Default string -->
          <InputText
            v-else
            v-model="(content as any)[f.name]"
            :placeholder="f.description || f.label"
            class="field-input"
          />
        </div>
      </div>

      <!-- Timestamp -->
      <div class="footer-meta">
        <i class="pi pi-clock" />
        Requested at {{ formatTime(request.timestampUtc) }}
      </div>
    </div>

    <template #footer>
      <div class="action-row">
        <Button
          label="Decline"
          icon="pi pi-times"
          severity="secondary"
          text
          :loading="declining"
          :disabled="submitting"
          @click="decline"
        />
        <Button
          label="Accept"
          icon="pi pi-check"
          severity="success"
          :loading="submitting"
          :disabled="declining"
          @click="accept"
        />
      </div>
    </template>
  </Dialog>
</template>

<style scoped>
/* Header */
:deep(.elicit-dialog-header) {
  background: linear-gradient(135deg, rgba(245,158,11,0.12), rgba(245,158,11,0.04));
  border-bottom: 1px solid rgba(245,158,11,0.3);
  padding: 16px 20px !important;
}
:deep(.elicit-dialog-content) { padding: 20px !important; }
:deep(.elicit-dialog-footer) { padding: 12px 20px !important; border-top: 1px solid var(--border); }

.header-inner { display:flex; align-items:flex-start; justify-content:space-between; width:100%; gap:12px; }
.header-left { display:flex; align-items:flex-start; gap:12px; }
.header-icon { font-size:22px; line-height:1; flex-shrink:0; margin-top:2px; }
.header-title { font-size:15px; font-weight:700; color:var(--text-primary); }
.header-sub { font-size:11px; color:var(--text-muted); margin-top:2px; }
.header-right { display:flex; align-items:center; gap:8px; flex-shrink:0; }
.conn-tag :deep(.p-tag) { font-size:11px; }
.step-badge {
  background: rgba(245,158,11,0.15);
  color: var(--warning, #f59e0b);
  border: 1px solid rgba(245,158,11,0.35);
  border-radius: 999px;
  padding: 2px 10px;
  font-size: 11px;
  font-weight: 600;
}

/* Body */
.elicit-body { display:flex; flex-direction:column; gap:14px; }
.server-message {
  display:flex; align-items:flex-start; gap:10px;
  background: var(--bg-raised);
  border: 1px solid var(--border);
  border-left: 3px solid var(--warning, #f59e0b);
  border-radius: var(--border-radius-sm);
  padding: 10px 14px;
}
.message-icon { color: var(--warning, #f59e0b); margin-top: 2px; flex-shrink:0; }
.message-text { margin:0; font-size:13px; color:var(--text-primary); line-height:1.5; }
.fields-hint { font-size:11px; color:var(--text-muted); }
.required-hint { color:var(--danger); }

/* Fields */
.fields-list { display:flex; flex-direction:column; gap:12px; }
.field-row { display:flex; flex-direction:column; gap:6px; }
.field-label-row { display:flex; align-items:center; gap:6px; }
.field-type-icon { font-size:12px; color:var(--text-muted); }
.field-label-row label { font-size:12px; font-weight:600; color:var(--text-secondary); text-transform:uppercase; letter-spacing:.04em; flex:1; }
.req-mark { color:var(--danger); margin-left:2px; }
.type-pill { font-size:10px; color:var(--text-muted); background:var(--bg-raised); border:1px solid var(--border); padding:1px 6px; border-radius:4px; font-family:var(--font-family-mono); }
.field-desc { margin:0; font-size:12px; color:var(--text-muted); line-height:1.4; }
.field-input { width:100% !important; }

/* Boolean toggle */
.bool-row { display:flex; align-items:center; gap:10px; }
.bool-toggle {
  position:relative; width:40px; height:22px;
  background: var(--bg-raised); border: 1px solid var(--border);
  border-radius: 999px; cursor:pointer; transition: background .2s, border-color .2s;
  padding: 0; flex-shrink:0;
}
.bool-toggle.active { background: var(--success, #22c55e); border-color: var(--success, #22c55e); }
.bool-knob {
  position:absolute; top:2px; left:2px;
  width:16px; height:16px; background:#fff;
  border-radius:50%; transition: transform .2s;
  display:block;
}
.bool-toggle.active .bool-knob { transform: translateX(18px); }
.bool-label-text { font-size:13px; color:var(--text-secondary); }

/* Enum select */
.native-select {
  width:100%; background:var(--bg-raised); border:1px solid var(--border);
  color:var(--text-primary); padding:8px 10px; border-radius:var(--border-radius-sm);
  font-size:13px; outline:none;
}
.native-select:focus { border-color: var(--accent); }

/* Date input */
.native-date-input {
  width:100%; background:var(--bg-raised); border:1px solid var(--border);
  color:var(--text-primary); padding:8px 10px; border-radius:var(--border-radius-sm);
  font-size:13px; outline:none; font-family: inherit;
}
.native-date-input:focus { border-color: var(--accent); }

/* Radio / Checkbox option cards */
.options-group {
  display: flex;
  flex-direction: column;
  gap: 6px;
}
.option-card {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 9px 12px;
  background: var(--bg-raised);
  border: 1px solid var(--border);
  border-radius: var(--border-radius-sm);
  cursor: pointer;
  transition: border-color .15s, background .15s;
  user-select: none;
}
.option-card:hover {
  border-color: var(--accent);
  background: color-mix(in srgb, var(--accent) 6%, var(--bg-raised));
}
.option-card.selected {
  border-color: var(--accent);
  background: color-mix(in srgb, var(--accent) 10%, var(--bg-raised));
}
/* Hide the native input visually but keep it accessible */
.option-input {
  position: absolute;
  opacity: 0;
  width: 0;
  height: 0;
  pointer-events: none;
}
/* Custom radio indicator */
.option-indicator {
  flex-shrink: 0;
  width: 16px;
  height: 16px;
  border: 2px solid var(--border);
  background: var(--bg-surface);
  transition: border-color .15s, background .15s;
}
.radio-indicator {
  border-radius: 50%;
}
.check-indicator {
  border-radius: 3px;
}
.option-card.selected .option-indicator {
  border-color: var(--accent);
  background: var(--accent);
  box-shadow: inset 0 0 0 3px var(--bg-raised);
}
.option-card.selected .check-indicator {
  background: var(--accent);
  box-shadow: none;
  /* Simple checkmark via clip-path or content */
  position: relative;
}
.option-card.selected .check-indicator::after {
  content: '';
  position: absolute;
  left: 3px;
  top: 1px;
  width: 5px;
  height: 8px;
  border: 2px solid white;
  border-top: none;
  border-left: none;
  transform: rotate(45deg);
}
.option-label {
  font-size: 13px;
  color: var(--text-primary);
  line-height: 1.4;
}

/* Footer */
.footer-meta { display:flex; align-items:center; gap:6px; font-size:11px; color:var(--text-muted); }
.action-row { display:flex; justify-content:flex-end; gap:8px; width:100%; }
</style>
