<script setup lang="ts">
/**
 * The question in front of something that cannot be undone from inside the app.
 *
 * Not `window.confirm`: that is a system dialog with the host name in its
 * title, it cannot be styled, and on a phone it looks like the browser is
 * asking rather than q2. It is also modal to the whole page, which in a
 * Capacitor build is the operating system's dialog rather than the app's.
 *
 * Two destructive actions use it — ending a friendship and leaving a group —
 * and both are destructive for somebody else as well, which is exactly when a
 * question is worth asking.
 */
defineProps<{
  title: string
  description: string
  confirmLabel: string
  /** The description interpolates a name or other personal value. */
  privateDescription?: boolean
}>()

const emit = defineEmits<{ confirm: [] }>()

const open = defineModel<boolean>('open', { required: true })

const t = useMessages()

function onConfirm() {
  open.value = false
  emit('confirm')
}
</script>

<template>
  <UModal
    v-model:open="open"
    :title="title"
    :description="description"
    :ui="{ content: 'max-w-[360px]' }"
  >
    <template #description>
      <span :data-q2-private="privateDescription ? '' : undefined">
        {{ description }}
      </span>
    </template>

    <template #footer>
      <div
        class="flex w-full gap-2"
        data-testid="confirm-dialog"
      >
        <UButton
          type="button"
          variant="soft"
          color="neutral"
          block
          size="lg"
          class="min-h-11 flex-1 justify-center font-bold"
          data-testid="confirm-cancel"
          @click="open = false"
        >
          {{ t.common.close }}
        </UButton>

        <UButton
          type="button"
          color="error"
          block
          size="lg"
          class="min-h-11 flex-1 justify-center font-extrabold"
          data-testid="confirm-accept"
          @click="onConfirm"
        >
          {{ confirmLabel }}
        </UButton>
      </div>
    </template>
  </UModal>
</template>
