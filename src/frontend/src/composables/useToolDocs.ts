import { marked } from 'marked'
import DOMPurify from 'dompurify'
import type { ActiveTool } from '@/api/types'

// Configure marked for safety
marked.setOptions({ gfm: true, breaks: false })

export function generateToolMarkdown(tool: ActiveTool): string {
  const schema = tool.inputSchema as any
  const props = schema?.properties ?? {}
  const required: string[] = schema?.required ?? []

  const lines: string[] = []

  lines.push(`# \`${tool.name}\``)
  lines.push('')
  if (tool.description) {
    lines.push(tool.description)
    lines.push('')
  }

  // Parameters table
  const paramKeys = Object.keys(props)
  if (paramKeys.length > 0) {
    lines.push('## Parameters')
    lines.push('')
    lines.push('| Name | Type | Required | Description |')
    lines.push('|------|------|:--------:|-------------|')
    for (const key of paramKeys) {
      const p = props[key]
      const type = Array.isArray(p.type)
        ? p.type.join(' \\| ')
        : (p.enum ? `enum` : (p.type ?? 'any'))
      const req = required.includes(key) ? '✓' : ''
      const desc = p.description ?? (p.enum ? `One of: ${p.enum.map((v: unknown) => `\`${v}\``).join(', ')}` : '')
      lines.push(`| \`${key}\` | \`${type}\` | ${req} | ${desc} |`)
    }
    lines.push('')
  } else {
    lines.push('## Parameters')
    lines.push('')
    lines.push('_This tool takes no parameters._')
    lines.push('')
  }

  // Input Schema (collapsible via details/summary in HTML, raw in markdown)
  if (schema) {
    lines.push('## Input Schema')
    lines.push('')
    lines.push('```json')
    lines.push(JSON.stringify(schema, null, 2))
    lines.push('```')
    lines.push('')
  }

  return lines.join('\n')
}

/**
 * Generates a combined markdown reference document for a list of tools.
 * Produces a top-level summary table followed by a section per tool.
 */
export function generateToolsListMarkdown(tools: ActiveTool[]): string {
  const lines: string[] = []

  lines.push(`# Tools Reference`)
  lines.push('')
  lines.push(`_${tools.length} tool${tools.length === 1 ? '' : 's'} listed._`)
  lines.push('')

  // Summary table
  lines.push('| Tool | Description |')
  lines.push('|------|-------------|')
  for (const tool of tools) {
    const desc = (tool.description ?? '').replace(/\|/g, '\\|').replace(/\n/g, ' ')
    lines.push(`| [\`${tool.name}\`](#${tool.name.toLowerCase().replace(/[^a-z0-9]+/g, '-')}) | ${desc} |`)
  }
  lines.push('')
  lines.push('---')
  lines.push('')

  // Per-tool sections
  for (const tool of tools) {
    const schema = tool.inputSchema as any
    const props = schema?.properties ?? {}
    const required: string[] = schema?.required ?? []

    const anchor = tool.name.toLowerCase().replace(/[^a-z0-9]+/g, '-')
    lines.push(`## \`${tool.name}\` {#${anchor}}`)
    lines.push('')
    if (tool.description) {
      lines.push(tool.description)
      lines.push('')
    }

    const paramKeys = Object.keys(props)
    if (paramKeys.length > 0) {
      lines.push('| Name | Type | Required | Description |')
      lines.push('|------|------|:--------:|-------------|')
      for (const key of paramKeys) {
        const p = props[key]
        const type = Array.isArray(p.type)
          ? p.type.join(' \\| ')
          : (p.enum ? 'enum' : (p.type ?? 'any'))
        const req = required.includes(key) ? '✓' : ''
        const desc = p.description ?? (p.enum ? `One of: ${p.enum.map((v: unknown) => `\`${v}\``).join(', ')}` : '')
        lines.push(`| \`${key}\` | \`${type}\` | ${req} | ${desc} |`)
      }
      lines.push('')
    } else {
      lines.push('_No parameters._')
      lines.push('')
    }

    lines.push('---')
    lines.push('')
  }

  return lines.join('\n')
}

export function renderMarkdown(md: string): string {
  const html = marked.parse(md)
  const htmlString = typeof html === 'string' ? html : ''
  return DOMPurify.sanitize(htmlString)
}
