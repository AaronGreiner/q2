<script setup lang="ts">
/**
 * The picture on each welcome slide, drawn from the app's own parts.
 *
 * Not artwork: a row of Dos with the friends who check them, a photograph
 * waiting for its verdict, a streak. Somebody who has seen these three
 * pictures recognises the screens they are about to use, which is worth more
 * than an illustration of people high-fiving.
 *
 * Decorative by the rules of the design: no accent, because nothing here can
 * be pressed; the flame gradient only on the streak, because that is what it
 * means everywhere else. Hidden from assistive technology — the slide's
 * heading and text already say what it shows.
 */
defineProps<{ kind: 'promise' | 'proof' | 'streak' }>()

const t = useMessages()

const sample = computed(() => t.value.onboarding.welcome.sample)

const promises = computed(() => [
  { icon: 'i-lucide-book-open', title: sample.value.read, meta: sample.value.daily },
  { icon: 'i-lucide-medal', title: sample.value.run, meta: sample.value.weekly },
  { icon: 'i-lucide-ban', title: sample.value.phone, meta: sample.value.dont },
])

/** Kept, kept, kept, kept, today, and two still to come. */
const week = ['done', 'done', 'done', 'done', 'today', 'future', 'future'] as const
</script>

<template>
  <div
    class="relative mx-auto flex h-[228px] w-full max-w-[320px] items-center justify-center"
    aria-hidden="true"
    :data-testid="`onboarding-illustration-${kind}`"
  >
    <!-- Three promises, each with somebody watching. -->
    <div
      v-if="kind === 'promise'"
      class="flex w-full flex-col gap-2.5"
    >
      <div
        v-for="(row, index) in promises"
        :key="row.title"
        class="flex items-center gap-3 rounded-(--q2-radius-lg) border border-(--ui-border) bg-(--ui-bg-muted) px-3.5 py-3"
        :class="index === 1 ? 'translate-x-3' : index === 2 ? 'translate-x-1.5 opacity-80' : ''"
      >
        <span class="flex size-9 shrink-0 items-center justify-center rounded-full bg-(--ui-bg-accented) text-(--ui-text-muted)">
          <UIcon
            :name="row.icon"
            class="size-[18px]"
          />
        </span>
        <span class="min-w-0 flex-1">
          <span class="block truncate text-[13px] font-extrabold">{{ row.title }}</span>
          <span class="block truncate text-[11px] font-semibold text-(--ui-text-muted)">{{ row.meta }}</span>
        </span>
        <span class="flex -space-x-1">
          <span
            v-for="initials in sample.initials.slice(index, index + 2)"
            :key="initials"
            class="flex size-7 items-center justify-center rounded-full bg-(--q2-avatar) text-[10px] font-extrabold text-(--ui-text-toned) ring-2 ring-(--ui-bg-muted)"
          >{{ initials }}</span>
        </span>
      </div>
    </div>

    <!-- A photograph, and the two answers friends have for it. -->
    <div
      v-else-if="kind === 'proof'"
      class="w-[260px] overflow-hidden rounded-(--q2-radius-xl) border border-(--ui-border) bg-(--ui-bg-muted)"
    >
      <div class="relative flex h-[112px] items-center justify-center bg-(--ui-bg-accented)">
        <UIcon
          name="i-lucide-book-open"
          class="size-12 text-(--ui-text-dimmed)"
        />
        <span class="absolute start-2.5 top-2.5 inline-flex items-center gap-1 rounded-full bg-(--q2-surface) px-2 py-0.5 text-[10px] font-extrabold">
          <UIcon
            name="i-lucide-camera"
            class="size-3"
          />
          {{ sample.proofMeta }}
        </span>
      </div>
      <div class="px-3.5 pt-3 pb-3.5">
        <p class="text-[13px] font-extrabold">
          {{ sample.proofTitle }}
        </p>
        <p class="mt-0.5 flex items-center gap-1.5 text-[11px] font-semibold text-(--ui-text-muted)">
          <span class="flex -space-x-1">
            <span
              v-for="initials in sample.initials.slice(0, 2)"
              :key="initials"
              class="flex size-6 items-center justify-center rounded-full bg-(--q2-avatar) text-[8px] font-extrabold text-(--ui-text-toned) ring-2 ring-(--ui-bg-muted)"
            >{{ initials }}</span>
          </span>
          {{ sample.confirmed }}
        </p>
        <div class="mt-3 grid grid-cols-2 gap-2">
          <span class="flex h-9 items-center justify-center gap-1.5 rounded-full border border-(--ui-border-accented) text-[12px] font-bold text-(--ui-text-muted)">
            <UIcon
              name="i-lucide-x"
              class="size-3.5"
            />
            {{ sample.doubt }}
          </span>
          <span class="flex h-9 items-center justify-center gap-1.5 rounded-full bg-(--ui-text) text-[12px] font-bold text-(--ui-bg)">
            <UIcon
              name="i-lucide-check"
              class="size-3.5"
            />
            {{ sample.confirm }}
          </span>
        </div>
      </div>
    </div>

    <!-- The streak, the only place the flame gradient belongs. -->
    <div
      v-else
      class="flex w-[260px] flex-col items-center"
    >
      <span class="flex size-16 items-center justify-center rounded-full bg-linear-to-br from-(--q2-flame-from) to-(--q2-flame-to) text-black">
        <UIcon
          name="i-lucide-flame"
          class="size-8"
        />
      </span>
      <p class="mt-3 flex items-baseline gap-2">
        <span class="text-[52px] leading-none font-extrabold tracking-[-0.04em]">{{ sample.streakValue }}</span>
        <span class="text-base font-semibold">{{ sample.streakLabel }}</span>
      </p>
      <ul class="mt-5 flex w-full list-none gap-1.5 p-0">
        <li
          v-for="(state, index) in week"
          :key="index"
          class="flex h-7 flex-1 items-center justify-center rounded-full text-[11px] font-bold"
          :class="{
            'bg-(--ui-text) text-(--ui-bg)': state === 'done',
            'bg-(--ui-bg-accented) text-(--ui-text) ring-2 ring-(--ui-text) ring-inset': state === 'today',
            'text-(--ui-text-dimmed) ring-1 ring-(--ui-border) ring-inset': state === 'future',
          }"
        >
          <UIcon
            v-if="state === 'done'"
            name="i-lucide-check"
            class="size-3.5"
          />
          <template v-else>
            {{ t.time.weekdayInitials[index] }}
          </template>
        </li>
      </ul>
    </div>
  </div>
</template>
