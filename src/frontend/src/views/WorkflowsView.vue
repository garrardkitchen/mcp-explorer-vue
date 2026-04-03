<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useToast } from 'primevue/usetoast'
import { useConfirm } from 'primevue/useconfirm'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Button from 'primevue/button'
import InputText from 'primevue/inputtext'
import Textarea from 'primevue/textarea'
import Dialog from 'primevue/dialog'
import Select from 'primevue/select'
import Tag from 'primevue/tag'
import Skeleton from 'primevue/skeleton'
import ConfirmDialog from 'primevue/confirmdialog'
import Tabs from 'primevue/tabs'
import TabList from 'primevue/tablist'
import Tab from 'primevue/tab'
import TabPanels from 'primevue/tabpanels'
import TabPanel from 'primevue/tabpanel'
import ProgressBar from 'primevue/progressbar'
import JsonViewer from '@/components/common/JsonViewer.vue'
import WorkflowExecutionViewer from '@/components/workflows/WorkflowExecutionViewer.vue'
import { workflowsApi } from '@/api/workflows'
import { connectionsApi } from '@/api/connections'
import { useConnectionsStore } from '@/stores/connections'
import type { WorkflowDefinition, WorkflowStep, WorkflowExecution, LoadTestResult, ParameterMapping, MappingSourceType, ArrayIterationMode, ActiveTool } from '@/api/types'

const toast = useToast()
const confirm = useConfirm()
const connStore = useConnectionsStore()

const workflows = ref<WorkflowDefinition[]>([])
const loading = ref(false)
const selectedWorkflow = ref<WorkflowDefinition | null>(null)
const activeTab = ref('0')

// Edit dialog
const showEditDialog = ref(false)
const editMode = ref(false)
const saving = ref(false)
const form = ref<Partial<WorkflowDefinition>>({
  name: '', description: '', defaultConnectionName: '', steps: [],
})

// Execute
const executing = ref(false)
const execConnName = ref('')
const execResult = ref<WorkflowExecution | null>(null)
const execError = ref<string | null>(null)

// History
const history = ref<WorkflowExecution[]>([])
const historyLoading = ref(false)
const selectedExecution = ref<WorkflowExecution | null>(null)

// Load test
const showLoadTest = ref(false)
const loadTestConnName = ref('')
const loadTestDuration = ref(10)
const loadTestParallel = ref(5)
const loadTestRunning = ref(false)
const loadTestResult = ref<LoadTestResult | null>(null)

// Import/Export
const fileInputRef = ref<HTMLInputElement>()

// Property path browser
const showPathBrowser = ref(false)
const pathBrowserPaths = ref<string[]>([])
const pathBrowserLoading = ref(false)
const activeMappingForPath = ref<ParameterMapping | null>(null)

function flattenJsonPaths(obj: unknown, prefix = ''): string[] {
  if (obj === null || obj === undefined) return []
  const paths: string[] = []
  if (Array.isArray(obj)) {
    if (obj.length > 0) {
      paths.push(`${prefix}[]`)
      const elementPaths = flattenJsonPaths(obj[0], `${prefix}[]`)
      paths.push(...elementPaths)
    }
  } else if (typeof obj === 'object') {
    for (const [key, val] of Object.entries(obj as Record<string, unknown>)) {
      const fullKey = prefix ? `${prefix}.${key}` : key
      paths.push(fullKey)
      if (typeof val === 'object' && val !== null) {
        paths.push(...flattenJsonPaths(val, fullKey))
      }
    }
  }
  return paths
}

async function openPathBrowser(mapping: ParameterMapping, step: WorkflowStep) {
  activeMappingForPath.value = mapping
  pathBrowserPaths.value = []
  pathBrowserLoading.value = true
  showPathBrowser.value = true
  try {
    // step.stepNumber is 1-based; previous step is at 0-based index (stepNumber - 2)
    const prevStepIdx = mapping.sourceStepIndex === null || mapping.sourceStepIndex === undefined
      ? step.stepNumber - 2
      : Number(mapping.sourceStepIndex)
    if (prevStepIdx < 0) {
      pathBrowserLoading.value = false
      return
    }

    let prevOutputJson: string | undefined

    // 1. Try from live execResult first
    if (execResult.value?.stepResults?.[prevStepIdx]) {
      prevOutputJson = execResult.value.stepResults[prevStepIdx].outputJson
    }
    // 2. Fall back to most recent history entry
    if (!prevOutputJson && history.value.length > 0) {
      prevOutputJson = history.value[0].stepResults?.[prevStepIdx]?.outputJson
    }
    // 3. Fall back to a live sample invocation of the previous step's tool
    if (!prevOutputJson) {
      const prevStep = form.value.steps?.[prevStepIdx]
      const connName = execConnName.value || form.value.defaultConnectionName
      if (prevStep?.toolName && connName) {
        // Build params from ManualValue mappings only (safe — no side-effectful upstream steps needed)
        const params: Record<string, unknown> = {}
        for (const m of prevStep.parameterMappings ?? []) {
          if (m.sourceType === 'ManualValue' && m.targetParameter && m.manualValue != null) {
            params[m.targetParameter] = m.manualValue
          }
        }
        const toolResp = await connectionsApi.invokeTool(connName, prevStep.toolName, params)
        prevOutputJson = toolResp.result
      }
    }

    if (prevOutputJson) {
      const parsed = JSON.parse(prevOutputJson)
      pathBrowserPaths.value = flattenJsonPaths(parsed)
    }
  } catch {
    // silently ignore — show empty state with helpful message
  } finally {
    pathBrowserLoading.value = false
  }
}

function selectPath(path: string) {
  if (activeMappingForPath.value) {
    activeMappingForPath.value.sourcePropertyPath = path
  }
  showPathBrowser.value = false
}

const errorModeOptions = [
  { label: 'Stop on Error', value: 'StopOnError' },
  { label: 'Continue on Error', value: 'ContinueOnError' },
]

const sourceModeOptions = [
  { label: 'Manual Value', value: 'ManualValue' },
  { label: 'From Previous Step', value: 'FromPreviousStep' },
  { label: 'Prompt at Runtime', value: 'PromptAtRuntime' },
]

const iterationModeOptions = [
  { label: 'None (as-is)', value: 'None' },
  { label: 'Each – iterate all elements', value: 'Each' },
  { label: 'First element only', value: 'First' },
  { label: 'Last element only', value: 'Last' },
]

function addMapping(step: WorkflowStep) {
  step.parameterMappings.push({
    targetParameter: '',
    sourceType: 'ManualValue',
    sourceStepIndex: null,
    sourcePropertyPath: null,
    manualValue: null,
    iterationMode: 'None'
  })
}

function removeMapping(step: WorkflowStep, idx: number) {
  step.parameterMappings.splice(idx, 1)
}

function pathContainsArray(path?: string | null) {
  return !!path && /\[/.test(path)
}

const connectedConnections = computed(() => connStore.activeConnections.filter(c => c.isConnected))

// Tool objects per connection name — stores full ActiveTool so we can extract parameter names
const connectionToolsMap = ref<Record<string, ActiveTool[]>>({})
const toolsLoading = ref(false)

async function fetchConnectionTools(connectionName: string) {
  if (!connectionName) return
  // Only skip if we already have a non-empty cached list — errors are NOT cached
  if (connectionToolsMap.value[connectionName]?.length) return
  toolsLoading.value = true
  try {
    const tools = await connectionsApi.getTools(connectionName)
    connectionToolsMap.value[connectionName] = tools.sort((a, b) => a.name.localeCompare(b.name))
  } catch {
    // Don't cache failures — the connection may not be connected yet; allow re-fetch next time
  } finally {
    toolsLoading.value = false
  }
}

const currentConnectionToolNames = computed(() => {
  const name = form.value.defaultConnectionName
  return name ? (connectionToolsMap.value[name]?.map(t => t.name) ?? []) : []
})

function getToolParamNames(connectionName: string | undefined, toolName: string): string[] {
  if (!connectionName || !toolName) return []
  const tool = connectionToolsMap.value[connectionName]?.find(t => t.name === toolName)
  if (!tool?.inputSchema) return []
  const props = (tool.inputSchema as any)?.properties
  return props ? Object.keys(props).sort() : []
}

// When the default connection changes in the edit dialog, load its tools
watch(() => form.value.defaultConnectionName, (name) => {
  if (name && showEditDialog.value) fetchConnectionTools(name)
})

async function load() {
  loading.value = true
  try {
    workflows.value = await workflowsApi.getAll()
    await connStore.loadActive()
  } catch (e: any) {
    toast.add({ severity: 'error', summary: 'Failed to load workflows', detail: e.message, life: 5000 })
  } finally { loading.value = false }
}

function openCreate() {
  editMode.value = false
  form.value = { name: '', description: '', defaultConnectionName: '', steps: [] }
  showEditDialog.value = true
}

function openEdit(wf: WorkflowDefinition) {
  editMode.value = true
  form.value = JSON.parse(JSON.stringify(wf))
  // Normalise stepNumbers to 1-based for the UI (stored as 0-based in settings.json)
  form.value.steps?.forEach((s, i) => { s.stepNumber = i + 1 })
  showEditDialog.value = true
  if (form.value.defaultConnectionName) fetchConnectionTools(form.value.defaultConnectionName)
}

async function save() {
  if (!form.value.name?.trim()) {
    toast.add({ severity: 'warn', summary: 'Validation', detail: 'Name is required', life: 3000 }); return
  }
  saving.value = true
  try {
    if (editMode.value && form.value.id) {
      await workflowsApi.update(form.value.id, form.value as WorkflowDefinition)
      toast.add({ severity: 'success', summary: 'Updated', life: 2000 })
    } else {
      await workflowsApi.create(form.value)
      toast.add({ severity: 'success', summary: 'Created', life: 2000 })
    }
    showEditDialog.value = false; await load()
  } catch (e: any) {
    toast.add({ severity: 'error', summary: 'Save failed', detail: e.message, life: 5000 })
  } finally { saving.value = false }
}

function confirmDelete(wf: WorkflowDefinition) {
  confirm.require({
    message: `Delete workflow "${wf.name}"?`,
    header: 'Confirm Delete', icon: 'pi pi-trash',
    rejectProps: { label: 'Cancel', severity: 'secondary' },
    acceptProps: { label: 'Delete', severity: 'danger' },
    accept: async () => {
      try { await workflowsApi.delete(wf.id); await load(); toast.add({ severity: 'success', summary: 'Deleted', life: 2000 }) }
      catch (e: any) { toast.add({ severity: 'error', summary: 'Delete failed', detail: e.message, life: 5000 }) }
    },
  })
}

async function selectWorkflow(wf: WorkflowDefinition) {
  selectedWorkflow.value = wf
  execConnName.value = wf.defaultConnectionName ?? ''
  execResult.value = null; execError.value = null
  activeTab.value = '0'
  await loadHistory(wf.id)
}

async function loadHistory(id: string) {
  historyLoading.value = true
  try { history.value = await workflowsApi.getHistory(id) }
  catch { history.value = [] }
  finally { historyLoading.value = false }
}

async function execute() {
  if (!selectedWorkflow.value) return
  executing.value = true; execResult.value = null; execError.value = null
  try {
    const r = await workflowsApi.execute(selectedWorkflow.value.id, execConnName.value)
    execResult.value = r
    await loadHistory(selectedWorkflow.value.id)
    toast.add({ severity: r.status === 'Completed' ? 'success' : 'warn', summary: `Execution ${r.status}`, life: 3000 })
  } catch (e: any) {
    execError.value = e.message
    toast.add({ severity: 'error', summary: 'Execution failed', detail: e.message, life: 5000 })
  } finally { executing.value = false }
}

async function runLoadTest() {
  if (!selectedWorkflow.value) return
  loadTestRunning.value = true; loadTestResult.value = null
  try {
    loadTestResult.value = await workflowsApi.runLoadTest(selectedWorkflow.value.id, loadTestConnName.value, loadTestDuration.value, loadTestParallel.value)
    toast.add({ severity: 'success', summary: 'Load test complete', life: 2000 })
  } catch (e: any) {
    toast.add({ severity: 'error', summary: 'Load test failed', detail: e.message, life: 5000 })
  } finally { loadTestRunning.value = false }
}

// Steps builder
function addStep() {
  if (!form.value.steps) form.value.steps = []
  form.value.steps.push({ stepNumber: form.value.steps.length + 1, toolName: '', parameterMappings: [], errorHandling: 'StopOnError' })
}
function removeStep(idx: number) { form.value.steps?.splice(idx, 1); renumberSteps() }
function renumberSteps() { form.value.steps?.forEach((s, i) => s.stepNumber = i + 1) }
function moveStep(idx: number, dir: 1 | -1) {
  const steps = form.value.steps!
  const to = idx + dir
  if (to < 0 || to >= steps.length) return;
  [steps[idx], steps[to]] = [steps[to], steps[idx]]; renumberSteps()
}

// Export/Import
async function exportWorkflow(wf: WorkflowDefinition) {
  try {
    const json = await workflowsApi.exportToJson(wf.id)
    const blob = new Blob([typeof json === 'string' ? json : JSON.stringify(json, null, 2)], { type: 'application/json' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a'); a.href = url; a.download = `${wf.name}.json`; a.click(); URL.revokeObjectURL(url)
    toast.add({ severity: 'success', summary: 'Exported', life: 2000 })
  } catch (e: any) { toast.add({ severity: 'error', summary: 'Export failed', detail: e.message, life: 5000 }) }
}

function triggerImport() { fileInputRef.value?.click() }
async function onImportFile(e: Event) {
  const file = (e.target as HTMLInputElement).files?.[0]; if (!file) return
  try {
    const text = await file.text()
    await workflowsApi.importFromJson(text)
    await load(); toast.add({ severity: 'success', summary: 'Imported', life: 2000 })
  } catch (e: any) { toast.add({ severity: 'error', summary: 'Import failed', detail: e.message, life: 5000 }) }
  finally { if (fileInputRef.value) fileInputRef.value.value = '' }
}

function statusSeverity(s: string) {
  return s === 'Completed' ? 'success' : s === 'Failed' ? 'danger' : s === 'PartiallyCompleted' ? 'warn' : 'secondary'
}

function formatDuration(d: string | undefined) {
  if (!d) return '—'
  return d
}

onMounted(load)
</script>

<template>
  <div class="workflows-view">
    <!-- Toolbar -->
    <div class="toolbar">
      <span class="toolbar-title">Workflows</span>
      <div class="toolbar-actions">
        <Button label="Import" icon="pi pi-upload" text size="small" @click="triggerImport" />
        <Button label="New Workflow" icon="pi pi-plus" size="small" @click="openCreate" />
        <input ref="fileInputRef" type="file" accept=".json" hidden @change="onImportFile" />
      </div>
    </div>

    <div class="main-layout">
      <!-- Workflow list -->
      <div class="workflow-list-panel">
        <div v-if="loading" class="p-3"><Skeleton v-for="i in 4" :key="i" height="56px" class="mb-2" /></div>
        <div v-else-if="workflows.length === 0" class="empty-panel">
          <i class="pi pi-sitemap" /><p>No workflows yet</p>
          <Button label="Create one" size="small" @click="openCreate" />
        </div>
        <div v-else class="w-list">
          <div v-for="wf in workflows" :key="wf.id" class="w-item"
               :class="{ active: selectedWorkflow?.id === wf.id }" @click="selectWorkflow(wf)">
            <div class="w-info">
              <span class="w-name">{{ wf.name }}</span>
              <span class="w-meta">{{ wf.steps.length }} steps</span>
              <span v-if="wf.description" class="w-desc">{{ wf.description }}</span>
            </div>
            <div class="w-actions" @click.stop>
              <Button icon="pi pi-pencil" text size="small" v-tooltip="'Edit'" @click="openEdit(wf)" />
              <Button icon="pi pi-download" text size="small" v-tooltip="'Export'" @click="exportWorkflow(wf)" />
              <Button icon="pi pi-trash" text size="small" severity="danger" v-tooltip="'Delete'" @click="confirmDelete(wf)" />
            </div>
          </div>
        </div>
      </div>

      <!-- Detail panel -->
      <div class="workflow-detail-panel">
        <div v-if="!selectedWorkflow" class="empty-panel">
          <i class="pi pi-sitemap" /><p>Select a workflow</p>
        </div>
        <template v-else>
          <Tabs v-model:value="activeTab">
            <TabList>
              <Tab value="0">Execute</Tab>
              <Tab value="1">History</Tab>
              <Tab value="2">Load Test</Tab>
            </TabList>
            <TabPanels>
              <!-- Execute -->
              <TabPanel value="0">
                <div class="tab-content">
                  <div class="exec-form">
                    <div class="form-field">
                      <label>Connection</label>
                      <Select v-model="execConnName" :options="connectedConnections" optionLabel="name" optionValue="name"
                              placeholder="Select connection" style="width:100%" />
                    </div>
                  </div>

                  <div class="steps-preview">
                    <div class="section-label">Steps</div>
                    <div v-for="step in selectedWorkflow.steps" :key="step.stepNumber" class="step-card">
                      <span class="step-num">{{ step.stepNumber }}</span>
                      <div class="step-body">
                        <span class="step-tool">{{ step.toolName }}</span>
                        <Tag :value="step.errorHandling" severity="secondary" />
                      </div>
                    </div>
                  </div>

                  <div class="action-bar">
                    <Button label="Execute" icon="pi pi-play" :loading="executing" :disabled="!execConnName" @click="execute" />
                  </div>

                  <div v-if="execError" class="error-box"><i class="pi pi-times-circle" /> {{ execError }}</div>

                  <div v-if="execResult" class="exec-result">
                    <WorkflowExecutionViewer :execution="execResult" />
                  </div>
                </div>
              </TabPanel>

              <!-- History -->
              <TabPanel value="1">
                <div class="tab-content">
                  <div v-if="historyLoading" class="p-3"><Skeleton v-for="i in 3" :key="i" height="48px" class="mb-2" /></div>
                  <div v-else-if="history.length === 0" class="empty-panel" style="min-height:200px"><p>No executions yet</p></div>
                  <div v-else>
                    <div v-for="exec in history" :key="exec.id" class="hist-item"
                         :class="{ active: selectedExecution?.id === exec.id }" @click="selectedExecution = exec">
                      <Tag :value="exec.status" :severity="statusSeverity(exec.status)" />
                      <span class="hist-time">{{ new Date(exec.startedUtc).toLocaleString() }}</span>
                      <span class="hist-conn">{{ exec.connectionName }}</span>
                      <span class="muted">{{ formatDuration(exec.duration) }}</span>
                    </div>
                    <div v-if="selectedExecution" class="exec-detail">
                      <WorkflowExecutionViewer :execution="selectedExecution" />
                    </div>
                  </div>
                </div>
              </TabPanel>

              <!-- Load Test -->
              <TabPanel value="2">
                <div class="tab-content">
                  <div class="load-test-form">
                    <div class="form-field">
                      <label>Connection</label>
                      <Select v-model="loadTestConnName" :options="connectedConnections" optionLabel="name" optionValue="name" placeholder="Select connection" style="width:100%" />
                    </div>
                    <div class="form-field">
                      <label>Duration (seconds)</label>
                      <input v-model.number="loadTestDuration" type="number" min="1" max="300" class="native-num-input" />
                    </div>
                    <div class="form-field">
                      <label>Max Parallel</label>
                      <input v-model.number="loadTestParallel" type="number" min="1" max="50" class="native-num-input" />
                    </div>
                    <Button label="Run Load Test" icon="pi pi-chart-bar" :loading="loadTestRunning" :disabled="!loadTestConnName" @click="runLoadTest" />
                  </div>
                  <div v-if="loadTestResult" class="load-test-results">
                    <div class="section-label">Results</div>
                    <div class="stats-grid">
                      <div class="stat-card"><div class="stat-value">{{ loadTestResult.totalRequests }}</div><div class="stat-label">Total</div></div>
                      <div class="stat-card success"><div class="stat-value">{{ loadTestResult.successfulRequests }}</div><div class="stat-label">Success</div></div>
                      <div class="stat-card danger"><div class="stat-value">{{ loadTestResult.failedRequests }}</div><div class="stat-label">Failed</div></div>
                      <div class="stat-card"><div class="stat-value">{{ loadTestResult.requestsPerSecond.toFixed(1) }}</div><div class="stat-label">req/s</div></div>
                      <div class="stat-card"><div class="stat-value">{{ loadTestResult.averageResponseMs.toFixed(0) }}ms</div><div class="stat-label">Avg</div></div>
                      <div class="stat-card"><div class="stat-value">{{ loadTestResult.p99ResponseMs.toFixed(0) }}ms</div><div class="stat-label">P99</div></div>
                    </div>
                    <div class="error-rate">
                      Error rate: <strong :style="loadTestResult.errorRate > 0.05 ? 'color:var(--danger)' : 'color:var(--success)'">{{ (loadTestResult.errorRate * 100).toFixed(1) }}%</strong>
                    </div>
                  </div>
                </div>
              </TabPanel>
            </TabPanels>
          </Tabs>
        </template>
      </div>
    </div>

    <!-- Edit dialog -->
    <Dialog v-model:visible="showEditDialog" :header="editMode ? 'Edit Workflow' : 'New Workflow'" modal :style="{ width: '720px', maxHeight: '90vh' }">
      <div class="edit-form">
        <div class="form-row">
          <div class="form-field">
            <label>Name *</label>
            <InputText v-model="form.name" class="w-full" />
          </div>
          <div class="form-field">
            <label>Default Connection</label>
            <Select v-model="form.defaultConnectionName" :options="connectedConnections" optionLabel="name" optionValue="name" placeholder="Optional" class="w-full" />
          </div>
        </div>
        <div class="form-field">
          <label>Description</label>
          <Textarea v-model="form.description" rows="2" class="w-full" autoResize />
        </div>

        <div class="steps-editor">
          <div class="section-header">
            <span class="section-label">Steps</span>
            <Button label="Add Step" icon="pi pi-plus" text size="small" @click="addStep" />
          </div>
          <div v-if="!form.steps?.length" class="muted-sm">No steps yet. Add the first step.</div>
          <div v-for="(step, idx) in form.steps" :key="idx" class="step-editor-card">
            <div class="step-editor-header">
              <span class="step-num">{{ step.stepNumber }}</span>
              <div class="step-order-btns">
                <Button icon="pi pi-chevron-up" text size="small" :disabled="idx === 0" @click="moveStep(idx, -1)" />
                <Button icon="pi pi-chevron-down" text size="small" :disabled="idx === (form.steps?.length ?? 0) - 1" @click="moveStep(idx, 1)" />
              </div>
              <Button icon="pi pi-times" text size="small" severity="danger" @click="removeStep(idx)" />
            </div>
            <div class="step-editor-body">
              <div class="form-field">
                <label>Tool Name</label>
                <Select v-if="currentConnectionToolNames.length > 0"
                        v-model="step.toolName"
                        :options="currentConnectionToolNames"
                        editable
                        placeholder="Select or type tool name"
                        class="w-full"
                        :loading="toolsLoading" />
                <InputText v-else v-model="step.toolName" placeholder="e.g. get_weather (connect first to browse)" class="w-full" />
              </div>
              <div class="form-field">
                <label>Error Handling</label>
                <Select v-model="step.errorHandling" :options="errorModeOptions" optionLabel="label" optionValue="value" class="w-full" />
              </div>
              <div v-if="step.notes !== undefined" class="form-field full-width">
                <label>Notes</label>
                <InputText v-model="step.notes" class="w-full" />
              </div>
            </div>

            <!-- Parameter Mappings -->
            <div class="param-mappings-section">
              <div class="param-mappings-header">
                <span class="section-label">Parameter Mappings</span>
                <Button label="Add Mapping" icon="pi pi-plus" text size="small" @click="addMapping(step)" />
              </div>
              <div v-if="step.parameterMappings.length === 0" class="no-mappings-hint">
                No mappings — add mappings to pass data between steps or set fixed values.
              </div>
              <div v-for="(mapping, mIdx) in step.parameterMappings" :key="mIdx" class="mapping-row">
                <div class="mapping-fields">
                  <!-- Target parameter -->
                  <div class="form-field mapping-field">
                    <label>Parameter</label>
                    <Select v-if="getToolParamNames(form.defaultConnectionName, step.toolName).length > 0"
                            v-model="mapping.targetParameter"
                            :options="getToolParamNames(form.defaultConnectionName, step.toolName)"
                            editable
                            placeholder="Select parameter"
                            class="w-full" size="small" />
                    <InputText v-else v-model="mapping.targetParameter" placeholder="e.g. userId" class="w-full" size="small" />
                  </div>
                  <!-- Source type -->
                  <div class="form-field mapping-field">
                    <label>Source</label>
                    <Select v-model="mapping.sourceType" :options="sourceModeOptions" optionLabel="label" optionValue="value" class="w-full" size="small" />
                  </div>
                  <!-- Manual value -->
                  <div v-if="mapping.sourceType === 'ManualValue'" class="form-field mapping-field full-mapping-width">
                    <label>Value</label>
                    <InputText v-model="mapping.manualValue" placeholder="Enter value" class="w-full" size="small" />
                  </div>
                   <!-- From previous step -->
                  <template v-if="mapping.sourceType === 'FromPreviousStep'">
                    <div class="form-field mapping-field">
                      <label>Step</label>
                      <Select
                        :model-value="mapping.sourceStepIndex === null || mapping.sourceStepIndex === undefined ? '__auto__' : String(mapping.sourceStepIndex)"
                        @update:model-value="v => mapping.sourceStepIndex = v === '__auto__' ? null : Number(v)"
                        :options="[{ label: 'Previous step (auto)', value: '__auto__' }, ...Array.from({length: step.stepNumber - 1}, (_, i) => ({ label: `Step ${i + 1}`, value: String(i) }))]"
                        optionLabel="label" optionValue="value" class="w-full" size="small" />
                    </div>
                    <div class="form-field mapping-field full-mapping-width">
                      <label>Property Path</label>
                      <div class="path-input-row">
                        <InputText v-model="mapping.sourcePropertyPath" placeholder="e.g. data[0].id or data[].id" class="w-full" size="small" />
                        <Button icon="pi pi-sitemap" text size="small" v-tooltip="'Browse output fields from previous step'" @click="openPathBrowser(mapping, step)" />
                      </div>
                    </div>
                    <div v-if="pathContainsArray(mapping.sourcePropertyPath)" class="form-field mapping-field full-mapping-width">
                      <label>Array Iteration</label>
                      <Select v-model="mapping.iterationMode" :options="iterationModeOptions" optionLabel="label" optionValue="value" class="w-full" size="small" />
                    </div>
                  </template>
                  <!-- Prompt at runtime hint -->
                  <div v-if="mapping.sourceType === 'PromptAtRuntime'" class="form-field mapping-field full-mapping-width">
                    <label>Runtime Key</label>
                    <InputText :model-value="mapping.targetParameter" disabled placeholder="Uses parameter name as key" class="w-full" size="small" />
                    <small class="muted-sm">Value will be prompted when executing the workflow.</small>
                  </div>
                </div>
                <Button icon="pi pi-times" text severity="danger" size="small" @click="removeMapping(step, mIdx)" />
              </div>
            </div>
          </div>
        </div>
      </div>
      <template #footer>
        <Button label="Cancel" severity="secondary" text @click="showEditDialog = false" />
        <Button :label="editMode ? 'Save' : 'Create'" icon="pi pi-check" :loading="saving" @click="save" />
      </template>
    </Dialog>

    <ConfirmDialog />

    <!-- Property path browser dialog -->
    <Dialog v-model:visible="showPathBrowser" header="Browse Output Fields" modal :style="{ width: '480px' }">
      <div class="path-browser">
        <p class="muted-sm" style="margin-bottom:12px">
          Select a property path from the previous step's output. Use <code>[]</code> paths with <strong>Each</strong> iteration to loop over arrays.
        </p>
        <div v-if="pathBrowserLoading" class="path-browser-empty">Loading fields from tool…</div>
        <div v-else-if="pathBrowserPaths.length === 0" class="path-browser-empty">
          No output available. Ensure the connection is active and the previous step's tool name is set.
        </div>
        <div v-else class="path-list">
          <button
            v-for="path in pathBrowserPaths"
            :key="path"
            class="path-item"
            :class="{ 'is-array': path.includes('[]') }"
            @click="selectPath(path)"
          >
            <i :class="path.includes('[]') ? 'pi pi-list' : 'pi pi-key'" />
            <code>{{ path }}</code>
          </button>
        </div>
      </div>
    </Dialog>
  </div>
</template>

<style scoped>
.workflows-view { display:flex; flex-direction:column; height:100%; background:var(--bg-base); color:var(--text-primary); }
.toolbar { display:flex; align-items:center; justify-content:space-between; padding:12px 20px; background:var(--bg-surface); border-bottom:1px solid var(--border); flex-shrink:0; }
.toolbar-title { font-weight:600; font-size:15px; }
.toolbar-actions { display:flex; gap:8px; }
.main-layout { display:flex; flex:1; overflow:hidden; }
.workflow-list-panel { width:280px; flex-shrink:0; border-right:1px solid var(--border); display:flex; flex-direction:column; overflow:hidden; background:var(--bg-surface); }
.workflow-detail-panel { flex:1; overflow-y:auto; }
.empty-panel { display:flex; flex-direction:column; align-items:center; justify-content:center; gap:10px; flex:1; color:var(--text-muted); font-size:13px; text-align:center; padding:24px; }
.empty-panel i { font-size:32px; }
.w-list { overflow-y:auto; flex:1; }
.w-item { display:flex; align-items:flex-start; gap:8px; padding:12px 14px; cursor:pointer; border-left:2px solid transparent; border-bottom:1px solid var(--border); transition:var(--transition-fast); }
.w-item:hover { background:var(--nav-item-hover); }
.w-item.active { background:var(--nav-item-active); border-left-color:var(--accent); }
.w-info { flex:1; min-width:0; }
.w-name { display:block; font-size:13px; font-weight:500; color:var(--text-primary); }
.w-meta { display:block; font-size:11px; color:var(--text-muted); }
.w-desc { display:block; font-size:11px; color:var(--text-muted); white-space:nowrap; overflow:hidden; text-overflow:ellipsis; }
.w-actions { display:flex; gap:2px; opacity:0; transition:var(--transition-fast); }
.w-item:hover .w-actions { opacity:1; }
.tab-content { padding:16px 20px; }
.exec-form { margin-bottom:16px; }
.form-field { display:flex; flex-direction:column; gap:6px; margin-bottom:12px; }
.form-field label { font-size:12px; font-weight:500; color:var(--text-secondary); text-transform:uppercase; letter-spacing:.04em; }
.section-label { font-size:11px; font-weight:600; text-transform:uppercase; letter-spacing:.05em; color:var(--text-muted); margin-bottom:10px; }
.section-header { display:flex; align-items:center; justify-content:space-between; margin-bottom:10px; }
.steps-preview { margin-bottom:16px; }
.step-card { display:flex; align-items:center; gap:10px; padding:8px 10px; background:var(--bg-raised); border:1px solid var(--border); border-radius:var(--border-radius-sm); margin-bottom:6px; }
.step-num { width:24px; height:24px; display:flex; align-items:center; justify-content:center; background:var(--accent-muted); color:var(--accent); border-radius:50%; font-size:11px; font-weight:600; flex-shrink:0; }
.step-body { flex:1; display:flex; align-items:center; gap:8px; }
.step-tool { font-family:var(--font-family-mono); font-size:13px; color:var(--text-primary); flex:1; }
.action-bar { margin-bottom:16px; }
.error-box { padding:10px 14px; background:rgba(239,68,68,.1); border:1px solid var(--danger); border-radius:var(--border-radius-sm); color:var(--danger); font-size:13px; display:flex; gap:8px; margin-bottom:16px; }
.exec-result { }
.result-header { display:flex; align-items:center; gap:10px; margin-bottom:12px; }
.muted { color:var(--text-muted); font-size:12px; }
.step-result { background:var(--bg-raised); border:1px solid var(--border); border-radius:var(--border-radius-sm); margin-bottom:8px; overflow:hidden; }
.step-result.error { border-left:3px solid var(--danger); }
.step-result.success { border-left:3px solid var(--success); }
.sr-header { display:flex; align-items:center; gap:8px; padding:8px 12px; }
.sr-tool { flex:1; font-family:var(--font-family-mono); font-size:13px; }
.sr-error { padding:4px 12px 8px; color:var(--danger); font-size:12px; }
.sr-result-wrap { max-height:200px; overflow:auto; padding:8px 12px; }
.hist-item { display:flex; align-items:center; gap:10px; padding:10px 14px; border-bottom:1px solid var(--border); cursor:pointer; font-size:12px; }
.hist-item:hover { background:var(--bg-raised); }
.hist-item.active { background:var(--nav-item-active); }
.hist-time { color:var(--text-secondary); flex:1; }
.hist-conn { font-family:var(--font-family-mono); color:var(--text-muted); }
.exec-detail { margin-top:12px; }
.load-test-form { display:grid; grid-template-columns:1fr 1fr; gap:12px; align-items:end; margin-bottom:16px; }
.load-test-results { }
.stats-grid { display:grid; grid-template-columns:repeat(3,1fr); gap:10px; margin-bottom:12px; }
.stat-card { background:var(--bg-raised); border:1px solid var(--border); border-radius:var(--border-radius-sm); padding:12px; text-align:center; }
.stat-card.success { border-color:var(--success); }
.stat-card.danger { border-color:var(--danger); }
.stat-value { font-size:20px; font-weight:600; color:var(--text-primary); }
.stat-label { font-size:11px; color:var(--text-muted); margin-top:2px; }
.error-rate { font-size:13px; color:var(--text-secondary); }
/* Edit dialog */
.edit-form { display:flex; flex-direction:column; gap:12px; }
.form-row { display:grid; grid-template-columns:1fr 1fr; gap:12px; }
.w-full { width:100%; }
.steps-editor { border-top:1px solid var(--border); padding-top:12px; }
.muted-sm { color:var(--text-muted); font-size:12px; }
.step-editor-card { background:var(--bg-raised); border:1px solid var(--border); border-radius:var(--border-radius-sm); padding:12px; margin-bottom:10px; }
.step-editor-header { display:flex; align-items:center; gap:8px; margin-bottom:8px; }
.step-order-btns { display:flex; }
.step-editor-body { display:grid; grid-template-columns:1fr 1fr; gap:10px; }
.param-mappings-section { margin-top:10px; border-top:1px solid var(--border); padding-top:10px; }
.param-mappings-header { display:flex; align-items:center; justify-content:space-between; margin-bottom:6px; }
.no-mappings-hint { font-size:12px; color:var(--text-muted); padding:4px 0 8px; }
.mapping-row { display:flex; align-items:flex-start; gap:8px; padding:8px; background:var(--bg-surface); border:1px solid var(--border); border-radius:var(--border-radius-sm); margin-bottom:6px; }
.mapping-fields { flex:1; display:grid; grid-template-columns:1fr 1fr; gap:8px; }
.mapping-field { }
.full-mapping-width { grid-column:1 / -1; }
.native-num-input { width:100%; background:var(--bg-raised); border:1px solid var(--border); color:var(--text-primary); padding:8px 10px; border-radius:var(--border-radius-sm); font-size:14px; }
.native-num-input:focus { outline:none; border-color:var(--accent); }
.path-input-row { display:flex; align-items:center; gap:4px; }
.path-input-row .p-inputtext { flex:1; }
/* Property path browser */
.path-browser { }
.path-browser-empty { color:var(--text-muted); font-size:13px; padding:12px 0; text-align:center; }
.path-list { display:flex; flex-direction:column; gap:4px; max-height:380px; overflow-y:auto; }
.path-item { display:flex; align-items:center; gap:8px; padding:8px 10px; background:var(--bg-raised); border:1px solid var(--border); border-radius:var(--border-radius-sm); cursor:pointer; text-align:left; font-size:13px; transition:var(--transition-fast); }
.path-item:hover { background:var(--nav-item-hover); border-color:var(--accent); }
.path-item.is-array { border-left:2px solid var(--accent); }
.path-item i { color:var(--text-muted); font-size:11px; flex-shrink:0; }
.path-item.is-array i { color:var(--accent); }
.path-item code { font-family:var(--font-family-mono); font-size:12px; color:var(--text-primary); word-break:break-all; }
</style>
