<script setup lang="ts">
import { computed } from 'vue'
import Button from 'primevue/button'
import Chip from 'primevue/chip'
import Message from 'primevue/message'
import Slider from 'primevue/slider'
import Tag from 'primevue/tag'
import type { WebhookEvent } from '@/api/types'
import TrafficSparkline from '@/components/devtunnels/TrafficSparkline.vue'

const props = defineProps<{
  events: WebhookEvent[]
  selectedEventId?: string | null
  playing: boolean
  histogramBars: Array<{ index: number; value: number; ratio: number }>
}>()

const emit = defineEmits<{
  select: [eventId: string]
  togglePlayback: []
}>()

const orderedEvents = computed(() =>
  [...props.events].sort((a, b) => new Date(a.receivedAtUtc).getTime() - new Date(b.receivedAtUtc).getTime()),
)

const selectedIndex = computed(() => Math.max(0, orderedEvents.value.findIndex(e => e.id === props.selectedEventId)))

const selectedEvent = computed(() =>
  orderedEvents.value.find(event => event.id === props.selectedEventId)
  ?? orderedEvents.value[selectedIndex.value]
  ?? orderedEvents.value[orderedEvents.value.length - 1]
  ?? null,
)

const selectedPreview = computed(() => {
  const event = selectedEvent.value
  if (!event)
    return 'Select an event from the live feed to keep a compact body preview and playback cursor in view.'

  const raw = event.bodyText ?? event.bodyBase64 ?? 'No body'
  return raw.length > 320 ? `${raw.slice(0, 320)}…` : raw
})

const sliderIndex = computed({
  get: () => selectedIndex.value,
  set: (value: number) => {
    const selected = orderedEvents.value[value]
    if (selected)
      emit('select', selected.id)
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
  <section class="timeline-shell">
    <section class="signal-card">
      <div class="timeline-head">
        <div>
          <div class="eyebrow">Traffic pulse</div>
          <h3>Pulse window</h3>
        </div>
        <Chip :label="`${orderedEvents.length} captured`" class="timeline-count" />
      </div>
      <TrafficSparkline :bars="histogramBars" variant="compact" empty-label="Pulse window wakes up on first traffic" />
    </section>

    <section v-if="orderedEvents.length" class="signal-card rail-card">
      <div class="timeline-head">
        <div>
          <div class="eyebrow">Time travel</div>
          <h3>Playback rail</h3>
        </div>
        <div class="timeline-actions">
          <Chip :label="`Cursor ${selectedIndex + 1}`" class="timeline-count" />
          <Button
            :label="playing ? 'Pause replay' : 'Play from cursor'"
            :icon="playing ? 'pi pi-pause' : 'pi pi-play'"
            severity="secondary"
            outlined
            :disabled="orderedEvents.length < 2"
            @click="emit('togglePlayback')"
          />
        </div>
      </div>

      <div class="slider-shell">
        <Slider
          v-model="sliderIndex"
          :min="0"
          :max="Math.max(orderedEvents.length - 1, 0)"
          :step="1"
          class="time-slider"
          :disabled="orderedEvents.length < 2"
        />
        <div class="timeline-stamps">
          <span>{{ new Date(orderedEvents[0].receivedAtUtc).toLocaleTimeString() }}</span>
          <span>{{ new Date(orderedEvents[orderedEvents.length - 1].receivedAtUtc).toLocaleTimeString() }}</span>
        </div>
      </div>
    </section>
    <Message v-else severity="info" :closable="false" class="timeline-empty">
      Once events arrive, this rail becomes your scrubber for jumping backwards and replaying the timeline.
    </Message>

    <section class="signal-card selection-card">
      <div class="timeline-head selection-head">
        <div>
          <div class="eyebrow">Selected frame</div>
          <h3>{{ selectedEvent ? `${selectedEvent.method} ${selectedEvent.path}` : 'Waiting for traffic' }}</h3>
        </div>
        <Tag
          v-if="selectedEvent"
          :value="selectedEvent.method"
          :severity="severity(selectedEvent.method)"
          rounded
        />
      </div>

      <div v-if="selectedEvent" class="selection-meta">
        <span>{{ new Date(selectedEvent.receivedAtUtc).toLocaleTimeString() }}</span>
        <span>{{ selectedEvent.contentType || 'No content type' }}</span>
        <span>{{ selectedEvent.bodySize }} bytes</span>
      </div>
      <div v-if="selectedEvent" class="selection-path">{{ selectedEvent.path }}<span v-if="selectedEvent.queryString">{{ selectedEvent.queryString }}</span></div>
      <pre class="selection-preview">{{ selectedPreview }}</pre>
    </section>
  </section>
</template>

<style scoped>
.timeline-shell {
  display: grid;
  gap: 0.75rem;
  height: 100%;
  min-height: 0;
  grid-template-rows: auto auto minmax(0, 1fr);
}

.signal-card {
  display: grid;
  gap: 0.75rem;
  padding: 0.95rem 1rem;
  border-radius: 1.15rem;
  border: 1px solid color-mix(in srgb, var(--border) 62%, transparent);
  background: color-mix(in srgb, var(--bg-base) 50%, transparent);
}

.rail-card {
  gap: 0.7rem;
}

.timeline-head,
.selection-head {
  display: flex;
  justify-content: space-between;
  gap: 1rem;
  align-items: center;
}

.eyebrow {
  font-size: 0.68rem;
  letter-spacing: 0.22em;
  text-transform: uppercase;
  color: var(--text-muted);
  margin-bottom: 0.35rem;
}

.timeline-head h3 {
  margin: 0;
  color: var(--text-primary);
  font-size: 1.15rem;
  font-weight: 600;
}

.timeline-actions {
  display: flex;
  gap: 0.75rem;
  align-items: center;
  flex-wrap: wrap;
}

.timeline-count {
  background: color-mix(in srgb, var(--bg-base) 55%, transparent);
  border: 1px solid color-mix(in srgb, var(--border) 70%, transparent);
  color: var(--text-secondary);
}

.slider-shell {
  display: grid;
  gap: 0.55rem;
}

.time-slider {
  width: 100%;
}

.timeline-stamps {
  display: flex;
  justify-content: space-between;
  gap: 1rem;
  color: var(--text-muted);
  font-size: 0.74rem;
  font-family: var(--font-family-mono);
}

.timeline-empty {
  border-radius: 1rem;
}

.selection-card {
  min-height: 0;
  grid-template-rows: auto auto auto minmax(0, 1fr);
}

.selection-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 0.55rem 0.85rem;
  color: var(--text-muted);
  font-size: 0.74rem;
  font-family: var(--font-family-mono);
}

.selection-path {
  color: var(--text-primary);
  font-family: var(--font-family-mono);
  word-break: break-word;
}

.selection-preview {
  margin: 0;
  padding: 0.85rem 0.95rem;
  border-radius: 0.95rem;
  background: color-mix(in srgb, var(--bg-raised) 72%, transparent);
  border: 1px solid color-mix(in srgb, var(--border) 58%, transparent);
  color: var(--text-secondary);
  font-family: var(--font-family-mono);
  font-size: 0.78rem;
  line-height: 1.6;
  white-space: pre-wrap;
  word-break: break-word;
  overflow: hidden;
}

@media (max-width: 900px) {
  .timeline-head,
  .selection-head {
    display: grid;
  }
}
</style>
