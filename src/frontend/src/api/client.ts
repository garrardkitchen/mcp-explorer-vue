// src/api/client.ts
// Central axios instance with base URL from env

import axios from 'axios'

const BASE_URL = import.meta.env.VITE_API_BASE_URL ?? ''

export const apiClient = axios.create({
  baseURL: `${BASE_URL}/api/v1`,
  headers: { 'Content-Type': 'application/json' },
  timeout: 30_000,
})

apiClient.interceptors.response.use(
  (res) => res,
  (err) => {
    // Preserve structured error messages from the API
    return Promise.reject(err)
  }
)
