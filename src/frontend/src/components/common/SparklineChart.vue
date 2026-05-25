<template>
  <svg :width="svgWidth" :height="height" class="sparkline-chart" aria-hidden="true">
    <rect
      v-for="(bar, i) in bars"
      :key="i"
      :x="i * (barWidth + gap)"
      :y="height - barHeight(bar.value)"
      :width="barWidth"
      :height="barHeight(bar.value)"
      :fill="bar.color"
      rx="1"
    />
    <text v-if="!bars.length" :x="svgWidth / 2" :y="height / 2 + 4" text-anchor="middle" class="no-data">—</text>
  </svg>
</template>

<script setup lang="ts">
import { computed } from 'vue'

export interface SparklineBar {
  value: number
  color: string
}

const props = withDefaults(defineProps<{
  bars: SparklineBar[]
  barWidth?: number
  gap?: number
  height?: number
  minBarHeight?: number
}>(), {
  barWidth: 7,
  gap: 2,
  height: 28,
  minBarHeight: 3
})

const svgWidth = computed(() => {
  if (!props.bars.length) return 80
  return props.bars.length * (props.barWidth + props.gap) - props.gap
})

const maxValue = computed(() => {
  const m = Math.max(...props.bars.map(b => b.value))
  return m > 0 ? m : 1
})

function barHeight(value: number): number {
  if (value <= 0) return props.minBarHeight
  const ratio = value / maxValue.value
  const h = Math.max(props.minBarHeight, Math.round(ratio * (props.height - 2)))
  return Math.min(h, props.height)
}
</script>

<style scoped>
.sparkline-chart {
  display: block;
  overflow: visible;
}
.no-data {
  font-size: 11px;
  fill: var(--p-text-muted-color, #888);
}
</style>
