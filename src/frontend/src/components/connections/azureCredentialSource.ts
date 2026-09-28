import type { CertificateReference, KeyVaultSecretReference } from '@/api/types'

export type AzureCredentialSource = 'paste' | 'keyVault' | 'certificate'

/** Infer source from persisted credentials (certificate > Key Vault > paste). */
export function inferAzureCredentialSource(
  creds: {
    clientSecret?: string | null
    keyVaultSecretRef?: KeyVaultSecretReference | null
    certificateRef?: CertificateReference | null
  } | null | undefined,
  allowCertificate = true,
): AzureCredentialSource {
  if (allowCertificate && creds?.certificateRef?.certificateName)
    return 'certificate'
  if (creds?.keyVaultSecretRef?.vaultName && creds?.keyVaultSecretRef?.secretName)
    return 'keyVault'
  return 'paste'
}
