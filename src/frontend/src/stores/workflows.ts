// src/stores/workflows.ts
import { defineStore } from 'pinia'
import { ref } from 'vue'
import { workflowsApi } from '@/api/workflows'
import type { WorkflowDefinition, WorkflowExecution, LoadTestResult } from '@/api/types'

export const useWorkflowsStore = defineStore('workflows', () => {
  const workflows = ref<WorkflowDefinition[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)
  const activeExecution = ref<WorkflowExecution | null>(null)
  const executionHistory = ref<Record<string, WorkflowExecution[]>>({})
  const loadTestResults = ref<Record<string, LoadTestResult>>({})

  async function loadAll() {
    loading.value = true
    try {
      workflows.value = await workflowsApi.getAll()
    } catch (e: any) {
      error.value = e.message
    } finally {
      loading.value = false
    }
  }

  async function create(def: Partial<WorkflowDefinition>): Promise<WorkflowDefinition> {
    const w = await workflowsApi.create(def)
    workflows.value.push(w)
    return w
  }

  async function update(id: string, def: Partial<WorkflowDefinition>): Promise<WorkflowDefinition> {
    const w = await workflowsApi.update(id, def as WorkflowDefinition)
    const idx = workflows.value.findIndex((x: WorkflowDefinition) => x.id === id)
    if (idx >= 0) workflows.value[idx] = w
    return w
  }

  async function deleteWorkflow(id: string) {
    await workflowsApi.delete(id)
    workflows.value = workflows.value.filter((w: WorkflowDefinition) => w.id !== id)
  }

  async function execute(id: string, connectionName = ''): Promise<WorkflowExecution> {
    const exec = await workflowsApi.execute(id, connectionName)
    activeExecution.value = exec
    return exec
  }

  async function getHistory(id: string): Promise<WorkflowExecution[]> {
    const history = await workflowsApi.getHistory(id)
    executionHistory.value[id] = history
    return history
  }

  async function runLoadTest(id: string, connectionName = '', concurrency: number, iterations: number): Promise<LoadTestResult> {
    const result = await workflowsApi.runLoadTest(id, connectionName, iterations, concurrency)
    loadTestResults.value[id] = result
    return result
  }

  return {
    workflows, loading, error, activeExecution, executionHistory, loadTestResults,
    loadAll, create, update, deleteWorkflow, execute, getHistory, runLoadTest,
  }
})
