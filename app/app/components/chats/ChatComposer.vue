<script setup lang="ts">
/**
 * The message box, with the one-tap encouragements above it.
 *
 * A real `<form>`, so the on-screen keyboard shows a send key and pressing it
 * submits — which on a phone is how most messages are actually sent.
 */
defineProps<{ busy?: boolean }>()

const emit = defineEmits<{ send: [text: string] }>()

const t = useMessages()
const draft = ref('')

function submit() {
  const text = draft.value.trim()
  if (!text) return

  emit('send', text)
  draft.value = ''
}
</script>

<template>
  <div class="shrink-0 border-t border-(--ui-border) bg-(--q2-surface)">
    <div class="q2-scroll-x flex gap-2 px-3 pt-2.5 pb-1">
      <button
        v-for="cheer in t.chats.quickCheers"
        :key="cheer.label"
        type="button"
        class="shrink-0 rounded-full bg-(--q2-accent-soft) px-3.5 py-1.5 text-[13px] font-bold whitespace-nowrap text-(--q2-accent-soft-text) focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
        data-testid="quick-cheer"
        @click="emit('send', cheer.text)"
      >
        {{ cheer.label }}
      </button>
    </div>

    <form
      class="flex items-center gap-2 px-3 pt-1 pb-3"
      @submit.prevent="submit"
    >
      <label
        class="sr-only"
        for="chat-message"
      >{{ t.chats.messagePlaceholder }}</label>

      <input
        id="chat-message"
        v-model="draft"
        :placeholder="t.chats.messagePlaceholder"
        autocomplete="off"
        enterkeyhint="send"
        class="h-10 min-w-0 flex-1 rounded-full border border-(--ui-border) bg-(--ui-bg) px-4 text-sm text-(--ui-text) outline-none focus-visible:border-(--ui-primary)"
        data-testid="chat-input"
      >

      <button
        type="submit"
        :disabled="busy || draft.trim().length === 0"
        class="flex size-10 shrink-0 items-center justify-center rounded-full bg-(--q2-accent-solid) text-white transition-opacity focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary) disabled:opacity-40"
        :aria-label="t.chats.send"
        data-testid="chat-send"
      >
        <UIcon
          name="i-lucide-send-horizontal"
          class="size-5"
          aria-hidden="true"
        />
      </button>
    </form>
  </div>
</template>
