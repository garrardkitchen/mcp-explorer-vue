// src/api/client.ts
// Central axios instance with base URL from env

import axios, { type AxiosError } from 'axios'

const BASE_URL = import.meta.env.VITE_API_BASE_URL ?? ''

export const apiClient = axios.create({
  baseURL: `${BASE_URL}/api/v1`,
  headers: { 'Content-Type': 'application/json' },
  // No global timeout — endpoints like tool invocations and LLM streaming can
  // take several minutes. Per-request timeouts are set where needed.
})

/** Extract the most useful error message from an axios error. */
export function extractApiError(err: unknown): string {
  const axiosErr = err as AxiosError<{ error?: string; title?: string; detail?: string }>
  if (axiosErr?.response?.data) {
    const d = axiosErr.response.data
    return d.error ?? d.detail ?? d.title ?? JSON.stringify(d)
  }
  if (axiosErr?.message) return axiosErr.message
  return String(err)
}

apiClient.interceptors.response.use(
  (res) => res,
  (err: AxiosError) => {
    // Re-throw — callers use extractApiError() to get a human-readable message
    return Promise.reject(err)
  }
)
