<script setup lang="ts">
import type { LanguagePreference, ThemePreference } from '~/api/types'
import type { AccentName } from '~/composables/useAccent'

/**
 * Light or dark, the accent, and the language — the three things somebody
 * chooses once and then never thinks about again.
 *
 * Every choice applies the moment it is made, the whole app behind it
 * included, the way the settings screen does it: the page writes it, and this
 * only says what was chosen. The preview is the point of the step — one row
 * of the start screen with its camera button, which is the accent doing the
 * one job it has (docs/adr/0015-qdos-design-language.md). Choosing a colour
 * by looking at what it will mean is the honest way to choose it.
 */
const props = defineProps<{
  step: number
  total: number
  theme: ThemePreference
  accent: AccentName
  language: LanguagePreference
}>()

const emit = defineEmits<{
  'back': []
  'next': []
  'update:theme': [value: ThemePreference]
  'update:accent': [value: AccentName]
  'update:language': [value: LanguagePreference]
}>()

const t = useMessages()

const themeOptions = computed(() => [
  { value: 'System' as const, label: t.value.settings.themeSystem, icon: 'i-lucide-monitor' },
  { value: 'Light' as const, label: t.value.settings.themeLight, icon: 'i-lucide-sun' },
  { value: 'Dark' as const, label: t.value.settings.themeDark, icon: 'i-lucide-moon' },
])

const languageOptions = computed(() => [
  { value: 'German' as const, label: t.value.settings.languageGerman },
  { value: 'English' as const, label: t.value.settings.languageEnglish },
])

const theme = computed<ThemePreference>({
  get: () => props.theme,
  set: value => emit('update:theme', value),
})

const accent = computed<AccentName>({
  get: () => props.accent,
  set: value => emit('update:accent', value),
})

const language = computed<LanguagePreference>({
  get: () => props.language,
  set: value => emit('update:language', value),
})
</script>

<template>
  <OnboardingFrame
    :step="step"
    :total="total"
    :eyebrow="t.onboarding.appearance.eyebrow"
    :heading="t.onboarding.appearance.heading"
    :intro="t.onboarding.appearance.intro"
    @back="emit('back')"
  >
    <div class="flex flex-col gap-6">
      <section
        class="rounded-(--q2-radius-xl) bg-(--ui-bg) p-3"
        :aria-label="t.onboarding.appearance.preview"
        data-testid="onboarding-preview"
      >
        <p class="q2-eyebrow mb-2 px-1">
          {{ t.onboarding.appearance.preview }}
        </p>
        <div
          class="flex items-center gap-3 rounded-(--q2-radius-lg) bg-(--ui-bg-muted) px-3.5 py-3"
          aria-hidden="true"
        >
          <span class="flex size-10 shrink-0 items-center justify-center rounded-full bg-(--ui-bg-accented) text-(--ui-text-muted)">
            <UIcon
              name="i-lucide-book-open"
              class="size-5"
            />
          </span>
          <span class="min-w-0 flex-1">
            <span class="block truncate text-[14px] font-extrabold">{{ t.onboarding.appearance.previewTitle }}</span>
            <span class="block truncate text-[12px] font-semibold text-(--ui-text-muted)">{{ t.onboarding.appearance.previewMeta }}</span>
          </span>
          <span class="flex h-10 items-center gap-1.5 rounded-full bg-(--q2-accent-solid) px-4 text-[13px] font-extrabold text-(--q2-accent-contrast)">
            <UIcon
              name="i-lucide-camera"
              class="size-4"
            />
            {{ t.onboarding.appearance.previewAction }}
          </span>
        </div>
      </section>

      <div>
        <p class="mb-3 text-sm font-bold">
          {{ t.onboarding.appearance.theme }}
        </p>
        <AppSegmented
          v-model="theme"
          :options="themeOptions"
          :label="t.onboarding.appearance.theme"
        />
      </div>

      <AccentPicker v-model="accent" />

      <div>
        <p class="mb-3 text-sm font-bold">
          {{ t.settings.language }}
        </p>
        <AppSegmented
          v-model="language"
          :options="languageOptions"
          :label="t.settings.language"
        />
      </div>
    </div>

    <template #actions>
      <UButton
        block
        size="xl"
        trailing-icon="i-lucide-chevron-right"
        class="min-h-12 justify-center font-extrabold"
        data-testid="onboarding-appearance-next"
        @click="emit('next')"
      >
        {{ t.onboarding.next }}
      </UButton>
    </template>
  </OnboardingFrame>
</template>
