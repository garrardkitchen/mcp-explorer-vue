<script setup lang="ts">
import { computed, ref } from 'vue'
import Dialog from 'primevue/dialog'
import Tabs from 'primevue/tabs'
import Tab from 'primevue/tab'
import TabList from 'primevue/tablist'
import TabPanel from 'primevue/tabpanel'
import TabPanels from 'primevue/tabpanels'
import Button from 'primevue/button'
import { useToast } from 'primevue/usetoast'
import type { ActiveTool } from '@/api/types'
import { generateToolMarkdown, generateToolsListMarkdown, renderMarkdown } from '@/composables/useToolDocs'

const props = defineProps<{
  /** Single-tool mode — show docs for one tool */
  tool?: ActiveTool | null
  /** List mode — show combined reference for multiple tools */
  tools?: ActiveTool[]
  visible: boolean
}>()

const emit = defineEmits<{
  'update:visible': [value: boolean]
}>()

const toast = useToast()
const activeTab = ref('preview')
const previewRef = ref<HTMLElement | null>(null)

const isListMode = computed(() => Array.isArray(props.tools) && props.tools.length > 0)

const dialogHeader = computed(() => {
  if (isListMode.value) return `Documentation: ${props.tools!.length} tool${props.tools!.length === 1 ? '' : 's'}`
  return props.tool ? `Documentation: ${props.tool.name}` : 'Documentation'
})

const markdown = computed(() => {
  if (isListMode.value) return generateToolsListMarkdown(props.tools!)
  return props.tool ? generateToolMarkdown(props.tool) : ''
})

const renderedHtml = computed(() => renderMarkdown(markdown.value))

async function copyMarkdown() {
  try {
    await navigator.clipboard.writeText(markdown.value)
    toast.add({ severity: 'success', summary: 'Markdown copied', life: 2000 })
  } catch {
    toast.add({ severity: 'error', summary: 'Copy failed', life: 3000 })
  }
}

/** Intercept in-page anchor clicks and scroll within the preview container. */
function onPreviewClick(e: MouseEvent) {
  const target = e.target as HTMLElement
  const anchor = target.closest('a') as HTMLAnchorElement | null
  if (!anchor) return

  const href = anchor.getAttribute('href')
  if (!href?.startsWith('#')) return

  e.preventDefault()
  const id = href.slice(1)
  if (!previewRef.value) return

  // Try matching by id attribute first, then by generated slug from heading text
  let el = previewRef.value.querySelector<HTMLElement>(`#${CSS.escape(id)}`)

  if (!el) {
    // Fallback: match headings whose text slugifies to the id
    const headings = previewRef.value.querySelectorAll<HTMLElement>('h1,h2,h3,h4,h5,h6')
    for (const h of headings) {
      if (slugify(h.textContent ?? '') === id) { el = h; break }
    }
  }

  el?.scrollIntoView({ behavior: 'smooth', block: 'start' })
}

function slugify(text: string): string {
  return text
    .toLowerCase()
    .replace(/[^\w\s-]/g, '')
    .trim()
    .replace(/\s+/g, '-')
}
</script>

<template>
  <Dialog
    :visible="visible"
    @update:visible="emit('update:visible', $event)"
    modal
    :header="dialogHeader"
    :style="{ width: '780px', maxHeight: '85vh' }"
    :pt="{ content: { style: 'padding: 0; overflow: hidden;' } }"
  >
    <Tabs v-model:value="activeTab" class="docs-tabs">
      <TabList>
        <Tab value="preview">
          <i class="pi pi-eye" style="margin-right: 6px" />
          Preview
        </Tab>
        <Tab value="raw">
          <i class="pi pi-code" style="margin-right: 6px" />
          Raw Markdown
        </Tab>
      </TabList>
      <TabPanels>
        <TabPanel value="preview">
          <div class="docs-preview" ref="previewRef" v-html="renderedHtml" @click="onPreviewClick" />
        </TabPanel>
        <TabPanel value="raw">
          <div class="docs-raw-bar">
            <Button icon="pi pi-copy" text size="small" label="Copy Markdown" @click="copyMarkdown" />
          </div>
          <pre class="docs-raw">{{ markdown }}</pre>
        </TabPanel>
      </TabPanels>
    </Tabs>
  </Dialog>
</template>

<style scoped>
.docs-tabs { height: 100%; display: flex; flex-direction: column; }
.docs-preview {
  padding: 20px 24px;
  overflow-y: auto;
  max-height: 60vh;
  color: var(--text-primary);
  font-size: 14px;
  line-height: 1.7;
}
.docs-raw-bar {
  padding: 8px 12px;
  border-bottom: 1px solid var(--border);
  display: flex;
  justify-content: flex-end;
}
.docs-raw {
  padding: 16px 20px;
  margin: 0;
  font-family: var(--font-family-mono);
  font-size: 12px;
  line-height: 1.6;
  color: var(--text-secondary);
  white-space: pre-wrap;
  word-break: break-word;
  overflow-y: auto;
  max-height: 55vh;
  background: var(--code-bg);
}
</style>

<style>
/* Markdown preview styles — global so they affect v-html content */
.docs-preview h1 { font-size: 22px; font-weight: 700; margin: 0 0 16px; color: var(--text-primary); border-bottom: 1px solid var(--border); padding-bottom: 8px; }
.docs-preview h2 { font-size: 16px; font-weight: 600; margin: 20px 0 10px; color: var(--text-primary); }
.docs-preview h3 { font-size: 14px; font-weight: 600; margin: 16px 0 8px; color: var(--text-secondary); }
.docs-preview p { margin: 0 0 12px; }
.docs-preview code { font-family: var(--font-family-mono); font-size: 12px; background: var(--code-bg); padding: 2px 5px; border-radius: 3px; color: var(--json-str, #85e89d); }
.docs-preview pre { background: var(--code-bg); border: 1px solid var(--border); border-radius: var(--border-radius-sm); padding: 14px; margin: 10px 0; overflow-x: auto; }
.docs-preview pre code { background: none; padding: 0; color: var(--text-secondary); font-size: 12px; }
.docs-preview table { width: 100%; border-collapse: collapse; margin: 10px 0; font-size: 13px; }
.docs-preview th { background: var(--bg-raised); padding: 8px 12px; text-align: left; font-weight: 600; color: var(--text-secondary); border: 1px solid var(--border); }
.docs-preview td { padding: 7px 12px; border: 1px solid var(--border); color: var(--text-primary); vertical-align: top; }
.docs-preview tr:nth-child(even) td { background: var(--bg-raised); }
.docs-preview em { font-style: italic; color: var(--text-muted); }
</style>
