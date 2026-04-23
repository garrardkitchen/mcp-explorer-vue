import { apiClient } from './client'
import type {
  DevTunnel,
  DevTunnelUserState,
  ReplayWebhookResult,
  TunnelAccess,
  WebhookEvent,
} from './types'

const BASE_URL = import.meta.env.VITE_API_BASE_URL ?? ''

export const devTunnelsApi = {
  getAll: () => apiClient.get<DevTunnel[]>('/devtunnels').then(r => r.data),

  get: (tunnelId: string) => apiClient.get<DevTunnel>(`/devtunnels/${encodeURIComponent(tunnelId)}`).then(r => r.data),

  getUserState: () => apiClient.get<DevTunnelUserState>('/devtunnels/user').then(r => r.data),

  create: (payload: { name: string; access: TunnelAccess; deleteOnExit: boolean }) =>
    apiClient.post<DevTunnel>('/devtunnels', payload).then(r => r.data),

  start: (tunnelId: string) =>
    apiClient.post<DevTunnel>(`/devtunnels/${encodeURIComponent(tunnelId)}/start`).then(r => r.data),

  stop: (tunnelId: string) =>
    apiClient.post<DevTunnel>(`/devtunnels/${encodeURIComponent(tunnelId)}/stop`).then(r => r.data),

  remove: (tunnelId: string) =>
    apiClient.delete(`/devtunnels/${encodeURIComponent(tunnelId)}`),

  getEvents: (tunnelId: string, limit = 250) =>
    apiClient.get<WebhookEvent[]>(`/devtunnels/${encodeURIComponent(tunnelId)}/events`, { params: { limit } }).then(r => r.data),

  replay: (tunnelId: string, eventId: string, payload: {
    targetUrl: string
    methodOverride?: string
    headersOverride?: Record<string, string>
    bodyTextOverride?: string
    bodyBase64Override?: string
    contentTypeOverride?: string
  }) => apiClient.post<ReplayWebhookResult>(`/devtunnels/${encodeURIComponent(tunnelId)}/replay/${encodeURIComponent(eventId)}`, payload).then(r => r.data),

  createEventStream: (
    tunnelId: string,
    onEvent: (event: WebhookEvent) => void,
    onError?: (event: Event) => void,
    onOpen?: () => void,
  ): EventSource => {
    const es = new EventSource(`${BASE_URL}/api/v1/devtunnels/${encodeURIComponent(tunnelId)}/stream`)
    es.addEventListener('webhook_event', (e: MessageEvent) => {
      try {
        onEvent(JSON.parse(e.data) as WebhookEvent)
      } catch {
        // Ignore malformed stream events.
      }
    })
    if (onError) es.onerror = onError
    if (onOpen) es.onopen = onOpen
    return es
  },

  createLoginStream: (
    onLine: (line: string) => void,
    onComplete?: () => void,
    onError?: (event: Event) => void,
  ): EventSource => {
    const es = new EventSource(`${BASE_URL}/api/v1/devtunnels/login/stream`)
    es.addEventListener('login_line', (e: MessageEvent) => {
      try {
        const payload = JSON.parse(e.data) as { line: string }
        onLine(payload.line)
      } catch {
        // Ignore malformed stream events.
      }
    })
    es.addEventListener('login_complete', () => {
      onComplete?.()
      es.close()
    })
    if (onError) es.onerror = onError
    return es
  },
}
