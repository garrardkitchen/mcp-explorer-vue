<script setup lang="ts">
import { computed, watch, ref } from 'vue'
import Dialog from 'primevue/dialog'
import type { DevTunnelUserState } from '@/api/types'

const props = defineProps<{
  visible: boolean
  lines: string[]
  loading: boolean
  userState: DevTunnelUserState | null
  phase?: 'idle' | 'connecting' | 'connected' | 'starting-tunnel'
}>()

const emit = defineEmits<{
  'update:visible': [value: boolean]
  start: []
}>()

const copied = ref(false)
const copyFailed = ref(false)

// Parse the devtunnel CLI output (device-code flow) for a verification URL
// and a device code. Example line formats:
//   "To sign in, use a web browser to open the page https://microsoft.com/devicelogin
//    and enter the code ABCD-WXYZ to authenticate."
//   "Use code: ABCD-WXYZ at https://microsoft.com/devicelogin"
const verificationUrl = computed(() => {
  const joined = props.lines.join('\n')
  const m = joined.match(/https?:\/\/[^\s]*(devicelogin|github\.com\/login\/device)[^\s]*/i)
  return m ? m[0].replace(/[).,]+$/, '') : null
})

const deviceCode = computed(() => {
  const joined = props.lines.join(' ')
  // Matches typical MS/GitHub device codes: letters, digits, hyphen, length 6-12
  const patterns = [
    /enter the code\s+([A-Z0-9-]{6,12})/i,
    /code[:\s]+([A-Z0-9-]{6,12})\b/i,
    /\b([A-Z]{4}-[A-Z]{4})\b/,
    /\b([A-Z0-9]{8,10})\s+to authenticate/i,
  ]
  for (const p of patterns) {
    const m = joined.match(p)
    if (m) return m[1]
  }
  return null
})

const headline = computed(() => {
  if (props.phase === 'starting-tunnel') return 'Connected — starting tunnel'
  if (props.phase === 'connected') return 'Connected'
  if (props.loading || props.phase === 'connecting') return deviceCode.value ? 'Authorize this workspace' : 'Preparing connection…'
  return 'Connect to Dev Tunnels'
})

const subline = computed(() => {
  if (props.phase === 'starting-tunnel') return 'Bringing your tunnel online…'
  if (props.phase === 'connected') return 'Your workspace is linked. You can now host tunnels.'
  if (deviceCode.value && verificationUrl.value)
    return 'Open the verification page in your browser and enter the code below to link this workspace.'
  return 'We\'ll link this workspace to Dev Tunnels so you can host public callback URLs.'
})

async function copyCode() {
  if (!deviceCode.value) return

  const resetFlags = () => {
    setTimeout(() => {
      copied.value = false
      copyFailed.value = false
    }, 2000)
  }

  try {
    if (navigator.clipboard && window.isSecureContext) {
      await navigator.clipboard.writeText(deviceCode.value)
      copied.value = true
      copyFailed.value = false
      resetFlags()
      return
    }
  } catch {
    // fall back to the legacy copy path
  }

  try {
    const ta = document.createElement('textarea')
    ta.value = deviceCode.value
    ta.setAttribute('readonly', '')
    ta.style.position = 'fixed'
    ta.style.top = '-1000px'
    ta.style.opacity = '0'
    document.body.appendChild(ta)
    ta.select()
    ta.setSelectionRange(0, deviceCode.value.length)
    const ok = document.execCommand('copy')
    document.body.removeChild(ta)
    copied.value = ok
    copyFailed.value = !ok
    resetFlags()
  } catch {
    copied.value = false
    copyFailed.value = true
    resetFlags()
  }
}

function openVerification() {
  if (verificationUrl.value)
    window.open(verificationUrl.value, '_blank', 'noopener,noreferrer')
}

// Auto-start the device-code stream as soon as the dialog opens.
watch(() => props.visible, (isOpen) => {
  if (isOpen && !props.loading && props.phase !== 'connected' && props.phase !== 'starting-tunnel')
    emit('start')
})
</script>

<template>
  <Dialog
    :visible="visible"
    modal
    :closable="phase !== 'starting-tunnel'"
    header=""
    :style="{ width: 'min(92vw, 44rem)' }"
    @update:visible="emit('update:visible', $event)"
  >
    <div class="connect-dialog">
      <header class="connect-hero">
        <div class="eyebrow">Dev Tunnels · Workspace link</div>
        <h3>{{ headline }}</h3>
        <p>{{ subline }}</p>
      </header>

      <section v-if="deviceCode && verificationUrl && phase !== 'connected' && phase !== 'starting-tunnel'" class="code-card">
        <div class="code-step">
          <span class="step-num">1</span>
          <div class="step-copy">
            <span class="step-label">Open verification page</span>
            <button class="url-chip" @click="openVerification">
              <i class="pi pi-external-link" />
              <span>{{ verificationUrl }}</span>
            </button>
          </div>
        </div>
        <div class="code-step">
          <span class="step-num">2</span>
          <div class="step-copy">
            <span class="step-label">Enter this code</span>
            <button class="code-chip" @click="copyCode" :aria-label="'Copy code ' + deviceCode">
              <span class="code-text">{{ deviceCode }}</span>
                <span class="code-copy">
                  <i :class="copied ? 'pi pi-check' : copyFailed ? 'pi pi-times' : 'pi pi-copy'" />
                  {{ copied ? 'Copied' : copyFailed ? 'Copy failed' : 'Copy' }}
                </span>
              </button>
            </div>
          </div>
        <div class="code-hint">
          <i class="pi pi-spin pi-spinner" />
          Waiting for you to complete the authorization…
        </div>
      </section>

      <section v-else-if="phase === 'connected' || phase === 'starting-tunnel'" class="success-card">
        <i class="pi pi-check-circle" />
        <div>
          <strong>{{ userState?.userName ? `Linked as ${userState.userName}` : 'Workspace linked' }}</strong>
          <span>{{ phase === 'starting-tunnel' ? 'Opening tunnel host…' : 'You can close this dialog.' }}</span>
        </div>
      </section>

      <section v-else-if="loading" class="pending-card">
        <i class="pi pi-spin pi-spinner" />
        <span>Asking the Dev Tunnels service for a device code…</span>
      </section>

      <details class="raw-output" v-if="lines.length">
        <summary>CLI output</summary>
        <pre>{{ lines.join('\n') }}</pre>
      </details>
    </div>
  </Dialog>
</template>

<style scoped>
.connect-dialog {
  display: grid;
  gap: 1.1rem;
}

.eyebrow {
  font-size: 0.66rem;
  letter-spacing: 0.26em;
  text-transform: uppercase;
  color: color-mix(in srgb, var(--info) 80%, var(--text-muted));
  margin-bottom: 0.4rem;
}

.connect-hero {
  padding: 1.25rem 1.3rem;
  border-radius: 1.15rem;
  background:
    radial-gradient(circle at top right, color-mix(in srgb, var(--info) 24%, transparent), transparent 45%),
    linear-gradient(135deg, color-mix(in srgb, var(--bg-surface) 94%, #08101a 6%), color-mix(in srgb, var(--bg-raised) 92%, #121f2d 8%));
  border: 1px solid color-mix(in srgb, var(--border) 68%, transparent);
}

.connect-hero h3 {
  margin: 0;
  font-family: 'Iowan Old Style', 'Palatino Linotype', Georgia, serif;
  color: var(--text-primary);
  font-size: 1.7rem;
  line-height: 1.2;
}

.connect-hero p {
  margin: 0.65rem 0 0;
  color: var(--text-secondary);
  line-height: 1.55;
}

.code-card {
  display: grid;
  gap: 0.9rem;
  padding: 1.15rem;
  border-radius: 1.1rem;
  background: color-mix(in srgb, var(--bg-surface) 96%, #000 4%);
  border: 1px solid color-mix(in srgb, var(--border) 66%, transparent);
}

.code-step {
  display: grid;
  grid-template-columns: auto 1fr;
  gap: 0.9rem;
  align-items: start;
}

.step-num {
  width: 1.65rem;
  height: 1.65rem;
  border-radius: 50%;
  display: grid;
  place-items: center;
  font-weight: 600;
  color: #081019;
  background: linear-gradient(135deg, #93c5fd, #c4b5fd);
  font-size: 0.85rem;
}

.step-copy { display: grid; gap: 0.35rem; }

.step-label {
  font-size: 0.7rem;
  letter-spacing: 0.18em;
  text-transform: uppercase;
  color: var(--text-muted);
}

.url-chip,
.code-chip {
  all: unset;
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  gap: 0.65rem;
  padding: 0.6rem 0.9rem;
  border-radius: 0.75rem;
  background: rgba(15, 23, 42, 0.6);
  border: 1px solid color-mix(in srgb, var(--border) 80%, transparent);
  color: #e0f2fe;
  font-family: var(--font-family-mono);
  font-size: 0.85rem;
  transition: transform 0.12s ease, background 0.2s ease, border-color 0.2s ease;
}

.url-chip:hover,
.code-chip:hover {
  background: rgba(30, 41, 59, 0.75);
  border-color: color-mix(in srgb, var(--info) 60%, transparent);
  transform: translateY(-1px);
}

.code-chip {
  justify-content: space-between;
  width: min(100%, 22rem);
  padding: 0.75rem 1rem;
}

.code-text {
  font-size: 1.25rem;
  letter-spacing: 0.22em;
  color: #f8fafc;
  font-weight: 600;
}

.code-copy {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  font-size: 0.75rem;
  color: var(--text-muted);
}

.code-hint {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  color: var(--text-muted);
  font-size: 0.8rem;
}

.success-card {
  display: flex;
  gap: 0.9rem;
  align-items: center;
  padding: 1.1rem 1.2rem;
  border-radius: 1.1rem;
  background: color-mix(in srgb, var(--success) 14%, transparent);
  border: 1px solid color-mix(in srgb, var(--success) 50%, transparent);
  color: var(--text-primary);
}

.success-card i { font-size: 1.6rem; color: var(--success); }
.success-card strong { display: block; }
.success-card span { color: var(--text-secondary); font-size: 0.85rem; }

.pending-card {
  display: flex;
  gap: 0.75rem;
  align-items: center;
  padding: 1rem 1.2rem;
  border-radius: 1rem;
  border: 1px dashed color-mix(in srgb, var(--border) 70%, transparent);
  color: var(--text-secondary);
}

.raw-output {
  border-radius: 0.85rem;
  background: #081019;
  border: 1px solid color-mix(in srgb, var(--border) 55%, transparent);
  padding: 0.65rem 0.85rem;
}

.raw-output summary {
  cursor: pointer;
  font-size: 0.74rem;
  letter-spacing: 0.16em;
  text-transform: uppercase;
  color: #93c5fd;
  list-style: none;
}

.raw-output pre {
  margin: 0.6rem 0 0;
  max-height: 12rem;
  overflow: auto;
  white-space: pre-wrap;
  color: #c7f9ff;
  font-family: var(--font-family-mono);
  font-size: 0.8rem;
  line-height: 1.6;
}
</style>
