<script setup lang="ts">
import * as Sentry from '@sentry/nuxt'
import { createDiagnosticsApi } from '~/api/diagnostics'
import type { SentryStatus } from '~/api/diagnostics'
import { normalizeApiError, toApiFailure, type ApiFailure } from '~/api/errors'

/**
 * A documented way to answer "is error reporting actually working?" without
 * waiting for a real incident.
 *
 * Only reachable when `NUXT_PUBLIC_DIAGNOSTICS_ENABLED` is set, which is the
 * case in development, manual testing and E2E — never in production, where the
 * matching backend routes do not exist either.
 */
const { public: config } = useRuntimeConfig()

if (!config.diagnosticsEnabled) {
  throw createError({ statusCode: 404, statusMessage: 'Not found', fatal: true })
}

const apiFetch = $fetch.create({ baseURL: config.apiBaseUrl, retry: 0, timeout: 10_000 })
const diagnostics = createDiagnosticsApi(apiFetch as never)

const serverError = ref<ApiFailure | null>(null)
const clientErrorResult = ref<string | null>(null)
const backendStatus = ref<SentryStatus | null>(null)
const statusError = ref<ApiFailure | null>(null)

onMounted(async () => {
  try {
    backendStatus.value = await diagnostics.sentryStatus()
  }
  catch (error) {
    statusError.value = toApiFailure(normalizeApiError(error))
  }
})

/** Hits an endpoint that always throws, to exercise the 500 path end to end. */
async function triggerServerError() {
  serverError.value = null
  try {
    await diagnostics.triggerServerError()
  }
  catch (error) {
    serverError.value = toApiFailure(normalizeApiError(error))
  }
}

/**
 * Reports a synthetic client-side error. Captured explicitly rather than
 * thrown, so the outcome is deterministic for the E2E suite.
 *
 * The message says what actually happened. A diagnostics page that claims
 * "reported to Sentry" while the SDK has no DSN and sends nothing is worse than
 * no diagnostics page at all — it is the one place that must not guess.
 */
function triggerClientError() {
  const error = new Error('Synthetic q2 frontend diagnostics error. This is not a real incident.')
  Sentry.captureException(error, { tags: { 'q2.feature': 'diagnostics', 'q2.action': 'client-error' } })

  const client = Sentry.getClient()
  const isSending = Boolean(client?.getDsn()) && client?.getOptions().enabled !== false

  clientErrorResult.value = isSending
    ? 'A synthetic client error was reported to Sentry.'
    : 'A synthetic client error was captured, but the browser SDK has no DSN configured, so nothing was sent.'
}

useHead({ title: 'Diagnostics' })
</script>

<template>
  <div class="flex flex-col gap-8">
    <section class="flex flex-col gap-2">
      <h1 class="text-2xl font-semibold">
        Diagnostics
      </h1>
      <p class="max-w-prose text-(--ui-text-muted)">
        Triggers synthetic failures so error handling and Sentry can be verified
        deliberately. Everything here is fake — no real data is involved.
      </p>
    </section>

    <section
      class="flex flex-col gap-3"
      aria-labelledby="status-heading"
    >
      <h2
        id="status-heading"
        class="text-lg font-medium"
      >
        Backend reporting status
      </h2>

      <UCard>
        <dl
          v-if="backendStatus"
          class="grid gap-2 sm:grid-cols-2"
          data-testid="sentry-status"
        >
          <div>
            <dt class="text-sm text-(--ui-text-muted)">
              Sending events
            </dt>
            <dd>{{ backendStatus.enabled ? 'yes' : 'no (no DSN configured)' }}</dd>
          </div>
          <div>
            <dt class="text-sm text-(--ui-text-muted)">
              Environment
            </dt>
            <dd data-testid="sentry-environment">
              {{ backendStatus.environment }}
            </dd>
          </div>
          <div>
            <dt class="text-sm text-(--ui-text-muted)">
              Release
            </dt>
            <dd>{{ backendStatus.release }}</dd>
          </div>
          <div>
            <dt class="text-sm text-(--ui-text-muted)">
              Transport
            </dt>
            <dd>{{ backendStatus.recordingTransport ? 'local recorder (test)' : 'Sentry HTTP' }}</dd>
          </div>
        </dl>

        <AppErrorState
          v-else-if="statusError"
          :error="statusError"
        />

        <p
          v-else
          class="text-(--ui-text-muted)"
        >
          Loading…
        </p>
      </UCard>
    </section>

    <section
      class="flex flex-col gap-3"
      aria-labelledby="triggers-heading"
    >
      <h2
        id="triggers-heading"
        class="text-lg font-medium"
      >
        Trigger a failure
      </h2>

      <div class="flex flex-wrap gap-3">
        <UButton
          icon="i-lucide-server-crash"
          color="error"
          variant="soft"
          data-testid="trigger-server-error"
          @click="triggerServerError"
        >
          Trigger server error
        </UButton>

        <UButton
          icon="i-lucide-bug"
          color="warning"
          variant="soft"
          data-testid="trigger-client-error"
          @click="triggerClientError"
        >
          Trigger client error
        </UButton>
      </div>

      <AppErrorState
        v-if="serverError"
        :error="serverError"
      />

      <p
        v-if="clientErrorResult"
        class="text-sm text-(--ui-text-muted)"
        role="status"
        data-testid="client-error-sent"
      >
        {{ clientErrorResult }}
      </p>
    </section>
  </div>
</template>
