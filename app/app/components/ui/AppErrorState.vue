<script setup lang="ts">
import type { ApiFailure } from '~/api/errors'

/**
 * Renders a failed API call for a person.
 *
 * Two rules it exists to enforce:
 *  - the user sees the friendly message for the *kind* of failure, never the
 *    backend's `detail`, an exception name or a stack trace;
 *  - when the backend recorded the incident, the reference id is shown so a
 *    support request can be tied to the Sentry issue.
 */
const props = defineProps<{
  error: ApiFailure
  /** Shows a retry button and emits `retry` when pressed. */
  retryable?: boolean
}>()

const emit = defineEmits<{ retry: [] }>()

const t = useMessages()

const title = computed(() => {
  switch (props.error.kind) {
    case 'network':
      return t.value.errors.title.network
    case 'notFound':
      return t.value.errors.title.notFound
    case 'unauthorized':
      return t.value.errors.title.unauthorized
    default:
      return t.value.errors.title.other
  }
})

const description = computed(() => t.value.errors[props.error.kind])
const reference = computed(() => props.error.errorId ?? props.error.traceId)
</script>

<template>
  <AppStateMessage
    :icon="error.kind === 'network' ? 'i-lucide-wifi-off' : 'i-lucide-triangle-alert'"
    :title="title"
    :description="description"
    tone="error"
    data-testid="error-state"
  >
    <div class="flex flex-col items-center gap-2">
      <UButton
        v-if="retryable"
        icon="i-lucide-rotate-cw"
        variant="soft"
        data-testid="error-retry"
        @click="emit('retry')"
      >
        {{ t.common.retry }}
      </UButton>

      <!--
        Muted rather than dimmed, and not text-xs: this is the id a user is
        asked to quote in a support request, so it has to be readable.
        `--ui-text-dimmed` fails WCAG AA for body text; `--ui-text-muted` does
        not.
      -->
      <p
        v-if="reference"
        class="text-sm text-(--ui-text-muted)"
      >
        {{ t.common.reference }}: <code class="font-mono">{{ reference }}</code>
      </p>
    </div>
  </AppStateMessage>
</template>
