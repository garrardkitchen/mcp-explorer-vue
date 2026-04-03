// src/api/llmModels.ts
import { apiClient } from './client'
import type { LlmModelDefinition } from './types'

export const llmModelsApi = {
  getAll: () => apiClient.get<LlmModelDefinition[]>('/llm-models').then(r => r.data),

  create: (model: LlmModelDefinition) =>
    apiClient.post<LlmModelDefinition>('/llm-models', model).then(r => r.data),

  update: (name: string, model: LlmModelDefinition) =>
    apiClient.put<LlmModelDefinition>(`/llm-models/${encodeURIComponent(name)}`, model).then(r => r.data),

  delete: (name: string) => apiClient.delete(`/llm-models/${encodeURIComponent(name)}`),

  getSelected: () =>
    apiClient.get<{ selectedModelName: string | null }>('/llm-models/selected').then(r => r.data),

  setSelected: (modelName: string) =>
    apiClient.put('/llm-models/selected', { modelName }),

  patchSystemPrompt: (name: string, systemPrompt: string) =>
    apiClient.patch(`/llm-models/${encodeURIComponent(name)}/system-prompt`, { systemPrompt }),
}
