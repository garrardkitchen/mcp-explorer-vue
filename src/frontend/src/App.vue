<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import Toast from 'primevue/toast'
import ConfirmDialog from 'primevue/confirmdialog'
import { useToast } from 'primevue/usetoast'
import { useThemeStore } from '@/stores/themes'
import { useConnectionsStore } from '@/stores/connections'
import { useDevTunnelsStore } from '@/stores/devTunnels'
import CommandPalette from '@/components/common/CommandPalette.vue'
import ThemeSwitcher from '@/components/common/ThemeSwitcher.vue'
import { systemApi } from '@/api/system'

const route = useRoute()
const toast = useToast()
useThemeStore()  // ensure theme is initialised
const connectionsStore = useConnectionsStore()
const devTunnelsStore = useDevTunnelsStore()

const sidebarCollapsed = ref(false)
const showCommandPalette = ref(false)

// Version info — fetched once on mount
const apiVersion = ref<string | null>(null)
const dotnetVersion = ref<string | null>(null)
const frontendVersion = __APP_VERSION__
const tunnelWorkspaceVisibility = computed(() => ({
  dashboardVisible: route.name === 'dev-tunnels',
  inspectorTunnelId: route.name === 'dev-tunnel-inspector' ? String(route.params.id ?? '') : null,
}))

const navGroups = [
  {
    label: 'Infrastructure',
    accent: 'blue',
    items: [
      { name: 'connections', label: 'Connections', icon: 'pi-server' },
    ],
  },
  {
    label: 'Dev Tunnels',
    accent: 'cyan',
    items: [
      { name: 'dev-tunnels', label: 'Dev Tunnels', icon: 'pi-globe' },
    ],
  },
  {
    label: 'Configuration',
    accent: 'amber',
    items: [
      { name: 'ai-models',        label: 'AI Models',  icon: 'pi-microchip-ai' },
      { name: 'sensitive-fields', label: 'Data Guard', icon: 'pi-shield'       },
    ],
  },
  {
    label: 'MCP Explorer',
    accent: 'teal',
    items: [
      { name: 'tools',              label: 'Tools',        icon: 'pi-wrench'    },
      { name: 'prompts',            label: 'Prompts',      icon: 'pi-file-edit' },
      { name: 'resources',          label: 'Resources',    icon: 'pi-database'  },
      { name: 'resource-templates', label: 'Templates',    icon: 'pi-copy'      },
      { name: 'elicitations',       label: 'Elicitations', icon: 'pi-bell'      },
    ],
  },
  {
    label: 'Testing',
    accent: 'violet',
    items: [
      { name: 'workflows', label: 'Workflows', icon: 'pi-sitemap'  },
      { name: 'chat',      label: 'Chat',      icon: 'pi-comments' },
    ],
  },
]

const isActive = (name: string) => route.name === name

function handleKeydown(e: KeyboardEvent) {
  if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
    e.preventDefault()
    showCommandPalette.value = !showCommandPalette.value
  }
  if (e.key === 'Escape') showCommandPalette.value = false
}

watch(tunnelWorkspaceVisibility, (visibility) => {
  devTunnelsStore.setWorkspaceVisibility(visibility)
}, { immediate: true })

watch(() => devTunnelsStore.pendingNotifications.length, () => {
  const notifications = devTunnelsStore.consumePendingNotifications()
  for (const notification of notifications) {
    toast.add({
      severity: 'info',
      summary: `${notification.tunnelName} · ${notification.method}`,
      detail: notification.path,
      life: 4200,
    })
  }
})

onMounted(async () => {
  window.addEventListener('keydown', handleKeydown)
  await Promise.all([
    connectionsStore.loadSaved(),
    connectionsStore.loadActive(),
    systemApi.getInfo().then((info) => {
      apiVersion.value = info.apiVersion
      dotnetVersion.value = info.dotnetVersion
    }).catch(() => { /* non-fatal */ }),
  ])
  connectionsStore.initialized = true
  void devTunnelsStore.loadAll().catch(() => undefined)
})

onUnmounted(() => {
  window.removeEventListener('keydown', handleKeydown)
  devTunnelsStore.disconnectAllStreams()
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

      <div class="topbar-center">
        <!-- Version pills -->
        <span
          v-if="dotnetVersion"
          class="version-pill dotnet-pill"
          v-tooltip="`Runtime: ${dotnetVersion}`"
        >
          <i class="pi pi-server" />
          {{ dotnetVersion.replace('Microsoft .NET', '.NET') }}
        </span>
        <span class="version-pill fe-pill" v-tooltip="`Frontend v${frontendVersion}`">
          <i class="pi pi-desktop" />
          UI v{{ frontendVersion }}
        </span>
        <span
          v-if="apiVersion"
          class="version-pill api-pill"
          v-tooltip="`API v${apiVersion}`"
        >
          <i class="pi pi-cloud" />
          API v{{ apiVersion }}
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
        <span
          v-if="devTunnelsStore.runningCount"
          class="conn-badge tunnel-badge"
          v-tooltip="`${devTunnelsStore.runningCount} live dev tunnel(s)`"
        >
          <i class="pi pi-send tunnel-dot" />
          {{ devTunnelsStore.runningCount }}
        </span>
        <span
          v-if="devTunnelsStore.unseenTotalCount"
          class="conn-badge tunnel-unseen-badge"
          v-tooltip="`${devTunnelsStore.unseenTotalCount} tunnel event(s) arrived while you were away`"
        >
          <i class="pi pi-wave-pulse tunnel-dot" />
          {{ devTunnelsStore.unseenTotalCount }}
        </span>
        <button
          class="topbar-btn tunnel-notifications-btn"
          :class="{ paused: devTunnelsStore.notificationPaused }"
          :aria-pressed="devTunnelsStore.notificationPaused"
          @click="devTunnelsStore.toggleNotificationPaused()"
          v-tooltip="devTunnelsStore.notificationPaused ? 'Resume dev tunnel toasts' : 'Pause dev tunnel toasts'"
        >
          <i :class="`pi ${devTunnelsStore.notificationPaused ? 'pi-bell-slash' : 'pi-bell'}`" />
          <span class="tunnel-notification-label">
            {{ devTunnelsStore.notificationPaused ? 'Tunnel toasts paused' : 'Tunnel toasts live' }}
          </span>
        </button>

        <!-- Author credit -->
        <a
          class="author-credit"
          href="mailto:garrardkitchen@gmail.com"
          v-tooltip="'garrardkitchen@gmail.com'"
          title="garrardkitchen@gmail.com"
        >
          <i class="pi pi-user author-icon" />
          <span class="author-by">by</span>
          <span class="author-name">Garrard Kitchen</span>
        </a>
      </div>
    </header>

    <!-- Body: sidebar + main -->
    <div class="app-body">
      <!-- Sidebar nav -->
      <nav class="sidebar" :class="{ collapsed: sidebarCollapsed }" aria-label="Main navigation">
        <ul class="nav-list">
          <template v-for="(group, gi) in navGroups" :key="group.label">
            <!-- Separator between groups (not before first) -->
            <li v-if="gi > 0" class="nav-separator" aria-hidden="true" />

            <!-- Group header (hidden when collapsed) -->
            <li v-if="!sidebarCollapsed" class="nav-group-header" :class="`accent-${group.accent}`">
              <span class="nav-group-dot" />
              <span class="nav-group-label">{{ group.label }}</span>
            </li>

            <!-- Nav items -->
            <li v-for="item in group.items" :key="item.name">
              <RouterLink
                :to="{ name: item.name }"
                class="nav-item"
                :class="{ active: isActive(item.name), [`accent-${group.accent}`]: isActive(item.name) }"
                v-tooltip.right="sidebarCollapsed ? item.label : ''"
              >
                <i :class="`pi ${item.icon} nav-icon`" />
                <span v-if="!sidebarCollapsed" class="nav-label">{{ item.label }}</span>
              </RouterLink>
            </li>
          </template>
        </ul>
      </nav>

      <!-- Main content area -->
      <main class="main-content">
        <div class="page-host">
          <div class="page-content">
            <RouterView v-slot="{ Component }">
              <Transition name="page">
                <component :is="Component" :key="route.name" />
              </Transition>
            </RouterView>
          </div>
        </div>
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
  gap: 12px;
}

.topbar-left,
.topbar-right {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-shrink: 0;
}

.topbar-center {
  display: flex;
  align-items: center;
  gap: 6px;
  flex: 1;
  justify-content: center;
  overflow: hidden;
}

.tunnel-badge {
  background: color-mix(in srgb, var(--info) 20%, transparent);
  color: var(--text-primary);
}

.tunnel-unseen-badge {
  background: color-mix(in srgb, var(--warning) 18%, transparent);
  color: var(--text-primary);
}

.tunnel-dot {
  color: var(--info);
}

/* Version pills */
.version-pill {
  display: flex;
  align-items: center;
  gap: 4px;
  font-size: 10px;
  font-weight: 500;
  padding: 2px 8px;
  border-radius: 10px;
  white-space: nowrap;
  cursor: default;
  font-family: var(--font-family-mono);
  letter-spacing: 0.02em;
  transition: opacity var(--transition-fast);
}
.version-pill:hover { opacity: 0.85; }
.version-pill .pi { font-size: 9px; }

.dotnet-pill {
  background: rgba(99, 102, 241, 0.12);
  color: #a5b4fc;
  border: 1px solid rgba(99, 102, 241, 0.3);
}
.fe-pill {
  background: rgba(56, 189, 248, 0.1);
  color: var(--accent);
  border: 1px solid rgba(56, 189, 248, 0.25);
}
.api-pill {
  background: rgba(52, 211, 153, 0.1);
  color: #6ee7b7;
  border: 1px solid rgba(52, 211, 153, 0.25);
}

/* Author credit */
.author-credit {
  display: flex;
  align-items: center;
  gap: 5px;
  padding: 4px 10px;
  border-radius: 20px;
  background: linear-gradient(135deg, rgba(139, 92, 246, 0.15), rgba(56, 189, 248, 0.12));
  border: 1px solid rgba(139, 92, 246, 0.35);
  text-decoration: none;
  cursor: pointer;
  white-space: nowrap;
  transition: background var(--transition-fast), border-color var(--transition-fast),
              box-shadow var(--transition-fast);
}
.author-credit:hover {
  background: linear-gradient(135deg, rgba(139, 92, 246, 0.25), rgba(56, 189, 248, 0.2));
  border-color: rgba(139, 92, 246, 0.6);
  box-shadow: 0 0 10px rgba(139, 92, 246, 0.25);
}
.author-icon {
  font-size: 11px;
  color: #a78bfa;
}
.author-by {
  font-size: 10px;
  color: var(--text-muted);
  font-style: italic;
  letter-spacing: 0.02em;
}
.author-name {
  font-size: 12px;
  font-weight: 600;
  background: linear-gradient(90deg, #a78bfa, #38bdf8);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  background-clip: text;
  letter-spacing: 0.01em;
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

.tunnel-notifications-btn {
  border: 1px solid color-mix(in srgb, var(--info) 24%, transparent);
  background: color-mix(in srgb, var(--info) 10%, transparent);
  color: var(--text-primary);
}

.tunnel-notifications-btn.paused {
  border-color: color-mix(in srgb, var(--warning) 26%, transparent);
  background: color-mix(in srgb, var(--warning) 10%, transparent);
  color: var(--warning);
}

.tunnel-notification-label {
  white-space: nowrap;
  font-size: 12px;
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

@media (max-width: 1260px) {
  .tunnel-notification-label {
    display: none;
  }
}

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
  padding: 6px 0 12px;
}

/* ── Group headers ── */
.nav-group-header {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 12px 16px 4px;
  font-size: 10px;
  font-weight: 700;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: var(--text-muted);
  user-select: none;
}
.nav-group-header:first-child { padding-top: 4px; }
.nav-group-dot {
  width: 5px;
  height: 5px;
  border-radius: 50%;
  flex-shrink: 0;
  background: currentColor;
}
.nav-group-label { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }

/* accent colours on group headers */
.nav-group-header.accent-blue   { color: var(--nav-accent-blue); opacity: 0.85; }
.nav-group-header.accent-cyan   { color: var(--info); opacity: 0.9; }
.nav-group-header.accent-amber  { color: var(--nav-accent-amber); opacity: 0.85; }
.nav-group-header.accent-teal   { color: var(--nav-accent-teal); opacity: 0.85; }
.nav-group-header.accent-violet { color: var(--nav-accent-violet); opacity: 0.85; }

/* ── Separators ── */
.nav-separator {
  height: 1px;
  background: var(--border);
  margin: 8px 12px;
  opacity: 0.5;
}

/* ── Nav items ── */
.nav-item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 9px 16px;
  color: var(--text-secondary);
  text-decoration: none;
  border-left: 2px solid transparent;
  transition: background var(--transition-fast), color var(--transition-fast), border-color var(--transition-fast);
  font-size: 13px;
  white-space: nowrap;
  overflow: hidden;
}
.nav-item:hover {
  background: var(--nav-item-hover);
  color: var(--text-primary);
}

/* Active state per accent group */
.nav-item.active { color: var(--text-primary); font-weight: 500; }

.nav-item.active.accent-blue {
  background: color-mix(in srgb, var(--nav-accent-blue) 13%, transparent);
  border-left-color: var(--nav-accent-blue);
}
.nav-item.active.accent-blue .nav-icon { color: var(--nav-accent-blue); }

.nav-item.active.accent-cyan {
  background: color-mix(in srgb, var(--info) 13%, transparent);
  border-left-color: var(--info);
}
.nav-item.active.accent-cyan .nav-icon { color: var(--info); }

.nav-item.active.accent-amber {
  background: color-mix(in srgb, var(--nav-accent-amber) 11%, transparent);
  border-left-color: var(--nav-accent-amber);
}
.nav-item.active.accent-amber .nav-icon { color: var(--nav-accent-amber); }

.nav-item.active.accent-teal {
  background: color-mix(in srgb, var(--nav-accent-teal) 11%, transparent);
  border-left-color: var(--nav-accent-teal);
}
.nav-item.active.accent-teal .nav-icon { color: var(--nav-accent-teal); }

.nav-item.active.accent-violet {
  background: color-mix(in srgb, var(--nav-accent-violet) 11%, transparent);
  border-left-color: var(--nav-accent-violet);
}
.nav-item.active.accent-violet .nav-icon { color: var(--nav-accent-violet); }

.nav-icon { width: 16px; font-size: 15px; flex-shrink: 0; }
.nav-label { overflow: hidden; text-overflow: ellipsis; }

/* ── Main content ── */
.main-content {
  flex: 1;
  min-width: 0;
  min-height: 0;
  overflow: hidden;
  display: flex;
  flex-direction: column;
  background: var(--bg-base);
}

.page-host {
  flex: 1 1 auto;
  min-width: 0;
  min-height: 0;
  display: flex;
  overflow: hidden;
}

.page-content {
  flex: 1 1 auto;
  min-width: 0;
  min-height: 0;
  display: flex;
}

.page-content > * {
  flex: 1 1 auto;
  min-width: 0;
  min-height: 0;
}

/* ── Page transitions ── */
.page-enter-active { transition: opacity 120ms ease; }
.page-leave-active { transition: opacity 80ms ease; position: absolute; width: 100%; pointer-events: none; }
.page-enter-from   { opacity: 0; }
.page-leave-to     { opacity: 0; }
</style>
