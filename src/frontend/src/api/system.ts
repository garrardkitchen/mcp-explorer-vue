import { apiClient } from './client'

export interface SystemInfo {
  apiVersion: string
  dotnetVersion: string
}

export const systemApi = {
  getInfo: () => apiClient.get<SystemInfo>('/system/info').then(r => r.data),
}
