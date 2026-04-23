<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useToast } from 'primevue/usetoast'
import { useConfirm } from 'primevue/useconfirm'
import Button from 'primevue/button'
import Card from 'primevue/card'
import Checkbox from 'primevue/checkbox'
import Dialog from 'primevue/dialog'
import Message from 'primevue/message'
import InputText from 'primevue/inputtext'
import Select from 'primevue/select'
import ConfirmDialog from 'primevue/confirmdialog'
import { extractApiError } from '@/api/client'
import { devTunnelsApi } from '@/api/devtunnels'
import type { TunnelAccess } from '@/api/types'
import { buildDevTunnelSampleCurlCommand, copyTextToClipboard } from '@/composables/devTunnelsClipboard'
import { useDevTunnelsStore } from '@/stores/devTunnels'
import TrafficSparkline from '@/components/devtunnels/TrafficSparkline.vue'
import TunnelCard from '@/components/devtunnels/TunnelCard.vue'
import DeviceCodeLoginDialog from '@/components/devtunnels/DeviceCodeLoginDialog.vue'

const toast = useToast()
const confirm = useConfirm()
const router = useRouter()
const route = useRoute()
const store = useDevTunnelsStore()

const showCreateDialog = ref(false)
const createName = ref('')
const createAccess = ref<TunnelAccess>('Anonymous')
const deleteOnExit = ref(false)
const saving = ref(false)

const showLoginDialog = ref(false)
const loginStreaming = ref(false)
const loginLines = ref<string[]>([])
type ConnectPhase = 'idle' | 'connecting' | 'connected' | 'starting-tunnel'
const connectPhase = ref<ConnectPhase>('idle')
const pendingStartId = ref<string | null>(null)
let loginStream: EventSource | null = null
let loginFlowToken = 0

const accessOptions = [
  { label: 'Anonymous', value: 'Anonymous' },
  { label: 'Authenticated', value: 'Authenticated' },
]

const liveShareRatio = computed(() => `${store.runningCount}/${store.tunnels.length || 0}`)
const cliUnavailable = computed(() => store.userState?.isAvailable === false)
const cliSummary = computed(() => {
  if (cliUnavailable.value) return 'CLI unavailable'
  return store.userState?.isLoggedIn ? store.userState.userName ?? 'Connected' : 'Not connected'
})
const cliDetail = computed(() => {
  if (cliUnavailable.value) return store.userState?.detail ?? 'Install or configure the devtunnel CLI.'
  return store.userState?.provider ?? 'Click Start on a tunnel to connect'
})

const totalCapturedEvents = computed(() => store.totalCapturedEvents)
const unseenEvents = computed(() => store.unseenTotalCount)
const notificationModeLabel = computed(() => store.notificationPaused ? 'Paused' : 'Live')
const busiestTunnel = computed(() => {
  const ranked = store.tunnels
    .map(tunnel => ({ tunnel, count: store.getEventCountForTunnel(tunnel.id) }))
    .sort((left, right) => right.count - left.count)
  return ranked[0]?.count ? ranked[0].tunnel.name : 'No traffic yet'
})

async function refreshDashboard() {
  await store.loadAll()
  await store.ensureEventData(undefined, 1000, true)
}

function openCreate() {
  if (cliUnavailable.value) {
    toast.add({ severity: 'warn', summary: 'CLI unavailable', detail: cliDetail.value, life: 6000 })
    return
  }
  createName.value = ''
  createAccess.value = 'Anonymous'
  deleteOnExit.value = false
  showCreateDialog.value = true
}

async function createTunnel() {
  if (!createName.value.trim()) {
    toast.add({ severity: 'warn', summary: 'Name required', detail: 'Give the tunnel a memorable name.', life: 3000 })
    return
  }

  saving.value = true
  try {
    const tunnel = await store.createTunnel({
      name: createName.value.trim(),
      access: createAccess.value,
      deleteOnExit: deleteOnExit.value,
    })
    await store.ensureEventData([tunnel.id])
    showCreateDialog.value = false
    toast.add({ severity: 'success', summary: 'Tunnel created', detail: `${tunnel.name} is ready.`, life: 3000 })
    await router.push({ name: 'dev-tunnel-inspector', params: { id: tunnel.id } })
  } catch (error) {
    toast.add({ severity: 'error', summary: 'Create failed', detail: extractApiError(error), life: 5000 })
  } finally {
    saving.value = false
  }
}

async function copyWebhook(url: string) {
  const ok = await copyTextToClipboard(url)
  if (ok) {
    toast.add({ severity: 'success', summary: 'Copied', detail: 'Webhook URL copied to your clipboard.', life: 2500 })
  } else {
    toast.add({ severity: 'warn', summary: 'Copy failed', detail: 'Clipboard blocked — URL printed to console.', life: 4000 })
    console.info('[dev-tunnels] Webhook URL:', url)
  }
}

async function copySampleCurl(url: string) {
  const command = buildDevTunnelSampleCurlCommand(url)
  const ok = await copyTextToClipboard(command)
  if (ok) {
    toast.add({ severity: 'success', summary: 'Copied', detail: 'Sample curl command copied to your clipboard.', life: 3000 })
  } else {
    toast.add({ severity: 'warn', summary: 'Copy failed', detail: 'Clipboard blocked — curl command printed to console.', life: 4000 })
    console.info('[dev-tunnels] Sample curl command:\n%s', command)
  }
}

async function copyTunnel(url: string) {
  const ok = await copyTextToClipboard(url)
  if (ok) {
    toast.add({ severity: 'success', summary: 'Copied', detail: 'Tunnel URL copied to your clipboard.', life: 2500 })
  } else {
    toast.add({ severity: 'warn', summary: 'Copy failed', detail: 'Clipboard blocked — URL printed to console.', life: 4000 })
    console.info('[dev-tunnels] Tunnel URL:', url)
  }
}

async function startTunnel(id: string) {
  try {
    const tunnel = await store.startTunnel(id)
    if (tunnel.status === 'LoginRequired') {
      pendingStartId.value = id
      openConnectDialog()
      return
    }
    toast.add({
      severity: 'success',
      summary: 'Tunnel starting',
      detail: `${tunnel.name} is coming online.`,
      life: 4000,
    })
  } catch (error) {
    toast.add({ severity: 'error', summary: 'Start failed', detail: extractApiError(error), life: 5000 })
  }
}

async function stopTunnel(id: string) {
  try {
    const tunnel = await store.stopTunnel(id)
    toast.add({ severity: 'success', summary: 'Tunnel stopped', detail: `${tunnel?.name ?? 'Tunnel'} is now idle.`, life: 3000 })
  } catch (error) {
    toast.add({ severity: 'error', summary: 'Stop failed', detail: extractApiError(error), life: 5000 })
  }
}

function deleteTunnel(id: string) {
  const tunnel = store.tunnels.find(t => t.id === id)
  if (!tunnel) return

  confirm.require({
    message: `Delete ${tunnel.name}? This removes the saved tunnel definition from MCP Explorer.`,
    header: 'Delete tunnel',
    icon: 'pi pi-trash',
    rejectProps: { label: 'Cancel', severity: 'secondary', outlined: true },
    acceptProps: { label: 'Delete', severity: 'danger' },
    accept: async () => {
      try {
        await store.deleteTunnel(id)
        toast.add({ severity: 'success', summary: 'Tunnel deleted', life: 2500 })
      } catch (error) {
        toast.add({ severity: 'error', summary: 'Delete failed', detail: extractApiError(error), life: 5000 })
      }
    },
  })
}

function openConnectDialog() {
  if (cliUnavailable.value) {
    toast.add({ severity: 'warn', summary: 'CLI unavailable', detail: cliDetail.value, life: 6000 })
    return
  }
  loginLines.value = []
  connectPhase.value = 'idle'
  showLoginDialog.value = true
}

function teardownLoginFlow(options?: { clearPendingStart?: boolean }) {
  loginFlowToken += 1
  loginStream?.close()
  loginStream = null
  loginStreaming.value = false
  loginLines.value = []
  connectPhase.value = 'idle'
  if (options?.clearPendingStart ?? true)
    pendingStartId.value = null
}

function startLogin() {
  teardownLoginFlow({ clearPendingStart: false })
  const flowToken = loginFlowToken
  loginLines.value = []
  loginStreaming.value = true
  connectPhase.value = 'connecting'

  loginStream = devTunnelsApi.createLoginStream(
    line => {
      if (flowToken !== loginFlowToken)
        return
      loginLines.value = [...loginLines.value, line]
    },
    async () => {
      try {
        if (flowToken !== loginFlowToken)
          return

        loginStreaming.value = false
        connectPhase.value = 'connected'
        await refreshDashboard()
        if (flowToken !== loginFlowToken)
          return

        toast.add({ severity: 'success', summary: 'Connected', detail: 'Dev Tunnels workspace is ready.', life: 3000 })

        if (pendingStartId.value) {
          const id = pendingStartId.value
          if (flowToken !== loginFlowToken)
            return

          connectPhase.value = 'starting-tunnel'
          const tunnel = await store.startTunnel(id)
          if (flowToken !== loginFlowToken) {
            try {
              await store.stopTunnel(id)
            } catch (stopError) {
              toast.add({
                severity: 'warn',
                summary: 'Tunnel cleanup failed',
                detail: extractApiError(stopError),
                life: 5000,
              })
            }
            return
          }

          pendingStartId.value = null
          toast.add({ severity: 'success', summary: 'Tunnel starting', detail: `${tunnel.name} is coming online.`, life: 4000 })
          showLoginDialog.value = false
          connectPhase.value = 'idle'
          loginStream?.close()
          loginStream = null
          await router.push({ name: 'dev-tunnel-inspector', params: { id } })
          return
        }

        loginStream?.close()
        loginStream = null
      } catch (error) {
        if (flowToken !== loginFlowToken)
          return

        teardownLoginFlow({ clearPendingStart: false })
        showLoginDialog.value = false
        toast.add({ severity: 'error', summary: 'Connect failed', detail: extractApiError(error), life: 5000 })
      }
    },
    () => {
      if (flowToken !== loginFlowToken)
        return

      teardownLoginFlow({ clearPendingStart: false })
    },
  )
}

watch(showLoginDialog, (visible) => {
  if (!visible)
    teardownLoginFlow()
})

onMounted(async () => {
  await refreshDashboard()

  const loginParam = route.query.login
  const targetId = Array.isArray(loginParam) ? loginParam[0] : loginParam
  if (targetId && typeof targetId === 'string' && !store.userState?.isLoggedIn) {
    pendingStartId.value = targetId
    openConnectDialog()
    await router.replace({ name: 'dev-tunnels' })
  }
})

onUnmounted(() => {
  teardownLoginFlow()
})
</script>

<template>
  <div class="devtunnels-view">
    <section class="hero-shell">
      <div class="hero-copy">
        <div class="hero-kicker">Signal Deck</div>
        <h1>Dev Tunnels</h1>
        <p>
          A live control room for webhook ingress: create public callbacks, confirm schedules at a glance,
          and jump from traffic patterns into frame-level inspection without leaving the deck.
        </p>
        <div class="hero-actions">
          <Button label="Refresh deck" icon="pi pi-refresh" severity="secondary" outlined @click="refreshDashboard" />
          <Button label="Create tunnel" icon="pi pi-plus" :disabled="cliUnavailable" @click="openCreate" />
        </div>
      </div>

      <div class="hero-summary">
        <div class="summary-grid">
          <Card class="summary-card">
            <template #content>
              <span class="summary-label">Captured events</span>
              <strong>{{ totalCapturedEvents }}</strong>
              <small>Across the retained tunnel history</small>
            </template>
          </Card>
          <Card class="summary-card">
            <template #content>
              <span class="summary-label">Live tunnels</span>
              <strong>{{ store.runningCount }}</strong>
              <small>{{ liveShareRatio }} online right now</small>
            </template>
          </Card>
          <Card class="summary-card">
            <template #content>
              <span class="summary-label">Unseen while away</span>
              <strong>{{ unseenEvents }}</strong>
              <small>Tracked by the global event toast stream</small>
            </template>
          </Card>
          <Card class="summary-card">
            <template #content>
              <span class="summary-label">Tunnel toasts</span>
              <strong>{{ notificationModeLabel }}</strong>
              <small>Pause or resume from the top bar</small>
            </template>
          </Card>
        </div>

        <Card class="pulse-card">
          <template #content>
            <div class="pulse-header">
              <div>
                <span class="summary-label">Overall event pulse</span>
                <strong class="pulse-title">{{ busiestTunnel }}</strong>
              </div>
              <small>{{ cliSummary }}</small>
            </div>
            <TrafficSparkline :bars="store.overallHistogram" variant="hero" empty-label="No traffic captured yet" />
            <p class="pulse-detail">{{ cliDetail }}</p>
          </template>
        </Card>
      </div>
    </section>

    <Message v-if="cliUnavailable" severity="warn" :closable="false" class="login-banner">
      <div class="login-banner-copy">
        <strong>The devtunnel CLI is unavailable.</strong>
        <p>{{ cliDetail }}</p>
      </div>
    </Message>

    <section v-if="store.tunnels.length" class="tunnel-grid">
      <TunnelCard
        v-for="tunnel in store.tunnels"
        :key="tunnel.id"
        :tunnel="tunnel"
        :stream-state="store.streamStateByTunnel[tunnel.id] ?? 'disconnected'"
        :last-event="store.getLastEventForTunnel(tunnel.id)"
        :event-count="store.getEventCountForTunnel(tunnel.id)"
        :unseen-count="store.unseenCountByTunnel[tunnel.id] ?? 0"
        :histogram-bars="store.getHistogramForTunnel(tunnel.id)"
        @inspect="router.push({ name: 'dev-tunnel-inspector', params: { id: $event } })"
        @start="startTunnel"
        @stop="stopTunnel"
        @delete="deleteTunnel"
        @copy-webhook="copyWebhook"
        @copy-sample-curl="copySampleCurl"
        @copy-tunnel="copyTunnel"
      />
    </section>

    <section v-else class="empty-state">
      <Card class="empty-frame">
        <template #content>
          <i class="pi pi-send" />
          <h3>No dev tunnels yet</h3>
          <p>Create your first public callback tunnel and start capturing webhook traffic in real time.</p>
          <Button label="Create the first tunnel" icon="pi pi-plus" @click="openCreate" />
        </template>
      </Card>
    </section>

    <Dialog v-model:visible="showCreateDialog" modal header="Create Dev Tunnel" :style="{ width: 'min(92vw, 34rem)' }">
      <div class="create-form">
        <label class="field">
          <span>Name</span>
          <InputText v-model="createName" placeholder="Stripe payouts, Batch callbacks, Agent hooks…" />
        </label>
        <label class="field">
          <span>Access</span>
          <Select v-model="createAccess" :options="accessOptions" optionLabel="label" optionValue="value" />
        </label>
        <label class="checkbox-row">
          <Checkbox v-model="deleteOnExit" :binary="true" />
          <span>Delete from Dev Tunnels when the app exits</span>
        </label>
      </div>
      <template #footer>
        <Button label="Cancel" text @click="showCreateDialog = false" />
        <Button label="Create tunnel" icon="pi pi-plus" :loading="saving" @click="createTunnel" />
      </template>
    </Dialog>

    <DeviceCodeLoginDialog
      v-model:visible="showLoginDialog"
      :lines="loginLines"
      :loading="loginStreaming"
      :user-state="store.userState"
      :phase="connectPhase"
      @start="startLogin"
    />
    <ConfirmDialog />
  </div>
</template>

<style scoped>
.devtunnels-view {
  height: 100%;
  overflow: auto;
  padding: 1.4rem;
  display: grid;
  gap: 1.25rem;
  background:
    radial-gradient(circle at top left, color-mix(in srgb, var(--info) 12%, transparent), transparent 24%),
    radial-gradient(circle at top right, color-mix(in srgb, var(--warning) 8%, transparent), transparent 24%),
    linear-gradient(180deg, color-mix(in srgb, var(--bg-base) 97%, #02070d 3%), var(--bg-base));
}

.hero-shell,
.login-banner,
.empty-frame {
  border-radius: 1.5rem;
  border: 1px solid color-mix(in srgb, var(--border) 70%, transparent);
  background:
    linear-gradient(135deg, color-mix(in srgb, var(--bg-surface) 92%, #08131d 8%), color-mix(in srgb, var(--bg-raised) 92%, #111f2e 8%));
  box-shadow: 0 24px 90px rgba(0, 0, 0, 0.16);
}

.hero-shell {
  display: grid;
  grid-template-columns: minmax(0, 1.12fr) minmax(24rem, 0.88fr);
  gap: 1rem;
  padding: 1.5rem;
}

.hero-kicker,
.summary-label,
.field span {
  font-size: 0.7rem;
  letter-spacing: 0.22em;
  text-transform: uppercase;
  color: color-mix(in srgb, var(--info) 74%, var(--text-secondary));
}

.hero-copy h1,
.empty-frame h3 {
  margin: 0.45rem 0 0;
  font-family: 'Avenir Next Condensed', 'Arial Narrow', 'Franklin Gothic Medium', sans-serif;
  color: var(--text-primary);
  text-transform: uppercase;
  letter-spacing: 0.03em;
  line-height: 0.9;
}

.hero-copy h1 {
  font-size: clamp(3rem, 5vw, 4.8rem);
}

.hero-copy p,
.empty-frame p,
.login-banner p,
.pulse-detail {
  margin: 0.85rem 0 0;
  color: var(--text-secondary);
  line-height: 1.7;
  max-width: 46rem;
}

.hero-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 0.7rem;
  margin-top: 1.15rem;
}

.hero-summary {
  display: grid;
  gap: 1rem;
}

.summary-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(11rem, 1fr));
  gap: 0.8rem;
}

.summary-card,
.pulse-card {
  border-radius: 1.15rem;
  border: 1px solid color-mix(in srgb, var(--border) 66%, transparent);
  background: color-mix(in srgb, var(--bg-base) 56%, transparent);
}

.summary-card :deep(.p-card-body),
.pulse-card :deep(.p-card-body),
.empty-frame :deep(.p-card-body) {
  padding: 1rem;
}

.summary-card :deep(.p-card-content),
.pulse-card :deep(.p-card-content),
.empty-frame :deep(.p-card-content) {
  padding: 0;
}

.summary-card strong,
.pulse-title {
  display: block;
  margin-top: 0.5rem;
  color: var(--text-primary);
}

.summary-card strong {
  font-size: 1.9rem;
  line-height: 0.95;
}

.summary-card small,
.pulse-card small {
  color: var(--text-muted);
}

.pulse-header {
  display: flex;
  justify-content: space-between;
  gap: 1rem;
  align-items: flex-start;
}

.pulse-title {
  font-family: 'Avenir Next Condensed', 'Arial Narrow', 'Franklin Gothic Medium', sans-serif;
  font-size: 2rem;
  letter-spacing: 0.03em;
  line-height: 0.95;
  text-transform: uppercase;
}

.login-banner {
  border-radius: 1.5rem;
}

.login-banner-copy {
  display: grid;
  gap: 0.4rem;
}

.login-banner strong {
  color: var(--text-primary);
}

.tunnel-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(min(100%, 28rem), 1fr));
  gap: 1rem;
}

.empty-state {
  min-height: 22rem;
  display: grid;
  place-items: center;
}

.empty-frame {
  text-align: center;
  max-width: 34rem;
}

.empty-frame :deep(.p-card-body) {
  padding: 2rem;
}

.empty-frame i {
  font-size: 2.4rem;
  color: var(--info);
}

.create-form {
  display: grid;
  gap: 0.95rem;
}

.field {
  display: grid;
  gap: 0.45rem;
}

.checkbox-row {
  display: flex;
  align-items: center;
  gap: 0.7rem;
  color: var(--text-secondary);
}

@media (max-width: 1320px) {
  .hero-shell {
    grid-template-columns: 1fr;
  }

  .tunnel-grid {
    grid-template-columns: repeat(auto-fit, minmax(min(100%, 24rem), 1fr));
  }
}

@media (max-width: 980px) {
  .login-banner {
    display: grid;
    align-items: start;
  }
}
</style>
