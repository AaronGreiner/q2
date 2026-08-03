<script setup lang="ts">
/**
 * The frame for a screen that has been pushed on top of the app: a chat
 * thread, a goal, the settings.
 *
 * Same phone column, no bottom navigation. These screens are somewhere you go
 * *into* and come back out of, and the design gives them their own back
 * arrow — leaving the tab bar underneath would offer two competing ways out.
 */
const t = useMessages()

/*
 * The bottom edge belongs to the screen, never to this layout.
 *
 * Padding it here shortens whatever the screen puts inside: a scroll region
 * stops clipping its content a safe area above the display and leaves a dead
 * band of --ui-bg under it, and a bar like the chat composer gets that band
 * instead of its own surface. Each screen therefore carries --q2-safe-bottom
 * itself — inside the scroll region, so the last row can still be scrolled
 * clear of the home indicator. AppBottomNav does the same in the default
 * layout.
 *
 * The top inset is this layout's, because most screens open with a plain
 * heading over --ui-bg. A screen whose own bar sits on the top edge says
 * `edgeToEdge: true` and pads that bar itself — which it has to, since
 * installed on iOS the status bar is tinted with the page background and any
 * padding above the bar shows up as a seam across the top.
 */
const route = useRoute()
const edgeToEdge = computed(() => route.meta.edgeToEdge === true)
</script>

<template>
  <div class="mx-auto flex h-[var(--q2-viewport-height,100dvh)] w-full max-w-[430px] flex-col overflow-hidden bg-(--ui-bg) text-(--ui-text)">
    <a
      href="#main"
      class="sr-only focus:not-sr-only focus:absolute focus:start-4 focus:top-4 focus:z-50 focus:rounded-(--q2-radius-lg) focus:bg-(--ui-bg-elevated) focus:px-4 focus:py-2 focus:ring-2 focus:ring-(--ui-primary)"
    >
      {{ t.app.skipToContent }}
    </a>

    <main
      id="main"
      class="flex min-h-0 flex-1 flex-col"
      :class="edgeToEdge ? '' : 'pt-[max(0.5rem,env(safe-area-inset-top))]'"
    >
      <slot />
    </main>
  </div>
</template>
