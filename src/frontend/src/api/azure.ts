import { apiClient } from './client'
import type { AzureAccountInfo, AzureSubscription, AzureAppRegistration, AzureKeyVaultInfo } from './types'

export const azureApi = {
  /** Returns the active Azure account/subscription context. */
  getAccount: () =>
    apiClient.get<AzureAccountInfo>('/azure/account').then(r => r.data),

  /** Lists all subscriptions accessible to the current credential. */
  getSubscriptions: () =>
    apiClient.get<AzureSubscription[]>('/azure/subscriptions').then(r => r.data),

  /** Lists all app registrations visible to DefaultAzureCredential. */
  getAppRegistrations: () =>
    apiClient.get<AzureAppRegistration[]>('/azure/app-registrations').then(r => r.data),

  /** Lists all Key Vaults in the given subscription (or the default if omitted). */
  getKeyVaults: (subscriptionId?: string) =>
    apiClient.get<AzureKeyVaultInfo[]>('/azure/keyvaults', {
      params: subscriptionId ? { subscriptionId } : undefined
    }).then(r => r.data),

  /** Lists all enabled secret names in a Key Vault. */
  getKeyVaultSecrets: (vaultName: string) =>
    apiClient.get<string[]>(`/azure/keyvaults/${encodeURIComponent(vaultName)}/secrets`).then(r => r.data),
}
