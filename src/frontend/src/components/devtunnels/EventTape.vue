<script setup lang="ts">
import { computed } from 'vue'
import Chip from 'primevue/chip'
import Listbox from 'primevue/listbox'
import Message from 'primevue/message'
import Tag from 'primevue/tag'
import type { WebhookEvent } from '@/api/types'

const props = defineProps<{
  events: WebhookEvent[]
  streamState: 'connecting' | 'connected' | 'disconnected'
  selectedEventId?: string | null
}>()

const emit = defineEmits<{
  select: [eventId: string]
}>()

const orderedEvents = computed(() =>
  [...props.events].sort((a, b) => new Date(b.receivedAtUtc).getTime() - new Date(a.receivedAtUtc).getTime()),
)

const selectedEvent = computed({
  get: () => props.selectedEventId ?? null,
  set: (value: string | null) => {
    if (value)
      emit('select', value)
  },
})

function severity(method: string) {
  const normalized = method.toUpperCase()
  if (normalized === 'POST') return 'success'
  if (normalized === 'GET') return 'info'
  if (normalized === 'DELETE') return 'danger'
  return 'warn'
}
</script>

<template>
  <section class="event-tape">
    <div class="event-tape-header">
      <div>
        <div class="eyebrow">Traffic</div>
        <h3>Incoming traffic</h3>
      </div>
      <div class="header-meta">
        <Chip
          :label="streamState === 'connected' ? 'SSE online' : streamState === 'connecting' ? 'Syncing stream' : 'Stream offline'"
          class="status-chip"
          :class="`state-${streamState}`"
        />
        <Chip :label="`${orderedEvents.length} captured`" class="count-chip" />
      </div>
    </div>

    <div v-if="orderedEvents.length" class="event-scroll-area">
      <Listbox
        v-model="selectedEvent"
        :options="orderedEvents"
        optionValue="id"
        dataKey="id"
        class="event-listbox"
      >
        <template #option="{ option }">
          <div class="event-card">
            <div class="event-meta">
              <Tag :value="option.method" :severity="severity(option.method)" />
              <span>{{ new Date(option.receivedAtUtc).toLocaleTimeString() }}</span>
              <span>{{ option.bodySize }} bytes</span>
            </div>
            <div class="event-path">{{ option.path }}<span v-if="option.queryString">{{ option.queryString }}</span></div>
            <p class="event-preview">
              {{ option.bodyText ?? option.bodyBase64 ?? 'No body' }}
            </p>
          </div>
        </template>
      </Listbox>
    </div>
    <Message v-else severity="info" :closable="false" class="event-empty">
      The tape is quiet. As soon as a webhook hits the tunnel, it appears here in real time.
    </Message>
  </section>
</template>

<style scoped>
.event-tape {
  display: flex;
  flex-direction: column;
  gap: 0.7rem;
  padding: 0;
  height: 100%;
  min-height: 0;
  overflow: hidden;
}

.event-tape-header {
  display: flex;
  flex-shrink: 0;
  justify-content: space-between;
  gap: 1rem;
  align-items: center;
}

.eyebrow {
  font-size: 0.68rem;
  letter-spacing: 0.22em;
  text-transform: uppercase;
  color: color-mix(in srgb, var(--info) 72%, var(--text-secondary));
  margin-bottom: 0.35rem;
}

.event-tape-header h3 {
  margin: 0;
  font-family: 'Avenir Next Condensed', 'Arial Narrow', 'Franklin Gothic Medium', sans-serif;
  font-size: clamp(1.55rem, 2vw, 2.05rem);
  letter-spacing: 0.03em;
  line-height: 0.92;
  text-transform: uppercase;
  color: var(--text-primary);
}

.event-tape-header p {
  display: none;
}

.header-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 0.65rem;
  justify-content: flex-end;
}

.status-chip,
.count-chip {
  border: 1px solid color-mix(in srgb, var(--border) 70%, transparent);
  background: color-mix(in srgb, var(--bg-base) 56%, transparent);
}

.state-connected {
  color: var(--success);
}

.state-connecting {
  color: var(--warning);
}

.state-disconnected {
  color: var(--danger);
}

/* ── Scroll area — simple flex div, no PrimeVue ScrollPanel ── */
.event-scroll-area {
  flex: 1 1 0;
  min-height: 0;
  overflow-y: auto;
  padding-right: 0.25rem;
}

.event-listbox {
  border: none;
  background: transparent;
}

/* Prevent PrimeVue Listbox from adding its own scroll container */
.event-listbox :deep(.p-listbox) {
  border: none;
  background: transparent;
  height: auto;
  max-height: none;
}

.event-listbox :deep(.p-listbox-list-container) {
  height: auto;
  max-height: none;
  overflow: visible;
}

.event-listbox :deep(.p-listbox-list) {
  display: grid;
  gap: 0.85rem;
  padding: 0 0.2rem 0.5rem 0;
}

.event-listbox :deep(.p-listbox-option) {
  padding: 0;
  border-radius: 1rem;
  background: transparent;
}

.event-listbox :deep(.p-listbox-option.p-listbox-option-selected) {
  background: transparent;
}

.event-listbox :deep(.p-listbox-option.p-focus) {
  box-shadow: none;
}

.event-card {
  width: 100%;
  padding: 0.8rem 0.9rem;
  border-radius: 1rem;
  border: 1px solid color-mix(in srgb, var(--border) 72%, transparent);
  background: color-mix(in srgb, var(--bg-base) 42%, transparent);
  color: inherit;
  animation: slideIn 320ms ease;
}

.event-listbox :deep(.p-listbox-option-selected) .event-card,
.event-listbox :deep(.p-listbox-option:focus) .event-card {
  border-color: color-mix(in srgb, var(--info) 55%, transparent);
  box-shadow: 0 0 0 1px color-mix(in srgb, var(--info) 18%, transparent);
}

.event-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 0.55rem;
  align-items: center;
  margin-bottom: 0.6rem;
  color: var(--text-muted);
  font-size: 0.76rem;
}

.event-path {
  color: var(--text-primary);
  font-family: var(--font-family-mono);
  margin-bottom: 0.55rem;
  word-break: break-word;
}

.event-preview {
  margin: 0;
  color: var(--text-secondary);
  line-height: 1.55;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.event-empty {
  border-radius: 1rem;
}

@keyframes slideIn {
  from { opacity: 0; transform: translateY(10px); }
  to { opacity: 1; transform: translateY(0); }
}

@media (max-width: 900px) {
  .event-tape-header {
    display: grid;
  }

  .header-meta {
    justify-content: flex-start;
  }
}
</style>
