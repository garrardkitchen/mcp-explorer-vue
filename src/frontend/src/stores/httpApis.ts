// src/stores/httpApis.ts
import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { httpApisApi } from '@/api/httpApis'
import type { HttpApiDefinition, HttpApiGroup } from '@/api/types'

export const useHttpApisStore = defineStore('httpApis', () => {
  const definitions = ref<HttpApiDefinition[]>([])
  const favouriteIds = ref<Set<string>>(new Set())
  const groups = ref<HttpApiGroup[]>([])
  const loading = ref(false)
  const showFavouritesFirst = ref(false)

  const favouriteDefinitions = computed(() =>
    definitions.value.filter(d => favouriteIds.value.has(d.id))
  )

  async function loadAll() {
    loading.value = true
    try {
      const [{ definitions: defs, favouriteIds: favs }, latestStatuses] = await Promise.all([
        httpApisApi.getAll(),
        httpApisApi.getLatestStatuses(),
      ])
      // Overlay the most recent status from history onto each definition
      definitions.value = defs.map(d => {
        const hist = latestStatuses[d.id]
        if (!hist) return d
        const histTime = new Date(hist.invokedAt).getTime()
        const defTime  = d.lastInvokedAt ? new Date(d.lastInvokedAt).getTime() : 0
        if (histTime > defTime) return { ...d, lastStatusCode: hist.statusCode, lastInvokedAt: hist.invokedAt }
        return d
      })
      favouriteIds.value = new Set(favs)
    } finally {
      loading.value = false
    }
  }

  async function loadGroups() {
    groups.value = await httpApisApi.getGroups()
  }

  function isFavourite(id: string) {
    return favouriteIds.value.has(id)
  }

  async function toggleFavourite(id: string) {
    const nowFav = !favouriteIds.value.has(id)
    await httpApisApi.setFavourite(id, nowFav)
    if (nowFav) favouriteIds.value.add(id)
    else favouriteIds.value.delete(id)
  }

  async function deleteDefinition(id: string) {
    await httpApisApi.delete(id)
    definitions.value = definitions.value.filter(d => d.id !== id)
    favouriteIds.value.delete(id)
  }

  async function copyDefinition(id: string) {
    const copy = await httpApisApi.copy(id)
    definitions.value.push(copy)
    return copy
  }

  function addOrUpdate(def: HttpApiDefinition) {
    const idx = definitions.value.findIndex(d => d.id === def.id)
    if (idx >= 0) definitions.value[idx] = def
    else definitions.value.push(def)
  }

  return {
    definitions,
    favouriteIds,
    groups,
    loading,
    showFavouritesFirst,
    favouriteDefinitions,
    loadAll,
    loadGroups,
    isFavourite,
    toggleFavourite,
    deleteDefinition,
    copyDefinition,
    addOrUpdate,
  }
})
