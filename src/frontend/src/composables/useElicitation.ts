import { ref, computed, watch, onUnmounted } from 'vue'
import type { Ref } from 'vue'
import { elicitationApi } from '@/api/elicitation'
import type { ElicitationRequest } from '@/api/types'

/**
 * Manages the SSE stream and elicitation request queue for a single MCP connection.
 *
 * - Opens an SSE connection whenever `connectionName` is non-null.
 * - Filters incoming events to the active connection only.
 * - Maintains an ordered queue; callers process one request at a time via `current`.
 * - Re-opens the stream automatically when the connection name changes.
 */
export function useElicitation(connectionName: Ref<string | null>) {
  const queue = ref<ElicitationRequest[]>([])
  const stepNumber = ref(0)           // monotonically-increasing step counter
  const sseStatus = ref<'connecting' | 'connected' | 'disconnected'>('disconnected')
  let es: EventSource | null = null

  const current = computed(() => queue.value[0] ?? null)
  const visible = computed(() => current.value !== null)

  function openStream() {
    if (es) { es.close() }
    if (!connectionName.value) return

    sseStatus.value = 'connecting'
    es = elicitationApi.createStream(
      (req: ElicitationRequest) => {
        // Only handle requests for the currently selected connection.
        if (req.connectionName !== connectionName.value) return
        if (queue.value.find(r => r.id === req.id)) return   // de-dupe
        stepNumber.value++
        queue.value = [...queue.value, req]
      },
      () => { sseStatus.value = 'disconnected' },
    )
    es.onopen = () => { sseStatus.value = 'connected' }
  }

  function closeStream() {
    es?.close()
    es = null
    queue.value = []
    stepNumber.value = 0
    sseStatus.value = 'disconnected'
  }

  /** Submit a response for the current (head-of-queue) request. */
  async function respond(action: string, content?: Record<string, unknown>) {
    const req = current.value
    if (!req) return
    await elicitationApi.respond(req.id, action, action === 'Accept' ? content : undefined)
    // Remove the request from the queue regardless of what comes next.
    queue.value = queue.value.filter(r => r.id !== req.id)
  }

  // Re-open SSE whenever the selected connection changes.
  watch(
    connectionName,
    (name) => {
      closeStream()
      if (name) openStream()
    },
    { immediate: true },
  )

  onUnmounted(closeStream)

  return {
    current,
    visible,
    stepNumber,
    sseStatus,
    respond,
  }
}
