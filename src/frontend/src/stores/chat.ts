// src/stores/chat.ts
import { defineStore } from 'pinia'
import { ref, nextTick } from 'vue'
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
    connectionNames: string[],
    promptName?: string,
    promptInvocationParams?: Record<string, string>,
  ) {
    if (!activeSessionId.value) throw new Error('No active session')
    error.value = null
    streaming.value = true
    streamingContent.value = ''
    thinkingMs.value = 0
    lastUsage.value = null

    const paramsJson = promptInvocationParams && Object.keys(promptInvocationParams).length
      ? JSON.stringify(promptInvocationParams)
      : undefined

    // Add user message optimistically — include prompt metadata so the timeline
    // renders the invocation block immediately before the server confirms it
    const userMsg: ChatMessage = {
      id: crypto.randomUUID(),
      role: 'user',
      content: text,
      timestampUtc: new Date().toISOString(),
      modelName,
      promptName,
      promptInvocationParams: paramsJson,
    }
    messages.value.push(userMsg)

    // Start "thinking" timer (time to first token)
    const startMs = Date.now()
    let firstToken = false
    _thinkingInterval = setInterval(() => {
      if (!firstToken) thinkingMs.value = Date.now() - startMs
    }, 100)

    _streamAbort = new AbortController()

    // Track accumulated data for the final assistant message
    let assistantId: string | null = null
    let assistantThinkingMs: number | null = null
    let assistantTokenUsage: ChatTokenUsage | null = null
    let assistantContent = ''

    try {
      for await (const evt of chatApi.streamMessage(
        activeSessionId.value,
        text,
        modelName,
        connectionNames,
        _streamAbort.signal,
        promptName,
        paramsJson,
      )) {
        if (evt.type === 'token' && evt.text) {
          if (!firstToken) {
            firstToken = true
            assistantThinkingMs = Date.now() - startMs
            thinkingMs.value = assistantThinkingMs
            if (_thinkingInterval) { clearInterval(_thinkingInterval); _thinkingInterval = null }
          }
          assistantContent += evt.text
          streamingContent.value = assistantContent
        } else if (evt.type === 'tool-call') {
          // Push tool call message — it appears above the streaming placeholder in the DOM
          const toolMsg: ChatMessage = {
            id: crypto.randomUUID(),
            role: 'system',
            content: `Calling ${evt.toolName}`,
            timestampUtc: new Date().toISOString(),
            toolCallName: evt.toolName,
            toolCallParameters: evt.toolParameters,
            connectionName: evt.connectionName,
            modelName,
          }
          messages.value.push(toolMsg)
          await nextTick() // flush DOM so tool call renders immediately
        } else if (evt.type === 'usage' && evt.usage) {
          assistantTokenUsage = evt.usage
          lastUsage.value = evt.usage
        } else if (evt.type === 'done') {
          if (evt.messageId) assistantId = evt.messageId
        } else if (evt.type === 'error') {
          error.value = evt.errorMessage ?? 'Unknown streaming error'
        }
      }
    } catch (e: any) {
      if (e.name !== 'AbortError') error.value = e.message
    } finally {
      // Push complete assistant message — replaces the streaming placeholder visually
      messages.value.push({
        id: assistantId ?? crypto.randomUUID(),
        role: 'assistant',
        content: assistantContent,
        timestampUtc: new Date().toISOString(),
        modelName,
        thinkingMilliseconds: assistantThinkingMs ?? undefined,
        tokenUsage: assistantTokenUsage ?? undefined,
      })
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

  async function clearMessages() {
    if (!activeSessionId.value) return
    // Delete and recreate the session to clear messages
    const id = activeSessionId.value
    await chatApi.deleteSession(id)
    const newSession = await chatApi.createSession()
    await loadSessions()
    await selectSession(newSession.id)
  }

  return {
    sessions, activeSessionId, messages, streaming,
    streamingContent, thinkingMs, error, lastUsage,
    loadSessions, createSession, deleteSession, selectSession, sendMessage, cancelStream, clearMessages,
  }
})
