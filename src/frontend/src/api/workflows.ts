// src/api/workflows.ts
import { apiClient } from './client'
import type { WorkflowDefinition, WorkflowExecution, LoadTestResult } from './types'

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

  runLoadTest: (id: string, connectionName: string, durationSeconds: number, maxParallel: number, runtimeParameters?: Record<string, string>) =>
    apiClient.post<LoadTestResult>(`/workflows/${id}/load-test`, { connectionName, durationSeconds, maxParallel, runtimeParameters }).then(r => r.data),

  exportToJson: (id: string) =>
    apiClient.get<string>(`/workflows/export/${id}`).then(r => r.data),

  importFromJson: (json: string) =>
    apiClient.post<WorkflowDefinition>('/workflows/import', json).then(r => r.data),
}
