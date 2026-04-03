<script setup lang="ts">
import { ref, computed, watch, onMounted, onUnmounted, nextTick } from 'vue'
import VueJsonPretty from 'vue-json-pretty'
import 'vue-json-pretty/lib/styles.css'
import Mark from 'mark.js'
import Button from 'primevue/button'
import InputText from 'primevue/inputtext'
import Dialog from 'primevue/dialog'
import { useToast } from 'primevue/usetoast'

const props = withDefaults(defineProps<{
  data: unknown
  title?: string
  initiallyExpanded?: boolean
}>(), {
  title: 'JSON Output',
  initiallyExpanded: false,
})

const toast = useToast()
const search = ref('')
const fullscreen = ref(false)
const expandDepth = computed(() => props.initiallyExpanded ? 999 : 3)

// ── Search state ────────────────────────────────────────────────────────────
const matchCount = ref(0)
const matchIndex = ref(-1)

const jvBodyRef = ref<HTMLElement | null>(null)
const fsBodyRef  = ref<HTMLElement | null>(null)

let markInst:   Mark | null = null
let fsMarkInst: Mark | null = null
let observer:   MutationObserver | null = null
// Separate element arrays for inline and fullscreen — both activated simultaneously
let markEls:   HTMLElement[] = []
let fsMarkEls: HTMLElement[] = []
// Re-entry guard: prevents the MutationObserver from re-triggering while mark.js is running
let isMarking = false

// ── Deep-parse embedded JSON strings ─────────────────────────────────────
function deepParseSafe(value: unknown, depth = 0): unknown {
  if (depth > 10) return value
  if (typeof value === 'string') {
    const t = value.trim()
    if ((t.startsWith('{') || t.startsWith('[')) && t.length > 1) {
      try {
        const parsed = JSON.parse(t)
        if (parsed !== null && typeof parsed === 'object')
          return deepParseSafe(parsed, depth + 1)
      } catch { /* not JSON */ }
    }
    return value
  }
  if (Array.isArray(value))
    return value.map(item => deepParseSafe(item, depth + 1))
  if (value !== null && typeof value === 'object')
    return Object.fromEntries(
      Object.entries(value as Record<string, unknown>).map(
        ([k, v]) => [k, deepParseSafe(v, depth + 1)]
      )
    )
  return value
}

const parsedData = computed(() => deepParseSafe(props.data))

// ── mark.js helpers ──────────────────────────────────────────────────────
function getMarkInst(el: HTMLElement): Mark {
  if (!markInst) markInst = new Mark(el)
  return markInst
}
function getFsMarkInst(el: HTMLElement): Mark {
  if (!fsMarkInst) fsMarkInst = new Mark(el)
  return fsMarkInst
}

function resumeObserver() {
  if (jvBodyRef.value && observer)
    observer.observe(jvBodyRef.value, { childList: true, subtree: true, characterData: false })
}

/** Highlight the active match in both views; scroll the visible one into view. */
function activateCurrent() {
  const idx = matchIndex.value
  for (const els of [markEls, fsMarkEls]) {
    els.forEach((el, i) => {
      el.classList.toggle('jv-mark-active', i === idx)
      el.classList.toggle('jv-mark', i !== idx)
    })
  }
  // Scroll whichever container is currently visible
  const activeSet = fullscreen.value ? fsMarkEls : markEls
  if (idx >= 0 && idx < activeSet.length)
    activeSet[idx].scrollIntoView({ block: 'nearest', behavior: 'smooth' })
}

/**
 * Apply mark.js search to one container.
 * @param isPrimary - true for the inline view (owns matchCount/matchIndex/markEls);
 *                    false for the fullscreen view (owns fsMarkEls only).
 */
function applySearch(term: string, containerEl: HTMLElement, inst: Mark, isPrimary: boolean) {
  if (isPrimary) {
    isMarking = true
    observer?.disconnect()
  }

  inst.unmark({
    done() {
      if (!term.trim()) {
        if (isPrimary) {
          matchCount.value = 0
          matchIndex.value = -1
          markEls = []
          isMarking = false
          resumeObserver()
        } else {
          fsMarkEls = []
        }
        return
      }

      inst.mark(term, {
        caseSensitive: false,
        separateWordSearch: false,
        accuracy: 'partially',
        done() {
          if (isPrimary) {
            markEls = Array.from(
              containerEl.querySelectorAll<HTMLElement>('mark[data-markjs="true"]')
            )
            matchCount.value = markEls.length
            matchIndex.value = matchCount.value > 0 ? 0 : -1
            activateCurrent()
            isMarking = false
            resumeObserver()
          } else {
            fsMarkEls = Array.from(
              containerEl.querySelectorAll<HTMLElement>('mark[data-markjs="true"]')
            )
            // Re-apply active class to freshly created fullscreen mark elements
            activateCurrent()
          }
        },
      })
    },
  })
}

function runSearch(term: string) {
  if (jvBodyRef.value) applySearch(term, jvBodyRef.value, getMarkInst(jvBodyRef.value), true)
  if (fsBodyRef.value) applySearch(term, fsBodyRef.value, getFsMarkInst(fsBodyRef.value), false)
}

function nextMatch() {
  if (!matchCount.value) return
  matchIndex.value = (matchIndex.value + 1) % matchCount.value
  activateCurrent()
}
function prevMatch() {
  if (!matchCount.value) return
  matchIndex.value = (matchIndex.value - 1 + matchCount.value) % matchCount.value
  activateCurrent()
}

// Re-apply search when fullscreen dialog opens (fsBodyRef available after nextTick)
watch(fullscreen, async (val) => {
  if (val && search.value.trim()) {
    await nextTick()
    if (fsBodyRef.value)
      applySearch(search.value, fsBodyRef.value, getFsMarkInst(fsBodyRef.value), false)
  }
})

watch(search, async (val) => {
  await nextTick()
  runSearch(val)
})

watch(parsedData, async () => {
  await nextTick()
  if (search.value.trim()) runSearch(search.value)
})

onMounted(() => {
  if (!jvBodyRef.value) return
  observer = new MutationObserver(() => {
    // isMarking guard prevents re-entry while mark.js is writing to the DOM
    if (!isMarking && search.value.trim())
      nextTick(() => runSearch(search.value))
  })
  resumeObserver()
})

onUnmounted(() => {
  observer?.disconnect()
  observer = null
  markInst?.unmark()
  markInst = null
  fsMarkInst?.unmark()
  fsMarkInst = null
})

// ── Clipboard ────────────────────────────────────────────────────────────
async function copy() {
  try {
    await navigator.clipboard.writeText(JSON.stringify(props.data, null, 2))
    toast.add({ severity: 'success', summary: 'Copied to clipboard', life: 2000 })
  } catch {
    toast.add({ severity: 'error', summary: 'Copy failed', life: 3000 })
  }
}
</script>

<template>
  <div class="json-viewer">
    <div class="jv-toolbar">
      <span class="jv-title">{{ title }}</span>
      <div class="jv-actions">
        <div class="jv-search-box">
          <InputText
            v-model="search"
            placeholder="Search…"
            size="small"
            class="jv-search"
            @keydown.enter.exact.prevent="nextMatch"
            @keydown.shift.enter.prevent="prevMatch"
            @keydown.escape="search = ''"
          />
          <span
            v-if="search.trim()"
            class="jv-match-counter"
            :class="{ 'jv-no-match': matchCount === 0 }"
          >
            {{ matchCount === 0 ? 'No matches' : `${matchIndex + 1} / ${matchCount}` }}
          </span>
          <button v-if="matchCount > 0" class="jv-nav-btn" v-tooltip="'Previous (Shift+Enter)'" @click="prevMatch">
            <i class="pi pi-chevron-up" />
          </button>
          <button v-if="matchCount > 0" class="jv-nav-btn" v-tooltip="'Next (Enter)'" @click="nextMatch">
            <i class="pi pi-chevron-down" />
          </button>
        </div>
        <Button icon="pi pi-copy" text size="small" v-tooltip="'Copy JSON'" @click="copy" />
        <Button icon="pi pi-window-maximize" text size="small" v-tooltip="'Fullscreen'" @click="fullscreen = true" />
      </div>
    </div>
    <div class="jv-body" ref="jvBodyRef">
      <VueJsonPretty
        :data="(parsedData as any)"
        :deep="expandDepth"
        :show-length="true"
        :show-line="true"
        :show-double-quotes="true"
        :highlight-mouseover-node="true"
        :show-icon="true"
        class="jvp"
      />
    </div>
  </div>

  <!-- Fullscreen modal -->
  <Dialog v-model:visible="fullscreen" modal maximizable :style="{ width: '90vw', maxHeight: '90vh' }" :header="title">
    <div class="jv-toolbar" style="margin-bottom: 8px">
      <div class="jv-search-box">
        <InputText
          v-model="search"
          placeholder="Search…"
          size="small"
          class="jv-search"
          @keydown.enter.exact.prevent="nextMatch"
          @keydown.shift.enter.prevent="prevMatch"
          @keydown.escape="search = ''"
        />
        <span v-if="search.trim()" class="jv-match-counter" :class="{ 'jv-no-match': matchCount === 0 }">
          {{ matchCount === 0 ? 'No matches' : `${matchIndex + 1} / ${matchCount}` }}
        </span>
        <button v-if="matchCount > 0" class="jv-nav-btn" @click="prevMatch"><i class="pi pi-chevron-up" /></button>
        <button v-if="matchCount > 0" class="jv-nav-btn" @click="nextMatch"><i class="pi pi-chevron-down" /></button>
      </div>
      <Button icon="pi pi-copy" text size="small" v-tooltip="'Copy JSON'" @click="copy" />
    </div>
    <div class="jv-body" ref="fsBodyRef" style="max-height: 70vh;">
      <VueJsonPretty
        :data="(parsedData as any)"
        :deep="999"
        :show-length="true"
        :show-line="true"
        :show-double-quotes="true"
        :show-icon="true"
        class="jvp"
      />
    </div>
  </Dialog>
</template>

<style scoped>
.json-viewer {
  display: flex;
  flex-direction: column;
  background: var(--code-bg);
  border: 1px solid var(--border);
  border-radius: var(--border-radius-md);
  overflow: hidden;
  height: 100%;
}
.jv-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 6px 10px;
  background: var(--bg-raised);
  border-bottom: 1px solid var(--border);
  gap: 8px;
  flex-shrink: 0;
}
.jv-title {
  font-size: 11px;
  font-weight: 600;
  color: var(--text-muted);
  text-transform: uppercase;
  letter-spacing: .05em;
  flex-shrink: 0;
}
.jv-actions {
  display: flex;
  align-items: center;
  gap: 4px;
}
.jv-search-box {
  display: flex;
  align-items: center;
  gap: 2px;
}
.jv-search { width: 150px; font-size: 12px; }
.jv-match-counter {
  font-size: 11px;
  color: var(--text-muted);
  min-width: 58px;
  text-align: center;
  white-space: nowrap;
  padding: 0 4px;
}
.jv-no-match { color: var(--danger, #f97583); }
.jv-nav-btn {
  background: none;
  border: none;
  cursor: pointer;
  color: var(--text-muted);
  padding: 3px 4px;
  border-radius: 3px;
  display: flex;
  align-items: center;
  font-size: 11px;
}
.jv-nav-btn:hover { background: var(--nav-item-hover); color: var(--text-primary); }
.jv-body {
  flex: 1;
  overflow: auto;
  padding: 8px 4px;
}
</style>

<style>
/* Override vue-json-pretty colours to match our theme */
.jvp {
  background: transparent !important;
  font-family: var(--font-family-mono) !important;
  font-size: 12px !important;
  line-height: 1.6 !important;
  padding: 6px 10px !important;
}
.jvp .vjs-tree-node { color: var(--text-secondary); }
.jvp .vjs-key { color: var(--json-key, #79b8ff) !important; }
.jvp .vjs-value-string { color: var(--json-str, #85e89d) !important; }
.jvp .vjs-value-number { color: var(--json-num, #f8c555) !important; }
.jvp .vjs-value-boolean { color: var(--json-bool, #b392f0) !important; }
.jvp .vjs-value-null { color: var(--json-null, #f97583) !important; }
.jvp .vjs-tree__brackets { color: var(--text-muted) !important; }
.jvp .vjs-tree__content--inside { border-left: 1px dotted var(--border) !important; }

/* Search highlights — applied by mark.js */
mark[data-markjs="true"].jv-mark {
  background: rgba(251, 191, 36, 0.30);
  color: inherit;
  border-radius: 2px;
  padding: 0 1px;
  outline: 1px solid rgba(251, 191, 36, 0.45);
}
mark[data-markjs="true"].jv-mark-active {
  background: rgba(251, 191, 36, 0.85);
  color: #1a1a2e;
  border-radius: 2px;
  padding: 0 1px;
  outline: 2px solid #fbbf24;
  font-weight: 600;
}
</style>
