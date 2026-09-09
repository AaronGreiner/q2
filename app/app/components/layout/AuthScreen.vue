<script setup lang="ts">
/**
 * The shell both the sign-in and the sign-up screen sit in.
 *
 * They are the same screen with different fields, and giving them one frame is
 * what keeps the wordmark, the spacing and the footer link from drifting apart
 * between two files nobody edits at the same time.
 *
 * The mark is the wordmark itself rather than a picture of something: this is
 * the first screen anybody sees, and a lettermark on the accent says the name
 * where an icon would only have decorated it.
 */
defineProps<{
  heading: string
  intro: string
}>()

const t = useMessages()
</script>

<template>
  <div class="q2-scroll flex min-h-0 flex-1 flex-col justify-center px-[22px] py-8">
    <div class="mx-auto w-full max-w-[360px]">
      <div class="mb-8 flex flex-col items-center text-center">
        <div
          class="mb-4 flex size-14 items-center justify-center rounded-(--q2-radius-lg) bg-(--q2-accent-solid) text-[22px] leading-none font-extrabold tracking-[-0.05em] text-(--q2-accent-contrast)"
          aria-hidden="true"
        >
          Q2
        </div>

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

      <slot />
    </div>
  </div>
</template>
