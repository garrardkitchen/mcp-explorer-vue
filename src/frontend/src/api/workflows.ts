// src/api/workflows.ts
import { apiClient } from './client'
import type { WorkflowDefinition, WorkflowExecution, LoadTestResult, LoadTestProgress } from './types'

export const workflowsApi = {
  getAll: () => apiClient.get<WorkflowDefinition[]>('/workflows').then(r => r.data),

  create: (wf: Partial<WorkflowDefinition>) =>
    apiClient.post<WorkflowDefinition>('/workflows', wf).then(r => r.data),

  update: (id: string, wf: WorkflowDefinition) =>
    apiClient.put<WorkflowDefinition>(`/workflows/${id}`, wf).then(r => r.data),

  delete: (id: string) => apiClient.delete(`/workflows/${id}`),

  execute: (id: string, connectionName: string, runtimeParameters?: Record<string, string>) =>
    apiClient.post<WorkflowExecution>(`/workflows/${id}/execute`, { connectionName, runtimeParameters }).then(r => r.data),

  getHistory: (id: string) =>
    apiClient.get<WorkflowExecution[]>(`/workflows/${id}/history`).then(r => r.data),

  startLoadTest: (id: string, connectionName: string, durationSeconds: number, maxParallel: number, runtimeParameters?: Record<string, string>) =>
    apiClient.post<{ runId: string }>(`/workflows/${id}/load-test`, { connectionName, durationSeconds, maxParallel, runtimeParameters }).then(r => r.data),

  getLoadTestProgress: (runId: string) =>
    apiClient.get<LoadTestProgress>(`/workflows/load-test-progress/${runId}`).then(r => r.data),

  getLoadTestHistory: (id: string) =>
    apiClient.get<LoadTestResult[]>(`/workflows/${id}/load-test-history`).then(r => r.data),

  exportToJson: (id: string) =>
    apiClient.get<string>(`/workflows/export/${id}`).then(r => r.data),

  importFromJson: (workflow: unknown) =>
    apiClient.post<WorkflowDefinition>('/workflows/import', workflow).then(r => r.data),
}
