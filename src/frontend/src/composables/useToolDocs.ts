import { marked, type RendererObject } from 'marked'
import DOMPurify from 'dompurify'
import type { ActiveTool, ActivePrompt, ActiveResource, ActiveResourceTemplate } from '@/api/types'

// ---------------------------------------------------------------------------
// Custom renderer: extract {#id} Pandoc-style anchors from headings and emit
// a real HTML id attribute so in-page anchor links work inside the dialog.
// ---------------------------------------------------------------------------
const renderer: RendererObject = {
  heading({ text, depth }: { text: string; depth: number }) {
    // Extract explicit {#anchor} if present
    const idMatch = text.match(/\{#([\w-]+)\}/)
    const id = idMatch ? idMatch[1] : toolSlug(text.replace(/`/g, '').trim())
    const cleanText = text.replace(/\s*\{#[\w-]+\}/, '').trim()
    return `<h${depth} id="${id}">${cleanText}</h${depth}>\n`
  },
}

marked.use({ renderer, gfm: true, breaks: false })

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/** Slug used for both link hrefs and heading ids — must be identical. */
function toolSlug(name: string): string {
  return name.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '')
}

function schemaBlock(schema: object, title: string): string[] {
  return [
    `### ${title}`,
    '',
    '```json',
    JSON.stringify(schema, null, 2),
    '```',
    '',
  ]
}

function paramTable(schema: Record<string, unknown> | undefined): string[] {
  const props = (schema as any)?.properties ?? {}
  const required: string[] = (schema as any)?.required ?? []
  const keys = Object.keys(props)

  if (keys.length === 0) return ['_No parameters._', '']

  const lines = [
    '| Name | Type | Required | Description |',
    '|------|------|:--------:|-------------|',
  ]
  for (const key of keys) {
    const p = props[key] as any
    const type = Array.isArray(p.type)
      ? p.type.join(' \\| ')
      : (p.enum ? 'enum' : (p.type ?? 'any'))
    const req = required.includes(key) ? '✓' : ''
    const desc = p.description ?? (p.enum ? `One of: ${(p.enum as unknown[]).map(v => `\`${v}\``).join(', ')}` : '')
    lines.push(`| \`${key}\` | \`${type}\` | ${req} | ${desc} |`)
  }
  lines.push('')
  return lines
}

function annotationsBadges(tool: ActiveTool): string[] {
  const ann = tool.annotations
  if (!ann) return []

  const hints: string[] = []
  if (ann.readOnlyHint === true)    hints.push('🔒 Read-only')
  if (ann.destructiveHint === true) hints.push('⚠️ Destructive')
  if (ann.idempotentHint === true)  hints.push('♻️ Idempotent')
  if (ann.openWorldHint === true)   hints.push('🌐 Open-world')

  if (hints.length === 0 && !ann.title) return []

  const lines: string[] = []
  if (ann.title) lines.push(`> **${ann.title}**`, '')
  if (hints.length) lines.push(hints.join(' · '), '')
  return lines
}

// ---------------------------------------------------------------------------
// Single-tool markdown
// ---------------------------------------------------------------------------
export function generateToolMarkdown(tool: ActiveTool): string {
  const lines: string[] = []

  lines.push(`# \`${tool.name}\``)
  lines.push('')
  if (tool.description) { lines.push(tool.description); lines.push('') }

  lines.push(...annotationsBadges(tool))

  // Parameters table
  lines.push('## Parameters')
  lines.push('')
  lines.push(...paramTable(tool.inputSchema as Record<string, unknown> | undefined))

  // Input schema raw
  if (tool.inputSchema) {
    lines.push('## Input Schema')
    lines.push('')
    lines.push(...schemaBlock(tool.inputSchema, ''))
    // schemaBlock already ends with blank, remove the duplicate heading
    lines.splice(-1, 0) // no-op — keep as-is
  }

  // Output schema
  if (tool.outputSchema) {
    lines.push('## Output Schema')
    lines.push('')
    lines.push(...schemaBlock(tool.outputSchema, ''))
  }

  return lines.join('\n')
}

// ---------------------------------------------------------------------------
// List markdown (collection docs)
// ---------------------------------------------------------------------------
export function generateToolsListMarkdown(tools: ActiveTool[]): string {
  const lines: string[] = []

  lines.push('# Tools Reference')
  lines.push('')
  lines.push(`_${tools.length} tool${tools.length === 1 ? '' : 's'} listed._`)
  lines.push('')

  // Summary table with anchor links
  lines.push('| Tool | Description |')
  lines.push('|------|-------------|')
  for (const tool of tools) {
    const desc = (tool.description ?? '').replace(/\|/g, '\\|').replace(/\n/g, ' ')
    lines.push(`| [\`${tool.name}\`](#${toolSlug(tool.name)}) | ${desc} |`)
  }
  lines.push('')
  lines.push('---')
  lines.push('')

  // Per-tool sections — heading id matches toolSlug(tool.name) exactly
  for (const tool of tools) {
    const slug = toolSlug(tool.name)

    lines.push(`## \`${tool.name}\` {#${slug}}`)
    lines.push('')
    if (tool.description) { lines.push(tool.description); lines.push('') }

    lines.push(...annotationsBadges(tool))

    // Parameters
    lines.push('### Parameters')
    lines.push('')
    lines.push(...paramTable(tool.inputSchema as Record<string, unknown> | undefined))

    // Input schema
    if (tool.inputSchema) {
      lines.push(...schemaBlock(tool.inputSchema, 'Input Schema'))
    }

    // Output schema
    if (tool.outputSchema) {
      lines.push(...schemaBlock(tool.outputSchema, 'Output Schema'))
    }

    lines.push('---')
    lines.push('')
  }

  return lines.join('\n')
}

// ---------------------------------------------------------------------------
// Render
// ---------------------------------------------------------------------------
export function renderMarkdown(md: string): string {
  const html = marked.parse(md)
  const htmlString = typeof html === 'string' ? html : ''
  return DOMPurify.sanitize(htmlString)
}

// ---------------------------------------------------------------------------
// Prompts
// ---------------------------------------------------------------------------
function promptArgTable(args: ActivePrompt['arguments']): string[] {
  if (!args?.length) return ['_No arguments._', '']
  const lines = [
    '| Name | Required | Description |',
    '|------|:--------:|-------------|',
  ]
  for (const a of args) {
    lines.push(`| \`${a.name}\` | ${a.required ? '✓' : ''} | ${a.description ?? ''} |`)
  }
  lines.push('')
  return lines
}

function itemSlug(name: string): string {
  return name.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '')
}

export function generatePromptMarkdown(prompt: ActivePrompt): string {
  const lines: string[] = []
  lines.push(`# \`${prompt.name}\``, '')
  if (prompt.description) { lines.push(prompt.description, '') }
  lines.push('## Arguments', '')
  lines.push(...promptArgTable(prompt.arguments))
  return lines.join('\n')
}

export function generatePromptsListMarkdown(prompts: ActivePrompt[]): string {
  const lines: string[] = []
  lines.push('# Prompts Reference', '')
  lines.push(`_${prompts.length} prompt${prompts.length === 1 ? '' : 's'} listed._`, '')
  lines.push('| Prompt | Arguments | Description |')
  lines.push('|--------|:---------:|-------------|')
  for (const p of prompts) {
    const desc = (p.description ?? '').replace(/\|/g, '\\|').replace(/\n/g, ' ')
    const argc = p.arguments?.length ?? 0
    lines.push(`| [\`${p.name}\`](#${itemSlug(p.name)}) | ${argc} | ${desc} |`)
  }
  lines.push('', '---', '')
  for (const p of prompts) {
    const slug = itemSlug(p.name)
    lines.push(`## \`${p.name}\` {#${slug}}`, '')
    if (p.description) { lines.push(p.description, '') }
    lines.push('### Arguments', '')
    lines.push(...promptArgTable(p.arguments))
    lines.push('---', '')
  }
  return lines.join('\n')
}

// ---------------------------------------------------------------------------
// Resources
// ---------------------------------------------------------------------------
export function generateResourceMarkdown(resource: ActiveResource): string {
  const lines: string[] = []
  lines.push(`# ${resource.name}`, '')
  lines.push(`**URI:** \`${resource.uri}\``, '')
  if (resource.mimeType) { lines.push(`**MIME Type:** \`${resource.mimeType}\``, '') }
  if (resource.description) { lines.push(resource.description, '') }
  return lines.join('\n')
}

export function generateResourcesListMarkdown(resources: ActiveResource[]): string {
  const lines: string[] = []
  lines.push('# Resources Reference', '')
  lines.push(`_${resources.length} resource${resources.length === 1 ? '' : 's'} listed._`, '')
  lines.push('| Name | URI | MIME Type | Description |')
  lines.push('|------|-----|-----------|-------------|')
  for (const r of resources) {
    const desc = (r.description ?? '').replace(/\|/g, '\\|').replace(/\n/g, ' ')
    const slug = itemSlug(r.name)
    lines.push(`| [${r.name}](#${slug}) | \`${r.uri}\` | ${r.mimeType ?? ''} | ${desc} |`)
  }
  lines.push('', '---', '')
  for (const r of resources) {
    const slug = itemSlug(r.name)
    lines.push(`## ${r.name} {#${slug}}`, '')
    lines.push(`**URI:** \`${r.uri}\``, '')
    if (r.mimeType) { lines.push(`**MIME Type:** \`${r.mimeType}\``, '') }
    if (r.description) { lines.push(r.description, '') }
    lines.push('---', '')
  }
  return lines.join('\n')
}

// ---------------------------------------------------------------------------
// Resource Templates
// ---------------------------------------------------------------------------
function templateVarTable(uriTemplate: string): string[] {
  const vars = [...uriTemplate.matchAll(/\{([^}]+)\}/g)].map(m => m[1])
  if (!vars.length) return ['_No parameters._', '']
  const lines = ['| Parameter | Description |', '|-----------|-------------|']
  for (const v of vars) {
    lines.push(`| \`${v}\` | Substitute into URI template |`)
  }
  lines.push('')
  return lines
}

export function generateResourceTemplateMarkdown(template: ActiveResourceTemplate): string {
  const lines: string[] = []
  lines.push(`# ${template.name}`, '')
  lines.push(`**URI Template:** \`${template.uriTemplate}\``, '')
  if (template.description) { lines.push(template.description, '') }
  lines.push('## Parameters', '')
  lines.push(...templateVarTable(template.uriTemplate))
  return lines.join('\n')
}

export function generateResourceTemplatesListMarkdown(templates: ActiveResourceTemplate[]): string {
  const lines: string[] = []
  lines.push('# Resource Templates Reference', '')
  lines.push(`_${templates.length} template${templates.length === 1 ? '' : 's'} listed._`, '')
  lines.push('| Name | URI Template | Description |')
  lines.push('|------|-------------|-------------|')
  for (const t of templates) {
    const desc = (t.description ?? '').replace(/\|/g, '\\|').replace(/\n/g, ' ')
    const slug = itemSlug(t.name)
    lines.push(`| [${t.name}](#${slug}) | \`${t.uriTemplate}\` | ${desc} |`)
  }
  lines.push('', '---', '')
  for (const t of templates) {
    const slug = itemSlug(t.name)
    lines.push(`## ${t.name} {#${slug}}`, '')
    lines.push(`**URI Template:** \`${t.uriTemplate}\``, '')
    if (t.description) { lines.push(t.description, '') }
    lines.push('### Parameters', '')
    lines.push(...templateVarTable(t.uriTemplate))
    lines.push('---', '')
  }
  return lines.join('\n')
}

