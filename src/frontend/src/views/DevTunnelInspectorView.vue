<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useToast } from 'primevue/usetoast'
import Button from 'primevue/button'
import Card from 'primevue/card'
import InputGroup from 'primevue/inputgroup'
import InputGroupAddon from 'primevue/inputgroupaddon'
import InputText from 'primevue/inputtext'
import Message from 'primevue/message'
import Tabs from 'primevue/tabs'
import Tab from 'primevue/tab'
import TabList from 'primevue/tablist'
import TabPanel from 'primevue/tabpanel'
import TabPanels from 'primevue/tabpanels'
import { extractApiError } from '@/api/client'
import type { ReplayWebhookResult, WebhookEvent } from '@/api/types'
import { devTunnelsApi } from '@/api/devtunnels'
import { buildDevTunnelSampleCurlCommand, copyTextToClipboard } from '@/composables/devTunnelsClipboard'
import { useDevTunnelsStore } from '@/stores/devTunnels'
import EventTape from '@/components/devtunnels/EventTape.vue'
import EventTimeline from '@/components/devtunnels/EventTimeline.vue'
import EventDetail from '@/components/devtunnels/EventDetail.vue'
import ReplayDialog from '@/components/devtunnels/ReplayDialog.vue'

const route = useRoute()
const router = useRouter()
const toast = useToast()
const store = useDevTunnelsStore()

const selectedEventId = ref<string | null>(null)
const playing = ref(false)
const replayVisible = ref(false)
const replayEvent = ref<WebhookEvent | null>(null)
const replayResult = ref<ReplayWebhookResult | null>(null)
const replayLoading = ref(false)
const activeTab = ref<'traffic' | 'archive'>('traffic')
let playbackTimer: number | null = null

const tunnelId = computed(() => String(route.params.id ?? ''))
const tunnel = computed(() => store.tunnels.find(item => item.id === tunnelId.value) ?? null)
const events = computed(() => store.getEventsForTunnel(tunnelId.value))
const streamState = computed(() => store.streamStateByTunnel[tunnelId.value] ?? 'disconnected')
const selectedIndex = computed(() => events.value.findIndex(event => event.id === selectedEventId.value))
const latestEvent = computed(() => events.value.length ? events.value[events.value.length - 1] : null)
const histogramBars = computed(() => store.getHistogramForTunnel(tunnelId.value, 24))

async function loadInspector() {
  await store.loadAll()
  if (!tunnelId.value) return

  try {
    await Promise.all([
      store.refreshTunnel(tunnelId.value),
      store.loadEvents(tunnelId.value, 1000),
    ])
    if (!selectedEventId.value && events.value.length)
      selectedEventId.value = events.value[events.value.length - 1].id
  } catch (error) {
    toast.add({ severity: 'error', summary: 'Load failed', detail: extractApiError(error), life: 5000 })
  }
}

function stopPlayback() {
  if (playbackTimer !== null) {
    window.clearInterval(playbackTimer)
    playbackTimer = null
  }
  playing.value = false
}

function togglePlayback() {
  if (playing.value) {
    stopPlayback()
    return
  }

  if (events.value.length < 2) return
  playing.value = true
  playbackTimer = window.setInterval(() => {
    const currentIndex = selectedIndex.value < 0 ? 0 : selectedIndex.value
    const next = events.value[currentIndex + 1]
    if (!next) {
      stopPlayback()
      return
    }
    selectedEventId.value = next.id
  }, 850)
}

async function copyWebhook() {
  if (!tunnel.value?.webhookUri) return
  const ok = await copyTextToClipboard(tunnel.value.webhookUri)
  toast.add({
    severity: ok ? 'success' : 'warn',
    summary: ok ? 'Copied' : 'Copy failed',
    detail: ok ? 'Webhook URL copied to clipboard.' : 'Clipboard blocked — URL printed to console.',
    life: 3000,
  })
  if (!ok)
    console.info('[dev-tunnels] Webhook URL:', tunnel.value.webhookUri)
}

async function copyTunnel() {
  if (!tunnel.value?.tunnelUri) return
  const ok = await copyTextToClipboard(tunnel.value.tunnelUri)
  toast.add({
    severity: ok ? 'success' : 'warn',
    summary: ok ? 'Copied' : 'Copy failed',
    detail: ok ? 'Tunnel URL copied to clipboard.' : 'Clipboard blocked — URL printed to console.',
    life: 3000,
  })
  if (!ok)
    console.info('[dev-tunnels] Tunnel URL:', tunnel.value.tunnelUri)
}

async function copyWebhookCurl() {
  if (!tunnel.value?.webhookUri) return
  const command = buildDevTunnelSampleCurlCommand(tunnel.value.webhookUri)
  const ok = await copyTextToClipboard(command)
  toast.add({
    severity: ok ? 'success' : 'warn',
    summary: ok ? 'Copied' : 'Copy failed',
    detail: ok ? 'Sample curl command copied to clipboard.' : 'Clipboard blocked — curl command printed to console.',
    life: 3500,
  })
  if (!ok)
    console.info('[dev-tunnels] Sample curl command:\n%s', command)
}

function selectReadonlyField(event: FocusEvent) {
  (event.target as HTMLInputElement | null)?.select()
}

async function startTunnel() {
  if (!tunnel.value) return
  try {
    const result = await store.startTunnel(tunnel.value.id)
    if (result.status === 'LoginRequired') {
      await router.push({ name: 'dev-tunnels', query: { login: tunnel.value.id } })
      return
    }
    await store.refreshTunnel(tunnel.value.id)
  } catch (error) {
    toast.add({ severity: 'error', summary: 'Start failed', detail: extractApiError(error), life: 5000 })
  }
}

async function stopTunnel() {
  if (!tunnel.value) return
  try {
    await store.stopTunnel(tunnel.value.id)
    await store.refreshTunnel(tunnel.value.id)
    stopPlayback()
  } catch (error) {
    toast.add({ severity: 'error', summary: 'Stop failed', detail: extractApiError(error), life: 5000 })
  }
}

function openReplay(event: WebhookEvent) {
  replayEvent.value = event
  replayResult.value = null
  replayVisible.value = true
}

async function submitReplay(payload: {
  targetUrl: string
  methodOverride?: string
  headersOverride?: Record<string, string>
  bodyTextOverride?: string
  bodyBase64Override?: string
  contentTypeOverride?: string
}) {
  if (!tunnel.value || !replayEvent.value) return
  replayLoading.value = true
  try {
    replayResult.value = await devTunnelsApi.replay(tunnel.value.id, replayEvent.value.id, payload)
  } catch (error) {
    toast.add({ severity: 'error', summary: 'Replay failed', detail: extractApiError(error), life: 6000 })
  } finally {
    replayLoading.value = false
  }
}

watch(events, (next) => {
  if (!next.length) {
    selectedEventId.value = null
    return
  }

  if (!selectedEventId.value || !next.some(event => event.id === selectedEventId.value))
    selectedEventId.value = next[next.length - 1].id
})

watch(tunnelId, async () => {
  stopPlayback()
  if (tunnelId.value)
    await loadInspector()
})

onMounted(async () => {
  await loadInspector()
})

onUnmounted(() => {
  stopPlayback()
})
</script>

<template>
  <div class="inspector-view">
    <section class="inspector-hero">
      <div class="hero-copy">
        <Button label="Back to tunnels" icon="pi pi-arrow-left" text size="small" @click="router.push({ name: 'dev-tunnels' })" />
        <div class="hero-text">
          <div class="hero-heading">
            <div class="hero-kicker">Signal room</div>
            <h1>{{ tunnel?.name ?? 'Tunnel inspector' }}</h1>
          </div>
          <div class="hero-url-field">
            <label class="hero-field-label">Webhook URL</label>
            <InputGroup class="hero-url-group">
              <InputText
                :modelValue="tunnel?.webhookUri ?? ''"
                readonly
                :placeholder="'Waiting for a public webhook URL.'"
                class="hero-url-input"
                @focus="selectReadonlyField"
              />
              <InputGroupAddon>
                <Button
                  icon="pi pi-copy"
                  text
                  rounded
                  size="small"
                  :disabled="!tunnel?.webhookUri"
                  aria-label="Copy webhook URL"
                  @click="copyWebhook"
                />
              </InputGroupAddon>
              <InputGroupAddon>
                <Button
                  icon="pi pi-code"
                  text
                  rounded
                  size="small"
                  :disabled="!tunnel?.webhookUri"
                  aria-label="Copy sample curl command"
                  @click="copyWebhookCurl"
                />
              </InputGroupAddon>
            </InputGroup>
          </div>
          <div v-if="tunnel?.tunnelUri" class="hero-url-field">
            <label class="hero-field-label">Tunnel URL</label>
            <InputGroup class="hero-url-group">
              <InputText
                :modelValue="tunnel.tunnelUri"
                readonly
                class="hero-url-input"
                @focus="selectReadonlyField"
              />
              <InputGroupAddon>
                <Button
                  icon="pi pi-copy"
                  text
                  rounded
                  size="small"
                  aria-label="Copy tunnel URL"
                  @click="copyTunnel"
                />
              </InputGroupAddon>
            </InputGroup>
          </div>
        </div>
      </div>

      <div class="hero-side">
        <div class="hero-metrics">
          <div class="metric-card">
            <span class="metric-label">Captured</span>
            <strong>{{ events.length }}</strong>
          </div>
          <div class="metric-card">
            <span class="metric-label">SSE status</span>
            <strong class="metric-word">{{ streamState === 'connected' ? 'Online' : streamState === 'connecting' ? 'Syncing' : 'Offline' }}</strong>
          </div>
          <div class="metric-card">
            <span class="metric-label">Latest frame</span>
            <strong class="metric-word">{{ latestEvent ? new Date(latestEvent.receivedAtUtc).toLocaleTimeString() : 'Waiting' }}</strong>
          </div>
          <div class="metric-card">
            <span class="metric-label">Notifications</span>
            <strong class="metric-word">{{ store.notificationPaused ? 'Paused' : 'Live' }}</strong>
          </div>
        </div>

        <div class="hero-actions">
          <Button label="Refresh" icon="pi pi-refresh" severity="secondary" outlined size="small" @click="loadInspector" />
          <Button
            v-if="tunnel?.status === 'Running' || tunnel?.status === 'Starting'"
            label="Stop tunnel"
            icon="pi pi-pause"
            severity="secondary"
            size="small"
            @click="stopTunnel"
          />
          <Button v-else label="Start tunnel" icon="pi pi-play" size="small" @click="startTunnel" />
        </div>
      </div>
    </section>

    <Message
      v-if="tunnel?.lastError"
      severity="error"
      :closable="false"
      class="error-banner"
    >
      {{ tunnel.lastError }}
    </Message>

    <Card v-if="tunnel" class="inspector-shell">
      <template #content>
        <Tabs v-model:value="activeTab">
          <TabList>
            <Tab value="traffic">Traffic</Tab>
            <Tab value="archive">Archive</Tab>
          </TabList>
          <TabPanels>
            <TabPanel value="traffic">
              <div class="traffic-panel">
                <div class="traffic-rail">
                  <EventTimeline
                    :events="events"
                    :selected-event-id="selectedEventId"
                    :playing="playing"
                    :histogram-bars="histogramBars"
                    @select="selectedEventId = $event"
                    @toggle-playback="togglePlayback"
                  />
                </div>
                <div class="traffic-feed">
                <EventTape
                  :events="events"
                  :stream-state="streamState"
                  :selected-event-id="selectedEventId"
                  @select="selectedEventId = $event"
                />
                </div>
              </div>
            </TabPanel>
            <TabPanel value="archive">
              <div class="archive-panel">
                <EventDetail
                  :events="events"
                  :selected-event-id="selectedEventId"
                  @select="selectedEventId = $event"
                  @replay="openReplay"
                />
              </div>
            </TabPanel>
          </TabPanels>
        </Tabs>
      </template>
    </Card>
    <Card v-else class="missing-state">
      <template #content>
        <i class="pi pi-exclamation-circle" />
        <h3>Tunnel not found</h3>
        <p>The requested tunnel either does not exist or has been deleted.</p>
      </template>
    </Card>

    <ReplayDialog
      v-model:visible="replayVisible"
      :event="replayEvent"
      :loading="replayLoading"
      :result="replayResult"
      @submit="submitReplay"
    />
  </div>
</template>

<style scoped>
.inspector-view {
  height: 100%;
  min-height: 0;
  overflow: hidden;
  padding: 1.4rem;
  display: grid;
  grid-template-rows: auto auto minmax(0, 1fr);
  gap: 1rem;
  background:
    radial-gradient(circle at top left, color-mix(in srgb, var(--info) 12%, transparent), transparent 24%),
    radial-gradient(circle at bottom right, color-mix(in srgb, var(--warning) 8%, transparent), transparent 24%),
    linear-gradient(180deg, color-mix(in srgb, var(--bg-base) 96%, #03070d 4%), var(--bg-base));
}

/* ── Hero — compact layout used for all tabs ── */
.inspector-hero,
.missing-state {
  display: grid;
  grid-template-columns: minmax(0, 1.3fr) minmax(22rem, 0.7fr);
  gap: 0.85rem;
  padding: 1rem 1.15rem;
  border-radius: 1.5rem;
  border: 1px solid color-mix(in srgb, var(--border) 70%, transparent);
  background:
    linear-gradient(135deg, color-mix(in srgb, var(--bg-surface) 92%, #08131d 8%), color-mix(in srgb, var(--bg-raised) 92%, #132233 8%));
}

.hero-copy {
  display: grid;
  gap: 0.55rem;
}

.hero-heading {
  display: grid;
  gap: 0.45rem;
  grid-row: 1 / span 2;
  align-self: start;
}

.hero-text {
  display: grid;
  grid-template-columns: minmax(16rem, 0.7fr) minmax(0, 1fr);
  gap: 0.55rem 0.8rem;
  align-items: end;
}

.hero-kicker,
.metric-label {
  font-size: 0.7rem;
  letter-spacing: 0.22em;
  text-transform: uppercase;
  color: color-mix(in srgb, var(--info) 76%, var(--text-secondary));
}

.hero-text h1,
.missing-state h3 {
  margin: 0;
  color: var(--text-primary);
  font-family: 'Avenir Next Condensed', 'Arial Narrow', 'Franklin Gothic Medium', sans-serif;
  font-size: clamp(2.2rem, 3vw, 3.35rem);
  letter-spacing: 0.03em;
  line-height: 0.9;
  text-transform: uppercase;
}

.hero-text p,
.missing-state p {
  margin: 0;
  color: var(--text-secondary);
  line-height: 1.65;
}

.hero-url-field {
  display: grid;
  gap: 0.28rem;
}

.hero-field-label {
  font-size: 0.7rem;
  letter-spacing: 0.18em;
  text-transform: uppercase;
  color: color-mix(in srgb, var(--info) 76%, var(--text-secondary));
}

.hero-url-input {
  width: 100%;
}

.hero-url-group :deep(.p-inputgroupaddon) {
  background: color-mix(in srgb, var(--bg-base) 56%, transparent);
  border-color: color-mix(in srgb, var(--border) 66%, transparent);
}

.hero-url-input :deep(.p-inputtext) {
  width: 100%;
  font-family: var(--font-family-mono);
  word-break: break-word;
}

.hero-side {
  display: grid;
  gap: 0.6rem;
}

.hero-metrics {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0.55rem;
}

.metric-card {
  display: grid;
  align-content: start;
  gap: 0.35rem;
  min-height: auto;
  padding: 0.7rem 0.8rem;
  border-radius: 1.05rem;
  border: 1px solid color-mix(in srgb, var(--border) 68%, transparent);
  background: color-mix(in srgb, var(--bg-base) 55%, transparent);
}

.metric-card strong {
  color: var(--text-primary);
  font-size: 1.35rem;
  line-height: 0.95;
}

.metric-card .metric-word {
  font-size: 0.92rem;
  line-height: 1.2;
}

.hero-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 0.55rem;
}

/* ── Inspector shell — full-height card ── */
.inspector-shell {
  grid-row: 3;
  height: 100%;
  min-height: 0;
  border-radius: 1.4rem;
  border: 1px solid color-mix(in srgb, var(--border) 70%, transparent);
  background: linear-gradient(180deg, color-mix(in srgb, var(--bg-surface) 90%, #08111a 10%), color-mix(in srgb, var(--bg-raised) 92%, #0d1724 8%));
}

.inspector-shell :deep(.p-card-body) {
  height: 100%;
  min-height: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
}

.inspector-shell :deep(.p-card-content),
.missing-state :deep(.p-card-content) {
  padding: 0;
}

.inspector-shell :deep(.p-card-content) {
  flex: 1 1 0;
  min-height: 0;
  display: flex;
  flex-direction: column;
}

.inspector-shell :deep(.p-tabs) {
  flex: 1 1 0;
  min-height: 0;
  display: grid;
  grid-template-rows: auto minmax(0, 1fr);
}

.inspector-shell :deep(.p-tabpanels) {
  padding: 0;
  min-height: 0;
  display: flex;
  flex-direction: column;
}

.inspector-shell :deep(.p-tabpanel) {
  min-height: 0;
}

.inspector-shell :deep(.p-tabpanel[data-p-active='true']) {
  display: flex;
  flex: 1 1 0;
  flex-direction: column;
  overflow: hidden;
}

/* ── Panel layouts ── */
.archive-panel {
  flex: 1 1 0;
  min-height: 0;
  display: grid;
  grid-template-rows: minmax(0, 1fr);
}

.traffic-panel {
  flex: 1 1 0;
  min-height: 0;
  display: grid;
  grid-template-columns: minmax(19rem, 0.82fr) minmax(0, 1.18fr);
  align-items: stretch;
  gap: 1rem;
}

.traffic-rail,
.traffic-feed {
  min-height: 0;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

.error-banner :deep(.p-message) {
  border-radius: 1.1rem;
}

.missing-state {
  min-height: 18rem;
  place-items: center;
  text-align: center;
  grid-template-columns: 1fr;
}

.missing-state i {
  font-size: 2.5rem;
  color: var(--danger);
}

@media (max-width: 1180px) {
  .inspector-hero {
    grid-template-columns: 1fr;
  }

  .hero-text,
  .traffic-panel {
    grid-template-columns: 1fr;
  }

  .hero-heading {
    grid-row: auto;
  }
}

@media (max-width: 760px) {
  .hero-metrics {
    grid-template-columns: 1fr;
  }

  .hero-actions {
    display: grid;
  }

  .hero-text {
    gap: 0.45rem;
  }
}
</style>
