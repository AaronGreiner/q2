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

const t = useMessages()
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

useHead({ title: () => (isNotFound.value ? t.value.errors.pageNotFound : t.value.errors.title.other) })
</script>

<template>
  <div class="mx-auto flex h-dvh w-full max-w-[430px] items-center justify-center bg-(--ui-bg) px-6">
    <div
      class="flex flex-col items-center gap-4 text-center"
      role="alert"
      data-testid="app-error"
    >
      <UIcon
        :name="isNotFound ? 'i-lucide-compass' : 'i-lucide-triangle-alert'"
        class="size-10 text-(--ui-text-dimmed)"
        aria-hidden="true"
      />

      <h1 class="text-xl font-extrabold text-(--ui-text)">
        {{ isNotFound ? t.errors.pageNotFound : t.errors.title.other }}
      </h1>

      <!--
        Never `error.message` or `error.stack`: those are written for
        developers and can contain internals.
      -->
      <p class="text-(--ui-text-muted)">
        {{ isNotFound ? t.errors.pageNotFoundHint : t.errors.unexpected }}
      </p>

      <UButton
        icon="i-lucide-house"
        data-testid="app-error-home"
        @click="goHome"
      >
        {{ t.common.toHome }}
      </UButton>
    </div>
  </div>
</template>
