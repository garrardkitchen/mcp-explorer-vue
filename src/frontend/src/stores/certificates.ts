import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import { certificatesApi } from '@/api/certificates'
import { extractApiError } from '@/api/client'
import type { CertificateWithUsage } from '@/api/types'

export interface CertificateNotification {
  severity: 'warn' | 'error'
  summary: string
  detail: string
}

const EXPIRY_WARNING_DAYS = 30

export const useCertificatesStore = defineStore('certificates', () => {
  const items = ref<CertificateWithUsage[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)
  const initialized = ref(false)

  /** Queued expiry notifications, drained by the App.vue toast watcher. */
  const pendingNotifications = ref<CertificateNotification[]>([])
  // Notify at most once per app session so navigation doesn't re-toast.
  let expiryNotified = false

  const activeCertificates = computed(() =>
    items.value.filter(i => i.certificate.state !== 'Superseded'))

  const expiringSoon = computed(() => activeCertificates.value.filter((i) => {
    const notAfter = i.certificate.notAfter
    if (!notAfter) return false
    const remainingMs = new Date(notAfter).getTime() - Date.now()
    return remainingMs > 0 && remainingMs <= EXPIRY_WARNING_DAYS * 24 * 60 * 60 * 1000
  }))

  const expired = computed(() => activeCertificates.value.filter((i) => {
    const notAfter = i.certificate.notAfter
    return !!notAfter && new Date(notAfter).getTime() <= Date.now()
  }))

  const uploadedCount = computed(() =>
    activeCertificates.value.filter(i => i.certificate.uploadedTo.length > 0).length)

  /** Count shown on the topbar badge: expiring soon + already expired. */
  const attentionCount = computed(() => expiringSoon.value.length + expired.value.length)

  async function load() {
    loading.value = true
    error.value = null
    try {
      items.value = await certificatesApi.getAll()
      initialized.value = true
      queueExpiryNotifications()
    }
    catch (err) {
      error.value = extractApiError(err)
    }
    finally {
      loading.value = false
    }
  }

  function queueExpiryNotifications() {
    if (expiryNotified) return
    expiryNotified = true

    for (const item of expired.value) {
      pendingNotifications.value.push({
        severity: 'error',
        summary: `Certificate expired: ${item.certificate.name}`,
        detail: 'Renew it from the Certificates page — connections using it will fail to authenticate.',
      })
    }
    for (const item of expiringSoon.value) {
      const days = Math.ceil((new Date(item.certificate.notAfter!).getTime() - Date.now()) / (24 * 60 * 60 * 1000))
      pendingNotifications.value.push({
        severity: 'warn',
        summary: `Certificate expiring: ${item.certificate.name}`,
        detail: `Expires in ${days} day${days === 1 ? '' : 's'}. Renew it from the Certificates page.`,
      })
    }
  }

  function consumePendingNotifications() {
    const drained = [...pendingNotifications.value]
    pendingNotifications.value = []
    return drained
  }

  return {
    items,
    loading,
    error,
    initialized,
    pendingNotifications,
    activeCertificates,
    expiringSoon,
    expired,
    uploadedCount,
    attentionCount,
    load,
    consumePendingNotifications,
  }
})
