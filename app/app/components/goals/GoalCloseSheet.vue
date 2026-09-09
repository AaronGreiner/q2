<script setup lang="ts">
/**
 * Stopping a goal: carried through, or given up.
 *
 * Two buttons rather than one with a checkbox, because they are two different
 * things to have done and the difference is the person's own claim — the app
 * has no way to tell them apart and should not pretend to.
 *
 * What both share is the sentence under them: the record stays exactly as it
 * is. That is the whole reason this exists. Before it, the only way to stop was
 * to delete the goal for everybody, so anybody who wanted to stop after half a
 * year had to destroy their own balance to do it.
 */
defineProps<{ submitting: boolean }>()

const emit = defineEmits<{ close: [completed: boolean] }>()

const open = defineModel<boolean>('open', { required: true })

const t = useMessages()
</script>

<template>
  <UDrawer
    v-model:open="open"
    :title="t.close.heading"
    :description="t.close.subtitle"
    :ui="{ container: 'max-w-[430px] mx-auto' }"
  >
    <template #body>
      <div class="flex flex-col gap-3 pb-2">
        <UButton
          block
          size="xl"
          icon="i-lucide-trophy"
          :label="t.close.completed"
          :disabled="submitting"
          data-testid="close-completed"
          @click="emit('close', true)"
        />

        <p class="text-[13px] font-semibold text-(--ui-text-muted)">
          {{ t.close.completedHint }}
        </p>

        <UButton
          block
          size="xl"
          color="neutral"
          variant="outline"
          icon="i-lucide-archive"
          :label="t.close.archived"
          :disabled="submitting"
          data-testid="close-archived"
          @click="emit('close', false)"
        />

        <p class="text-[13px] font-semibold text-(--ui-text-muted)">
          {{ t.close.archivedHint }}
        </p>

        <p class="mt-2 text-[13px] font-semibold text-(--ui-text-dimmed)">
          {{ t.close.note }}
        </p>
      </div>
    </template>
  </UDrawer>
</template>
