<script setup lang="ts">
import type { NotificationSettings, UpdateNotificationSettingsRequest } from '~/api/types'

/**
 * Which notifications may reach this person's devices — one switch each —
 * under Profil → Einstellungen.
 *
 * Two kinds of control live here, and they are not the same thing
 * (docs/adr/0023-web-push.md). "Dieses Gerät" is one browser's permission and
 * subscription, which cannot travel; the switches below it are the account's,
 * and apply to every device that has said yes.
 *
 * **A switch decides what may interrupt, not what may be found.** The bell
 * keeps everything whatever is switched off here, the same way the message
 * switch never took a message out of a chat — and the screen says so
 * (docs/adr/0024-one-notification-pipeline.md).
 *
 * Every control applies at once and saves afterwards, and only the control
 * that moved is sent, so two switches tapped in quick succession both stick.
 */
definePageMeta({ layout: 'plain' })

type Switch = Exclude<keyof NotificationSettings, 'quietHoursFrom' | 'quietHoursTo'>

const t = useMessages()
const { settings, update } = useAppSettings()

function change(notifications: UpdateNotificationSettingsRequest) {
  return update({ notifications })
}

/** One writable flag per switch, so each row stays a plain `v-model`. */
function toggle(key: Switch) {
  return computed<boolean>({
    // On until the account says otherwise: every switch starts on.
    get: () => settings.value?.notifications[key] ?? true,
    set: (value) => {
      const one: UpdateNotificationSettingsRequest = {}
      one[key] = value
      void change(one)
    },
  })
}

const messages = toggle('messages')
const friendships = toggle('friendships')
const votesDue = toggle('votesDue')
const proofResults = toggle('proofResults')
const goalUpdates = toggle('goalUpdates')
const friendsAtRisk = toggle('friendsAtRisk')
const reactions = toggle('reactions')
const challenge = toggle('challenge')

/*
 * Whether *this device* receives anything at all — a different question from
 * the switches, and resolved on mount rather than stored, because the browser
 * is the authority: a person can revoke the permission in its own settings and
 * the app would never hear about it.
 */
const push = usePushNotifications()

onMounted(() => push.resolve())

const quietHoursEnabled = computed<boolean>({
  get: () => Boolean(settings.value?.notifications.quietHoursFrom),
  set: value => void change({ quietHoursEnabled: value }),
})

/**
 * The two ends of the window, as `<input type="time">` wants them.
 *
 * The server stores a `TimeOnly` and serialises it with seconds; the control
 * takes and gives back `HH:mm`. Trimmed here rather than in the catalogue
 * because it is a fact about the control, not about the language.
 */
function quietHour(key: 'quietHoursFrom' | 'quietHoursTo') {
  return computed<string>({
    get: () => (settings.value?.notifications[key] ?? '').slice(0, 5),
    set: (value) => {
      const window: UpdateNotificationSettingsRequest = { quietHoursEnabled: true }
      window[key] = `${value}:00`
      void change(window)
    },
  })
}

const quietFrom = quietHour('quietHoursFrom')
const quietTo = quietHour('quietHoursTo')

useHead({ title: () => t.value.notificationSettings.heading })
</script>

<template>
  <div class="flex min-h-0 flex-1 flex-col">
    <AppScreenHeader
      :title="t.notificationSettings.heading"
      back-to="/settings"
      :back-label="t.common.back"
    />

    <div class="q2-scroll flex-1 px-[18px] pt-1 pb-[calc(2rem+var(--q2-safe-bottom))]">
      <!--
        This device first, because nothing below it reaches this phone until
        it says yes. Every state says what it is rather than showing a switch
        that would do nothing — a blocked browser in particular, which is the
        one the app cannot undo and the person can.
      -->
      <SettingsSection
        v-if="push.state.value !== 'unsupported'"
        :title="t.notificationSettings.device"
        :note="t.notificationSettings.deviceNote"
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

      <SettingsSection :title="t.notificationSettings.friends">
        <SettingsToggleRow
          v-model="messages"
          icon="i-lucide-message-circle"
          :label="t.notificationSettings.messages"
        />
        <SettingsToggleRow
          v-model="friendships"
          icon="i-lucide-user-plus"
          :label="t.notificationSettings.friendships"
        />
      </SettingsSection>

      <SettingsSection :title="t.notificationSettings.goals">
        <SettingsToggleRow
          v-model="votesDue"
          icon="i-lucide-gavel"
          :label="t.notificationSettings.votesDue"
        />
        <SettingsToggleRow
          v-model="proofResults"
          icon="i-lucide-circle-check-big"
          :label="t.notificationSettings.proofResults"
        />
        <SettingsToggleRow
          v-model="goalUpdates"
          icon="i-lucide-calendar"
          :label="t.notificationSettings.goalUpdates"
        />
        <SettingsToggleRow
          v-model="friendsAtRisk"
          icon="i-lucide-clock-alert"
          :label="t.notificationSettings.friendsAtRisk"
        />
      </SettingsSection>

      <SettingsSection :title="t.notificationSettings.encouragement">
        <SettingsToggleRow
          v-model="reactions"
          icon="i-lucide-hand-heart"
          :label="t.notificationSettings.reactions"
        />
      </SettingsSection>

      <SettingsSection
        :title="t.notificationSettings.challenge"
        :note="t.notificationSettings.switchesNote"
      >
        <SettingsToggleRow
          v-model="challenge"
          icon="i-lucide-zap"
          :label="t.notificationSettings.dailyChallenge"
        />
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
    </div>
  </div>
</template>
