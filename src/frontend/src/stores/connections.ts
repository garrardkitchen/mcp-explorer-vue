// src/stores/connections.ts
import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { connectionsApi } from '@/api/connections'
import type { ConnectionDefinition, ActiveConnection, ActiveTool, ActivePrompt, ActiveResource, ActiveResourceTemplate } from '@/api/types'

export const useConnectionsStore = defineStore('connections', () => {
  const savedConnections = ref<ConnectionDefinition[]>([])
  const activeConnections = ref<ActiveConnection[]>([])
  const selectedConnectionName = ref<string | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)
  /** Set to true after App.vue has finished loading both saved and active connections. */
  const initialized = ref(false)

  // Per-connection capability cache
  const toolsCache = ref<Record<string, ActiveTool[]>>({})
  const promptsCache = ref<Record<string, ActivePrompt[]>>({})
  const resourcesCache = ref<Record<string, ActiveResource[]>>({})
  const resourceTemplatesCache = ref<Record<string, ActiveResourceTemplate[]>>({})

  const selectedConnection = computed(() =>
    activeConnections.value.find(c => c.name === selectedConnectionName.value) ?? null
  )

  async function loadSaved() {
    try {
      savedConnections.value = await connectionsApi.getAll()
    } catch (e: any) {
      error.value = e.message
    }
  }

  const loadingActive = ref(false)

  async function loadActive() {
    if (loadingActive.value) return
    loadingActive.value = true
    try {
      activeConnections.value = await connectionsApi.getActive()
    } catch (e: any) {
      error.value = e.message
    } finally {
      loadingActive.value = false
    }
  }

  async function connect(name: string) {
    loading.value = true
    error.value = null
    try {
      const conn = await connectionsApi.connect(name)
      const existing = activeConnections.value.findIndex(c => c.name === name)
      if (existing >= 0) activeConnections.value[existing] = conn
      else activeConnections.value.push(conn)
      selectedConnectionName.value = name
      return conn
    } catch (e: any) {
      error.value = e.message
      throw e
    } finally {
      loading.value = false
    }
  }

  async function disconnect(name: string) {
    await connectionsApi.disconnect(name)
    activeConnections.value = activeConnections.value.filter(c => c.name !== name)
    // Clear capability cache for this connection
    delete toolsCache.value[name]
    delete promptsCache.value[name]
    delete resourcesCache.value[name]
    delete resourceTemplatesCache.value[name]
    if (selectedConnectionName.value === name) {
      selectedConnectionName.value = activeConnections.value[0]?.name ?? null
    }
  }

  async function createConnection(def: Partial<ConnectionDefinition>) {
    const created = await connectionsApi.create(def)
    savedConnections.value.push(created)
    return created
  }

  async function deleteConnection(name: string) {
    await connectionsApi.delete(name)
    savedConnections.value = savedConnections.value.filter(c => c.name !== name)
  }

  async function getTools(connectionName: string): Promise<ActiveTool[]> {
    if (!toolsCache.value[connectionName]) {
      toolsCache.value[connectionName] = await connectionsApi.getTools(connectionName)
    }
    return toolsCache.value[connectionName]
  }

  async function getPrompts(connectionName: string): Promise<ActivePrompt[]> {
    if (!promptsCache.value[connectionName]) {
      promptsCache.value[connectionName] = await connectionsApi.getPrompts(connectionName)
    }
    return promptsCache.value[connectionName]
  }

  async function getResources(connectionName: string): Promise<ActiveResource[]> {
    if (!resourcesCache.value[connectionName]) {
      resourcesCache.value[connectionName] = await connectionsApi.getResources(connectionName)
    }
    return resourcesCache.value[connectionName]
  }

  async function getResourceTemplates(connectionName: string): Promise<ActiveResourceTemplate[]> {
    if (!resourceTemplatesCache.value[connectionName]) {
      resourceTemplatesCache.value[connectionName] = await connectionsApi.getResourceTemplates(connectionName)
    }
    return resourceTemplatesCache.value[connectionName]
  }

  function clearCache(connectionName: string) {
    delete toolsCache.value[connectionName]
    delete promptsCache.value[connectionName]
    delete resourcesCache.value[connectionName]
    delete resourceTemplatesCache.value[connectionName]
  }

  return {
    savedConnections, activeConnections, selectedConnectionName, selectedConnection,
    loading, error, initialized, toolsCache, promptsCache, resourcesCache, resourceTemplatesCache,
    loadSaved, loadActive, connect, disconnect, createConnection, deleteConnection,
    getTools, getPrompts, getResources, getResourceTemplates, clearCache,
  }
})
