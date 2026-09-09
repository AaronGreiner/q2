<script setup lang="ts">
/**
 * The way in for somebody who does not know anybody here.
 *
 * The gap both q2 and the project it grew out of had: a new account has no
 * friends, and almost everything here is something you do where friends can
 * see. The card explains why the app looks empty and offers exactly one thing
 * that helps.
 *
 * The last line is the other half of the answer, and it is the reason this
 * stage came after the challenge: there is something to do today even with
 * nobody watching.
 *
 * The link is built in the browser from the page's own origin — the server
 * hands back a code and does not know which host the app is served from.
 */
const t = useMessages()

const { url, isLoading, isReplacing, share, replace } = useInvite()

const isConfirmingReplace = ref(false)
</script>

<template>
  <section
    class="q2-card px-4 py-4"
    aria-labelledby="invite-heading"
    data-testid="invite-card"
  >
    <h2
      id="invite-heading"
      class="text-base font-extrabold"
    >
      {{ t.invite.heading }}
    </h2>

    <p class="mt-1.5 text-[13px] leading-relaxed font-semibold text-(--ui-text-muted)">
      {{ t.invite.body }}
    </p>

    <!--
      The link is shown as text as well as handed to the share sheet: a browser
      that refuses the clipboard, or a person who wants to read it out, still
      has something to work with.
    -->
    <p
      v-if="url"
      class="q2-selectable mt-3 truncate rounded-(--q2-radius-sm) bg-(--ui-bg-elevated) px-3 py-2 text-[12px] font-semibold text-(--ui-text-muted)"
      data-testid="invite-link"
      data-q2-block
    >
      {{ url }}
    </p>

    <USkeleton
      v-else-if="isLoading"
      class="mt-3 h-9 w-full rounded-(--q2-radius-sm)"
    />

    <div class="mt-3 flex items-center gap-2">
      <UButton
        class="flex-1 justify-center"
        size="lg"
        icon="i-lucide-share-2"
        :label="t.invite.share"
        :disabled="!url"
        data-testid="invite-share"
        @click="share()"
      />

      <UButton
        class="min-h-11 min-w-11 justify-center"
        size="lg"
        color="neutral"
        variant="outline"
        icon="i-lucide-link"
        :aria-label="t.invite.replace"
        :disabled="!url || isReplacing"
        data-testid="invite-replace"
        @click="isConfirmingReplace = true"
      />
    </div>

    <p class="mt-3 text-[12px] font-semibold text-(--ui-text-dimmed)">
      {{ t.invite.meanwhile }}
    </p>

    <AppConfirmDialog
      v-model:open="isConfirmingReplace"
      :title="t.invite.replaceHeading"
      :description="t.invite.replaceBody"
      :confirm-label="t.invite.replaceConfirm"
      @confirm="replace()"
    />
  </section>
</template>
