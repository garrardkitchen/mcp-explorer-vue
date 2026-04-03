// src/stores/notifications.ts
import { defineStore } from 'pinia'
import { ref } from 'vue'

export type NotificationSeverity = 'success' | 'info' | 'warn' | 'error'

export interface AppNotification {
  id: string
  severity: NotificationSeverity
  summary: string
  detail?: string
  life?: number
}

export const useNotificationsStore = defineStore('notifications', () => {
  const queue = ref<AppNotification[]>([])

  function push(severity: NotificationSeverity, summary: string, detail?: string, life = 4000) {
    queue.value.push({ id: crypto.randomUUID(), severity, summary, detail, life })
  }

  const success = (summary: string, detail?: string) => push('success', summary, detail)
  const info    = (summary: string, detail?: string) => push('info',    summary, detail)
  const warn    = (summary: string, detail?: string) => push('warn',    summary, detail)
  const error   = (summary: string, detail?: string) => push('error',   summary, detail, 8000)

  function remove(id: string) {
    queue.value = queue.value.filter(n => n.id !== id)
  }

  return { queue, push, success, info, warn, error, remove }
})
