<template>
  <div class="step-progress">
    <!-- Waiting for the server -->
    <div v-if="busy && !steps" class="busy-row">
      <ProgressSpinner style="width:18px;height:18px" strokeWidth="5" />
      <span>{{ busyLabel || 'Working…' }}</span>
    </div>

    <!-- Step results (revealed one by one) -->
    <template v-if="steps">
      <div
        v-for="(step, idx) in steps"
        :key="step.id"
        class="step-row"
        :class="stepClass(step, idx)"
      >
        <span class="step-mark">
          <ProgressSpinner v-if="idx === revealed && revealed < steps.length" style="width:14px;height:14px" strokeWidth="6" />
          <i v-else-if="idx < revealed && step.status === 'Succeeded'" class="pi pi-check" />
          <i v-else-if="idx < revealed && step.status === 'Failed'" class="pi pi-times" />
          <i v-else-if="idx < revealed && step.status === 'Skipped'" class="pi pi-minus" />
          <span v-else class="step-num">{{ idx + 1 }}</span>
        </span>
        <div class="step-body">
          <span class="step-label">{{ step.label }}</span>
          <span v-if="idx < revealed && step.message" class="step-message">{{ step.message }}</span>
        </div>
      </div>

      <div v-if="fullyRevealed && failed && showRetry" class="retry-row">
        <Button label="Retry" icon="pi pi-refresh" size="small" severity="warn" outlined @click="emit('retry')" />
      </div>
    </template>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import Button from 'primevue/button'
import ProgressSpinner from 'primevue/progressspinner'
import type { StepResult } from '@/api/types'

const props = withDefaults(defineProps<{
  steps?: StepResult[] | null
  busy?: boolean
  busyLabel?: string
  showRetry?: boolean
}>(), {
  steps: null,
  busy: false,
  showRetry: true,
})

const emit = defineEmits<{
  retry: []
  /** Fired once every step has been revealed. */
  revealed: []
}>()

const revealed = ref(0)
let timer: ReturnType<typeof setTimeout> | null = null

const fullyRevealed = computed(() => !!props.steps && revealed.value >= props.steps.length)
const failed = computed(() => !!props.steps?.some(s => s.status === 'Failed'))

watch(() => props.steps, (steps) => {
  if (timer) { clearTimeout(timer); timer = null }
  revealed.value = 0
  if (!steps?.length) return
  const revealNext = () => {
    revealed.value += 1
    if (revealed.value < steps.length) {
      timer = setTimeout(revealNext, 350)
    }
    else {
      timer = null
      emit('revealed')
    }
  }
  timer = setTimeout(revealNext, 350)
}, { immediate: true })

function stepClass(step: StepResult, idx: number) {
  if (idx >= revealed.value) return 'pending'
  if (step.status === 'Succeeded') return 'done'
  if (step.status === 'Failed') return 'fail'
  return 'skipped'
}
</script>

<style scoped>
.step-progress {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.busy-row {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 12.5px;
  color: var(--text-secondary);
}

.step-row {
  display: flex;
  align-items: flex-start;
  gap: 9px;
}

.step-mark {
  width: 18px;
  height: 18px;
  border-radius: 50%;
  border: 1.5px solid var(--border);
  display: inline-flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  font-size: 10px;
  margin-top: 1px;
  color: var(--text-muted);
}

.step-row.done .step-mark {
  border-color: var(--success);
  background: color-mix(in srgb, var(--success) 15%, transparent);
  color: var(--success);
}

.step-row.fail .step-mark {
  border-color: var(--danger);
  background: color-mix(in srgb, var(--danger) 14%, transparent);
  color: var(--danger);
}

.step-row.skipped .step-mark {
  border-style: dashed;
}

.step-row.pending .step-mark {
  border-style: dashed;
  opacity: .6;
}

.step-num {
  font-size: 9.5px;
  font-weight: 700;
}

.step-body {
  display: flex;
  flex-direction: column;
  gap: 1px;
  min-width: 0;
}

.step-label {
  font-size: 12.5px;
  color: var(--text-secondary);
}

.step-row.done .step-label,
.step-row.fail .step-label {
  color: var(--text-primary);
}

.step-row.fail .step-label {
  color: var(--danger);
}

.step-row.pending .step-label {
  opacity: .55;
}

.step-message {
  font-size: 11.5px;
  color: var(--text-muted);
  overflow-wrap: anywhere;
}

.step-row.fail .step-message {
  color: color-mix(in srgb, var(--danger) 80%, var(--text-primary));
}

.retry-row {
  margin-top: 4px;
}
</style>
