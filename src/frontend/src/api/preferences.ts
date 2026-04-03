// src/api/preferences.ts
import { apiClient } from './client'
import type { UserPreferences, SensitiveFieldConfiguration } from './types'

export const preferencesApi = {
  getAll: () => apiClient.get<UserPreferences>('/preferences').then(r => r.data),

  patch: (partial: Partial<UserPreferences>) =>
    apiClient.patch<UserPreferences>('/preferences', partial).then(r => r.data),

  getTheme: () => apiClient.get<{ theme: string }>('/preferences/theme').then(r => r.data),

  setTheme: (theme: string) =>
    apiClient.put<{ theme: string }>('/preferences/theme', { theme }).then(r => r.data),

  getSensitiveFields: () =>
    apiClient.get<SensitiveFieldConfiguration>('/preferences/sensitive-fields').then(r => r.data),

  updateSensitiveFields: (config: SensitiveFieldConfiguration) =>
    apiClient.put<SensitiveFieldConfiguration>('/preferences/sensitive-fields', config).then(r => r.data),
}
