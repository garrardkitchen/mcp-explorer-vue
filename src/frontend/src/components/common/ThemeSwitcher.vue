<script setup lang="ts">
import { ref } from 'vue'
import Button from 'primevue/button'
import Popover from 'primevue/popover'
import { useThemeStore, THEMES } from '@/stores/themes'
import type { ThemeId } from '@/stores/themes'

const themeStore = useThemeStore()
const op = ref<InstanceType<typeof Popover> | null>(null)

function toggle(e: Event) { op.value?.toggle(e) }
function select(id: ThemeId) {
  themeStore.setTheme(id)
  op.value?.hide()
}
</script>

<template>
  <Button
    icon="pi pi-palette"
    text
    size="small"
    class="theme-btn"
    @click="toggle"
    v-tooltip="'Change theme'"
  />
  <Popover ref="op" class="theme-panel">
    <div class="theme-title">Theme</div>
    <ul class="theme-list">
      <li
        v-for="t in THEMES"
        :key="t.id"
        class="theme-item"
        :class="{ active: themeStore.activeTheme === t.id }"
        @click="select(t.id)"
      >
        <span class="theme-swatch" :style="{ background: t.preview }" />
        <span class="theme-label">{{ t.label }}</span>
        <i v-if="themeStore.activeTheme === t.id" class="pi pi-check theme-check" />
      </li>
    </ul>
  </Popover>
</template>

<style scoped>
.theme-panel { min-width: 210px; }
.theme-title {
  font-size: 11px; font-weight: 600; text-transform: uppercase;
  letter-spacing: .06em; color: var(--text-muted); padding: 4px 0 8px;
}
.theme-list { list-style: none; margin: 0; padding: 0; }
.theme-item {
  display: flex; align-items: center; gap: 10px;
  padding: 7px 8px; border-radius: var(--border-radius-sm);
  cursor: pointer; font-size: 13px; color: var(--text-primary);
  transition: background var(--transition-fast);
}
.theme-item:hover { background: var(--nav-item-hover); }
.theme-item.active { background: var(--nav-item-active); font-weight: 500; }
.theme-swatch {
  width: 18px; height: 18px; border-radius: 50%;
  border: 2px solid var(--border); flex-shrink: 0;
}
.theme-label { flex: 1; }
.theme-check { color: var(--accent); font-size: 12px; }
</style>
