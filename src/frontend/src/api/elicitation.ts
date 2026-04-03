// src/api/elicitation.ts
import { apiClient } from './client'
import type { ElicitationRequest, ElicitationHistoryEntry } from './types'

const BASE_URL = import.meta.env.VITE_API_BASE_URL ?? ''

export const elicitationApi = {
  getPending: (connectionName?: string) => {
    const params = connectionName ? { connectionName } : {}
    return apiClient.get<ElicitationRequest[]>('/elicitation/pending', { params }).then(r => r.data)
  },

  respond: (requestId: string, action: string, content?: Record<string, unknown>) =>
    apiClient.post(`/elicitation/${requestId}/respond`, { action, content }),

  getHistory: (connectionName?: string) => {
    const params = connectionName ? { connectionName } : {}
    return apiClient.get<ElicitationHistoryEntry[]>('/elicitation/history', { params }).then(r => r.data)
  },

  /**
   * SSE stream for live elicitation requests.
   * Returns an EventSource connected to /api/v1/elicitation/stream.
   * Caller is responsible for closing it.
   */
  createStream: (onRequest: (req: ElicitationRequest) => void, onError?: (e: Event) => void): EventSource => {
    const es = new EventSource(`${BASE_URL}/api/v1/elicitation/stream`)
    es.addEventListener('elicitation_request', (e: MessageEvent) => {
      try {
        onRequest(JSON.parse(e.data) as ElicitationRequest)
      } catch {
        // ignore malformed events
      }
    })
    if (onError) es.onerror = onError
    return es
  },
}
