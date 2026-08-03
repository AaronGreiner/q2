/**
 * The User Feedback dialog: what it is allowed to collect, and what it says.
 *
 * This is the one place in q2 where content a person wrote is sent to Sentry
 * on purpose. Everywhere else that is forbidden and `sentry.shared.ts` enforces
 * it — but a feedback event is not an error event, so `beforeSend` never sees
 * one, and the message travels exactly as it was typed. That inversion is the
 * whole reason this file exists rather than four flags in the client config:
 * the decision is deliberate, it is written down here, and it is unit-tested.
 *
 * See docs/adr/0014-user-feedback.md and docs/privacy.md section 4.
 */

/**
 * The id of the `<div>` the SDK appends to `<body>` to host the dialog's shadow
 * DOM. Set explicitly because main.css styles it by id — the dialog is inside a
 * shadow root, so the two rules there are the only reach the app has into it.
 */
export const feedbackHostId = 'sentry-feedback'

/**
 * Everything about the form that must not depend on where it was opened from.
 *
 * Each of these is a decision, not a default:
 *
 *  - `autoInject` off. The SDK's floating button would land on top of the tab
 *    bar, in English, on every screen. q2 opens the dialog from its own
 *    controls instead — see `useFeedback`.
 *  - `showName`, `showEmail` and their required flags off, and `useSentryUser`
 *    pointed at nothing. `scrubEvent` strips the account name and address out
 *    of every other event; a form that asks for them here — or quietly prefills
 *    them from the Sentry scope — would put back exactly what that removes.
 *    Feedback in q2 is anonymous, and there is no support inbox it could be
 *    answered from anyway.
 *  - `enableScreenshot` off. A screenshot of a q2 screen is somebody's goals,
 *    their messages and their name, in one attachment that no scrubber and no
 *    replay mask can reach into.
 *  - `showBranding` off. It is a link out of the app, and inside an installed
 *    PWA there is no browser chrome to come back from.
 */
export const feedbackFormOptions = {
  id: feedbackHostId,
  autoInject: false,
  showBranding: false,
  showName: false,
  showEmail: false,
  isNameRequired: false,
  isEmailRequired: false,
  enableScreenshot: false,

  // The keys the form would read a default name and address from. Empty ones
  // resolve to nothing, so a `Sentry.setUser` added later cannot leak into a
  // hidden field of this form without somebody changing this line.
  useSentryUser: { email: '', name: '' },
} as const

/** Where a dialog was opened from. A closed vocabulary, so it can be a tag. */
export type FeedbackSource = 'settings' | 'error-page'

/**
 * The words the dialog is built from.
 *
 * A structural type rather than an import of the catalogue: this module is
 * pulled into the Sentry client config, which loads before the app does, and it
 * has no business carrying a few hundred translated strings with it. The
 * catalogue satisfies the shape, and TypeScript checks that it still does.
 */
export interface FeedbackMessages {
  title: string
  messageLabel: string
  messagePlaceholder: string
  submit: string
  cancel: string
  success: string
  required: string
  errorEmpty: string
  errorUnavailable: string
  errorTimeout: string
  errorForbidden: string
  errorGeneric: string
}

/**
 * Maps the catalogue onto the SDK's text options.
 *
 * Applied per opening rather than once at `Sentry.init`, because the language
 * is a preference that changes while the app is running — and the screen it can
 * be changed on is the screen this dialog is opened from.
 *
 * Only the labels a q2 form actually shows are here. The trigger button, the
 * name and address fields and the screenshot editor are all switched off in
 * `feedbackFormOptions`, and a translated string for a control nobody can reach
 * is a string somebody has to keep translating.
 */
export function feedbackTextOptions(messages: FeedbackMessages) {
  return {
    formTitle: messages.title,
    messageLabel: messages.messageLabel,
    messagePlaceholder: messages.messagePlaceholder,
    submitButtonLabel: messages.submit,
    cancelButtonLabel: messages.cancel,
    successMessageText: messages.success,
    isRequiredLabel: messages.required,
    errorEmptyMessageText: messages.errorEmpty,
    errorNoClientText: messages.errorUnavailable,
    errorTimeoutText: messages.errorTimeout,
    errorForbiddenText: messages.errorForbidden,
    errorGenericText: messages.errorGeneric,
  }
}
