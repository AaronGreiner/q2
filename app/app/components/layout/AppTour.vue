<script setup lang="ts">
/**
 * The short tour over the real tab bar, once, right after the introduction
 * (docs/adr/0039-first-run-onboarding.md).
 *
 * Five stops, one per tab, each a sentence about what the tab is *for* — the
 * rest is learnt by using it. The tab itself is lit and everything else is
 * dimmed, so what is being explained is the thing on screen and not a picture
 * of it.
 *
 * Shown while the address carries `?tour=1`, which is how the introduction
 * hands over; ending it takes the parameter away. Nothing is stored: it
 * happens once because the introduction does, and a reload in the middle of it
 * showing it again is no harm.
 *
 * Mounted in the default layout, the one with the tab bar. Positioned from the
 * tab's own box, measured again when the window changes size, so it follows
 * the bar wherever the phone puts it.
 */
const route = useRoute()
const router = useRouter()
const t = useMessages()

const stops = ['home', 'search', 'dos', 'chats', 'profile'] as const

const index = ref(0)
const target = ref<DOMRect | null>(null)
const viewportHeight = ref(0)

const isOpen = computed(() => route.query.tour === '1')
const stop = computed(() => stops[index.value]!)
const isLast = computed(() => index.value === stops.length - 1)

const nextButton = useTemplateRef<{ $el: HTMLElement }>('nextButton')

function measure() {
  if (!import.meta.client) return

  viewportHeight.value = window.innerHeight
  target.value = document.querySelector(`[data-testid="nav-${stop.value}"]`)?.getBoundingClientRect() ?? null
}

watch([isOpen, index], async ([open]) => {
  if (!open) {
    target.value = null
    return
  }

  await nextTick()
  measure()
  await nextTick()
  nextButton.value?.$el?.focus()
}, { immediate: true })

onMounted(() => {
  window.addEventListener('resize', measure)
})

onBeforeUnmount(() => {
  window.removeEventListener('resize', measure)
})

function close() {
  index.value = 0

  const query = { ...route.query }
  delete query.tour
  router.replace({ query })
}

function advance() {
  if (isLast.value) close()
  else index.value++
}

/** Where the lit box sits: the tab, with a little room around it. */
const spotlight = computed(() => {
  const box = target.value
  if (!box) return null

  return {
    top: `${box.top - 4}px`,
    left: `${box.left + 4}px`,
    width: `${box.width - 8}px`,
    height: `${box.height + 4}px`,
  }
})

/** The card stands above the bar, with its pointer over the lit tab. */
const card = computed(() => {
  const box = target.value
  if (!box) return null

  return {
    bottom: `${viewportHeight.value - box.top + 14}px`,
    pointer: `${box.left + box.width / 2}px`,
  }
})
</script>

<template>
  <Teleport to="body">
    <div
      v-if="isOpen && spotlight && card"
      class="fixed inset-0 z-[60]"
      role="dialog"
      aria-modal="true"
      aria-labelledby="tour-title"
      aria-describedby="tour-body"
      data-testid="app-tour"
      :data-stop="stop"
      @keydown.esc="close"
    >
      <!-- Everything but the tab, dimmed — by the shadow of the lit box. -->
      <div
        class="pointer-events-none absolute rounded-(--q2-radius-lg) ring-2 ring-(--ui-text) transition-all duration-(--q2-motion-normal)"
        :style="{ ...spotlight, boxShadow: '0 0 0 9999px rgb(0 0 0 / 0.62)' }"
        aria-hidden="true"
      />

      <div
        class="absolute inset-x-0 mx-auto w-full max-w-[430px] px-4"
        :style="{ bottom: card.bottom }"
      >
        <section class="relative rounded-(--q2-radius-xl) bg-(--q2-surface) p-4 shadow-xl">
          <p class="q2-eyebrow">
            {{ t.tour.label }} · {{ t.tour.stepOf(index + 1, stops.length) }}
          </p>
          <h2
            id="tour-title"
            class="mt-1.5 text-lg font-extrabold"
            data-testid="app-tour-title"
          >
            {{ t.tour[stop].title }}
          </h2>
          <p
            id="tour-body"
            class="mt-1 text-sm leading-relaxed text-(--ui-text-muted)"
          >
            {{ t.tour[stop].body }}
          </p>

          <div class="mt-4 flex items-center justify-between gap-2">
            <UButton
              color="neutral"
              variant="ghost"
              size="lg"
              class="min-h-11 font-bold text-(--ui-text-muted)"
              data-testid="app-tour-skip"
              @click="close"
            >
              {{ t.tour.skip }}
            </UButton>

            <UButton
              ref="nextButton"
              size="lg"
              class="min-h-11 min-w-28 justify-center font-extrabold"
              :trailing-icon="isLast ? undefined : 'i-lucide-chevron-right'"
              data-testid="app-tour-next"
              @click="advance"
            >
              {{ isLast ? t.tour.done : t.tour.next }}
            </UButton>
          </div>
        </section>

        <span
          class="absolute -bottom-1.5 size-3 -translate-x-1/2 rotate-45 bg-(--q2-surface)"
          :style="{ left: `calc(${card.pointer} - max(0px, (100vw - 430px) / 2))` }"
          aria-hidden="true"
        />
      </div>
    </div>
  </Teleport>
</template>
