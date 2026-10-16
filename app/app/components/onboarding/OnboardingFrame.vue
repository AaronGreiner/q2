<script setup lang="ts">
/**
 * The frame every step of the first-run introduction sits in
 * (docs/adr/0039-first-run-onboarding.md).
 *
 * The same shape as the rest of q2 — a heading on the warm ground, and one
 * continuous surface rising under it with the 26px corner — so the
 * introduction already looks like the app it introduces. Three things are its
 * own:
 *
 * - **Where you are**, as short bars across the top. Drawn in the text
 *   colour, never the accent: progress is a state, not something to press.
 * - **A way back and a way past.** Back is an arrow at the start; "Später" or
 *   "Überspringen" sits at the end, only on a step that may be put off.
 * - **The buttons at the bottom edge**, outside the scroll area, where a thumb
 *   is — and where the keyboard pushes them up rather than covering them,
 *   because the plain layout already shrinks with it.
 *
 * The heading takes focus whenever it changes, so a screen reader announces
 * the new step instead of staying on the button that led to it.
 */
const props = withDefaults(defineProps<{
  /** One-based. */
  step: number
  total: number
  eyebrow?: string
  heading?: string
  intro?: string
  canGoBack?: boolean
  skipLabel?: string | null
}>(), {
  eyebrow: '',
  heading: '',
  intro: '',
  canGoBack: true,
  skipLabel: null,
})

const emit = defineEmits<{ back: [], skip: [] }>()

const t = useMessages()

const headingElement = useTemplateRef<HTMLElement>('headingElement')

// Not on the first render: arriving on the page is announced by its title.
watch(() => props.heading, async () => {
  await nextTick()
  headingElement.value?.focus({ preventScroll: true })
})
</script>

<template>
  <div
    class="flex min-h-0 flex-1 flex-col"
    data-testid="onboarding-frame"
    :data-step="step"
  >
    <div class="flex shrink-0 items-center gap-3 px-3 pt-1 pb-3">
      <UButton
        v-if="canGoBack"
        icon="i-lucide-chevron-left"
        color="neutral"
        variant="ghost"
        size="lg"
        :ui="{ base: 'size-11 justify-center rounded-full' }"
        :aria-label="t.onboarding.back"
        data-testid="onboarding-back"
        @click="emit('back')"
      />
      <span
        v-else
        class="size-11 shrink-0"
        aria-hidden="true"
      />

      <div
        class="flex flex-1 gap-1.5"
        role="progressbar"
        :aria-valuemin="1"
        :aria-valuemax="total"
        :aria-valuenow="step"
        :aria-valuetext="t.onboarding.progress(step, total)"
        :aria-label="t.onboarding.title"
        data-testid="onboarding-progress"
      >
        <span
          v-for="index in total"
          :key="index"
          class="h-1 flex-1 rounded-full transition-colors"
          :class="index <= step ? 'bg-(--ui-text)' : 'bg-(--ui-bg-accented)'"
        />
      </div>

      <UButton
        v-if="skipLabel"
        color="neutral"
        variant="ghost"
        size="lg"
        class="min-h-11 min-w-11 justify-center font-bold text-(--ui-text-muted)"
        data-testid="onboarding-skip"
        @click="emit('skip')"
      >
        {{ skipLabel }}
      </UButton>
      <span
        v-else
        class="size-11 shrink-0"
        aria-hidden="true"
      />
    </div>

    <div
      v-if="heading"
      class="shrink-0 px-[26px] pb-6"
    >
      <p
        v-if="eyebrow"
        class="q2-eyebrow"
      >
        {{ eyebrow }}
      </p>
      <h1
        ref="headingElement"
        class="q2-title mt-2 outline-none"
        tabindex="-1"
        data-testid="onboarding-heading"
      >
        {{ heading }}
      </h1>
      <p
        v-if="intro"
        class="mt-2 text-sm leading-relaxed text-(--ui-text-muted)"
        data-testid="onboarding-intro"
      >
        {{ intro }}
      </p>
    </div>

    <div class="flex min-h-0 flex-1 flex-col overflow-hidden rounded-t-(--q2-radius-panel) bg-(--q2-surface)">
      <div class="q2-content-arrival q2-scroll min-h-0 flex-1 overflow-x-hidden px-6 pt-6 pb-4">
        <slot />
      </div>

      <div
        v-if="$slots.actions"
        class="flex shrink-0 flex-col gap-2 px-6 pt-3 pb-(--q2-safe-bottom)"
      >
        <slot name="actions" />
      </div>
    </div>
  </div>
</template>
