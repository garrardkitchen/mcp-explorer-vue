// src/stores/chat.ts
import { defineStore } from 'pinia'
import { ref } from 'vue'
import { chatApi } from '@/api/chat'
import type { ChatSession, ChatMessage, ChatTokenUsage } from '@/api/types'

export const useChatStore = defineStore('chat', () => {
  const sessions = ref<ChatSession[]>([])
  const activeSessionId = ref<string | null>(null)
  const messages = ref<ChatMessage[]>([])
  const streaming = ref(false)
  const streamingContent = ref('')
  const thinkingMs = ref(0)
  const error = ref<string | null>(null)
  const lastUsage = ref<ChatTokenUsage | null>(null)

  let _thinkingInterval: ReturnType<typeof setInterval> | null = null
  let _streamAbort: AbortController | null = null

  function sortSessions(list: ChatSession[]) {
    return [...list].sort((a, b) =>
      new Date(b.lastActivityUtc).getTime() - new Date(a.lastActivityUtc).getTime()
    )
  }

  async function loadSessions() {
    const raw = await chatApi.getSessions()
    sessions.value = sortSessions(raw)
  }

  async function createSession(): Promise<string> {
    const s = await chatApi.createSession()
    await loadSessions()
    return s.id
  }

  async function deleteSession(id: string) {
    await chatApi.deleteSession(id)
    sessions.value = sessions.value.filter(s => s.id !== id)
    if (activeSessionId.value === id) {
      activeSessionId.value = null
      messages.value = []
    }
  }

  async function selectSession(id: string) {
    activeSessionId.value = id
    messages.value = await chatApi.getMessages(id)
  }

  async function sendMessage(
    text: string,
    modelName: string | undefined,
    connectionNames: string[]
  ) {
    if (!activeSessionId.value) throw new Error('No active session')
    error.value = null
    streaming.value = true
    streamingContent.value = ''
    thinkingMs.value = 0
    lastUsage.value = null

    // Add user message optimistically
    const userMsg: ChatMessage = {
      id: crypto.randomUUID(),
      role: 'user',
      content: text,
      timestampUtc: new Date().toISOString(),
    }
    messages.value.push(userMsg)

    // Start "thinking" timer (time to first token)
    const startMs = Date.now()
    let firstToken = false
    _thinkingInterval = setInterval(() => {
      if (!firstToken) thinkingMs.value = Date.now() - startMs
    }, 100)

    _streamAbort = new AbortController()

    // Add assistant message placeholder — tool calls will be spliced before it
    const assistantMsg: ChatMessage = {
      id: crypto.randomUUID(),
      role: 'assistant',
      content: '',
      timestampUtc: new Date().toISOString(),
      modelName,
    }
    messages.value.push(assistantMsg)
    let assistantIdx = messages.value.length - 1

    try {
      for await (const evt of chatApi.streamMessage(
        activeSessionId.value,
        text,
        modelName,
        connectionNames,
        _streamAbort.signal
      )) {
        if (evt.type === 'token' && evt.text) {
          if (!firstToken) {
            firstToken = true
            const elapsed = Date.now() - startMs
            thinkingMs.value = elapsed
            messages.value[assistantIdx].thinkingMilliseconds = elapsed
            if (_thinkingInterval) { clearInterval(_thinkingInterval); _thinkingInterval = null }
          }
          messages.value[assistantIdx].content += evt.text
          streamingContent.value = messages.value[assistantIdx].content
        } else if (evt.type === 'tool-call') {
          // Insert a tool call message before the assistant placeholder
          const toolMsg: ChatMessage = {
            id: crypto.randomUUID(),
            role: 'system',
            content: `Calling ${evt.toolName}`,
            timestampUtc: new Date().toISOString(),
            toolCallName: evt.toolName,
            toolCallParameters: evt.toolParameters,
            connectionName: evt.connectionName,
          }
          messages.value.splice(assistantIdx, 0, toolMsg)
          assistantIdx++ // assistant is now one further back
        } else if (evt.type === 'usage' && evt.usage) {
          messages.value[assistantIdx].tokenUsage = evt.usage
          lastUsage.value = evt.usage
        } else if (evt.type === 'done') {
          if (evt.messageId) messages.value[assistantIdx].id = evt.messageId
        } else if (evt.type === 'error') {
          error.value = evt.errorMessage ?? 'Unknown streaming error'
        }
      }
    } catch (e: any) {
      if (e.name !== 'AbortError') error.value = e.message
    } finally {
      streaming.value = false
      streamingContent.value = ''
      if (_thinkingInterval) { clearInterval(_thinkingInterval); _thinkingInterval = null }
      // Refresh session list to update message count & lastActivityUtc
      await loadSessions()
    }
  }

  function cancelStream() {
    _streamAbort?.abort()
  }

  return {
    sessions, activeSessionId, messages, streaming,
    streamingContent, thinkingMs, error, lastUsage,
    loadSessions, createSession, deleteSession, selectSession, sendMessage, cancelStream,
  }
})
