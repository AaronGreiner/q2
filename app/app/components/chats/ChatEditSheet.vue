<script setup lang="ts">
import type { ApiFailure } from '~/api/errors'
import type { ChatMessage } from '~/api/types'

/**
 * Correcting the words of your own message (#25).
 *
 * A sheet rather than the composer: the composer is for what comes next, and
 * turning it into an editor for something further up would leave a draft
 * somebody had started with nowhere to go. It says up front that the others
 * will see it was edited — "bearbeitet" beside the time — because a correction
 * that hides itself would be a way to change what somebody already answered.
 */
const props = withDefaults(defineProps<{
  message: ChatMessage | null
  busy?: boolean
  failure?: ApiFailure | null
}>(), {
  busy: false,
  failure: null,
})

const emit = defineEmits<{ save: [text: string] }>()

const open = defineModel<boolean>('open', { required: true })

const t = useMessages()

const text = ref('')

watch(open, (isOpen) => {
  if (isOpen) text.value = props.message?.text ?? ''
}, { immediate: true })

const trimmed = computed(() => text.value.trim())
const canSave = computed(() => trimmed.value.length > 0 && trimmed.value !== props.message?.text && !props.busy)

function fieldError(): string | undefined {
  return fieldErrorText(props.failure, 'text', t.value)
}

function save() {
  if (canSave.value) emit('save', trimmed.value)
}
</script>

<template>
  <UDrawer
    v-model:open="open"
    :title="t.chats.editHeading"
    :description="t.chats.editHint"
    :ui="{ container: 'max-w-[430px] mx-auto' }"
  >
    <template #body>
      <form
        class="flex flex-col gap-4 pb-2"
        novalidate
        data-testid="chat-edit-form"
        @submit.prevent="save"
      >
        <UFormField
          :error="fieldError()"
          name="text"
        >
          <UTextarea
            v-model="text"
            :rows="3"
            autoresize
            :maxrows="8"
            class="w-full"
            data-testid="chat-edit-text"
          />
        </UFormField>

        <UButton
          type="submit"
          block
          size="xl"
          icon="i-lucide-check"
          :label="t.chats.editSave"
          :disabled="!canSave"
          :loading="busy"
          data-testid="chat-edit-save"
        />
      </form>
    </template>
  </UDrawer>
</template>
