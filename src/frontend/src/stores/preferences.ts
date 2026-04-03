// src/stores/preferences.ts
import { defineStore } from 'pinia'
import { ref } from 'vue'
import { preferencesApi } from '@/api/preferences'
import type { UserPreferences, SensitiveFieldConfiguration } from '@/api/types'

export const usePreferencesStore = defineStore('preferences', () => {
  const prefs = ref<UserPreferences | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  async function load() {
    loading.value = true
    try {
      prefs.value = await preferencesApi.getAll()
    } catch (e: any) {
      error.value = e.message
    } finally {
      loading.value = false
    }
  }

  async function patch(partial: Partial<UserPreferences>) {
    await preferencesApi.patch(partial)
    if (prefs.value) Object.assign(prefs.value, partial)
  }

  async function getSensitiveFields(): Promise<SensitiveFieldConfiguration | null> {
    try {
      return await preferencesApi.getSensitiveFields()
    } catch {
      return null
    }
  }

  async function updateSensitiveFields(config: SensitiveFieldConfiguration) {
    await preferencesApi.updateSensitiveFields(config)
  }

  return { prefs, loading, error, load, patch, getSensitiveFields, updateSensitiveFields }
})
