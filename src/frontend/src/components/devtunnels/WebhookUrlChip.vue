<script setup lang="ts">
import { ref } from 'vue'
import Button from 'primevue/button'
import Popover from 'primevue/popover'
import type { DevTunnel } from '@/api/types'

defineProps<{
  tunnels: DevTunnel[]
}>()

const emit = defineEmits<{
  select: [url: string]
}>()

const popover = ref<InstanceType<typeof Popover> | null>(null)

function choose(url: string) {
  emit('select', url)
  popover.value?.hide()
}
</script>

<template>
  <div class="webhook-chip">
    <Button
      label="Use active tunnel"
      icon="pi pi-send"
      text
      size="small"
      class="chip-button"
      :disabled="!tunnels.length"
      @click="popover?.toggle($event)"
    />
    <Popover ref="popover">
      <div class="chip-popover">
        <div class="chip-head">Live tunnel webhook URLs</div>
        <button
          v-for="tunnel in tunnels"
          :key="tunnel.id"
          class="chip-option"
          @click="tunnel.webhookUri && choose(tunnel.webhookUri)"
        >
          <strong>{{ tunnel.name }}</strong>
          <span>{{ tunnel.webhookUri }}</span>
        </button>
        <p v-if="!tunnels.length" class="chip-empty">No running tunnels are ready to inject.</p>
      </div>
    </Popover>
  </div>
</template>

<style scoped>
.chip-button {
  justify-content: flex-start !important;
  padding: 0.2rem 0 !important;
  color: var(--info) !important;
}

.chip-popover {
  display: grid;
  gap: 0.45rem;
  min-width: 18rem;
  max-width: 28rem;
}

.chip-head {
  font-size: 0.7rem;
  letter-spacing: 0.14em;
  text-transform: uppercase;
  color: var(--text-muted);
  margin-bottom: 0.2rem;
}

.chip-option {
  display: grid;
  gap: 0.2rem;
  text-align: left;
  width: 100%;
  padding: 0.75rem 0.8rem;
  border-radius: 0.85rem;
  border: 1px solid color-mix(in srgb, var(--border) 70%, transparent);
  background: color-mix(in srgb, var(--bg-base) 60%, transparent);
  color: var(--text-primary);
  cursor: pointer;
}

.chip-option strong {
  font-size: 0.9rem;
}

.chip-option span {
  font-size: 0.75rem;
  color: var(--text-secondary);
  word-break: break-word;
}

.chip-empty {
  margin: 0;
  color: var(--text-muted);
}
</style>
