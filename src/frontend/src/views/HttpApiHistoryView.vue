<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import InputText from 'primevue/inputtext'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Tag from 'primevue/tag'
import Skeleton from 'primevue/skeleton'
import Tabs from 'primevue/tabs'
import TabList from 'primevue/tablist'
import Tab from 'primevue/tab'
import TabPanels from 'primevue/tabpanels'
import TabPanel from 'primevue/tabpanel'
import { httpApisApi } from '@/api/httpApis'
import type { HttpApiInvocationRecord } from '@/api/types'
import JsonViewer from '@/components/common/JsonViewer.vue'

const records = ref<HttpApiInvocationRecord[]>([])
const loading = ref(false)
const searchQuery = ref('')
const expandedRows = ref<HttpApiInvocationRecord[]>([])

const filtered = computed(() => {
  const q = searchQuery.value.toLowerCase()
  return records.value.filter(r =>
    !q ||
    r.endpointName.toLowerCase().includes(q) ||
    r.endpointId.toLowerCase().includes(q) ||
    r.collectionRunId?.toLowerCase().includes(q)
  )
})

function statusSeverity(code: number) {
  if (code >= 200 && code < 300) return 'success'
  if (code >= 400 && code < 500) return 'warn'
  if (code >= 500) return 'danger'
  return 'secondary'
}

// ── Latency sparkline: per-endpoint last 10 records ───────────────────────────
const endpointGroups = computed(() => {
  const map = new Map<string, HttpApiInvocationRecord[]>()
  for (const r of records.value) {
    if (!map.has(r.endpointId)) map.set(r.endpointId, [])
    map.get(r.endpointId)!.push(r)
  }
  const groups = []
  for (const [id, recs] of map) {
    const sorted = [...recs].sort((a, b) => new Date(b.invokedAt).getTime() - new Date(a.invokedAt).getTime())
    const last10 = sorted.slice(0, 10)
    const avgLatency = last10.reduce((s, r) => s + r.latencyMs, 0) / (last10.length || 1)
    const hasBreaking = last10.some(r => r.schemaMatchedSnapshot === false)
    groups.push({ id, name: recs[0].endpointName, last10, avgLatency, hasBreaking, total: recs.length })
  }
  return groups.sort((a, b) => a.name.localeCompare(b.name))
})

onMounted(async () => {
  loading.value = true
  try {
    records.value = await httpApisApi.getGlobalHistory(500)
    expandedRows.value = []
  }
  finally { loading.value = false }
})
</script>

<template>
  <div class="history-view">
    <div class="toolbar">
      <span class="p-input-icon-left">
        <i class="pi pi-search" />
        <InputText v-model="searchQuery" placeholder="Search history…" />
      </span>
      <span class="record-count">{{ filtered.length }} of {{ records.length }} records</span>
    </div>

    <!-- Per-endpoint summary cards -->
    <div v-if="!loading" class="summary-cards">
      <div v-for="g in endpointGroups" :key="g.id" class="summary-card">
        <div class="summary-name">{{ g.name }}</div>
        <div class="summary-stats">
          <span>{{ g.total }} calls</span>
          <span>avg {{ g.avgLatency.toFixed(0) }} ms</span>
          <Tag v-if="g.hasBreaking" value="Schema drift" severity="danger" />
          <Tag v-else value="Schema OK" severity="success" />
        </div>
        <!-- Mini latency bar chart (last 10 invocations oldest → newest) -->
        <div class="latency-bars">
          <div
            v-for="(r, i) in [...g.last10].reverse()" :key="i"
            class="latency-bar"
            :style="{ height: `${Math.min(100, (r.latencyMs / (g.avgLatency * 2 || 1)) * 40)}px` }"
            :class="{
              'latency-bar--ok': r.schemaMatchedSnapshot !== false,
              'latency-bar--drift': r.schemaMatchedSnapshot === false
            }"
            :title="`${r.latencyMs}ms – ${new Date(r.invokedAt).toLocaleString()}`"
          />
        </div>
      </div>
    </div>

    <Skeleton v-if="loading" height="3rem" v-for="i in 4" :key="i" class="mb-2" />

    <!-- Invocation log table -->
    <DataTable
      v-if="!loading"
      :value="filtered"
      v-model:expandedRows="expandedRows"
      :paginator="filtered.length > 50"
      :rows="50"
      size="small"
      class="log-table"
      :globalFilterFields="['endpointName', 'endpointId']"
    >
      <Column expander style="width: 2.5rem" />
      <Column field="invokedAt" header="When" sortable>
        <template #body="{ data }">{{ new Date(data.invokedAt).toLocaleString() }}</template>
      </Column>
      <Column field="endpointName" header="Endpoint" sortable />
      <Column field="statusCode" header="Status" sortable>
        <template #body="{ data }">
          <Tag :value="`${data.statusCode || '—'}`" :severity="statusSeverity(data.statusCode)" />
        </template>
      </Column>
      <Column field="latencyMs" header="Latency" sortable>
        <template #body="{ data }">{{ data.latencyMs }} ms</template>
      </Column>
      <Column field="schemaMatchedSnapshot" header="Schema" sortable>
        <template #body="{ data }">
          <Tag
            v-if="data.schemaMatchedSnapshot !== null && data.schemaMatchedSnapshot !== undefined"
            :value="data.schemaMatchedSnapshot ? '✓ Match' : '✗ Drift'"
            :severity="data.schemaMatchedSnapshot ? 'success' : 'danger'"
          />
          <span v-else class="text-muted">—</span>
        </template>
      </Column>
      <Column field="collectionRunId" header="Run ID">
        <template #body="{ data }">
          <span v-if="data.collectionRunId" class="run-id" :title="data.collectionRunId">
            {{ data.collectionRunId.slice(0, 8) }}…
          </span>
          <span v-else class="text-muted">—</span>
        </template>
      </Column>
      <Column field="errorMessage" header="Error">
        <template #body="{ data }">
          <span v-if="data.errorMessage" class="error-text" v-tooltip.top="data.errorMessage">⚠️</span>
        </template>
      </Column>
      <template #expansion="{ data }">
        <div class="history-expansion">
          <Tabs value="request" class="history-detail-tabs">
            <TabList>
              <Tab value="request">Request</Tab>
              <Tab value="response-headers">Response Headers</Tab>
              <Tab value="response-body">Response Body</Tab>
            </TabList>
            <TabPanels>
              <TabPanel value="request">
                <div class="request-meta">
                  <span><strong>URL:</strong> <code>{{ data.requestBaseUrl }}{{ data.requestPath }}</code></span>
                  <span><strong>Method:</strong> {{ data.requestMethod || '—' }}</span>
                </div>
                <div class="section-label">Request Headers</div>
                <DataTable
                  :value="Object.entries(data.requestHeaders ?? {}).map(([name, value]) => ({ name, value }))"
                  size="small"
                >
                  <Column field="name" header="Name" />
                  <Column field="value" header="Value" />
                </DataTable>
                <div class="section-label">Query Strings</div>
                <DataTable
                  :value="Object.entries(data.requestQueryParams ?? {}).map(([name, value]) => ({ name, value }))"
                  size="small"
                >
                  <Column field="name" header="Name" />
                  <Column field="value" header="Value" />
                </DataTable>
              </TabPanel>
              <TabPanel value="response-headers">
                <DataTable
                  :value="Object.entries(data.responseHeaders ?? {}).map(([name, value]) => ({ name, value }))"
                  size="small"
                >
                  <Column field="name" header="Name" />
                  <Column field="value" header="Value" />
                </DataTable>
              </TabPanel>
              <TabPanel value="response-body">
                <JsonViewer v-if="data.body" :data="data.body" />
                <span v-else class="text-muted">No response body captured.</span>
              </TabPanel>
            </TabPanels>
          </Tabs>
        </div>
      </template>
    </DataTable>
  </div>
</template>

<style scoped>
.history-view { display: flex; flex-direction: column; gap: 0.75rem; padding: 1rem; height: 100%; overflow: hidden; }
.toolbar { display: flex; align-items: center; gap: 0.75rem; }
.record-count { font-size: 0.8rem; color: var(--text-color-secondary); }

.summary-cards { display: flex; gap: 0.75rem; flex-wrap: wrap; overflow-x: auto; }
.summary-card { background: var(--surface-card); border: 1px solid var(--surface-border); border-radius: 8px; padding: 0.75rem; min-width: 180px; }
.summary-name { font-weight: 600; font-size: 0.85rem; margin-bottom: 0.25rem; }
.summary-stats { display: flex; align-items: center; gap: 0.5rem; font-size: 0.75rem; color: var(--text-color-secondary); flex-wrap: wrap; }
.latency-bars { display: flex; align-items: flex-end; gap: 3px; height: 48px; margin-top: 0.5rem; }
.latency-bar { width: 8px; min-height: 4px; border-radius: 2px 2px 0 0; cursor: help; }
.latency-bar--ok { background: var(--green-400, #4ade80); }
.latency-bar--drift { background: var(--red-400, #f87171); }

.log-table { flex: 1; overflow: auto; font-size: 0.82rem; }
.history-expansion { border-top: 1px solid var(--surface-border); padding-top: 0.5rem; }
.section-label { font-size: 0.75rem; font-weight: 600; color: var(--text-color-secondary); margin: 0.4rem 0 0.2rem; text-transform: uppercase; }
.text-muted { color: var(--text-color-secondary); }
.request-meta { display: flex; flex-direction: column; gap: 0.35rem; font-size: 0.82rem; }
.run-id { font-family: monospace; font-size: 0.75rem; }
.error-text { cursor: help; }
.mb-2 { margin-bottom: 0.5rem; }
</style>
