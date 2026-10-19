<script setup lang="ts">
import type { ChatMessage } from '~/api/types'

type MessageAction = 'edit' | 'delete' | 'report'

/**
 * What can be done with one message, after pressing and holding it.
 *
 * Your own: correct the words while the server still allows it (`canEdit`,
 * #25), or take it back. Somebody else's: report it (#104). Nothing for a
 * message that was taken back — there is nothing left in it.
 *
 * The choice is handed on only once this sheet has finished closing. What
 * follows is another sheet or a question, and two drawers open at once
 * deadlock (see PhotoCapture); waiting for the close is what lets the next one
 * open cleanly. A fallback timer covers a close that never reports its end.
 */
const props = defineProps<{ message: ChatMessage | null }>()

const emit = defineEmits<{
  edit: [message: ChatMessage]
  delete: [message: ChatMessage]
  report: [message: ChatMessage]
}>()

const open = defineModel<boolean>('open', { required: true })

const t = useMessages()

const actions = computed(() => {
  const message = props.message
  if (!message || message.isDeleted) return []

  if (!message.isMine) {
    return [{ key: 'report' as const, icon: 'i-lucide-flag', label: t.value.chats.reportMessage }]
  }

  return [
    ...(message.canEdit ? [{ key: 'edit' as const, icon: 'i-lucide-pencil', label: t.value.chats.editMessage }] : []),
    { key: 'delete' as const, icon: 'i-lucide-trash-2', label: t.value.chats.deleteMessage },
  ]
})

let chosen: { action: MessageAction, message: ChatMessage } | null = null
let fallback: ReturnType<typeof setTimeout> | null = null

function choose(action: MessageAction) {
  if (!props.message) return

  chosen = { action, message: props.message }
  open.value = false
  fallback = setTimeout(handOn, 500)
}

function handOn() {
  if (fallback) clearTimeout(fallback)
  fallback = null

  const next = chosen
  chosen = null
  if (!next) return

  if (next.action === 'edit') emit('edit', next.message)
  else if (next.action === 'delete') emit('delete', next.message)
  else emit('report', next.message)
}

function onAnimationEnd(isOpen: boolean) {
  if (!isOpen) handOn()
}

onBeforeUnmount(() => {
  if (fallback) clearTimeout(fallback)
})
</script>

<template>
  <UDrawer
    v-model:open="open"
    :title="t.chats.messageActionsHeading"
    :description="message?.text || undefined"
    :ui="{ container: 'max-w-[430px] mx-auto', description: 'line-clamp-2' }"
    @animation-end="onAnimationEnd"
  >
    <template #body>
      <div
        class="flex flex-col pb-2"
        data-testid="chat-message-actions"
      >
        <button
          v-for="action in actions"
          :key="action.key"
          type="button"
          class="flex min-h-12 items-center gap-3 border-b border-(--ui-border) px-1 py-3 text-left text-sm font-bold last:border-b-0 focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-(--ui-primary)"
          :class="action.key === 'delete' ? 'text-(--ui-error)' : 'text-(--ui-text)'"
          :data-testid="`chat-action-${action.key}`"
          @click="choose(action.key)"
        >
          <UIcon
            :name="action.icon"
            class="size-5 shrink-0"
            aria-hidden="true"
          />
          {{ action.label }}
        </button>
      </div>
    </template>
  </UDrawer>
</template>
