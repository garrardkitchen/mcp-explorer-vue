import { apiClient } from './client'
import type {
  CertOperationResult,
  CertUploadStatus,
  CertificateAuditEntry,
  CertificateInfo,
  CertificateWithUsage,
  GenerateCertificateRequest,
  GraphKeyCredentialInfo,
} from './types'

const enc = encodeURIComponent

export const certificatesApi = {
  /** Lists all certificates in the local store, with used-by info. */
  getAll: () =>
    apiClient.get<CertificateWithUsage[]>('/certificates').then(r => r.data),

  get: (name: string) =>
    apiClient.get<CertificateWithUsage>(`/certificates/${enc(name)}`).then(r => r.data),

  getExpiring: (days = 30) =>
    apiClient.get<CertificateInfo[]>('/certificates/expiring', { params: { days } }).then(r => r.data),

  generate: (request: GenerateCertificateRequest) =>
    apiClient.post<CertOperationResult>('/certificates/generate', request).then(r => r.data),

  remove: (name: string) =>
    apiClient.delete(`/certificates/${enc(name)}`),

  /** Uploads the public certificate to an App Registration via Microsoft Graph. */
  upload: (name: string, appId: string) =>
    apiClient.post<CertOperationResult>(`/certificates/${enc(name)}/upload`, { appId }).then(r => r.data),

  uploadStatus: (name: string, appId: string) =>
    apiClient.get<{ status: CertUploadStatus }>(`/certificates/${enc(name)}/upload-status`, { params: { appId } })
      .then(r => r.data.status),

  listKeyCredentials: (appId: string) =>
    apiClient.get<GraphKeyCredentialInfo[]>('/certificates/key-credentials', { params: { appId } }).then(r => r.data),

  removeKeyCredential: (appId: string, keyId: string) =>
    apiClient.delete('/certificates/key-credentials', { data: { appId, keyId } }),

  /** One-click rotation: new cert, re-upload, repoint connections, optional old-credential cleanup. */
  renew: (name: string, removeOldKeyCredential: boolean) =>
    apiClient.post<CertOperationResult>(`/certificates/${enc(name)}/renew`, { removeOldKeyCredential }).then(r => r.data),

  /** Dry-run token acquisition with ClientCertificateCredential. */
  testToken: (name: string, tenantId: string, clientId: string, scope: string) =>
    apiClient.post<CertOperationResult>(`/certificates/${enc(name)}/test-token`, { tenantId, clientId, scope })
      .then(r => r.data),

  /** Public certificate PEM — never contains key material. */
  downloadPem: (name: string) =>
    apiClient.get<Blob>(`/certificates/${enc(name)}/download`, { responseType: 'blob' }).then(r => r.data),

  /** Password-protected PFX bundle (includes the private key). */
  exportPfx: (name: string, password: string) =>
    apiClient.post<Blob>(`/certificates/${enc(name)}/export-pfx`, { password }, { responseType: 'blob' })
      .then(r => r.data),

  createCsr: (name: string, subjectCn?: string, keySize?: number) =>
    apiClient.post<CertOperationResult>('/certificates/csr', { name, subjectCn, keySize }).then(r => r.data),

  downloadCsr: (name: string) =>
    apiClient.get<Blob>(`/certificates/${enc(name)}/csr`, { responseType: 'blob' }).then(r => r.data),

  importIssued: (name: string, certificatePem: string) =>
    apiClient.post<CertOperationResult>(`/certificates/${enc(name)}/import-issued`, { certificatePem }).then(r => r.data),

  getAudit: (name?: string, limit = 200) =>
    apiClient.get<CertificateAuditEntry[]>('/certificates/audit', { params: { name, limit } }).then(r => r.data),
}
