<script setup lang="ts">
import type { FriendshipState } from '~/api/types'

/**
 * The one thing to do about somebody, on their profile (#86).
 *
 * A profile is where people go to decide about a person, so the answer is
 * there rather than back on the Freunde tab: add a stranger, answer somebody
 * who asked, take back your own request, or write to a friend. One primary
 * action per state, switched on the `state` the server sends, the way
 * `PersonSearchRow` does it for a result.
 *
 * The accent is only on what moves the friendship forward. Declining and
 * withdrawing are neutral: they are answers too, but not the one the screen
 * should lean towards. Ending a friendship stays behind the ellipsis, one
 * deliberate step away from "Nachricht schreiben".
 */
defineProps<{
  state: FriendshipState
  /** Whose profile it is, for the labels a screen reader reads out. */
  name: string
  busy?: boolean
}>()

const emit = defineEmits<{
  request: []
  withdraw: []
  accept: []
  decline: []
  message: []
}>()

const t = useMessages()
</script>

<template>
  <div
    v-if="state !== 'Self'"
    class="mt-4"
    data-testid="person-friendship"
    :data-state="state"
  >
    <UButton
      v-if="state === 'None'"
      block
      size="xl"
      icon="i-lucide-user-plus"
      class="min-h-11 justify-center font-extrabold"
      :disabled="busy"
      :aria-label="`${t.friends.add}: ${name}`"
      data-testid="person-request"
      @click="emit('request')"
    >
      {{ t.friends.add }}
    </UButton>

    <!--
      The request is out: said in grey, because waiting is a state and not
      something to do, with the way to take it back beside it.
    -->
    <div
      v-else-if="state === 'RequestSent'"
      class="q2-card flex items-center gap-3 py-1.5 pr-1.5 pl-3.5"
    >
      <p
        class="flex min-w-0 flex-1 items-center gap-1.5 text-sm font-bold text-(--ui-text-muted)"
        data-testid="person-requested"
      >
        <UIcon
          name="i-lucide-clock"
          class="size-4 shrink-0"
          aria-hidden="true"
        />
        {{ t.person.requestSent }}
      </p>
      <UButton
        color="neutral"
        variant="outline"
        size="lg"
        class="min-h-11 font-extrabold"
        :disabled="busy"
        :aria-label="`${t.friends.withdraw}: ${name}`"
        data-testid="person-withdraw"
        @click="emit('withdraw')"
      >
        {{ t.friends.withdraw }}
      </UButton>
    </div>

    <div
      v-else-if="state === 'RequestReceived'"
      class="flex flex-col gap-2"
    >
      <p class="text-center text-sm font-bold text-(--ui-text-muted)">
        {{ t.person.requestReceived }}
      </p>
      <div class="grid grid-cols-2 gap-2">
        <UButton
          block
          size="xl"
          icon="i-lucide-check"
          class="min-h-11 justify-center font-extrabold"
          :disabled="busy"
          :aria-label="`${t.friends.accept}: ${name}`"
          data-testid="person-accept"
          @click="emit('accept')"
        >
          {{ t.friends.accept }}
        </UButton>
        <UButton
          block
          size="xl"
          color="neutral"
          variant="outline"
          class="min-h-11 justify-center font-extrabold"
          :disabled="busy"
          :aria-label="`${t.friends.decline}: ${name}`"
          data-testid="person-decline"
          @click="emit('decline')"
        >
          {{ t.friends.decline }}
        </UButton>
      </div>
    </div>

    <UButton
      v-else-if="state === 'Friends'"
      block
      size="xl"
      icon="i-lucide-message-circle"
      class="min-h-11 justify-center font-extrabold"
      :disabled="busy"
      data-testid="person-message"
      @click="emit('message')"
    >
      {{ t.friends.message }}
    </UButton>
  </div>
</template>
