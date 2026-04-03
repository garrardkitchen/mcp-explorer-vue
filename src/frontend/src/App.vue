<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import { useRoute } from 'vue-router'
import Toast from 'primevue/toast'
import ConfirmDialog from 'primevue/confirmdialog'
import { useThemeStore } from '@/stores/themes'
import { useConnectionsStore } from '@/stores/connections'
import CommandPalette from '@/components/common/CommandPalette.vue'
import ThemeSwitcher from '@/components/common/ThemeSwitcher.vue'

const route = useRoute()
useThemeStore()  // ensure theme is initialised
const connectionsStore = useConnectionsStore()

const sidebarCollapsed = ref(false)
const showCommandPalette = ref(false)

const navItems = [
  { name: 'connections',       label: 'Connections',  icon: 'pi-server'       },
  { name: 'tools',             label: 'Tools',        icon: 'pi-wrench'       },
  { name: 'prompts',           label: 'Prompts',      icon: 'pi-file-edit'    },
  { name: 'resources',         label: 'Resources',    icon: 'pi-database'     },
  { name: 'resource-templates',label: 'Templates',    icon: 'pi-copy'         },
  { name: 'chat',              label: 'Chat',         icon: 'pi-comments'     },
  { name: 'workflows',         label: 'Workflows',    icon: 'pi-sitemap'      },
  { name: 'ai-models',         label: 'AI Models',    icon: 'pi-microchip-ai' },
  { name: 'sensitive-fields',  label: 'Sensitive',    icon: 'pi-shield'       },
  { name: 'elicitations',      label: 'Elicitations', icon: 'pi-bell'         },
]

const isActive = (name: string) => route.name === name

function handleKeydown(e: KeyboardEvent) {
  if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
    e.preventDefault()
    showCommandPalette.value = !showCommandPalette.value
  }
  if (e.key === 'Escape') showCommandPalette.value = false
}

onMounted(async () => {
  window.addEventListener('keydown', handleKeydown)
  await Promise.all([connectionsStore.loadSaved(), connectionsStore.loadActive()])
  connectionsStore.initialized = true
})

onUnmounted(() => {
  window.removeEventListener('keydown', handleKeydown)
})
</script>

<template>
  <div class="app-shell">
    <!-- Top bar -->
    <header class="topbar">
      <div class="topbar-left">
        <button class="sidebar-toggle" @click="sidebarCollapsed = !sidebarCollapsed"
          v-tooltip="sidebarCollapsed ? 'Expand sidebar' : 'Collapse sidebar'">
          <i class="pi pi-bars" />
        </button>
        <span class="brand">
          <i class="pi pi-bolt brand-icon" />
          <span v-if="!sidebarCollapsed" class="brand-text">MCP Explorer</span>
        </span>
      </div>

      <div class="topbar-right">
        <!-- Command palette trigger -->
        <button class="topbar-btn" @click="showCommandPalette = true"
          v-tooltip="'Command palette (Ctrl+K)'">
          <i class="pi pi-search" />
          <kbd class="kbd">⌘K</kbd>
        </button>

        <!-- Theme switcher -->
        <ThemeSwitcher />

        <!-- Active connections badge -->
        <span v-if="connectionsStore.activeConnections.length" class="conn-badge"
          v-tooltip="`${connectionsStore.activeConnections.length} active connection(s)`">
          <i class="pi pi-circle-fill status-dot" />
          {{ connectionsStore.activeConnections.length }}
        </span>
      </div>
    </header>

    <!-- Body: sidebar + main -->
    <div class="app-body">
      <!-- Sidebar nav -->
      <nav class="sidebar" :class="{ collapsed: sidebarCollapsed }">
        <ul class="nav-list">
          <li v-for="item in navItems" :key="item.name">
            <RouterLink
              :to="{ name: item.name }"
              class="nav-item"
              :class="{ active: isActive(item.name) }"
              v-tooltip.right="sidebarCollapsed ? item.label : ''"
            >
              <i :class="`pi ${item.icon} nav-icon`" />
              <span v-if="!sidebarCollapsed" class="nav-label">{{ item.label }}</span>
            </RouterLink>
          </li>
        </ul>
      </nav>

      <!-- Main content area -->
      <main class="main-content">
        <RouterView v-slot="{ Component }">
          <Transition name="page" mode="out-in">
            <component :is="Component" />
          </Transition>
        </RouterView>
      </main>
    </div>

    <!-- Global overlays -->
    <Toast position="bottom-right" />
    <ConfirmDialog />
    <CommandPalette v-if="showCommandPalette" @close="showCommandPalette = false" />
  </div>
</template>

<style scoped>
.app-shell {
  display: flex;
  flex-direction: column;
  height: 100dvh;
  overflow: hidden;
  background: var(--bg-base);
}

/* ── Topbar ── */
.topbar {
  height: var(--topbar-height);
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 12px;
  background: var(--nav-bg);
  border-bottom: 1px solid var(--border);
  flex-shrink: 0;
  z-index: 100;
}

.topbar-left,
.topbar-right {
  display: flex;
  align-items: center;
  gap: 8px;
}

.sidebar-toggle,
.topbar-btn {
  display: flex;
  align-items: center;
  gap: 6px;
  background: transparent;
  border: none;
  color: var(--text-secondary);
  cursor: pointer;
  padding: 6px 8px;
  border-radius: var(--border-radius-sm);
  transition: background var(--transition-fast), color var(--transition-fast);
  font-size: 14px;
}
.sidebar-toggle:hover,
.topbar-btn:hover {
  background: var(--nav-item-hover);
  color: var(--text-primary);
}

.brand { display: flex; align-items: center; gap: 6px; }
.brand-icon { color: var(--accent); font-size: 18px; }
.brand-text { font-weight: 600; font-size: 15px; color: var(--text-primary); letter-spacing: -0.01em; }

.kbd {
  font-family: var(--font-family-mono);
  font-size: 10px;
  background: var(--bg-raised);
  border: 1px solid var(--border);
  border-radius: 3px;
  padding: 1px 4px;
  color: var(--text-muted);
}

.conn-badge {
  display: flex;
  align-items: center;
  gap: 4px;
  background: var(--accent-muted);
  color: var(--accent);
  border-radius: 12px;
  padding: 2px 10px;
  font-size: 12px;
  font-weight: 600;
  cursor: default;
}
.status-dot { font-size: 8px; color: var(--success); }

/* ── Body ── */
.app-body {
  display: flex;
  flex: 1;
  overflow: hidden;
}

/* ── Sidebar ── */
.sidebar {
  width: var(--sidebar-width);
  flex-shrink: 0;
  background: var(--nav-bg);
  border-right: 1px solid var(--border);
  overflow-y: auto;
  overflow-x: hidden;
  transition: width 200ms ease;
  display: flex;
  flex-direction: column;
}
.sidebar.collapsed { width: var(--sidebar-collapsed-width); }

.nav-list {
  list-style: none;
  margin: 0;
  padding: 8px 0;
}
.nav-item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 9px 16px;
  color: var(--text-secondary);
  text-decoration: none;
  border-radius: 0;
  border-left: 3px solid transparent;
  transition: background var(--transition-fast), color var(--transition-fast), border-color var(--transition-fast);
  font-size: 13px;
  white-space: nowrap;
  overflow: hidden;
}
.nav-item:hover {
  background: var(--nav-item-hover);
  color: var(--text-primary);
}
.nav-item.active {
  background: var(--nav-item-active);
  color: var(--text-primary);
  border-left-color: var(--nav-item-active-border);
  font-weight: 500;
}
.nav-icon { width: 16px; font-size: 15px; flex-shrink: 0; }
.nav-label { overflow: hidden; text-overflow: ellipsis; }

/* ── Main content ── */
.main-content {
  flex: 1;
  overflow: hidden;
  display: flex;
  flex-direction: column;
  background: var(--bg-base);
}

/* ── Page transitions ── */
.page-enter-active,
.page-leave-active { transition: opacity 150ms ease, transform 150ms ease; }
.page-enter-from   { opacity: 0; transform: translateY(6px); }
.page-leave-to     { opacity: 0; transform: translateY(-4px); }
</style>
