<script setup lang="ts">
import { ref, watch } from 'vue'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'
import Textarea from 'primevue/textarea'
import JsonViewer from '@/components/common/JsonViewer.vue'
import type { ReplayWebhookResult, WebhookEvent } from '@/api/types'

const props = defineProps<{
  visible: boolean
  event: WebhookEvent | null
  loading: boolean
  result: ReplayWebhookResult | null
}>()

const emit = defineEmits<{
  'update:visible': [value: boolean]
  submit: [payload: {
    targetUrl: string
    methodOverride?: string
    headersOverride?: Record<string, string>
    bodyTextOverride?: string
    bodyBase64Override?: string
    contentTypeOverride?: string
  }]
}>()

const targetUrl = ref('')
const methodOverride = ref('')
const contentTypeOverride = ref('')
const headersJson = ref('{}')
const bodyTextOverride = ref('')
const bodyBase64Override = ref('')
const localError = ref('')

const bodyOverrideMode = ref<'text' | 'base64' | 'none'>('none')

watch(() => props.visible, (visible) => {
  if (!visible) return
  targetUrl.value = ''
  methodOverride.value = props.event?.method ?? ''
  contentTypeOverride.value = props.event?.contentType ?? ''
  bodyTextOverride.value = props.event?.bodyText ?? ''
  bodyBase64Override.value = props.event?.bodyBase64 ?? ''
  bodyOverrideMode.value = props.event?.bodyText != null
    ? 'text'
    : props.event?.bodyBase64
      ? 'base64'
      : 'none'
  headersJson.value = JSON.stringify(props.event?.headers ?? {}, null, 2)
  localError.value = ''
})

function submit() {
  try {
    const parsedHeaders = headersJson.value.trim() ? JSON.parse(headersJson.value) as Record<string, string> : {}
    emit('submit', {
      targetUrl: targetUrl.value,
      methodOverride: methodOverride.value || undefined,
      headersOverride: parsedHeaders,
      bodyTextOverride: bodyOverrideMode.value !== 'base64' ? bodyTextOverride.value : undefined,
      bodyBase64Override: bodyOverrideMode.value === 'base64' ? bodyBase64Override.value || undefined : undefined,
      contentTypeOverride: contentTypeOverride.value || undefined,
    })
  } catch {
    localError.value = 'Headers override must be valid JSON.'
  }
}
</script>

<template>
  <Dialog
    :visible="visible"
    modal
    header="Replay captured webhook"
    :style="{ width: 'min(92vw, 64rem)' }"
    @update:visible="emit('update:visible', $event)"
  >
    <div class="replay-layout">
      <div class="replay-form">
        <label class="field">
          <span>Target URL</span>
          <InputText v-model="targetUrl" placeholder="https://example.com/webhooks/retry" />
        </label>

        <div class="field-grid">
          <label class="field">
            <span>Method</span>
            <InputText v-model="methodOverride" />
          </label>
          <label class="field">
            <span>Content type</span>
            <InputText v-model="contentTypeOverride" />
          </label>
        </div>

        <label class="field">
          <span>Headers override (JSON)</span>
          <Textarea v-model="headersJson" autoResize rows="6" />
        </label>

        <label class="field">
          <span>{{ bodyOverrideMode === 'base64' ? 'Body override (base64)' : 'Body override' }}</span>
          <Textarea
            v-if="bodyOverrideMode === 'base64'"
            v-model="bodyBase64Override"
            autoResize
            rows="10"
            placeholder="Original binary payload encoded as base64."
          />
          <Textarea
            v-else
            v-model="bodyTextOverride"
            autoResize
            rows="10"
            :placeholder="bodyOverrideMode === 'none' ? 'No body captured for this event.' : undefined"
          />
          <small v-if="bodyOverrideMode === 'base64'" class="field-hint">
            This event was captured as binary data. Leave the base64 payload unchanged to replay the original body.
          </small>
        </label>

        <p v-if="localError" class="inline-error">{{ localError }}</p>
      </div>

      <div class="replay-result">
        <div class="result-head">
          <h4>Replay response</h4>
          <span v-if="result">HTTP {{ result.statusCode }} · {{ result.duration }}</span>
        </div>

        <JsonViewer
          v-if="result"
          :data="{
            statusCode: result.statusCode,
            reasonPhrase: result.reasonPhrase,
            bodySize: result.bodySize,
            headers: result.headers,
            bodyText: result.bodyText,
          }"
          title="Replay result"
          :initially-expanded="true"
        />
        <div v-else class="result-empty">
          Fire a replay to inspect the downstream response here.
        </div>
      </div>
    </div>

    <template #footer>
      <Button label="Close" text @click="emit('update:visible', false)" />
      <Button label="Send replay" icon="pi pi-send" :loading="loading" @click="submit" />
    </template>
  </Dialog>
</template>

<style scoped>
.replay-layout {
  display: grid;
  grid-template-columns: minmax(0, 1.1fr) minmax(0, 0.9fr);
  gap: 1rem;
}

.replay-form,
.replay-result {
  display: grid;
  gap: 0.9rem;
}

.field {
  display: grid;
  gap: 0.45rem;
}

.field span {
  font-size: 0.76rem;
  letter-spacing: 0.14em;
  text-transform: uppercase;
  color: var(--text-muted);
}

.field-hint {
  color: var(--text-muted);
  line-height: 1.5;
}

.field-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0.85rem;
}

.result-head {
  display: flex;
  justify-content: space-between;
  gap: 1rem;
  align-items: center;
}

.result-head h4 {
  margin: 0;
  color: var(--text-primary);
}

.result-empty,
.inline-error {
  padding: 1rem;
  border-radius: 1rem;
  background: color-mix(in srgb, var(--bg-base) 60%, transparent);
  color: var(--text-secondary);
}

.inline-error {
  color: var(--danger);
  margin: 0;
}

@media (max-width: 980px) {
  .replay-layout,
  .field-grid {
    grid-template-columns: 1fr;
  }
}
</style>
