<script setup lang="ts">
import { ref, computed, onMounted, nextTick } from 'vue'
import { useRouter } from 'vue-router'

const emit = defineEmits<{ close: [] }>()
const router = useRouter()

const query = ref('')
const inputRef = ref<HTMLInputElement | null>(null)

interface CommandItem {
  label: string
  icon: string
  action: () => void
  group?: string
}

const allCommands: CommandItem[] = [
  { label: 'Connections',        icon: 'pi-server',        group: 'Navigate', action: () => go('connections') },
  { label: 'AI Models',          icon: 'pi-microchip-ai',  group: 'Navigate', action: () => go('ai-models') },
  { label: 'Data Guard',         icon: 'pi-shield',        group: 'Navigate', action: () => go('sensitive-fields') },
  { label: 'Tools',              icon: 'pi-wrench',        group: 'Navigate', action: () => go('tools') },
  { label: 'Prompts',            icon: 'pi-file-edit',     group: 'Navigate', action: () => go('prompts') },
  { label: 'Resources',          icon: 'pi-database',      group: 'Navigate', action: () => go('resources') },
  { label: 'Resource Templates', icon: 'pi-copy',          group: 'Navigate', action: () => go('resource-templates') },
  { label: 'Elicitations',       icon: 'pi-bell',          group: 'Navigate', action: () => go('elicitations') },
  { label: 'Workflows',          icon: 'pi-sitemap',       group: 'Navigate', action: () => go('workflows') },
  { label: 'Chat',               icon: 'pi-comments',      group: 'Navigate', action: () => go('chat') },
]

const selectedIndex = ref(0)

const filtered = computed(() => {
  if (!query.value) return allCommands
  const q = query.value.toLowerCase()
  return allCommands.filter(c => c.label.toLowerCase().includes(q))
})

function go(name: string) {
  router.push({ name })
  emit('close')
}

function run(item: CommandItem) {
  item.action()
  emit('close')
}

function onKeydown(e: KeyboardEvent) {
  if (e.key === 'ArrowDown') {
    e.preventDefault()
    selectedIndex.value = (selectedIndex.value + 1) % filtered.value.length
  } else if (e.key === 'ArrowUp') {
    e.preventDefault()
    selectedIndex.value = (selectedIndex.value - 1 + filtered.value.length) % filtered.value.length
  } else if (e.key === 'Enter') {
    filtered.value[selectedIndex.value]?.action()
    emit('close')
  }
}

onMounted(() => nextTick(() => inputRef.value?.focus()))
</script>

<template>
  <Teleport to="body">
    <div class="palette-backdrop" @click="$emit('close')">
      <div class="palette-panel" @click.stop @keydown="onKeydown">
        <div class="palette-search">
          <i class="pi pi-search palette-search-icon" />
          <input
            ref="inputRef"
            v-model="query"
            class="palette-input"
            placeholder="Search commands…"
            autocomplete="off"
          />
          <kbd class="esc-hint">ESC</kbd>
        </div>
        <ul class="palette-list" v-if="filtered.length">
          <li
            v-for="(item, i) in filtered"
            :key="item.label"
            class="palette-item"
            :class="{ selected: i === selectedIndex }"
            @mouseenter="selectedIndex = i"
            @click="run(item)"
          >
            <i :class="`pi ${item.icon} palette-item-icon`" />
            <span>{{ item.label }}</span>
            <span class="palette-group">{{ item.group }}</span>
          </li>
        </ul>
        <div v-else class="palette-empty">No commands found</div>
      </div>
    </div>
  </Teleport>
</template>

<style scoped>
.palette-backdrop {
  position: fixed; inset: 0;
  background: rgba(0,0,0,.55);
  display: flex; align-items: flex-start; justify-content: center;
  padding-top: 12vh;
  z-index: 9999;
  backdrop-filter: blur(2px);
}

.palette-panel {
  background: var(--bg-surface);
  border: 1px solid var(--border);
  border-radius: var(--border-radius-lg);
  width: 540px;
  max-height: 460px;
  display: flex; flex-direction: column;
  box-shadow: var(--shadow-lg);
  overflow: hidden;
}

.palette-search {
  display: flex; align-items: center; gap: 10px;
  padding: 12px 16px;
  border-bottom: 1px solid var(--border);
}
.palette-search-icon { color: var(--text-muted); font-size: 16px; }
.palette-input {
  flex: 1; border: none; outline: none;
  background: transparent; color: var(--text-primary);
  font-size: 15px;
  font-family: var(--font-family-ui);
}
.esc-hint {
  font-size: 10px; font-family: var(--font-family-mono);
  background: var(--bg-raised); border: 1px solid var(--border);
  border-radius: 3px; padding: 1px 5px; color: var(--text-muted);
}

.palette-list { list-style: none; margin: 0; padding: 6px; overflow-y: auto; }
.palette-item {
  display: flex; align-items: center; gap: 10px;
  padding: 9px 12px; border-radius: var(--border-radius-sm);
  cursor: pointer; color: var(--text-primary); font-size: 13px;
}
.palette-item.selected { background: var(--nav-item-active); }
.palette-item-icon { width: 16px; color: var(--text-secondary); }
.palette-group { margin-left: auto; font-size: 11px; color: var(--text-muted); }
.palette-empty { padding: 20px; text-align: center; color: var(--text-muted); font-size: 13px; }
</style>
