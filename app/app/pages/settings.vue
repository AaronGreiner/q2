<script setup lang="ts">
import type { LanguagePreference, ThemePreference } from '~/api/types'

/**
 * Preferences.
 *
 * Every control applies immediately and saves afterwards — there is no "Save"
 * button, because a settings screen with one invites people to change three
 * things and leave without pressing it.
 *
 * The two notes on this screen are deliberate: the notification switches and
 * the three greyed-out account rows are real settings for features that do not
 * exist yet, and saying so is better than a row that quietly does nothing.
 * Signing out is not one of them — it works, so it is a real button.
 */
definePageMeta({ layout: 'plain' })

const t = useMessages()
const theme = useTheme()
const language = useLanguage()
const { settings, update } = useAppSettings()

const config = useRuntimeConfig()
const { person, logout } = useSession()

const isSigningOut = ref(false)

async function onSignOut() {
  if (isSigningOut.value) return

  isSigningOut.value = true

  try {
    await logout()

    // `replace`, so the back button does not lead into a signed-out app that
    // the middleware immediately bounces out of again.
    await navigateTo('/login', { replace: true })
  }
  finally {
    isSigningOut.value = false
  }
}

const themeOptions = computed(() => [
  { value: 'System' as const, label: t.value.settings.themeSystem, icon: 'i-lucide-monitor' },
  { value: 'Light' as const, label: t.value.settings.themeLight, icon: 'i-lucide-sun' },
  { value: 'Dark' as const, label: t.value.settings.themeDark, icon: 'i-lucide-moon' },
])

const languageOptions = computed(() => [
  { value: 'German' as const, label: t.value.settings.languageGerman },
  { value: 'English' as const, label: t.value.settings.languageEnglish },
])

const selectedTheme = computed<ThemePreference>({
  get: () => theme.preference.value,
  set: value => update({ theme: value }),
})

const selectedLanguage = computed<LanguagePreference>({
  get: () => language.value,
  set: value => update({ language: value }),
})

/** One writable flag per switch, so the row stays a plain `v-model`. */
function notification(key: 'notifyReminders' | 'notifyKudos' | 'notifyMessages' | 'notifyWeeklyReview') {
  return computed<boolean>({
    get: () => settings.value?.[key] ?? false,
    set: value => update({ [key]: value }),
  })
}

const reminders = notification('notifyReminders')
const kudos = notification('notifyKudos')
const chatMessages = notification('notifyMessages')
const weeklyReview = notification('notifyWeeklyReview')

const accountRows = computed(() => [
  { icon: 'i-lucide-user', label: t.value.settings.editProfile },
  { icon: 'i-lucide-lock', label: t.value.settings.privacy },
  { icon: 'i-lucide-circle-help', label: t.value.settings.help },
])

useHead({ title: () => t.value.settings.heading })
</script>

<template>
  <div class="flex min-h-0 flex-1 flex-col">
    <AppScreenHeader
      :title="t.settings.heading"
      back-to="/profile"
      :back-label="t.common.back"
    />

    <div class="q2-scroll flex-1 px-[18px] pt-1 pb-8">
      <SettingsSection :title="t.settings.appearance">
        <div class="px-4 py-3.5">
          <p
            id="theme-label"
            class="mb-2.5 text-sm font-bold"
          >
            {{ t.settings.theme }}
          </p>
          <AppSegmented
            v-model="selectedTheme"
            :options="themeOptions"
            :label="t.settings.theme"
          />

          <div
            class="my-4 h-px bg-(--ui-border)"
            aria-hidden="true"
          />

          <p class="mb-2.5 text-sm font-bold">
            {{ t.settings.language }}
          </p>
          <AppSegmented
            v-model="selectedLanguage"
            :options="languageOptions"
            :label="t.settings.language"
          />
        </div>
      </SettingsSection>

      <SettingsSection
        :title="t.settings.notifications"
        :note="t.settings.notificationsNote"
      >
        <SettingsToggleRow
          v-model="reminders"
          icon="i-lucide-alarm-clock"
          :label="t.settings.notifyReminders"
        />
        <SettingsToggleRow
          v-model="kudos"
          icon="i-lucide-hand-heart"
          :label="t.settings.notifyKudos"
        />
        <SettingsToggleRow
          v-model="chatMessages"
          icon="i-lucide-message-circle"
          :label="t.settings.notifyMessages"
        />
        <SettingsToggleRow
          v-model="weeklyReview"
          icon="i-lucide-calendar"
          :label="t.settings.notifyWeeklyReview"
        />
      </SettingsSection>

      <SettingsSection
        :title="t.settings.account"
        :note="t.settings.accountNote"
      >
        <!--
          Not buttons: there is nothing behind them yet, and a control that
          reacts to a tap by doing nothing is worse than one that is visibly
          unavailable.
        -->
        <div
          v-for="row in accountRows"
          :key="row.label"
          class="flex items-center gap-3 border-b border-(--ui-border) px-4 py-3 opacity-55 last:border-b-0"
        >
          <span
            class="flex size-9 shrink-0 items-center justify-center rounded-(--q2-radius-sm) bg-(--q2-track) text-(--ui-text-muted)"
            aria-hidden="true"
          >
            <UIcon
              :name="row.icon"
              class="size-[18px]"
            />
          </span>
          <span class="min-w-0 flex-1 text-sm font-semibold">{{ row.label }}</span>
        </div>
      </SettingsSection>

      <div class="mt-5">
        <p
          v-if="person"
          class="mb-2.5 px-1 text-center text-xs font-semibold text-(--ui-text-muted)"
          data-testid="signed-in-as"
          data-q2-private
        >
          {{ t.auth.signedInAs(person.displayName) }} · {{ person.handle }}
        </p>

        <UButton
          type="button"
          block
          color="error"
          variant="soft"
          size="xl"
          icon="i-lucide-log-out"
          :loading="isSigningOut"
          class="min-h-11 justify-center font-extrabold"
          data-testid="sign-out"
          @click="onSignOut"
        >
          {{ t.auth.signOut }}
        </UButton>
      </div>

      <p class="mt-5 text-center text-[11px] font-semibold text-(--ui-text-dimmed)">
        {{ t.settings.version(config.public.appEnv) }}
      </p>
    </div>
  </div>
</template>
