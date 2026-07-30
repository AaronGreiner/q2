import * as Sentry from '@sentry/nuxt'
import type { ApiFailure } from '~/api/errors'
import { isApiError, normalizeApiError, toApiFailure } from '~/api/errors'

export interface ReportContext {
  /** Which part of the app failed, e.g. 'goals'. */
  feature: string
  /** What the user was doing, e.g. 'list' or 'create'. */
  action: string
}

/**
 * The single decision point for "does this failure deserve a Sentry issue?".
 *
 * Three rules, and they are the reason this is a composable rather than a
 * scattering of `Sentry.captureException` calls:
 *
 *  1. Expected failures (validation, 404, offline) never create an issue. They
 *     are normal use, and reporting them buries the real defects.
 *  2. A 5xx that already carries an `errorId` was recorded by the backend in
 *     the q2-api project. The browser adds a breadcrumb instead of opening a
 *     second issue for the same incident in q2-app.
 *  3. Everything else — unexpected client errors, 5xx with no backend event —
 *     is reported once, with feature/action tags and no user content.
 *
 * Nothing here ever attaches a goal title, description or form input: those are
 * personal, and a stack trace does not need them. See docs/privacy.md.
 */
export function useErrorReporter() {
  /**
   * Reports the failure if it deserves it, and returns it as plain data that
   * can cross the SSR boundary.
   */
  function report(error: unknown, context: ReportContext): ApiFailure {
    const normalized = isApiError(error) ? error : normalizeApiError(error)

    Sentry.addBreadcrumb({
      category: 'q2.api',
      level: normalized.isExpected ? 'info' : 'error',
      message: `${context.feature}.${context.action} failed (${normalized.kind})`,
      data: {
        kind: normalized.kind,
        status: normalized.status ?? 'none',
      },
    })

    if (normalized.isExpected) {
      return toApiFailure(normalized)
    }

    if (normalized.errorId) {
      // Already an issue in q2-api. Recording it again here would double-count
      // one incident across two projects.
      Sentry.addBreadcrumb({
        category: 'q2.api',
        level: 'error',
        message: 'Backend reported this failure',
        data: { errorId: normalized.errorId },
      })
      return toApiFailure(normalized)
    }

    Sentry.captureException(normalized.cause ?? normalized, {
      tags: {
        'q2.feature': context.feature,
        'q2.action': context.action,
        'q2.error_kind': normalized.kind,
      },
      contexts: {
        q2: {
          status: normalized.status,
          traceId: normalized.traceId,
        },
      },
    })

    return toApiFailure(normalized)
  }

  return { report }
}
