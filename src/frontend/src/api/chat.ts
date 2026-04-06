// src/api/chat.ts
import { apiClient } from './client'
import type { ChatSession, ChatMessage, ChatStreamEvent } from './types'

const BASE_URL = import.meta.env.VITE_API_BASE_URL ?? ''

export const chatApi = {
  getSessions: () => apiClient.get<ChatSession[]>('/chat/sessions').then(r => r.data),

  createSession: () => apiClient.post<{ id: string; name: string }>('/chat/sessions').then(r => r.data),

  deleteSession: (id: string) => apiClient.delete(`/chat/sessions/${id}`),

  getMessages: (sessionId: string) =>
    apiClient.get<ChatMessage[]>(`/chat/sessions/${sessionId}/messages`).then(r => r.data),

  /**
   * SSE streaming: posts a message and returns an EventSource-like async iterator.
   * Uses fetch + ReadableStream so we can POST with a body (EventSource doesn't support POST).
   */
  streamMessage: async function* (
    sessionId: string,
    message: string,
    modelName: string | undefined,
    connectionNames: string[],
    signal?: AbortSignal,
    promptName?: string,
    promptInvocationParams?: string,
  ): AsyncGenerator<ChatStreamEvent> {
    const url = `${BASE_URL}/api/v1/chat/sessions/${sessionId}/messages`
    const response = await fetch(url, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Accept: 'text/event-stream' },
      body: JSON.stringify({ message, modelName, connectionNames, promptName, promptInvocationParams }),
      signal,
    })

    if (!response.ok) throw new Error(`Chat API error: ${response.status}`)
    if (!response.body) throw new Error('No response body')

    const reader = response.body.getReader()
    const decoder = new TextDecoder()
    let buffer = ''

    while (true) {
      const { done, value } = await reader.read()
      if (done) break
      buffer += decoder.decode(value, { stream: true })

      // Parse SSE frames
      const frames = buffer.split('\n\n')
      buffer = frames.pop() ?? ''

      for (const frame of frames) {
        if (!frame.trim()) continue
        const lines = frame.split('\n')
        let eventType = 'message'
        let data = ''
        for (const line of lines) {
          if (line.startsWith('event: ')) eventType = line.slice(7).trim()
          else if (line.startsWith('data: ')) data = line.slice(6)
        }
        if (data) {
          try {
            const parsed = JSON.parse(data) as ChatStreamEvent
            parsed.type = eventType as ChatStreamEvent['type']
            yield parsed
          } catch {
            // skip malformed frames
          }
        }
      }
    }
  },
}
