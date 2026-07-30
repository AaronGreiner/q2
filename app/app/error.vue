<script setup lang="ts">
import * as Sentry from '@sentry/nuxt'
import type { NuxtError } from '#app'

/**
 * The last line of defence: anything not handled inside a page ends up here —
 * an unhandled render error, a failed route, a Nitro server error.
 *
 * Two responsibilities:
 *  - show a human something useful instead of a blank screen;
 *  - make sure the incident is visible in Sentry exactly once.
 *
 * The Nuxt Sentry integration already captures unhandled errors, so this page
 * only adds context. Capturing again here would create a duplicate issue for
 * one failure.
 */
const props = defineProps<{ error: NuxtError }>()

const isNotFound = computed(() => props.error.statusCode === 404)

onMounted(() => {
  if (isNotFound.value) {
    // A mistyped URL is not a defect.
    return
  }

  Sentry.addBreadcrumb({
    category: 'q2.error-page',
    level: 'error',
    message: `Error page shown for status ${props.error.statusCode}`,
  })
})

async function goHome() {
  // Clears the error state before navigating, otherwise the page stays.
  await clearError({ redirect: '/' })
}

useHead({ title: isNotFound.value ? 'Page not found' : 'Something went wrong' })
</script>

<template>
  <div class="flex min-h-screen items-center justify-center bg-(--ui-bg) px-4">
    <div
      class="flex max-w-md flex-col items-center gap-4 text-center"
      role="alert"
      data-testid="app-error"
    >
      <UIcon
        :name="isNotFound ? 'i-lucide-compass' : 'i-lucide-triangle-alert'"
        class="size-10 text-(--ui-text-dimmed)"
        aria-hidden="true"
      />

      <h1 class="text-xl font-semibold text-(--ui-text)">
        {{ isNotFound ? 'Page not found' : 'Something went wrong' }}
      </h1>

      <!--
        Never `error.message` or `error.stack`: those are written for
        developers and can contain internals.
      -->
      <p class="text-(--ui-text-muted)">
        {{
          isNotFound
            ? 'That page does not exist. It may have been moved or removed.'
            : 'We hit an unexpected problem. The incident has been recorded — please try again.'
        }}
      </p>

      <UButton
        icon="i-lucide-house"
        data-testid="app-error-home"
        @click="goHome"
      >
        Back to goals
      </UButton>
    </div>
  </div>
</template>
