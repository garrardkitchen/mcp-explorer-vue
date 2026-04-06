// src/stores/theme.ts
import { defineStore } from 'pinia'
import { ref } from 'vue'
import { preferencesApi } from '@/api/preferences'

export type ThemeId =
  | 'command-dark'
  | 'command-light'
  | 'nord'
  | 'dracula'
  | 'catppuccin'
  | 'solarized'
  | 'material'
  | 'material-light'
  | 'github'
  | 'github-light'

export interface ThemeMeta {
  id: ThemeId
  label: string
  mode: 'dark' | 'light'
  preview: string // CSS hex for the swatch
}

export const THEMES: ThemeMeta[] = [
  { id: 'command-dark',  label: 'Command Dark',    mode: 'dark',  preview: '#0f172a' },
  { id: 'command-light', label: 'Command Light',   mode: 'light', preview: '#f8fafc' },
  { id: 'nord',          label: 'Nord',            mode: 'dark',  preview: '#2e3440' },
  { id: 'dracula',       label: 'Dracula',         mode: 'dark',  preview: '#282a36' },
  { id: 'catppuccin',    label: 'Catppuccin Mocha',mode: 'dark',  preview: '#1e1e2e' },
  { id: 'solarized',     label: 'Solarized Light', mode: 'light', preview: '#fdf6e3' },
  { id: 'material',       label: 'Material Dark',   mode: 'dark',  preview: '#1c1b1f' },
  { id: 'material-light', label: 'Material Light',  mode: 'light', preview: '#fffbfe' },
  { id: 'github',         label: 'GitHub Dark',     mode: 'dark',  preview: '#0d1117' },
  { id: 'github-light',   label: 'GitHub Light',    mode: 'light', preview: '#ffffff' },
]

const STORAGE_KEY = 'mcp-explorer-theme'

export const useThemeStore = defineStore('theme', () => {
  // Initialise from localStorage for instant load before API responds
  const saved = (localStorage.getItem(STORAGE_KEY) ?? 'command-dark') as ThemeId
  const activeTheme = ref<ThemeId>(saved)

  function applyTheme(id: ThemeId) {
    const root = document.documentElement
    // Remove all existing theme classes
    THEMES.forEach(t => root.classList.remove(`theme-${t.id}`))
    root.classList.add(`theme-${id}`)
    // Toggle dark/light mode class for PrimeVue
    const meta = THEMES.find(t => t.id === id)
    root.classList.toggle('dark-mode', meta?.mode === 'dark')
    localStorage.setItem(STORAGE_KEY, id)
  }

  async function setTheme(id: ThemeId) {
    activeTheme.value = id
    applyTheme(id)
    try {
      await preferencesApi.setTheme(id)
    } catch {
      // Non-critical: UI already updated
    }
  }

  async function loadFromServer() {
    try {
      const { theme } = await preferencesApi.getTheme()
      if (theme && theme !== activeTheme.value) {
        activeTheme.value = theme as ThemeId
        applyTheme(theme as ThemeId)
      }
    } catch {
      // Fall back to localStorage value
    }
  }

  // Apply on init
  applyTheme(activeTheme.value)

  return { activeTheme, THEMES, setTheme, loadFromServer }
})
