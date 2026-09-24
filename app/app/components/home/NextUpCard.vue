<script setup lang="ts">
import type { Goal } from '~/api/types'

/**
 * The one thing to do next, at the top of the start screen: the open window
 * that closes first, how long it has left, and the camera.
 *
 * The start screen used to open on two cards of numbers, and what to actually
 * do was a list further down. This is that list's first line, lifted out and
 * given the one full-width button on the screen — the accent is spent on it
 * because it is exactly what the accent means: something you can do now.
 */
const props = withDefaults(defineProps<{
  goal: Goal
  /** The shared clock (`useNow`), for the countdown. */
  now: number
  busy?: boolean
}>(), {
  busy: false,
})

const emit = defineEmits<{ deliver: [id: string] }>()

const t = useMessages()

const when = computed(() => {
  const window = props.goal.current
  if (!window) return ''

  const label = windowLabel(window, t.value)
  const left = deadlineLeft(window.dueAt, props.now, t.value)
  return left ? t.value.chats.nextStep(label, left) : label
})
</script>

<template>
  <section
    class="q2-card flex flex-col gap-3 px-4 py-4"
    aria-labelledby="next-up-heading"
    data-testid="next-up"
  >
    <div class="flex items-center gap-3">
      <span
        class="flex size-11 shrink-0 items-center justify-center rounded-(--q2-radius-md) bg-(--ui-bg-accented) text-(--ui-text)"
        aria-hidden="true"
      >
        <UIcon
          :name="goalIconName(goal.icon)"
          class="size-5"
        />
      </span>

      <div class="min-w-0 flex-1">
        <h2
          id="next-up-heading"
          class="q2-eyebrow"
        >
          {{ t.home.nextUpHeading }}
        </h2>
        <NuxtLink
          :to="`/goals/${goal.id}`"
          class="-my-3 block truncate py-3 text-[17px] font-extrabold hover:underline focus-visible:underline focus-visible:outline-none"
          data-q2-private
        >
          {{ goal.title }}
        </NuxtLink>
        <p
          class="text-[13px] font-semibold text-(--ui-text-muted)"
          data-testid="next-up-when"
        >
          {{ when }}
        </p>
      </div>
    </div>

    <UButton
      icon="i-lucide-camera"
      size="xl"
      class="q2-press min-h-12 justify-center rounded-full font-extrabold"
      :disabled="busy"
      data-testid="next-up-deliver"
      @click="emit('deliver', goal.id)"
    >
      {{ t.proof.deliver }}
    </UButton>
  </section>
</template>
