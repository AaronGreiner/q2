import { describe, expect, it } from 'vitest'
import { de, en } from '~/i18n/messages'
import {
  feedbackFormOptions,
  feedbackHostId,
  feedbackTextOptions,
  type FeedbackMessages,
} from '../../sentry.feedback'

/**
 * User Feedback is the one path in q2 that sends something a person wrote to
 * Sentry on purpose, so what the form may collect is not a preference — it is
 * the decision in docs/adr/0014-user-feedback.md, and these run against the
 * exact object handed to `Sentry.feedbackIntegration`.
 */

describe('what the feedback form collects', () => {
  it('never asks for a name or an address, and cannot be made to', () => {
    expect(feedbackFormOptions.showName).toBe(false)
    expect(feedbackFormOptions.showEmail).toBe(false)

    // The SDK shows a hidden field anyway when its "required" twin is on.
    expect(feedbackFormOptions.isNameRequired).toBe(false)
    expect(feedbackFormOptions.isEmailRequired).toBe(false)
  })

  it('reads no identity out of the Sentry scope', () => {
    // Empty keys resolve to nothing, so a `Sentry.setUser` added later cannot
    // arrive in the hidden name and email fields the form still submits.
    expect(feedbackFormOptions.useSentryUser).toEqual({ email: '', name: '' })
  })

  it('never attaches a screenshot', () => {
    // A screenshot of a q2 screen is somebody's goals, messages and name, in an
    // attachment that neither the scrubber nor the replay masking reaches.
    expect(feedbackFormOptions.enableScreenshot).toBe(false)
  })

  it('injects no button of its own', () => {
    // It would sit on top of the tab bar, in English, on every screen.
    expect(feedbackFormOptions.autoInject).toBe(false)
  })

  it('names the host element main.css styles', () => {
    expect(feedbackFormOptions.id).toBe(feedbackHostId)
    expect(feedbackHostId).toBe('sentry-feedback')
  })
})

describe('feedbackTextOptions', () => {
  it('gives the SDK every label the form renders', () => {
    const options = feedbackTextOptions(de.feedback)

    expect(options).toEqual({
      formTitle: de.feedback.title,
      messageLabel: de.feedback.messageLabel,
      messagePlaceholder: de.feedback.messagePlaceholder,
      submitButtonLabel: de.feedback.submit,
      cancelButtonLabel: de.feedback.cancel,
      successMessageText: de.feedback.success,
      isRequiredLabel: de.feedback.required,
      errorEmptyMessageText: de.feedback.errorEmpty,
      errorNoClientText: de.feedback.errorUnavailable,
      errorTimeoutText: de.feedback.errorTimeout,
      errorForbiddenText: de.feedback.errorForbidden,
      errorGenericText: de.feedback.errorGeneric,
    })
  })

  it('leaves no label for the SDK to fill in in English', () => {
    for (const messages of [de.feedback, en.feedback]) {
      for (const [key, value] of Object.entries(feedbackTextOptions(messages))) {
        expect(value, key).toBeTruthy()
      }
    }
  })

  it('speaks whichever language the app is in', () => {
    expect(feedbackTextOptions(de.feedback).submitButtonLabel)
      .not.toBe(feedbackTextOptions(en.feedback).submitButtonLabel)
  })

  it('is satisfied by the catalogue in both languages', () => {
    // The dialog takes its words from a structural type rather than importing
    // the catalogue, so this is where the two are held together.
    const german: FeedbackMessages = de.feedback
    const english: FeedbackMessages = en.feedback

    expect(german.title).toBe(de.feedback.title)
    expect(english.title).toBe(en.feedback.title)
  })
})
