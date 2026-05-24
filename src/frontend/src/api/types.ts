// src/api/types.ts
// TypeScript types mirroring the Core domain models

export type ConnectionAuthenticationMode = 'CustomHeaders' | 'AzureClientCredentials' | 'OAuth'

export interface ConnectionHeader {
  name: string
  value: string
  authorizationType?: string
  isAuthorization: boolean
}

export interface KeyVaultSecretReference {
  vaultName: string
  secretName: string
}

export interface AzureClientCredentialsOptions {
  tenantId: string
  clientId: string
  clientSecret: string
  scope: string
  authorityHost?: string
  keyVaultSecretRef?: KeyVaultSecretReference
  /** Azure subscription used for KV browsing in the UI — not used in auth flow */
  subscriptionId?: string
}

export interface OAuthConnectionOptions {
  clientId: string
  clientSecret?: string
  redirectUri: string
  scopes: string
  clientMetadataDocumentUri?: string
  keyVaultSecretRef?: KeyVaultSecretReference
}

// ── Azure context types ──────────────────────────────────────────────────────

export interface AzureAccountInfo {
  tenantId: string
  subscriptionId: string
  subscriptionName: string
  userPrincipalName: string
  location?: string
}

export interface AzureSubscription {
  id: string
  name: string
  tenantId: string
  isDefault: boolean
}

export interface AzureAppRegistration {
  appId: string
  displayName: string
  firstApiResourceId?: string
}

export interface AzureKeyVaultInfo {
  name: string
  resourceGroup?: string
  location?: string
}

export interface ConnectionDefinition {
  name: string
  endpoint: string
  authenticationMode: ConnectionAuthenticationMode
  headers: ConnectionHeader[]
  azureCredentials?: AzureClientCredentialsOptions
  oAuthOptions?: OAuthConnectionOptions
  note: string
  groupName?: string
  createdAt: string
  lastUpdatedAt?: string
  lastUsedAt?: string
}

export interface ConnectionGroup {
  name: string
  color: string
  description?: string
}

export interface ActiveConnection {
  name: string
  endpoint: string
  isConnected: boolean
  isHealthy: boolean
  toolCount: number
}

export type TunnelAccess = 'Anonymous' | 'Authenticated'
export type TunnelStatus = 'Stopped' | 'Starting' | 'Running' | 'LoginRequired' | 'Error'

export interface DevTunnel {
  id: string
  name: string
  access: TunnelAccess
  status: TunnelStatus
  tunnelUri?: string | null
  webhookUri?: string | null
  createdAtUtc: string
  lastStartedAtUtc?: string | null
  lastStoppedAtUtc?: string | null
  lastError?: string | null
  deleteOnExit: boolean
  restartCount: number
}

export interface DevTunnelUserState {
  isLoggedIn: boolean
  userName?: string | null
  provider?: string | null
  isAvailable?: boolean
  detail?: string | null
}

export interface WebhookEvent {
  id: string
  tunnelId: string
  receivedAtUtc: string
  method: string
  path: string
  queryString: string
  headers: Record<string, string>
  contentType?: string | null
  bodySize: number
  bodyText?: string | null
  bodyBase64?: string | null
  contentEncoding?: string | null
  remoteIp?: string | null
  truncated: boolean
}

export interface ReplayWebhookResult {
  statusCode: number
  reasonPhrase?: string | null
  headers: Record<string, string>
  bodyText?: string | null
  bodySize: number
  duration: string
}

export interface ToolAnnotations {
  title?: string | null
  readOnlyHint?: boolean | null
  destructiveHint?: boolean | null
  idempotentHint?: boolean | null
  openWorldHint?: boolean | null
}

export interface ActiveTool {
  name: string
  description: string
  inputSchema?: Record<string, unknown>
  outputSchema?: Record<string, unknown> | null
  annotations?: ToolAnnotations | null
  iconUrl?: string
}

export interface PromptArgument {
  name: string
  description?: string
  required: boolean
}

export interface ActivePrompt {
  name: string
  description?: string
  arguments: PromptArgument[]
  iconUrl?: string
}

export interface ActiveResource {
  uri: string
  name: string
  description?: string
  mimeType?: string
  iconUrl?: string
}

export interface ActiveResourceTemplate {
  uriTemplate: string
  name: string
  description?: string
  iconUrl?: string
}

export interface ChatTokenUsage {
  inputTokens: number
  outputTokens: number
  totalTokens: number
}

export interface ChatMessage {
  id: string
  role: 'user' | 'assistant' | 'system' | 'tool'
  content: string
  timestampUtc: string
  toolCallName?: string
  toolCallParameters?: string
  connectionName?: string
  modelName?: string
  tokenUsage?: ChatTokenUsage
  thinkingMilliseconds?: number
  // Prompt invocation — persisted on the user message when a prompt picker ran it
  promptName?: string | null
  promptInvocationParams?: string | null  // JSON string e.g. {"topic":"ml"}
}

export interface ChatSession {
  id: string
  name: string
  createdAtUtc: string
  lastActivityUtc: string
  messageCount: number
}

export type ChatStreamEventType = 'token' | 'tool-call' | 'tool-result' | 'usage' | 'done' | 'error'

export interface ChatStreamEvent {
  type: ChatStreamEventType
  text?: string
  toolName?: string
  toolParameters?: string
  toolResult?: string
  connectionName?: string
  usage?: ChatTokenUsage
  messageId?: string
  errorMessage?: string
}

export interface LlmModelDefinition {
  name: string
  providerType: string
  endpoint: string
  apiKey: string
  modelName: string
  systemPrompt: string
  deploymentName: string
  note: string
}

export type AiDetectionStrictness = 'Conservative' | 'Balanced' | 'Aggressive'

export interface SensitiveFieldConfiguration {
  additionalSensitiveFields: string[]
  allowedFields: string[]
  useAiDetection: boolean
  aiStrictness: AiDetectionStrictness
  showDetectionDebug: boolean
}

export interface UserPreferences {
  selectedConnectionName?: string
  connections: ConnectionDefinition[]
  connectionGroups: ConnectionGroup[]
  favoriteConnections: string[]
  favoriteTools: string[]
  showFavoritesFirst: boolean
  parameterHistory: Record<string, string[]>
  favoritePrompts: string[]
  showPromptFavoritesFirst: boolean
  favoriteResources: string[]
  showResourceFavoritesFirst: boolean
  favoriteResourceTemplates: string[]
  showResourceTemplateFavoritesFirst: boolean
  llmModels: LlmModelDefinition[]
  selectedLlmModelName?: string
  sensitiveFieldConfig: SensitiveFieldConfiguration
  showConnectionTimestamps: boolean
  connectionSortOrder: string
  showConnectionGroups: boolean
  theme: string
}

export type ErrorHandlingMode = 'StopOnError' | 'ContinueOnError'

export type MappingSourceType = 'FromPreviousStep' | 'PromptAtRuntime' | 'ManualValue'
export type ArrayIterationMode = 'None' | 'Each' | 'First' | 'Last'

export interface ParameterMapping {
  targetParameter: string
  sourceType: MappingSourceType
  sourceStepIndex?: number | null
  sourcePropertyPath?: string | null
  manualValue?: string | null
  iterationMode: ArrayIterationMode
}

export interface WorkflowStep {
  stepNumber: number
  toolName: string
  parameterMappings: ParameterMapping[]
  errorHandling: ErrorHandlingMode
  notes?: string
}

export interface WorkflowDefinition {
  id: string
  name: string
  description: string
  defaultConnectionName?: string
  steps: WorkflowStep[]
  highlightedProperties: string[]
  createdUtc: string
  modifiedUtc: string
}

export type WorkflowExecutionStatus = 'Running' | 'Completed' | 'Failed' | 'PartiallyCompleted'

export type StepExecutionStatus = 'Pending' | 'Running' | 'Completed' | 'Failed' | 'Skipped'

export interface WorkflowStepResult {
  stepNumber: number
  toolName: string
  status: StepExecutionStatus
  startedUtc?: string
  completedUtc?: string
  duration?: string
  inputJson?: string
  outputJson?: string
  errorMessage?: string
  // legacy fields (still returned by old history entries)
  success?: boolean
  result?: unknown
}

export interface WorkflowExecution {
  id: string
  workflowId: string
  workflowName: string
  connectionName: string
  startedUtc: string
  completedUtc?: string
  status: WorkflowExecutionStatus
  stepResults: WorkflowStepResult[]
  errorMessage?: string
  duration: string
}

export interface LoadTestSnapshot {
  elapsedMs: number
  cumulativeSuccesses: number
  cumulativeFailures: number
  activeExecutions: number
}

export interface LoadTestResult {
  workflowId: string
  workflowName: string
  connectionName: string
  durationSeconds: number
  maxParallelExecutions: number
  startedUtc: string
  completedUtc: string
  totalRequests: number
  successfulRequests: number
  failedRequests: number
  requestsPerSecond: number
  averageResponseMs: number
  p50ResponseMs: number
  p90ResponseMs: number
  p99ResponseMs: number
  errorRate: number
  snapshots: LoadTestSnapshot[]
}

export interface LoadTestProgress {
  runId: string
  isComplete: boolean
  percentComplete: number
  totalExecutions: number
  successfulExecutions: number
  failedExecutions: number
  activeExecutions: number
  result?: LoadTestResult
}

export interface ElicitationRequest {
  id: string
  connectionName: string
  timestampUtc: string
  message?: string
  schema: Record<string, unknown>
  status: 'Pending' | 'Accepted' | 'Rejected'
}

export interface ElicitationHistoryEntry {
  request: ElicitationRequest
  response?: {
    requestId: string
    timestampUtc: string
    action: string
    content?: Record<string, unknown>
  }
}

// ── HTTP API Explorer types ──────────────────────────────────────────────────

export type HttpApiAuthenticationMode = 'None' | 'ApiKey' | 'Bearer' | 'AzureClientCredentials' | 'CustomHeaders'

export interface HttpApiHeader {
  name: string
  value: string
}

export interface HttpApiQueryParam {
  name: string
  value: string
  enabled: boolean
}

export interface HttpApiAzureCredentialsOptions {
  tenantId: string
  clientId: string
  clientSecret: string
  scope: string
  authorityHost?: string
  keyVaultSecretRef?: KeyVaultSecretReference
  subscriptionId?: string
}

export interface HttpApiApiKeyOptions {
  headerName: string
  apiKey: string
  prefix?: string
}

export interface HttpApiBearerOptions {
  token: string
}

export interface HttpApiDefinition {
  id: string
  name: string
  baseUrl: string
  method: string
  path: string
  authenticationMode: HttpApiAuthenticationMode
  headers: HttpApiHeader[]
  queryParams: HttpApiQueryParam[]
  bodyTemplate?: string | null
  groupName?: string | null
  tags: string[]
  note: string
  azureCredentials?: HttpApiAzureCredentialsOptions | null
  apiKeyOptions?: HttpApiApiKeyOptions | null
  bearerOptions?: HttpApiBearerOptions | null
  goldenSnapshotId?: string | null
  createdAt: string
  lastUpdatedAt?: string | null
  lastInvokedAt?: string | null
  lastStatusCode?: number | null
}

export interface HttpApiCollection {
  id: string
  name: string
  description: string
  endpointIds: string[]
  groupName?: string | null
  createdAt: string
  lastUpdatedAt?: string | null
  lastRunAt?: string | null
  lastRunDurationMs?: number | null
  lastRunSuccessCount?: number | null
  lastRunTotalCount?: number | null
  lastRunId?: string | null
  lastRunEndpointSummaries?: HttpApiCollectionEndpointRunSummary[] | null
  lastRunInvokedVia?: string | null
}

export interface HttpApiCollectionEndpointRunSummary {
  endpointId: string
  endpointName: string
  statusCode: number
  latencyMs: number
  isSuccess: boolean
  skipped: boolean
}

export interface HttpApiGroup {
  name: string
  color: string
  description?: string | null
}

export interface HttpResponseSnapshot {
  id: string
  endpointId: string
  endpointName: string
  capturedAt: string
  statusCode: number
  latencyMs: number
  responseHeaders: Record<string, string>
  inferredSchema: Record<string, unknown>
  rawBodyTruncated?: string | null
  contentType?: string | null
  label?: string | null
  isGolden: boolean
}

export interface HttpApiInvocationRecord {
  id: string
  endpointId: string
  endpointName: string
  invokedAt: string
  statusCode: number
  latencyMs: number
  schemaHash: string
  schemaMatchedSnapshot?: boolean | null
  collectionRunId?: string | null
  errorMessage?: string | null
  requestMethod?: string
  requestBaseUrl?: string
  requestPath?: string
  requestHeaders?: Record<string, string>
  requestQueryParams?: Record<string, string>
  responseHeaders?: Record<string, string>
  contentType?: string | null
  body?: string | null
  invokedVia?: string | null
}

export interface SchemaPropertyChange {
  propertyPath: string
  previousType: string
  currentType: string
}

export interface HttpSchemaComparisonResult {
  endpointId: string
  endpointName: string
  snapshotId?: string | null
  comparedAt: string
  liveStatusCode: number
  snapshotStatusCode: number
  liveLatencyMs: number
  snapshotLatencyMs: number
  addedProperties: string[]
  removedProperties: string[]
  changedTypes: SchemaPropertyChange[]
  statusCodeChanged: boolean
  isBreaking: boolean
  isDegraded: boolean
  latencyRatio: number
}

export interface HttpApiInvokeResponse {
  statusCode: number
  latencyMs: number
  responseHeaders: Record<string, string>
  contentType?: string | null
  body?: string | null
  inferredSchema: Record<string, unknown>
  schemaHash: string
  errorMessage?: string | null
}

export interface HttpApiCompareResponse {
  comparison: HttpSchemaComparisonResult
  liveResponse: {
    statusCode: number
    latencyMs: number
    inferredSchema: Record<string, unknown>
    body?: string | null
    contentType?: string | null
  }
}

export interface HttpApiExportPayload {
  version: number
  salt: string
  nonce: string
  data: string
}

export interface HttpApiCollectionRunRecord {
  runId: string
  collectionId: string
  collectionName: string
  ranAt: string
  durationMs: number
  successCount: number
  totalCount: number
  invokedVia?: string | null
  endpointSummaries: HttpApiCollectionEndpointRunSummary[]
}

export interface HttpApiCollectionRunResult {
  runId: string
  collectionId: string
  ranAt: string
  durationMs: number
  successCount: number
  totalCount: number
  invokedVia: string
  endpointSummaries: HttpApiCollectionEndpointRunSummary[]
  results: HttpApiCollectionRunItem[]
}

export interface HttpApiCollectionRunItem {
  endpointId: string
  endpointName: string
  statusCode: number
  latencyMs: number
  isSuccess: boolean
  comparison?: HttpSchemaComparisonResult | null
  inferredSchema?: Record<string, unknown>
  errorMessage?: string | null
  skipped?: boolean
  error?: string | null
}
