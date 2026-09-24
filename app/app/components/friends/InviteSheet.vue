<script setup lang="ts">
/**
 * Your invite link, for when you already have friends.
 *
 * `InviteCard` is the empty state — "Noch keine Freunde", and gone with the
 * first friend. After that the link still matters: there is always somebody
 * else to bring in. This sheet is the same link without the empty-state
 * wording, opened from the friends tab, the profile and a search that found
 * nobody.
 *
 * Share and copy side by side, because a share sheet on a phone does not
 * always offer the app somebody wants to paste the link into. Replacing is
 * not here: it is a safety action, reached for once, and it lives in the
 * settings where the sheet's last line points.
 *
 * It reads the link itself, like `InviteCard`, rather than being handed it:
 * every screen that opens it would otherwise repeat the same three lines.
 */
const open = defineModel<boolean>('open', { required: true })

const t = useMessages()

const { url, isLoading, share, copy } = useInvite()
</script>

<template>
  <UDrawer
    v-model:open="open"
    :title="t.invite.open"
    :description="t.invite.sheetBody"
    :ui="{ container: 'max-w-[430px] mx-auto' }"
  >
    <template #body>
      <div
        class="flex flex-col gap-3 pb-2"
        data-testid="invite-sheet"
      >
        <!--
          Shown as text as well as handed to the share sheet: a browser that
          refuses the clipboard, or a person who wants to read it out, still
          has something to work with.
        -->
        <p
          v-if="url"
          class="q2-selectable truncate rounded-(--q2-radius-sm) bg-(--ui-bg-elevated) px-3 py-2.5 text-[13px] font-semibold text-(--ui-text-muted)"
          data-testid="invite-sheet-link"
          data-q2-block
        >
          {{ url }}
        </p>

        <USkeleton
          v-else-if="isLoading"
          class="h-10 w-full rounded-(--q2-radius-sm)"
        />

        <div class="flex gap-2">
          <UButton
            class="min-h-11 flex-1 justify-center font-extrabold"
            size="xl"
            icon="i-lucide-share-2"
            :label="t.invite.share"
            :disabled="!url"
            data-testid="invite-sheet-share"
            @click="share()"
          />

          <UButton
            class="min-h-11 flex-1 justify-center font-extrabold"
            size="xl"
            color="neutral"
            variant="outline"
            icon="i-lucide-copy"
            :label="t.invite.copy"
            :disabled="!url"
            data-testid="invite-sheet-copy"
            @click="copy()"
          />
        </div>

        <p class="text-[12px] leading-relaxed font-semibold text-(--ui-text-dimmed)">
          {{ t.invite.sheetHint }}
        </p>
      </div>
    </template>
  </UDrawer>
</template>
