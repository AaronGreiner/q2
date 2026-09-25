<script setup lang="ts">
/**
 * The shell both the sign-in and the sign-up screen sit in.
 *
 * They are the same screen with different fields, and giving them one frame is
 * what keeps the wordmark, the spacing and the footer link from drifting apart
 * between two files nobody edits at the same time.
 *
 * The mark is the Q2 ligature from the app icon, so the first screen after
 * tapping the icon shows the same shape again. It is drawn in the text colour
 * rather than on the accent: it names the app, it is not something to do.
 */
defineProps<{
  heading: string
  intro: string
}>()

const t = useMessages()
</script>

<template>
  <div class="q2-scroll flex min-h-0 flex-1 flex-col pt-8">
    <div class="flex min-h-full w-full flex-col">
      <div class="px-[26px] pb-7">
        <AppMark class="mb-5 h-10 text-(--ui-text-highlighted)" />

        <p class="q2-eyebrow">
          {{ t.app.name }}
        </p>

        <h1 class="mt-2 text-[27px] leading-tight font-extrabold tracking-[-0.026em]">
          {{ heading }}
        </h1>

        <p class="mt-2 text-sm leading-relaxed text-(--ui-text-muted)">
          {{ intro }}
        </p>
      </div>

      <UCard
        class="flex-1 rounded-t-(--q2-radius-panel) rounded-b-none bg-(--q2-surface) shadow-none ring-0"
        :ui="{ body: 'px-6 py-6 sm:p-6' }"
      >
        <slot />
      </UCard>
    </div>
  </div>
</template>
