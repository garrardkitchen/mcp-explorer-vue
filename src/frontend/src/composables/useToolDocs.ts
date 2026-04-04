import { marked, type RendererObject } from 'marked'
import DOMPurify from 'dompurify'
import type { ActiveTool } from '@/api/types'

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

