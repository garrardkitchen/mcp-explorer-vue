<script setup lang="ts">
import { computed, getCurrentInstance } from 'vue'

type HistogramBar = {
  index: number
  value: number
  ratio: number
}

const props = withDefaults(defineProps<{
  bars: HistogramBar[]
  variant?: 'compact' | 'hero' | 'detail'
  emptyLabel?: string
}>(), {
  variant: 'compact',
  emptyLabel: 'Waiting for traffic',
})

const width = 240
const height = computed(() => {
  switch (props.variant) {
    case 'hero': return 56
    case 'detail': return 64
    default: return 42
  }
})
const paddingX = 8
const paddingY = 8
const instance = getCurrentInstance()
const uid = `traffic-spark-${instance?.uid ?? Math.random().toString(36).slice(2, 9)}`

const hasEvents = computed(() => props.bars.some(bar => bar.value > 0))
const baselineY = computed(() => height.value - paddingY)

const plottedPoints = computed(() => {
  const total = Math.max(props.bars.length - 1, 1)
  return props.bars.map((bar, index) => {
    const x = paddingX + ((width - paddingX * 2) * index) / total
    const y = baselineY.value - (bar.ratio * (height.value - paddingY * 2))
    return { x, y }
  })
})

const linePoints = computed(() =>
  plottedPoints.value.map(point => `${point.x},${point.y}`).join(' '),
)

const areaPath = computed(() => {
  if (!plottedPoints.value.length)
    return ''

  const first = plottedPoints.value[0]
  const last = plottedPoints.value[plottedPoints.value.length - 1]
  const line = plottedPoints.value.map((point, index) =>
    `${index === 0 ? 'M' : 'L'} ${point.x} ${point.y}`,
  ).join(' ')

  return `${line} L ${last.x} ${baselineY.value} L ${first.x} ${baselineY.value} Z`
})

const gridLines = computed(() => {
  const steps = 3
  return Array.from({ length: steps }, (_, index) =>
    baselineY.value - (((height.value - paddingY * 2) / (steps + 1)) * (index + 1)),
  )
})

const emptyWave = computed(() => {
  const mid = height.value / 2
  return [
    `M ${paddingX} ${mid}`,
    `C ${paddingX + 24} ${mid - 10}, ${paddingX + 42} ${mid + 10}, ${paddingX + 66} ${mid}`,
    `S ${paddingX + 108} ${mid - 10}, ${paddingX + 132} ${mid}`,
    `S ${paddingX + 174} ${mid + 10}, ${paddingX + 198} ${mid}`,
    `S ${paddingX + 222} ${mid - 10}, ${width - paddingX} ${mid}`,
  ].join(' ')
})
</script>

<template>
  <div class="traffic-sparkline" :class="[`variant-${variant}`, { empty: !hasEvents }]">
    <svg :viewBox="`0 0 ${width} ${height}`" preserveAspectRatio="none" aria-hidden="true">
      <defs>
        <linearGradient :id="`${uid}-stroke`" x1="0%" y1="0%" x2="100%" y2="0%">
          <stop offset="0%" stop-color="var(--info)" stop-opacity="0.88" />
          <stop offset="100%" stop-color="var(--accent)" stop-opacity="0.92" />
        </linearGradient>
        <linearGradient :id="`${uid}-fill`" x1="0%" y1="0%" x2="0%" y2="100%">
          <stop offset="0%" stop-color="var(--info)" stop-opacity="0.22" />
          <stop offset="100%" stop-color="var(--info)" stop-opacity="0.03" />
        </linearGradient>
      </defs>

      <line
        v-for="lineY in gridLines"
        :key="lineY"
        class="grid-line"
        :x1="paddingX"
        :y1="lineY"
        :x2="width - paddingX"
        :y2="lineY"
      />
      <line
        class="baseline"
        :x1="paddingX"
        :y1="baselineY"
        :x2="width - paddingX"
        :y2="baselineY"
      />

      <template v-if="hasEvents">
        <path class="area" :d="areaPath" :fill="`url(#${uid}-fill)`" />
        <polyline class="line" :points="linePoints" :stroke="`url(#${uid}-stroke)`" />
        <circle
          v-if="plottedPoints.length"
          class="end-cap"
          :cx="plottedPoints[plottedPoints.length - 1].x"
          :cy="plottedPoints[plottedPoints.length - 1].y"
          r="3.5"
        />
      </template>
      <path v-else class="empty-wave" :d="emptyWave" />
    </svg>

    <span v-if="!hasEvents" class="empty-label">{{ emptyLabel }}</span>
  </div>
</template>

<style scoped>
.traffic-sparkline {
  position: relative;
  border-radius: 1rem;
  border: 1px solid color-mix(in srgb, var(--border) 66%, transparent);
  background:
    radial-gradient(circle at top left, color-mix(in srgb, var(--info) 10%, transparent), transparent 44%),
    color-mix(in srgb, var(--bg-base) 56%, transparent);
  overflow: hidden;
}

.traffic-sparkline svg {
  display: block;
  width: 100%;
  height: 100%;
}

.variant-compact {
  min-height: 2.8rem;
}

.variant-hero {
  min-height: 3.5rem;
}

.variant-detail {
  min-height: 4rem;
}

.grid-line,
.baseline {
  stroke: color-mix(in srgb, var(--border) 42%, transparent);
  stroke-width: 1;
}

.grid-line {
  stroke-dasharray: 3 6;
}

.line {
  fill: none;
  stroke-width: 2.5;
  stroke-linecap: round;
  stroke-linejoin: round;
  filter: drop-shadow(0 0 12px color-mix(in srgb, var(--info) 18%, transparent));
}

.area {
  opacity: 0.9;
}

.end-cap {
  fill: color-mix(in srgb, var(--accent) 74%, white 26%);
  filter: drop-shadow(0 0 8px color-mix(in srgb, var(--accent) 28%, transparent));
}

.empty-wave {
  fill: none;
  stroke: color-mix(in srgb, var(--text-muted) 72%, var(--info));
  stroke-width: 2.5;
  stroke-linecap: round;
  stroke-linejoin: round;
  stroke-dasharray: 5 6;
  opacity: 0.8;
}

.empty-label {
  position: absolute;
  right: 0.9rem;
  bottom: 0.45rem;
  padding: 0.2rem 0.55rem;
  border-radius: 999px;
  font-size: 0.62rem;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--text-muted);
  background: color-mix(in srgb, var(--bg-base) 70%, transparent);
}
</style>
