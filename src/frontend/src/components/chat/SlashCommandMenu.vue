<script setup lang="ts">
import { ref, computed, watch, nextTick } from 'vue'

export interface SlashCommand {
  id: string
  name: string           // e.g. /prompt
  label: string          // display name
  description: string
  icon: string           // emoji or pi class
  group: string
  groupIcon: string
  isNew?: boolean
}

const props = defineProps<{
  query: string          // text after the slash e.g. "pr" when user typed "/pr"
  visible: boolean
  commands: SlashCommand[]
}>()

const emit = defineEmits<{
  select: [command: SlashCommand]
  dismiss: []
}>()

const selectedIndex = ref(0)
const containerRef = ref<HTMLElement>()

// Group ordering
const GROUP_ORDER = ['MCP Prompts', 'Recent Messages', 'Chat Controls']

const filtered = computed(() => {
  const q = props.query.toLowerCase()
  return props.commands.filter(cmd =>
    !q || cmd.name.toLowerCase().includes(q) || cmd.label.toLowerCase().includes(q) || cmd.description.toLowerCase().includes(q)
  )
})

const groups = computed(() => {
  const map = new Map<string, SlashCommand[]>()
  for (const cmd of filtered.value) {
    if (!map.has(cmd.group)) map.set(cmd.group, [])
    map.get(cmd.group)!.push(cmd)
  }
  // Return in defined order
  const result: Array<{ label: string; icon: string; items: SlashCommand[] }> = []
  for (const g of GROUP_ORDER) {
    if (map.has(g)) {
      const items = map.get(g)!
      result.push({ label: g, icon: items[0].groupIcon, items })
    }
  }
  // Any remaining groups not in GROUP_ORDER
  for (const [label, items] of map) {
    if (!GROUP_ORDER.includes(label)) {
      result.push({ label, icon: items[0].groupIcon, items })
    }
  }
  return result
})

// Flat list for keyboard navigation
const flatItems = computed(() => filtered.value)

const selectedCommand = computed(() => flatItems.value[selectedIndex.value] ?? null)

watch(() => props.query, () => { selectedIndex.value = 0 })
watch(() => props.visible, (v) => { if (v) selectedIndex.value = 0 })

// Scroll selected item into view
watch(selectedIndex, async () => {
  await nextTick()
  const el = containerRef.value?.querySelector('.slash-item.selected') as HTMLElement | null
  el?.scrollIntoView({ block: 'nearest' })
})

function highlight(text: string): string {
  if (!props.query) return text
  const q = props.query.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
  return text.replace(new RegExp(`(${q})`, 'gi'), '<span class="match">$1</span>')
}

function navigate(delta: number) {
  const len = flatItems.value.length
  if (!len) return
  selectedIndex.value = (selectedIndex.value + delta + len) % len
}

function confirm() {
  if (selectedCommand.value) emit('select', selectedCommand.value)
}

// Expose keyboard API to parent
defineExpose({ navigate, confirm })
</script>

<template>
  <Transition name="slash-pop">
    <div v-if="visible && groups.length" ref="containerRef" class="slash-menu" role="listbox" aria-label="Slash commands">

      <!-- Groups -->
      <div v-for="group in groups" :key="group.label">
        <div class="slash-group-header">
          <span class="slash-group-icon">{{ group.icon }}</span>
          <span class="slash-group-label">{{ group.label }}</span>
        </div>

        <div
          v-for="cmd in group.items"
          :key="cmd.id"
          class="slash-item"
          :class="{ selected: cmd === selectedCommand }"
          role="option"
          :aria-selected="cmd === selectedCommand"
          @click="emit('select', cmd)"
          @mouseenter="selectedIndex = flatItems.indexOf(cmd)"
        >
          <div class="slash-icon-wrap" :class="`slash-ic-${cmd.id}`">
            <span class="slash-icon">{{ cmd.icon }}</span>
          </div>
          <div class="slash-body">
            <div class="slash-name" v-html="highlight(cmd.name)" />
            <div class="slash-desc">{{ cmd.description }}</div>
          </div>
          <span v-if="cmd.isNew" class="slash-new-badge">new</span>
          <span v-if="cmd === selectedCommand" class="slash-enter-hint">↵</span>
        </div>
      </div>

      <!-- Selected command preview footer -->
      <div v-if="selectedCommand" class="slash-preview">
        <span class="slash-preview-icon">{{ selectedCommand.icon }}</span>
        <span><strong>{{ selectedCommand.name }}</strong> — {{ selectedCommand.description }}</span>
      </div>

      <!-- Keyboard hints -->
      <div class="slash-footer">
        <span><kbd>↑↓</kbd> navigate</span>
        <span><kbd>↵</kbd> select</span>
        <span><kbd>Tab</kbd> insert</span>
        <span><kbd>Esc</kbd> dismiss</span>
      </div>
    </div>
  </Transition>
</template>

<style scoped>
.slash-menu {
  position: absolute;
  bottom: calc(100% + 8px);
  left: 0;
  right: 0;
  background: var(--bg-surface);
  border: 1px solid var(--border-focus);
  border-radius: 10px;
  overflow: hidden;
  box-shadow: 0 12px 40px rgba(0,0,0,.55);
  z-index: 200;
  max-height: 420px;
  overflow-y: auto;
  scrollbar-width: thin;
}

/* ── Group header ── */
.slash-group-header {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 7px 12px 4px;
  font-size: 10px;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: .08em;
  color: var(--text-muted);
  border-bottom: 1px solid var(--border);
  background: var(--bg-overlay);
}
.slash-group-icon { font-size: 12px; }
.slash-group-label { }

/* ── Item ── */
.slash-item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 8px 12px;
  cursor: pointer;
  border-bottom: 1px solid color-mix(in srgb, var(--border) 50%, transparent);
  transition: background .1s;
}
.slash-item:last-child { border-bottom: none; }
.slash-item:hover,
.slash-item.selected { background: var(--accent-muted); }

/* Icon badge */
.slash-icon-wrap {
  width: 30px;
  height: 30px;
  border-radius: 7px;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  font-size: 14px;
  background: var(--bg-raised);
}
.slash-ic-prompt  { background: color-mix(in srgb, var(--nav-accent-blue) 18%, transparent); }
.slash-ic-recent1,
.slash-ic-recent2,
.slash-ic-recent3 { background: color-mix(in srgb, var(--nav-accent-amber) 18%, transparent); }
.slash-ic-stats   { background: color-mix(in srgb, var(--nav-accent-teal) 18%, transparent); }
.slash-ic-report  { background: color-mix(in srgb, var(--nav-accent-violet) 18%, transparent); }
.slash-ic-system  { background: color-mix(in srgb, var(--nav-accent-teal) 15%, transparent); }
.slash-ic-clear   { background: color-mix(in srgb, var(--danger) 15%, transparent); }
.slash-ic-model   { background: color-mix(in srgb, var(--nav-accent-blue) 15%, transparent); }
.slash-ic-new     { background: color-mix(in srgb, var(--success) 15%, transparent); }

/* Body */
.slash-body { flex: 1; min-width: 0; }
.slash-name {
  font-weight: 600;
  font-size: 13px;
  color: var(--text-primary);
  font-family: var(--font-family-mono);
}
:deep(.match) { color: var(--accent); }
.slash-desc { font-size: 11px; color: var(--text-muted); margin-top: 1px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }

/* Badges */
.slash-new-badge {
  font-size: 9px; font-weight: 700; text-transform: uppercase; letter-spacing: .05em;
  background: color-mix(in srgb, var(--nav-accent-teal) 20%, transparent);
  color: var(--nav-accent-teal);
  border: 1px solid color-mix(in srgb, var(--nav-accent-teal) 40%, transparent);
  border-radius: 4px; padding: 2px 5px; flex-shrink: 0;
}
.slash-enter-hint {
  font-size: 11px; color: var(--text-muted);
  background: var(--bg-raised); border: 1px solid var(--border);
  border-radius: 4px; padding: 1px 5px; flex-shrink: 0;
}

/* Preview footer */
.slash-preview {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  background: var(--bg-overlay);
  border-top: 1px solid var(--border);
  font-size: 11px;
  color: var(--text-muted);
}
.slash-preview-icon { font-size: 15px; }
.slash-preview strong { color: var(--text-secondary); }

/* Keyboard hints */
.slash-footer {
  display: flex;
  gap: 16px;
  padding: 5px 12px;
  background: var(--bg-overlay);
  border-top: 1px solid var(--border);
  font-size: 10px;
  color: var(--text-muted);
}
.slash-footer span { display: flex; align-items: center; gap: 3px; }
kbd {
  background: var(--bg-raised);
  border: 1px solid var(--border);
  border-radius: 3px;
  padding: 1px 4px;
  font-size: 10px;
  font-family: inherit;
}

/* Transition */
.slash-pop-enter-active { transition: opacity 100ms ease, transform 100ms ease; }
.slash-pop-leave-active { transition: opacity 80ms ease, transform 80ms ease; }
.slash-pop-enter-from,
.slash-pop-leave-to    { opacity: 0; transform: translateY(6px); }
</style>
