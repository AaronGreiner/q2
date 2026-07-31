<script setup lang="ts">
/**
 * The shell both the sign-in and the sign-up screen sit in.
 *
 * They are the same screen with different fields, and giving them one frame is
 * what keeps the wordmark, the spacing and the footer link from drifting apart
 * between two files nobody edits at the same time.
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
      <div class="mb-7 flex flex-col items-center text-center">
        <div
          class="mb-4 flex size-14 items-center justify-center rounded-2xl bg-(--q2-accent-solid) text-2xl"
          aria-hidden="true"
        >
          💚
        </div>

        <p class="text-xs font-extrabold tracking-[0.14em] text-(--ui-text-muted) uppercase">
          {{ t.app.name }}
        </p>

        <h1 class="mt-1.5 text-2xl font-extrabold">
          {{ heading }}
        </h1>

        <p class="mt-1.5 text-sm text-(--ui-text-muted)">
          {{ intro }}
        </p>
      </div>

      <slot />
    </div>
  </div>
</template>
