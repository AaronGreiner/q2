<script setup lang="ts">
import { messageForKind, type ApiFailure } from '~/api/errors'

/**
 * Renders a failed API call for a person.
 *
 * Two rules it exists to enforce:
 *  - the user sees the friendly message, never the backend's `detail`, an
 *    exception name or a stack trace;
 *  - when the backend recorded the incident, the reference id is shown so a
 *    support request can be tied to the Sentry issue.
 */
const props = defineProps<{
  error: ApiFailure
  /** Shows a retry button and emits `retry` when pressed. */
  retryable?: boolean
}>()

const emit = defineEmits<{ retry: [] }>()

const title = computed(() => {
  switch (props.error.kind) {
    case 'network':
      return 'No connection'
    case 'notFound':
      return 'Not found'
    case 'unauthorized':
      return 'No access'
    default:
      return 'Something went wrong'
  }
})

const description = computed(() => messageForKind(props.error.kind))
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
        Try again
      </UButton>

      <!--
        Muted rather than dimmed, and not text-xs: this is the id a user is
        asked to quote in a support request, so it has to be readable.
        `--ui-text-dimmed` measures 2.63:1 against the page background, which
        fails WCAG AA (4.5:1 for body text); `--ui-text-muted` is 4.76:1.
      -->
      <p
        v-if="reference"
        class="text-sm text-(--ui-text-muted)"
      >
        Reference: <code class="font-mono">{{ reference }}</code>
      </p>
    </div>
  </AppStateMessage>
</template>
