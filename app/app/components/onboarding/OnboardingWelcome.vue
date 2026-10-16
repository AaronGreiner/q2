<script setup lang="ts">
import type { LanguagePreference } from '~/api/types'

/**
 * The first thing a new account sees: what Qdos is, in three slides.
 *
 * Before anybody is asked for a photograph or a promise, they are told what
 * the promise is for — the order every introduction that works keeps. Three
 * slides and not five: the promise, the photograph that proves it, the streak
 * that counts it. Everything else is learnt by using it.
 *
 * Swiped like any carousel on a phone, and stepped through with the button
 * for anybody who does not swipe. The dots are buttons too, so a keyboard or
 * a screen reader can jump. The language is switchable here, on the very
 * first screen, because somebody who does not read German should not have to
 * get through three slides of it to find the setting.
 */
const props = defineProps<{
  step: number
  total: number
  language: LanguagePreference
}>()

const emit = defineEmits<{
  'next': []
  'skip': []
  'signOut': []
  'update:language': [value: LanguagePreference]
}>()

const t = useMessages()

const slides = ['promise', 'proof', 'streak'] as const
const current = ref(0)
const track = useTemplateRef<HTMLElement>('track')

const isLast = computed(() => current.value === slides.length - 1)

const languageOptions = computed(() => [
  { value: 'German' as const, label: t.value.settings.languageGerman },
  { value: 'English' as const, label: t.value.settings.languageEnglish },
])

const language = computed<LanguagePreference>({
  get: () => props.language,
  set: value => emit('update:language', value),
})

/*
 * The slide a tap is scrolling to, while it is still on its way.
 *
 * A smooth scroll reports every position it passes, and reading the slide off
 * those would put a quick second tap back on the slide the first one left —
 * the button then never got past the second slide. So while a tap's scroll is
 * running, the scroll position is not asked; it is asked again once it has
 * arrived, or after a moment if a swipe took it somewhere else.
 */
let heading: number | null = null
let settle: ReturnType<typeof setTimeout> | undefined

function slideInView(element: HTMLElement): number {
  return Math.min(slides.length - 1, Math.max(0, Math.round(element.scrollLeft / element.clientWidth)))
}

/** Which slide is in view, read from the scroll position the swipe left. */
function onScroll() {
  const element = track.value
  if (!element || element.clientWidth === 0) return

  if (heading !== null) {
    if (slideInView(element) !== heading) return
    heading = null
  }

  current.value = slideInView(element)
}

function show(index: number) {
  current.value = index

  const element = track.value
  if (!element) return

  heading = index
  clearTimeout(settle)
  settle = setTimeout(() => {
    heading = null
    onScroll()
  }, 600)

  const reduced = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? false
  element.scrollTo({ left: index * element.clientWidth, behavior: reduced ? 'auto' : 'smooth' })
}

onBeforeUnmount(() => clearTimeout(settle))

function onNext() {
  if (isLast.value) emit('next')
  else show(current.value + 1)
}
</script>

<template>
  <OnboardingFrame
    :step="step"
    :total="total"
    :can-go-back="false"
    :skip-label="isLast ? null : t.onboarding.skip"
    @skip="emit('skip')"
  >
    <h1 class="sr-only">
      {{ t.onboarding.title }}
    </h1>

    <div class="mb-4 flex justify-end">
      <div class="w-48">
        <AppSegmented
          v-model="language"
          :options="languageOptions"
          :label="t.onboarding.language"
        />
      </div>
    </div>

    <section
      :aria-label="t.onboarding.welcome.label"
      aria-roledescription="carousel"
    >
      <!-- Focusable, and stepped with the arrow keys: a region that scrolls
           sideways has to be reachable without a finger. -->
      <div
        ref="track"
        class="q2-scroll-x -mx-6 flex snap-x snap-mandatory rounded-(--q2-radius-lg) focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
        tabindex="0"
        :aria-label="t.onboarding.welcome.label"
        data-testid="onboarding-slides"
        @scroll.passive="onScroll"
        @keydown.right.prevent="current < slides.length - 1 && show(current + 1)"
        @keydown.left.prevent="current > 0 && show(current - 1)"
      >
        <article
          v-for="(slide, index) in slides"
          :key="slide"
          class="w-full shrink-0 snap-center px-6"
          aria-roledescription="slide"
          :aria-label="t.onboarding.welcome.slideOf(index + 1, slides.length)"
          :aria-hidden="index === current ? undefined : 'true'"
          :data-testid="`onboarding-slide-${slide}`"
        >
          <OnboardingIllustration :kind="slide" />

          <p class="q2-eyebrow mt-6">
            {{ t.onboarding.welcome[slide].eyebrow }}
          </p>
          <h2 class="mt-2 text-[24px] leading-tight font-extrabold tracking-[-0.022em]">
            {{ t.onboarding.welcome[slide].heading }}
          </h2>
          <p class="mt-2 text-sm leading-relaxed text-(--ui-text-muted)">
            {{ t.onboarding.welcome[slide].body }}
          </p>
        </article>
      </div>

      <div class="mt-4 flex justify-center">
        <button
          v-for="(slide, index) in slides"
          :key="slide"
          type="button"
          class="flex size-11 items-center justify-center rounded-full focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
          :aria-label="t.onboarding.welcome.slideOf(index + 1, slides.length)"
          :aria-current="index === current ? 'step' : undefined"
          :data-testid="`onboarding-dot-${index}`"
          @click="show(index)"
        >
          <span
            class="block h-1.5 rounded-full transition-all"
            :class="index === current ? 'w-5 bg-(--ui-text)' : 'w-1.5 bg-(--ui-border-accented)'"
          />
        </button>
      </div>
    </section>

    <template #actions>
      <UButton
        block
        size="xl"
        :trailing-icon="isLast ? undefined : 'i-lucide-chevron-right'"
        class="min-h-12 justify-center font-extrabold"
        data-testid="onboarding-welcome-next"
        @click="onNext"
      >
        {{ isLast ? t.onboarding.welcome.start : t.onboarding.next }}
      </UButton>

      <!-- The only way out of a mandatory introduction, for somebody on the
           wrong account — a shared tablet, a mistyped address. -->
      <UButton
        color="neutral"
        variant="ghost"
        size="lg"
        class="min-h-11 justify-center text-[13px] font-bold text-(--ui-text-muted)"
        data-testid="onboarding-sign-out"
        @click="emit('signOut')"
      >
        {{ t.onboarding.signOut }}
      </UButton>
    </template>
  </OnboardingFrame>
</template>
