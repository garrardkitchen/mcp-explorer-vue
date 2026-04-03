// src/api/connections.ts
import { apiClient } from './client'
import type { ConnectionDefinition, ConnectionGroup, ActiveConnection, ActiveTool, ActivePrompt, ActiveResource, ActiveResourceTemplate } from './types'

export const connectionsApi = {
  getAll: () => apiClient.get<ConnectionDefinition[]>('/connections').then(r => r.data),

  create: (def: Partial<ConnectionDefinition>) =>
    apiClient.post<ConnectionDefinition>('/connections', def).then(r => r.data),

  update: (name: string, def: Partial<ConnectionDefinition>) =>
    apiClient.put<ConnectionDefinition>(`/connections/${encodeURIComponent(name)}`, def).then(r => r.data),

  delete: (name: string) =>
    apiClient.delete(`/connections/${encodeURIComponent(name)}`),

  connect: (name: string) =>
    apiClient.post<ActiveConnection>(`/connections/${encodeURIComponent(name)}/connect`).then(r => r.data),

  disconnect: (name: string) =>
    apiClient.post(`/connections/${encodeURIComponent(name)}/disconnect`),

  getActive: () => apiClient.get<ActiveConnection[]>('/connections/active').then(r => r.data),

  getTools: (connectionName: string) =>
    apiClient.get<ActiveTool[]>(`/connections/${encodeURIComponent(connectionName)}/tools`).then(r => r.data),

  invokeTool: (connectionName: string, toolName: string, parameters: Record<string, unknown>) =>
    apiClient.post<{ result: string }>(`/connections/${encodeURIComponent(connectionName)}/tools/${encodeURIComponent(toolName)}/invoke`, parameters).then(r => r.data),

  getPrompts: (connectionName: string) =>
    apiClient.get<ActivePrompt[]>(`/connections/${encodeURIComponent(connectionName)}/prompts`).then(r => r.data),

  executePrompt: (connectionName: string, promptName: string, args: Record<string, string>) =>
    apiClient.post<{ result: string }>(`/connections/${encodeURIComponent(connectionName)}/prompts/${encodeURIComponent(promptName)}/execute`, args).then(r => r.data),

  getResources: (connectionName: string) =>
    apiClient.get<ActiveResource[]>(`/connections/${encodeURIComponent(connectionName)}/resources`).then(r => r.data),

  readResource: (connectionName: string, uri: string) =>
    apiClient.get<{ result: string }>(`/connections/${encodeURIComponent(connectionName)}/resources/${encodeURIComponent(uri)}`).then(r => r.data),

  getResourceTemplates: (connectionName: string) =>
    apiClient.get<ActiveResourceTemplate[]>(`/connections/${encodeURIComponent(connectionName)}/resource-templates`).then(r => r.data),

  readResourceTemplate: (connectionName: string, uriTemplate: string, params: Record<string, string>) =>
    apiClient.get<{ result: string }>(
      `/connections/${encodeURIComponent(connectionName)}/resource-templates/${encodeURIComponent(uriTemplate)}`,
      { params }
    ).then(r => r.data),

  getGroups: () => apiClient.get<ConnectionGroup[]>('/connections/groups').then(r => r.data),

  exportConnections: (names?: string[]) =>
    apiClient.post<{ data: string }>('/connections/export', { names }).then(r => r.data),

  importConnections: (data: string) =>
    apiClient.post<ConnectionDefinition[]>('/connections/import', { data }).then(r => r.data),
}
