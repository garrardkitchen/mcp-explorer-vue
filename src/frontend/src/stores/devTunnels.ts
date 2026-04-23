import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import { devTunnelsApi } from '@/api/devtunnels'
import type { DevTunnel, DevTunnelUserState, WebhookEvent } from '@/api/types'

type StreamState = 'connecting' | 'connected' | 'disconnected'

type LiveNotification = {
  id: string
  tunnelId: string
  tunnelName: string
  method: string
  path: string
  receivedAtUtc: string
}

type AppendEventOptions = {
  live?: boolean
}

type WorkspaceVisibility = {
  dashboardVisible: boolean
  inspectorTunnelId: string | null
}

function sortTunnels(items: DevTunnel[]) {
  return [...items].sort((a, b) => a.name.localeCompare(b.name))
}

function sortEvents(items: WebhookEvent[]) {
  return [...items].sort((a, b) => new Date(a.receivedAtUtc).getTime() - new Date(b.receivedAtUtc).getTime())
}

function mergeEvents(...collections: WebhookEvent[][]) {
  const merged = new Map<string, WebhookEvent>()
  for (const collection of collections) {
    for (const event of collection)
      merged.set(event.id, event)
  }

  return sortEvents([...merged.values()]).slice(-1000)
}

function readNotificationPausePreference() {
  if (typeof window === 'undefined')
    return false

  return window.localStorage.getItem('dev-tunnels-notifications-paused') === 'true'
}

function writeNotificationPausePreference(value: boolean) {
  if (typeof window === 'undefined')
    return

  window.localStorage.setItem('dev-tunnels-notifications-paused', value ? 'true' : 'false')
}

function buildHistogram(events: WebhookEvent[], totalBuckets: number) {
  if (totalBuckets <= 0)
    return []

  if (events.length === 0)
    return Array.from({ length: totalBuckets }, (_, index) => ({ index, value: 0, ratio: 0 }))

  const ordered = sortEvents(events)
  const first = new Date(ordered[0].receivedAtUtc).getTime()
  const last = new Date(ordered[ordered.length - 1].receivedAtUtc).getTime()
  const span = Math.max(last - first, 1)
  const values = Array.from({ length: totalBuckets }, () => 0)

  for (const event of ordered) {
    const timestamp = new Date(event.receivedAtUtc).getTime()
    const normalized = (timestamp - first) / span
    const bucketIndex = Math.min(totalBuckets - 1, Math.floor(normalized * totalBuckets))
    values[bucketIndex] += 1
  }

  const max = Math.max(...values, 1)
  return values.map((value, index) => ({
    index,
    value,
    ratio: value / max,
  }))
}

export const useDevTunnelsStore = defineStore('dev-tunnels', () => {
  const tunnels = ref<DevTunnel[]>([])
  const userState = ref<DevTunnelUserState | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)
  const eventsByTunnel = ref<Record<string, WebhookEvent[]>>({})
  const streamStateByTunnel = ref<Record<string, StreamState>>({})
  const unseenCountByTunnel = ref<Record<string, number>>({})
  const pendingNotifications = ref<LiveNotification[]>([])
  const notificationPaused = ref(readNotificationPausePreference())
  const workspaceVisibility = ref<WorkspaceVisibility>({
    dashboardVisible: false,
    inspectorTunnelId: null,
  })

  const streams = new Map<string, EventSource>()
  const pollers = new Map<string, number>()

  const runningTunnels = computed(() =>
    tunnels.value.filter(t => t.status === 'Running' || t.status === 'Starting'),
  )

  const activeWebhookTunnels = computed(() =>
    runningTunnels.value.filter(t => !!t.webhookUri),
  )

  const runningCount = computed(() => runningTunnels.value.length)

  const totalCapturedEvents = computed(() =>
    Object.values(eventsByTunnel.value).reduce((total, events) => total + events.length, 0),
  )

  const unseenTotalCount = computed(() =>
    Object.values(unseenCountByTunnel.value).reduce((total, count) => total + count, 0),
  )

  const overallHistogram = computed(() =>
    buildHistogram(Object.values(eventsByTunnel.value).flat(), 24),
  )

  function setNotificationPaused(value: boolean) {
    notificationPaused.value = value
    writeNotificationPausePreference(value)
  }

  function toggleNotificationPaused() {
    setNotificationPaused(!notificationPaused.value)
  }

  function clearUnseenCounts(tunnelId?: string) {
    if (!tunnelId) {
      unseenCountByTunnel.value = {}
      return
    }

    if (!(tunnelId in unseenCountByTunnel.value))
      return

    const next = { ...unseenCountByTunnel.value }
    delete next[tunnelId]
    unseenCountByTunnel.value = next
  }

  function isTunnelVisible(tunnelId: string) {
    return workspaceVisibility.value.dashboardVisible || workspaceVisibility.value.inspectorTunnelId === tunnelId
  }

  function setWorkspaceVisibility(next: WorkspaceVisibility) {
    workspaceVisibility.value = next

    if (next.dashboardVisible) {
      clearUnseenCounts()
      return
    }

    if (next.inspectorTunnelId)
      clearUnseenCounts(next.inspectorTunnelId)
  }

  function ensureStatusPoll(tunnelId: string) {
    if (pollers.has(tunnelId))
      return

    const handle = window.setInterval(async () => {
      try {
        const latest = await devTunnelsApi.get(tunnelId)
        if (!latest) {
          stopStatusPoll(tunnelId)
          return
        }

        upsertTunnel(latest)
      } catch {
        // Keep polling while the tunnel transitions into a settled state.
      }
    }, 1500)

    pollers.set(tunnelId, handle)
  }

  function stopStatusPoll(tunnelId: string) {
    const handle = pollers.get(tunnelId)
    if (handle !== undefined) {
      window.clearInterval(handle)
      pollers.delete(tunnelId)
    }
  }

  function queueNotification(tunnelId: string, event: WebhookEvent) {
    if (notificationPaused.value || isTunnelVisible(tunnelId))
      return

    const tunnel = tunnels.value.find(item => item.id === tunnelId)
    if (!tunnel)
      return

    pendingNotifications.value = [
      ...pendingNotifications.value,
      {
        id: event.id,
        tunnelId,
        tunnelName: tunnel.name,
        method: event.method,
        path: event.path,
        receivedAtUtc: event.receivedAtUtc,
      },
    ]
  }

  function consumePendingNotifications() {
    const notifications = [...pendingNotifications.value]
    pendingNotifications.value = []
    return notifications
  }

  function connectStream(tunnelId: string) {
    if (streams.has(tunnelId))
      return

    streamStateByTunnel.value = { ...streamStateByTunnel.value, [tunnelId]: 'connecting' }

    const stream = devTunnelsApi.createEventStream(
      tunnelId,
      event => appendEvent(tunnelId, event, { live: true }),
      () => {
        streams.get(tunnelId)?.close()
        streams.delete(tunnelId)
        streamStateByTunnel.value = { ...streamStateByTunnel.value, [tunnelId]: 'disconnected' }
        void refreshTunnel(tunnelId).catch(() => {
          // If status refresh fails, the next explicit refresh/start action can recover the stream.
        })
      },
      () => {
        streamStateByTunnel.value = { ...streamStateByTunnel.value, [tunnelId]: 'connected' }
      },
    )

    streams.set(tunnelId, stream)
  }

  function disconnectStream(tunnelId: string) {
    streams.get(tunnelId)?.close()
    streams.delete(tunnelId)
    streamStateByTunnel.value = { ...streamStateByTunnel.value, [tunnelId]: 'disconnected' }
  }

  function syncTunnelStream(tunnel: DevTunnel) {
    const shouldStream = tunnel.status === 'Running' || tunnel.status === 'Starting'
    if (shouldStream)
      connectStream(tunnel.id)
    else
      disconnectStream(tunnel.id)
  }

  function upsertTunnel(tunnel: DevTunnel) {
    const existing = tunnels.value.findIndex(item => item.id === tunnel.id)
    if (existing === -1) {
      tunnels.value = sortTunnels([...tunnels.value, tunnel])
    } else {
      const next = [...tunnels.value]
      next[existing] = tunnel
      tunnels.value = sortTunnels(next)
    }

    if (tunnel.status === 'Starting')
      ensureStatusPoll(tunnel.id)
    else
      stopStatusPoll(tunnel.id)

    syncTunnelStream(tunnel)
  }

  async function loadAll() {
    loading.value = true
    error.value = null
    try {
      const [allTunnels, currentUserState] = await Promise.all([
        devTunnelsApi.getAll(),
        devTunnelsApi.getUserState().catch(() => null),
      ])

      tunnels.value = sortTunnels(allTunnels)
      userState.value = currentUserState

      const activeIds = new Set(allTunnels.map(tunnel => tunnel.id))
      eventsByTunnel.value = Object.fromEntries(
        Object.entries(eventsByTunnel.value).filter(([tunnelId]) => activeIds.has(tunnelId)),
      )
      unseenCountByTunnel.value = Object.fromEntries(
        Object.entries(unseenCountByTunnel.value).filter(([tunnelId]) => activeIds.has(tunnelId)),
      )
      pendingNotifications.value = pendingNotifications.value.filter(item => activeIds.has(item.tunnelId))

      for (const tunnel of allTunnels) {
        if (tunnel.status === 'Starting')
          ensureStatusPoll(tunnel.id)
        else
          stopStatusPoll(tunnel.id)

        syncTunnelStream(tunnel)
      }

      for (const tunnelId of streams.keys()) {
        if (!activeIds.has(tunnelId))
          disconnectStream(tunnelId)
      }

      for (const tunnelId of pollers.keys()) {
        if (!activeIds.has(tunnelId))
          stopStatusPoll(tunnelId)
      }
    } catch (e: any) {
      error.value = e.message
      throw e
    } finally {
      loading.value = false
    }
  }

  async function refreshTunnel(tunnelId: string) {
    const tunnel = await devTunnelsApi.get(tunnelId)
    upsertTunnel(tunnel)
    return tunnel
  }

  async function createTunnel(payload: { name: string; access: 'Anonymous' | 'Authenticated'; deleteOnExit: boolean }) {
    const tunnel = await devTunnelsApi.create(payload)
    upsertTunnel(tunnel)
    return tunnel
  }

  async function startTunnel(tunnelId: string) {
    const tunnel = await devTunnelsApi.start(tunnelId)
    upsertTunnel(tunnel)
    return tunnel
  }

  async function stopTunnel(tunnelId: string) {
    const tunnel = await devTunnelsApi.stop(tunnelId)
    upsertTunnel(tunnel)
    clearUnseenCounts(tunnelId)
    return tunnel
  }

  async function deleteTunnel(tunnelId: string) {
    await devTunnelsApi.remove(tunnelId)
    tunnels.value = tunnels.value.filter(tunnel => tunnel.id !== tunnelId)

    const nextEvents = { ...eventsByTunnel.value }
    delete nextEvents[tunnelId]
    eventsByTunnel.value = nextEvents

    const nextUnseen = { ...unseenCountByTunnel.value }
    delete nextUnseen[tunnelId]
    unseenCountByTunnel.value = nextUnseen

    pendingNotifications.value = pendingNotifications.value.filter(item => item.tunnelId !== tunnelId)

    disconnectStream(tunnelId)
    stopStatusPoll(tunnelId)
  }

  async function loadEvents(tunnelId: string, limit = 1000) {
    const events = await devTunnelsApi.getEvents(tunnelId, limit)
    const current = eventsByTunnel.value[tunnelId] ?? []
    eventsByTunnel.value = { ...eventsByTunnel.value, [tunnelId]: mergeEvents(current, events) }
    return eventsByTunnel.value[tunnelId]
  }

  async function ensureEventData(tunnelIds = tunnels.value.map(tunnel => tunnel.id), limit = 1000, force = false) {
    const targets = force
      ? tunnelIds
      : tunnelIds.filter(tunnelId => !(tunnelId in eventsByTunnel.value))
    if (!targets.length)
      return

    await Promise.allSettled(targets.map(tunnelId => loadEvents(tunnelId, limit)))
  }

  function appendEvent(tunnelId: string, event: WebhookEvent, options: AppendEventOptions = {}) {
    const current = eventsByTunnel.value[tunnelId] ?? []
    if (current.some(existing => existing.id === event.id))
      return

    const next = sortEvents([...current, event]).slice(-1000)
    eventsByTunnel.value = { ...eventsByTunnel.value, [tunnelId]: next }

    if (!options.live)
      return

    if (!isTunnelVisible(tunnelId)) {
      unseenCountByTunnel.value = {
        ...unseenCountByTunnel.value,
        [tunnelId]: (unseenCountByTunnel.value[tunnelId] ?? 0) + 1,
      }
    }

    queueNotification(tunnelId, event)
  }

  function disconnectAllStreams() {
    for (const tunnelId of streams.keys())
      disconnectStream(tunnelId)

    for (const tunnelId of [...pollers.keys()])
      stopStatusPoll(tunnelId)
  }

  function ensureStreamsForRunning() {
    for (const tunnel of runningTunnels.value)
      syncTunnelStream(tunnel)
  }

  function getEventsForTunnel(tunnelId: string) {
    return eventsByTunnel.value[tunnelId] ?? []
  }

  function getEventCountForTunnel(tunnelId: string) {
    return getEventsForTunnel(tunnelId).length
  }

  function getLastEventForTunnel(tunnelId: string) {
    const events = getEventsForTunnel(tunnelId)
    return events.length ? events[events.length - 1] : null
  }

  function getHistogramForTunnel(tunnelId: string, totalBuckets = 20) {
    return buildHistogram(getEventsForTunnel(tunnelId), totalBuckets)
  }

  return {
    tunnels,
    userState,
    loading,
    error,
    eventsByTunnel,
    streamStateByTunnel,
    unseenCountByTunnel,
    pendingNotifications,
    notificationPaused,
    runningTunnels,
    activeWebhookTunnels,
    runningCount,
    totalCapturedEvents,
    unseenTotalCount,
    overallHistogram,
    loadAll,
    refreshTunnel,
    createTunnel,
    startTunnel,
    stopTunnel,
    deleteTunnel,
    loadEvents,
    ensureEventData,
    appendEvent,
    connectStream,
    disconnectStream,
    disconnectAllStreams,
    ensureStreamsForRunning,
    getEventsForTunnel,
    getEventCountForTunnel,
    getLastEventForTunnel,
    getHistogramForTunnel,
    upsertTunnel,
    consumePendingNotifications,
    setNotificationPaused,
    toggleNotificationPaused,
    setWorkspaceVisibility,
    clearUnseenCounts,
  }
})
