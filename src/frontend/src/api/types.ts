// src/api/types.ts
// TypeScript types mirroring the Core domain models

export type ConnectionAuthenticationMode = 'CustomHeaders' | 'AzureClientCredentials' | 'OAuth'

export interface ConnectionHeader {
  name: string
  value: string
  authorizationType: string
  isAuthorization: boolean
}

export interface AzureClientCredentialsOptions {
  tenantId: string
  clientId: string
  clientSecret: string
  scope: string
  authorityHost?: string
}

export interface OAuthConnectionOptions {
  clientId: string
  clientSecret?: string
  redirectUri: string
  scopes: string
  clientMetadataDocumentUri?: string
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
  toolCount: number
}

export interface ActiveTool {
  name: string
  description: string
  inputSchema?: Record<string, unknown>
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
  llmModels: LlmModelDefinition[]
  selectedLlmModelName?: string
  sensitiveFieldConfig: SensitiveFieldConfiguration
  showConnectionTimestamps: boolean
  connectionSortOrder: string
  showConnectionGroups: boolean
  theme: string
}

export type ErrorHandlingMode = 'StopOnError' | 'ContinueOnError'

export interface ParameterMapping {
  parameterName: string
  value?: string
  sourceStepNumber?: number
  sourcePropertyName?: string
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

export interface WorkflowStepResult {
  stepNumber: number
  toolName: string
  startedUtc: string
  completedUtc?: string
  success: boolean
  result?: unknown
  errorMessage?: string
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

export interface LoadTestResult {
  workflowId: string
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
