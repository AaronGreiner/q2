<script setup lang="ts">
import type { LanguagePreference, ThemePreference } from '~/api/types'

/**
 * Preferences.
 *
 * Every control applies immediately and saves afterwards — there is no "Save"
 * button, because a settings screen with one invites people to change three
 * things and leave without pressing it.
 *
 * Two kinds of notification control sit here and they are not the same thing.
 * The switches are the account's preference and travel between devices; the row
 * below them is *this browser's* permission and subscription, which cannot.
 * Saying which is which is the difference between a switch that appears not to
 * work and one that plainly applies somewhere else.
 *
 * The three greyed-out account rows are still settings for features that do not
 * exist, and saying so is better than a row that quietly does nothing.
 */
definePageMeta({ layout: 'plain' })

const t = useMessages()
const theme = useTheme()
const language = useLanguage()
const { settings, update } = useAppSettings()

const config = useRuntimeConfig()
const { person, logout } = useSession()

const isDeleting = ref(false)
const feedback = useFeedback()

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
function notification(key: 'notifyReminders' | 'notifyKudos' | 'notifyMessages' | 'notifyWeeklyReview' | 'notifyChallenge') {
  return computed<boolean>({
    get: () => settings.value?.[key] ?? false,
    set: value => update({ [key]: value }),
  })
}

const reminders = notification('notifyReminders')
const kudos = notification('notifyKudos')
const chatMessages = notification('notifyMessages')
const weeklyReview = notification('notifyWeeklyReview')
const challenge = notification('notifyChallenge')

/*
 * Whether *this device* receives anything, which is a different question from
 * the switches above.
 *
 * The switches are the account's preference and travel between devices; this is
 * one browser's permission and subscription, and it cannot. Resolved on mount
 * rather than stored, because the browser is the authority: a person can revoke
 * the permission in Chrome's own settings and the app would never hear about it.
 */
const push = usePushNotifications()

onMounted(() => push.resolve())

const quietHoursEnabled = computed<boolean>({
  get: () => Boolean(settings.value?.quietHoursFrom),
  set: value => update({ quietHoursEnabled: value }),
})

/**
 * The two ends of the window, as `<input type="time">` wants them.
 *
 * The server stores a `TimeOnly` and serialises it with seconds; the control
 * takes and gives back `HH:mm`. Trimming here rather than in the catalogue
 * because it is a fact about the control, not about the language.
 */
function quietHour(key: 'quietHoursFrom' | 'quietHoursTo') {
  return computed<string>({
    get: () => (settings.value?.[key] ?? '').slice(0, 5),
    set: value => update({ quietHoursEnabled: true, [key]: `${value}:00` }),
  })
}

const quietFrom = quietHour('quietHoursFrom')
const quietTo = quietHour('quietHoursTo')

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
          v-model="challenge"
          icon="i-lucide-zap"
          :label="t.challenge.heading"
        />
        <SettingsToggleRow
          v-model="weeklyReview"
          icon="i-lucide-calendar"
          :label="t.settings.notifyWeeklyReview"
        />
      </SettingsSection>

      <!--
        This device, which is a different question from the switches above: they
        are the account's preference and travel; a browser's permission does not.

        Every state says what it is rather than showing a switch that would do
        nothing — a blocked browser in particular, because that is the one the
        app cannot undo and the person can.
      -->
      <SettingsSection
        v-if="push.state.value !== 'unsupported'"
        :title="t.settings.notifications"
      >
        <SettingsActionRow
          v-if="push.state.value === 'off' || push.state.value === 'on'"
          :icon="push.state.value === 'on' ? 'i-lucide-bell-off' : 'i-lucide-bell'"
          :label="push.state.value === 'on' ? t.settings.notificationsOff : t.settings.notificationsOn"
          :busy="push.isBusy.value"
          data-testid="push-toggle"
          @activate="push.state.value === 'on' ? push.disable() : push.enable()"
        />

        <p
          v-else
          class="px-4 py-3 text-[13px] font-semibold text-(--ui-text-muted)"
          data-testid="push-unavailable"
        >
          {{ push.state.value === 'blocked' ? t.settings.notificationsBlocked : t.settings.notificationsUnavailable }}
        </p>
      </SettingsSection>

      <SettingsSection
        :title="t.settings.quietHours"
        :note="t.settings.quietHoursNote"
      >
        <SettingsToggleRow
          v-model="quietHoursEnabled"
          icon="i-lucide-moon-star"
          :label="t.settings.quietHours"
        />

        <div
          v-if="quietHoursEnabled"
          class="flex items-center gap-3 px-4 py-3"
          data-testid="quiet-hours"
        >
          <label class="flex min-w-0 flex-1 flex-col gap-1">
            <span class="text-[11px] font-bold text-(--ui-text-muted)">{{ t.settings.quietHoursFrom }}</span>
            <input
              v-model="quietFrom"
              type="time"
              class="q2-selectable min-h-11 rounded-(--q2-radius-sm) border border-(--ui-border) bg-(--ui-bg-elevated) px-3 text-sm font-semibold"
              data-testid="quiet-hours-from"
            >
          </label>

          <label class="flex min-w-0 flex-1 flex-col gap-1">
            <span class="text-[11px] font-bold text-(--ui-text-muted)">{{ t.settings.quietHoursTo }}</span>
            <input
              v-model="quietTo"
              type="time"
              class="q2-selectable min-h-11 rounded-(--q2-radius-sm) border border-(--ui-border) bg-(--ui-bg-elevated) px-3 text-sm font-semibold"
              data-testid="quiet-hours-to"
            >
          </label>
        </div>
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

      <!--
        Only when there is a dialog behind it. Without a DSN the Sentry client
        sets up no integrations, and a row that opens nothing is worse than no
        row — the same reason the account rows above are visibly unavailable
        rather than quietly dead.
      -->
      <SettingsSection
        v-if="feedback.isAvailable.value"
        :title="t.feedback.section"
        :note="t.feedback.note"
      >
        <SettingsActionRow
          icon="i-lucide-message-square-heart"
          :label="t.feedback.open"
          :busy="feedback.isOpening.value"
          data-testid="open-feedback"
          @activate="feedback.open('settings')"
        />
      </SettingsSection>

      <!--
        The safety rows, and the way out, together at the bottom.

        Below everything else on purpose: these are not settings somebody
        adjusts, they are things somebody reaches for once. Deleting is last,
        under the sign-out, because it is the only row here that does not lead
        back.
      -->
      <SettingsSection :title="t.safety.blockedHeading">
        <SettingsActionRow
          icon="i-lucide-shield"
          :label="t.safety.blockedHeading"
          data-testid="open-blocked"
          @activate="navigateTo('/settings/blocked')"
        />
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

        <UButton
          type="button"
          block
          color="error"
          variant="ghost"
          size="lg"
          class="mt-2 min-h-11 justify-center font-bold"
          data-testid="open-delete-account"
          @click="isDeleting = true"
        >
          {{ t.deleteAccount.open }}
        </UButton>
      </div>

      <p class="mt-5 text-center text-[11px] font-semibold text-(--ui-text-dimmed)">
        {{ t.settings.version(config.public.appEnv) }}
      </p>
    </div>

    <DeleteAccountSheet v-model:open="isDeleting" />
  </div>
</template>
