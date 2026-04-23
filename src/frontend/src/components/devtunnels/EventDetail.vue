<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import Button from 'primevue/button'
import Listbox from 'primevue/listbox'
import Message from 'primevue/message'
import Paginator from 'primevue/paginator'
import ScrollPanel from 'primevue/scrollpanel'
import Splitter from 'primevue/splitter'
import SplitterPanel from 'primevue/splitterpanel'
import Tag from 'primevue/tag'
import Tabs from 'primevue/tabs'
import Tab from 'primevue/tab'
import TabList from 'primevue/tablist'
import TabPanel from 'primevue/tabpanel'
import TabPanels from 'primevue/tabpanels'
import JsonViewer from '@/components/common/JsonViewer.vue'
import type { WebhookEvent } from '@/api/types'

const props = defineProps<{
  events: WebhookEvent[]
  selectedEventId?: string | null
}>()

const emit = defineEmits<{
  select: [eventId: string]
  replay: [event: WebhookEvent]
}>()

const orderedEvents = computed(() =>
  [...props.events].sort((a, b) => new Date(b.receivedAtUtc).getTime() - new Date(a.receivedAtUtc).getTime()),
)

const selectedEvent = computed(() =>
  orderedEvents.value.find(event => event.id === props.selectedEventId) ?? orderedEvents.value[0] ?? null,
)

const selectedEventIdModel = computed({
  get: () => selectedEvent.value?.id ?? null,
  set: (value: string | null) => {
    if (value)
      emit('select', value)
  },
})

const parsedHeaders = computed(() => selectedEvent.value?.headers ?? {})
const activeTab = ref('body')
const first = ref(0)
const rows = ref(6)
const parsedBody = computed<{ isJson: boolean; value: unknown }>(() => {
  const body = selectedEvent.value?.bodyText
  if (body == null)
    return { isJson: false, value: null }

  try {
    return { isJson: true, value: JSON.parse(body) }
  } catch {
    return { isJson: false, value: null }
  }
})

const pagedEvents = computed(() =>
  orderedEvents.value.slice(first.value, first.value + rows.value),
)

function getLastPageStart(total: number, pageSize: number) {
  if (!total || !pageSize)
    return 0

  return Math.floor((total - 1) / pageSize) * pageSize
}

function severity(method: string) {
  const normalized = method.toUpperCase()
  if (normalized === 'POST') return 'success'
  if (normalized === 'GET') return 'info'
  if (normalized === 'DELETE') return 'danger'
  return 'warn'
}

function syncPageToSelection() {
  const total = orderedEvents.value.length

  if (!total) {
    first.value = 0
    return
  }

  const lastPageStart = getLastPageStart(total, rows.value)

  if (first.value > lastPageStart)
    first.value = lastPageStart

  const selectedId = selectedEventIdModel.value
  const selectedIndex = selectedId
    ? orderedEvents.value.findIndex(event => event.id === selectedId)
    : 0

  if (selectedIndex < 0)
    return

  if (selectedIndex < first.value || selectedIndex >= first.value + rows.value)
    first.value = Math.floor(selectedIndex / rows.value) * rows.value
}

function onPageChange(event: { first: number; rows: number }) {
  const nextRows = event.rows
  const nextFirst = Math.min(event.first, getLastPageStart(orderedEvents.value.length, nextRows))
  const nextPage = orderedEvents.value.slice(nextFirst, nextFirst + nextRows)

  rows.value = nextRows
  first.value = nextFirst

  if (!nextPage.length)
    return

  if (!nextPage.some(current => current.id === selectedEventIdModel.value))
    emit('select', nextPage[0].id)
}

watch([orderedEvents, selectedEventIdModel, rows], syncPageToSelection, { immediate: true })
</script>

<template>
  <section class="event-detail-shell">
    <Splitter class="archive-splitter">
      <SplitterPanel :size="40" :minSize="28">
        <div class="event-history">
          <div class="history-head">
            <div class="eyebrow">Archive</div>
            <h3>Captured events</h3>
            <p>Pick any frame to inspect headers, pretty JSON, raw payloads, or replay the request.</p>
          </div>
          <div v-if="orderedEvents.length" class="history-list-shell">
            <Listbox
              v-model="selectedEventIdModel"
              :options="pagedEvents"
              optionValue="id"
              dataKey="id"
              scrollHeight="100%"
              class="history-listbox"
            >
              <template #option="{ option }">
                <div class="history-card">
                  <div class="history-card-top">
                    <Tag :value="option.method" :severity="severity(option.method)" />
                    <span>{{ new Date(option.receivedAtUtc).toLocaleTimeString() }}</span>
                  </div>
                  <strong>{{ option.path }}</strong>
                  <p>{{ option.queryString || option.contentType || 'No query or content type' }}</p>
                </div>
              </template>
            </Listbox>
            <Paginator
              :first="first"
              :rows="rows"
              :total-records="orderedEvents.length"
              :rows-per-page-options="[6, 10, 14]"
              template="PrevPageLink CurrentPageReport NextPageLink RowsPerPageDropdown"
              current-page-report-template="{first}–{last} of {totalRecords}"
              class="history-paginator"
              @page="onPageChange"
            />
          </div>
          <Message v-else severity="info" :closable="false" class="history-empty">
            No webhook events captured yet.
          </Message>
        </div>
      </SplitterPanel>

      <SplitterPanel :size="60" :minSize="35">
        <div class="event-body">
          <div v-if="selectedEvent" class="event-body-header">
            <div>
              <div class="eyebrow">Selected frame</div>
              <h3>{{ selectedEvent.method }} {{ selectedEvent.path }}</h3>
              <p>{{ new Date(selectedEvent.receivedAtUtc).toLocaleString() }} · {{ selectedEvent.bodySize }} bytes</p>
            </div>
            <Button label="Replay" icon="pi pi-send" @click="emit('replay', selectedEvent)" />
          </div>

          <Tabs v-if="selectedEvent" v-model:value="activeTab">
            <TabList>
              <Tab value="body">Body</Tab>
              <Tab value="headers">Headers</Tab>
              <Tab value="raw">Raw</Tab>
            </TabList>
            <TabPanels>
              <TabPanel value="body">
                <JsonViewer
                  v-if="parsedBody.isJson"
                  :data="parsedBody.value"
                  title="Parsed JSON"
                  :initially-expanded="true"
                />
                <ScrollPanel v-else class="raw-scroll">
                  <pre class="raw-view">{{ selectedEvent.bodyText ?? selectedEvent.bodyBase64 ?? 'No body' }}</pre>
                </ScrollPanel>
              </TabPanel>
              <TabPanel value="headers">
                <JsonViewer :data="parsedHeaders" title="Headers" :initially-expanded="true" />
              </TabPanel>
              <TabPanel value="raw">
                <ScrollPanel class="raw-scroll">
                  <pre class="raw-view">{{ selectedEvent.bodyText ?? selectedEvent.bodyBase64 ?? 'No body' }}</pre>
                </ScrollPanel>
              </TabPanel>
            </TabPanels>
          </Tabs>

          <Message v-else severity="info" :closable="false" class="history-empty">
            Pick an event from the archive to inspect it in detail.
          </Message>
        </div>
      </SplitterPanel>
    </Splitter>
  </section>
</template>

<style scoped>
.event-detail-shell {
  flex: 1 1 auto;
  min-height: 0;
  display: flex;
  overflow: hidden;
}

.archive-splitter {
  flex: 1 1 auto;
  min-height: 0;
  overflow: hidden;
}

.archive-splitter :deep(.p-splitterpanel) {
  display: flex;
  padding: 0.15rem 0;
  min-height: 0;
}

.event-history,
.event-body {
  flex: 1 1 auto;
  display: grid;
  grid-template-rows: auto minmax(0, 1fr);
  align-content: start;
  gap: 0.85rem;
  padding: 0.2rem 0.9rem;
  min-height: 0;
  min-width: 0;
}

.eyebrow {
  font-size: 0.68rem;
  letter-spacing: 0.2em;
  text-transform: uppercase;
  color: color-mix(in srgb, var(--info) 72%, var(--text-secondary));
  margin-bottom: 0.35rem;
}

.history-head h3,
.event-body-header h3 {
  margin: 0;
  color: var(--text-primary);
  font-family: 'Avenir Next Condensed', 'Arial Narrow', 'Franklin Gothic Medium', sans-serif;
  font-size: clamp(1.8rem, 2.4vw, 2.35rem);
  letter-spacing: 0.03em;
  line-height: 0.92;
  text-transform: uppercase;
  overflow-wrap: anywhere;
}

.history-head p,
.event-body-header p {
  margin: 0.55rem 0 0;
  color: var(--text-secondary);
  line-height: 1.6;
}

.history-list-shell {
  display: grid;
  grid-template-rows: minmax(0, 1fr) auto;
  align-content: stretch;
  gap: 0.85rem;
  min-height: 0;
}

.history-listbox {
  width: 100%;
  min-height: 0;
  border: none;
  background: transparent;
}

.history-listbox :deep(.p-listbox-list-container) {
  height: 100%;
  max-height: none;
}

.history-listbox :deep(.p-virtualscroller) {
  height: 100%;
}

.history-listbox :deep(.p-listbox-list) {
  display: flex;
  flex-direction: column;
  align-items: stretch;
  gap: 0.75rem;
  padding: 0 0.25rem 0 0;
}

.history-listbox :deep(.p-listbox-option) {
  display: block;
  padding: 0;
  border-radius: 1rem;
  background: transparent;
  overflow: visible;
}

.history-listbox :deep(.p-listbox-option.p-listbox-option-selected) {
  background: transparent;
}

.history-listbox :deep(.p-listbox-option.p-focus) {
  box-shadow: none;
}

.history-card {
  display: grid;
  gap: 0.45rem;
  width: 100%;
  padding: 0.85rem 0.95rem;
  border-radius: 1rem;
  border: 1px solid color-mix(in srgb, var(--border) 68%, transparent);
  background: color-mix(in srgb, var(--bg-base) 40%, transparent);
  color: var(--text-secondary);
  cursor: pointer;
  box-sizing: border-box;
}

.history-listbox :deep(.p-listbox-option-selected) .history-card,
.history-listbox :deep(.p-listbox-option:focus) .history-card {
  border-color: color-mix(in srgb, var(--info) 65%, transparent);
  box-shadow: 0 0 0 1px color-mix(in srgb, var(--info) 20%, transparent);
}

.history-card-top {
  display: flex;
  justify-content: space-between;
  gap: 0.5rem;
  align-items: center;
  font-size: 0.75rem;
}

.history-card strong {
  color: var(--text-primary);
  font-family: var(--font-family-mono);
  word-break: break-word;
}

.history-card p {
  margin: 0;
  line-height: 1.5;
}

.history-paginator {
  align-self: end;
  border: none;
  background: transparent;
  padding: 0;
}

.history-paginator :deep(.p-paginator-content) {
  justify-content: space-between;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.event-body-header {
  display: flex;
  justify-content: space-between;
  gap: 1rem;
  align-items: flex-start;
}

.event-body-header > div {
  min-width: 0;
}

.event-body :deep(.p-tabs) {
  min-height: 0;
  display: grid;
  grid-template-rows: auto minmax(0, 1fr);
  align-content: start;
}

.event-body :deep(.p-tabpanels),
.event-body :deep(.p-tabpanel) {
  min-height: 0;
}

.event-body :deep(.p-tabpanels) {
  padding: 0;
}

.event-body :deep(.p-tabpanel) {
  height: 100%;
  display: grid;
  grid-template-rows: minmax(0, 1fr);
  align-content: start;
}

.raw-view {
  margin: 0;
  padding: 1rem;
  color: var(--text-primary);
  font-family: var(--font-family-mono);
  white-space: pre-wrap;
  word-break: break-word;
}

.raw-scroll {
  min-height: 0;
  height: 100%;
  width: 100%;
}

.history-empty {
  border-radius: 1rem;
}

@media (max-width: 1100px) {
  .archive-splitter {
    display: block;
  }

  .event-body-header {
    display: grid;
  }
}
</style>
