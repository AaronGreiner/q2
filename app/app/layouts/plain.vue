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
</script>

<template>
  <div class="mx-auto flex h-dvh w-full max-w-[430px] flex-col overflow-hidden bg-(--ui-bg) text-(--ui-text)">
    <a
      href="#main"
      class="sr-only focus:not-sr-only focus:absolute focus:start-4 focus:top-4 focus:z-50 focus:rounded-lg focus:bg-(--ui-bg-elevated) focus:px-4 focus:py-2 focus:ring-2 focus:ring-(--ui-primary)"
    >
      {{ t.app.skipToContent }}
    </a>

    <main
      id="main"
      class="flex min-h-0 flex-1 flex-col pt-[max(0.5rem,env(safe-area-inset-top))] pb-(--q2-safe-bottom)"
    >
      <slot />
    </main>
  </div>
</template>
