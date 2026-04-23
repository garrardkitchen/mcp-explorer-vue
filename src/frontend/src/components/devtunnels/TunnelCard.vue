<script setup lang="ts">
import { computed } from 'vue'
import Button from 'primevue/button'
import Card from 'primevue/card'
import Chip from 'primevue/chip'
import InputGroup from 'primevue/inputgroup'
import InputGroupAddon from 'primevue/inputgroupaddon'
import InputText from 'primevue/inputtext'
import Message from 'primevue/message'
import Tag from 'primevue/tag'
import type { DevTunnel, WebhookEvent } from '@/api/types'
import TrafficSparkline from '@/components/devtunnels/TrafficSparkline.vue'

type HistogramBar = {
  index: number
  value: number
  ratio: number
}

const props = defineProps<{
  tunnel: DevTunnel
  streamState?: 'connecting' | 'connected' | 'disconnected'
  lastEvent?: WebhookEvent | null
  eventCount: number
  unseenCount: number
  histogramBars: HistogramBar[]
}>()

const emit = defineEmits<{
  inspect: [id: string]
  start: [id: string]
  stop: [id: string]
  delete: [id: string]
  copyWebhook: [url: string]
  copySampleCurl: [url: string]
  copyTunnel: [url: string]
}>()

const statusSeverity = computed(() => {
  switch (props.tunnel.status) {
    case 'Running': return 'success'
    case 'Starting': return 'warn'
    case 'LoginRequired': return 'contrast'
    case 'Error': return 'danger'
    default: return 'secondary'
  }
})

const statusLabel = computed(() => {
  if (props.tunnel.status === 'Running' && props.streamState === 'connected')
    return 'Running · Live'
  if (props.tunnel.status === 'Running' && props.streamState === 'connecting')
    return 'Running · Syncing'
  return props.tunnel.status
})

const createdLabel = computed(() => new Date(props.tunnel.createdAtUtc).toLocaleString())
const lastEventLabel = computed(() =>
  props.lastEvent ? new Date(props.lastEvent.receivedAtUtc).toLocaleTimeString() : 'No captures yet',
)

const ingressLabel = computed(() =>
  props.tunnel.access === 'Anonymous' ? 'Public ingress' : 'Authenticated ingress',
)

function selectField(event: FocusEvent) {
  (event.target as HTMLInputElement | null)?.select()
}
</script>

<template>
  <Card class="tunnel-card" :class="`status-${tunnel.status.toLowerCase()}`">
    <template #content>
      <div class="tunnel-card-header">
        <div class="header-copy">
          <div class="eyebrow">Signal deck</div>
          <h3 class="tunnel-name">{{ tunnel.name }}</h3>
          <p class="tunnel-id">{{ tunnel.id }}</p>
        </div>
        <Tag :value="statusLabel" :severity="statusSeverity" />
      </div>

      <div class="signal-row">
        <Chip :label="ingressLabel" class="signal-chip" />
        <Chip :label="`Created ${createdLabel}`" class="signal-chip signal-chip-muted" />
      </div>

      <div class="metrics-grid">
        <div class="metric-cell">
          <span class="metric-label">Captured</span>
          <strong>{{ eventCount }}</strong>
        </div>
        <div class="metric-cell">
          <span class="metric-label">Latest</span>
          <strong class="metric-detail">{{ lastEventLabel }}</strong>
        </div>
        <div class="metric-cell" :class="{ highlight: unseenCount > 0 }">
          <span class="metric-label">Unseen</span>
          <strong>{{ unseenCount }}</strong>
        </div>
      </div>

      <div class="signal-stage">
        <div class="url-block">
          <label class="url-label">Webhook target</label>
          <InputGroup class="url-input-group">
            <InputText
              :modelValue="tunnel.webhookUri ?? ''"
              readonly
              :placeholder="'Start the tunnel to mint a public callback URL.'"
              class="url-input"
              @focus="selectField"
            />
            <InputGroupAddon>
              <Button
                icon="pi pi-copy"
                text
                rounded
                :disabled="!tunnel.webhookUri"
                aria-label="Copy webhook URL"
                @click="tunnel.webhookUri && emit('copyWebhook', tunnel.webhookUri)"
              />
            </InputGroupAddon>
            <InputGroupAddon>
              <Button
                icon="pi pi-code"
                text
                rounded
                :disabled="!tunnel.webhookUri"
                aria-label="Copy sample curl command"
                @click="tunnel.webhookUri && emit('copySampleCurl', tunnel.webhookUri)"
              />
            </InputGroupAddon>
          </InputGroup>
        </div>
        <div v-if="tunnel.tunnelUri" class="url-block subtle">
          <label class="url-label">Tunnel URL</label>
          <InputGroup class="url-input-group">
            <InputText
              :modelValue="tunnel.tunnelUri"
              readonly
              class="url-input"
              @focus="selectField"
            />
            <InputGroupAddon>
              <Button
                icon="pi pi-copy"
                text
                rounded
                aria-label="Copy tunnel URL"
                @click="emit('copyTunnel', tunnel.tunnelUri)"
              />
            </InputGroupAddon>
          </InputGroup>
        </div>
      </div>

      <div class="spark-shell">
        <div class="spark-header">
          <span class="metric-label">Traffic pattern</span>
          <span class="spark-meta">{{ props.histogramBars.reduce((count, bar) => count + bar.value, 0) }} events retained</span>
        </div>
        <TrafficSparkline :bars="histogramBars" variant="compact" empty-label="No events yet" />
      </div>

      <Message v-if="tunnel.lastError" severity="error" :closable="false" class="issue-strip">
        <div class="issue-copy">
          <span class="metric-label">Latest issue</span>
          <p>{{ tunnel.lastError }}</p>
        </div>
      </Message>
    </template>

    <template #footer>
      <div class="tunnel-actions">
        <Button label="Inspect" icon="pi pi-arrow-right" @click="emit('inspect', tunnel.id)" />
        <Button
          v-if="tunnel.status === 'Running' || tunnel.status === 'Starting'"
          label="Stop"
          icon="pi pi-pause"
          severity="secondary"
          outlined
          @click="emit('stop', tunnel.id)"
        />
        <Button
          v-else
          label="Start"
          icon="pi pi-play"
          severity="secondary"
          outlined
          @click="emit('start', tunnel.id)"
        />
        <Button label="Delete" icon="pi pi-trash" severity="danger" text @click="emit('delete', tunnel.id)" />
      </div>
    </template>
  </Card>
</template>

<style scoped>
.tunnel-card {
  display: grid;
  gap: 1rem;
  padding: 1.3rem;
  border-radius: 1.5rem;
  border: 1px solid color-mix(in srgb, var(--border) 74%, transparent);
  background:
    radial-gradient(circle at top right, color-mix(in srgb, var(--info) 18%, transparent), transparent 26%),
    linear-gradient(160deg, color-mix(in srgb, var(--bg-surface) 94%, #091725 6%), color-mix(in srgb, var(--bg-raised) 92%, #102233 8%));
  box-shadow: 0 24px 80px rgba(0, 0, 0, 0.18);
}

.tunnel-card :deep(.p-card-body) {
  gap: 1rem;
}

.tunnel-card :deep(.p-card-content),
.tunnel-card :deep(.p-card-footer) {
  padding: 0;
}

.tunnel-card-header {
  display: flex;
  justify-content: space-between;
  gap: 1rem;
  align-items: flex-start;
}

.header-copy {
  min-width: 0;
}

.eyebrow,
.metric-label,
.url-label {
  font-size: 0.68rem;
  letter-spacing: 0.22em;
  text-transform: uppercase;
  color: color-mix(in srgb, var(--info) 72%, var(--text-secondary));
}

.tunnel-name {
  margin: 0.4rem 0 0;
  color: var(--text-primary);
  font-family: 'Avenir Next Condensed', 'Arial Narrow', 'Franklin Gothic Medium', sans-serif;
  font-size: clamp(2rem, 3vw, 2.7rem);
  letter-spacing: 0.03em;
  line-height: 0.9;
  text-transform: uppercase;
}

.tunnel-id {
  margin: 0.4rem 0 0;
  color: var(--text-muted);
  font-family: var(--font-family-mono);
  font-size: 0.76rem;
  word-break: break-word;
}

.signal-row {
  display: flex;
  flex-wrap: wrap;
  gap: 0.65rem;
}

.signal-chip {
  background: color-mix(in srgb, var(--bg-base) 55%, transparent);
  color: var(--text-secondary);
  border: 1px solid color-mix(in srgb, var(--border) 70%, transparent);
}

.signal-chip-muted {
  opacity: 0.82;
}

.metrics-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 0.75rem;
}

.metric-cell {
  display: grid;
  align-content: start;
  gap: 0.5rem;
  min-height: 6rem;
  padding: 1rem 1.05rem;
  border-radius: 1rem;
  border: 1px solid color-mix(in srgb, var(--border) 68%, transparent);
  background: color-mix(in srgb, var(--bg-base) 42%, transparent);
}

.metric-cell.highlight {
  border-color: color-mix(in srgb, var(--warning) 48%, transparent);
  box-shadow: 0 0 0 1px color-mix(in srgb, var(--warning) 22%, transparent);
}

.metric-cell strong {
  display: block;
  margin-top: 0.15rem;
  color: var(--text-primary);
  font-size: 1.8rem;
  line-height: 0.95;
}

.metric-cell .metric-detail {
  font-size: 1rem;
  line-height: 1.2;
}

.signal-stage {
  display: grid;
  gap: 0.85rem;
  padding: 1rem;
  border-radius: 1.15rem;
  border: 1px solid color-mix(in srgb, var(--border) 68%, transparent);
  background:
    linear-gradient(180deg, rgba(255, 255, 255, 0.025), rgba(255, 255, 255, 0)),
    color-mix(in srgb, var(--bg-base) 56%, transparent);
}

.url-block {
  display: grid;
  gap: 0.45rem;
}

.url-input {
  width: 100%;
}

.url-input-group :deep(.p-inputgroupaddon) {
  background: color-mix(in srgb, var(--bg-base) 56%, transparent);
  border-color: color-mix(in srgb, var(--border) 66%, transparent);
}

.url-input :deep(.p-inputtext) {
  width: 100%;
  font-family: var(--font-family-mono);
  font-size: 0.8rem;
}

.subtle .url-input :deep(.p-inputtext) {
  color: var(--text-secondary);
}

.spark-shell {
  display: grid;
  gap: 0.7rem;
}

.spark-header {
  display: flex;
  justify-content: space-between;
  gap: 0.7rem;
  align-items: center;
}

.spark-meta {
  color: var(--text-muted);
  font-size: 0.76rem;
}

.issue-strip {
  border-radius: 1rem;
}

.issue-copy {
  display: grid;
  gap: 0.45rem;
}

.issue-copy p {
  margin: 0;
  color: var(--text-primary);
  line-height: 1.55;
}

.tunnel-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 0.65rem;
}

@media (max-width: 720px) {
  .tunnel-card-header,
  .spark-header {
    display: grid;
  }

  .metrics-grid {
    grid-template-columns: 1fr;
  }
}
</style>
