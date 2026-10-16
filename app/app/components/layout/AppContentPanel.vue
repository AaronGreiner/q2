<script setup lang="ts">
import type { ComponentPublicInstance } from 'vue'

/**
 * The continuous Ruhe surface, with an optional fixed toolbar above its scroll area.
 *
 * The scroll area never scrolls sideways: `overflow-y: auto` alone makes the
 * other axis scrollable too, and a card swiped off the edge (ProofSwipeStack)
 * would otherwise drag the whole screen with it.
 *
 * It is also the one thing on a screen that scrolls, so it is what remembers
 * how far down a screen was left and goes back there when the screen is gone
 * back to (#118). The window never scrolls in q2 — both layouts are one
 * viewport tall — so the router's own scroll restoration, which saves the
 * window's, has nothing to restore.
 */
const card = useTemplateRef<ComponentPublicInstance>('card')

function scrollArea(): HTMLElement | null {
  return card.value?.$el.querySelector?.('[data-slot="body"]') ?? null
}

const top = useReturnState('scroll', () => scrollArea()?.scrollTop || undefined)

let abandon: (() => void) | undefined

onMounted(() => {
  const area = scrollArea()
  if (area && top) abandon = restoreScroll(area, top)
})

onBeforeUnmount(() => abandon?.())
</script>

<template>
  <UCard
    ref="card"
    class="flex min-h-0 flex-1 flex-col overflow-hidden rounded-t-(--q2-radius-panel) rounded-b-none bg-(--q2-surface) shadow-none ring-0"
    :ui="{
      header: 'shrink-0 border-0 px-5 pt-4 pb-2 sm:px-5',
      body: ['q2-content-arrival q2-scroll min-h-0 flex-1 overflow-x-hidden px-5 pt-4 pb-8 sm:p-5', $slots.toolbar ? 'pt-0 sm:pt-0' : ''],
    }"
    data-testid="content-panel"
  >
    <template
      v-if="$slots.toolbar"
      #header
    >
      <slot name="toolbar" />
    </template>
    <slot />
  </UCard>
</template>
