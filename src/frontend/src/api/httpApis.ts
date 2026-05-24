// src/api/httpApis.ts
import { apiClient } from './client'
import type {
  HttpApiDefinition,
  HttpApiCollection,
  HttpApiGroup,
  HttpResponseSnapshot,
  HttpApiInvocationRecord,
  HttpApiInvokeResponse,
  HttpApiCompareResponse,
  HttpApiExportPayload,
  HttpApiCollectionRunResult,
  HttpApiCollectionRunRecord,
} from './types'

export const httpApisApi = {
  // ── Definitions ─────────────────────────────────────────────────────────────
  getAll: () =>
    apiClient.get<{ definitions: HttpApiDefinition[]; favouriteIds: string[] }>('/http-apis').then(r => r.data),

  getLatestStatuses: () =>
    apiClient.get<Record<string, { statusCode: number; invokedAt: string }>>('/http-apis/latest-statuses').then(r => r.data),

  getOne: (id: string) =>
    apiClient.get<HttpApiDefinition>(`/http-apis/${encodeURIComponent(id)}`).then(r => r.data),

  create: (def: Partial<HttpApiDefinition>) =>
    apiClient.post<HttpApiDefinition>('/http-apis', def).then(r => r.data),

  update: (id: string, def: Partial<HttpApiDefinition>) =>
    apiClient.put<HttpApiDefinition>(`/http-apis/${encodeURIComponent(id)}`, def).then(r => r.data),

  delete: (id: string) =>
    apiClient.delete(`/http-apis/${encodeURIComponent(id)}`),

  copy: (id: string) =>
    apiClient.post<HttpApiDefinition>(`/http-apis/${encodeURIComponent(id)}/copy`).then(r => r.data),

  // ── Favourites ───────────────────────────────────────────────────────────────
  setFavourite: (id: string, isFavourite: boolean) =>
    apiClient.patch(`/http-apis/${encodeURIComponent(id)}/favourite`, { isFavourite }),

  // ── Invoke / Bookmark / Compare ──────────────────────────────────────────────
  invoke: (id: string, inputs?: Record<string, string>) =>
    apiClient.post<HttpApiInvokeResponse>(
      `/http-apis/${encodeURIComponent(id)}/invoke`,
      inputs && Object.keys(inputs).length > 0 ? { inputs } : {}
    ).then(r => r.data),

  bookmark: (id: string, label?: string, inputs?: Record<string, string>) =>
    apiClient.post<HttpResponseSnapshot>(
      `/http-apis/${encodeURIComponent(id)}/bookmark`,
      {
        label,
        ...(inputs && Object.keys(inputs).length > 0 ? { inputs } : {}),
      }
    ).then(r => r.data),

  compare: (id: string, snapshotId?: string, inputs?: Record<string, string>) =>
    apiClient.post<HttpApiCompareResponse>(
      `/http-apis/${encodeURIComponent(id)}/compare`,
      inputs && Object.keys(inputs).length > 0 ? { inputs } : {},
      snapshotId ? { params: { snapshotId } } : undefined
    ).then(r => r.data),

  // ── Snapshots ─────────────────────────────────────────────────────────────────
  getSnapshots: (id: string) =>
    apiClient.get<HttpResponseSnapshot[]>(`/http-apis/${encodeURIComponent(id)}/snapshots`).then(r => r.data),

  deleteSnapshot: (id: string, snapshotId: string) =>
    apiClient.delete(`/http-apis/${encodeURIComponent(id)}/snapshots/${encodeURIComponent(snapshotId)}`),

  pinSnapshot: (id: string, snapshotId: string) =>
    apiClient.patch(`/http-apis/${encodeURIComponent(id)}/snapshots/${encodeURIComponent(snapshotId)}/pin`),

  // ── History ───────────────────────────────────────────────────────────────────
  getHistory: (id: string, limit?: number) =>
    apiClient.get<HttpApiInvocationRecord[]>(`/http-apis/${encodeURIComponent(id)}/history`, limit ? { params: { limit } } : undefined).then(r => r.data),

  getGlobalHistory: (limit?: number) =>
    apiClient.get<HttpApiInvocationRecord[]>('/http-api-history', limit ? { params: { limit } } : undefined).then(r => r.data),

  // ── Groups ────────────────────────────────────────────────────────────────────
  getGroups: () =>
    apiClient.get<HttpApiGroup[]>('/http-apis/groups').then(r => r.data),

  createGroup: (group: HttpApiGroup) =>
    apiClient.post<HttpApiGroup>('/http-apis/groups', group).then(r => r.data),

  updateGroup: (name: string, group: HttpApiGroup) =>
    apiClient.put<HttpApiGroup>(`/http-apis/groups/${encodeURIComponent(name)}`, group).then(r => r.data),

  deleteGroup: (name: string) =>
    apiClient.delete(`/http-apis/groups/${encodeURIComponent(name)}`),

  // ── Export / Import ───────────────────────────────────────────────────────────
  exportApis: (ids: string[], password: string) =>
    apiClient.post('/http-apis/export', { ids, password }, { responseType: 'blob' }).then(r => r.data),

  importApis: (payload: HttpApiExportPayload, password: string) =>
    apiClient.post<{ imported: number; total: number }>('/http-apis/import', { payload, password }).then(r => r.data),

  // ── Collections ───────────────────────────────────────────────────────────────
  getCollections: () =>
    apiClient.get<HttpApiCollection[]>('/http-api-collections').then(r => r.data),

  getCollection: (id: string) =>
    apiClient.get<HttpApiCollection>(`/http-api-collections/${encodeURIComponent(id)}`).then(r => r.data),

  createCollection: (c: Partial<HttpApiCollection>) =>
    apiClient.post<HttpApiCollection>('/http-api-collections', c).then(r => r.data),

  updateCollection: (id: string, c: Partial<HttpApiCollection>) =>
    apiClient.put<HttpApiCollection>(`/http-api-collections/${encodeURIComponent(id)}`, c).then(r => r.data),

  deleteCollection: (id: string) =>
    apiClient.delete(`/http-api-collections/${encodeURIComponent(id)}`),

  runCollection: (id: string, inputs?: Record<string, string>) =>
    apiClient.post<HttpApiCollectionRunResult>(
      `/http-api-collections/${encodeURIComponent(id)}/run`,
      inputs && Object.keys(inputs).length > 0 ? { inputs } : {}
    ).then(r => r.data),

  getCollectionRunHistory: (id: string, limit = 50) =>
    apiClient.get<HttpApiCollectionRunRecord[]>(
      `/http-api-collections/${encodeURIComponent(id)}/run-history`,
      { params: { limit } }
    ).then(r => r.data),
}
