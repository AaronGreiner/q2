import * as Sentry from '@sentry/nuxt'
import { feedbackHostId, feedbackTextOptions, type FeedbackSource } from '../../sentry.feedback'

/**
 * Opens Sentry's User Feedback dialog from q2's own controls.
 *
 * The SDK can inject a floating button by itself. It is switched off
 * (`sentry.feedback.ts`), because that button would sit on top of the tab bar
 * on every screen and say "Report a Bug" in English in a German app. What is
 * left is this: the settings screen and the error page own the control, and
 * this composable owns the dialog.
 *
 * A fresh form per opening rather than one kept around, so that the language
 * and the theme are whatever they are *now* — both are changed on the settings
 * screen, one section above the button that opens this.
 */
export function useFeedback() {
  const t = useMessages()
  const { isDark } = useTheme()
  const toast = useToastMessage()
  const { report } = useErrorReporter()

  /**
   * Whether there is a dialog to open at all.
   *
   * Without a DSN the client never sets its integrations up, so `getFeedback()`
   * comes back undefined and a "Send feedback" row would be a control that does
   * nothing when tapped. Resolved after mount: the server cannot know, and
   * answering differently there would change the markup under hydration.
   */
  const isAvailable = ref(false)
  const isOpening = ref(false)

  onMounted(() => {
    isAvailable.value = Boolean(Sentry.getFeedback())
  })

  /**
   * Tells main.css that the dialog is up.
   *
   * The dialog is in a shadow DOM, so a stylesheet cannot see whether it is
   * open, and its host element stays in `<body>` for good once the SDK has
   * created it. Marking it is what keeps the keyboard rule from applying to an
   * empty host over the rest of the app — see the rule for the reasoning.
   */
  function markHost(isOpen: boolean) {
    const host = document.getElementById(feedbackHostId)

    if (isOpen) host?.setAttribute('data-q2-open', '')
    else host?.removeAttribute('data-q2-open')
  }

  async function open(source: FeedbackSource) {
    const feedback = Sentry.getFeedback()

    if (!feedback || isOpening.value) return

    isOpening.value = true

    try {
      /*
       * The dialog's own light/dark styles are written into its shadow DOM once
       * and then only ever replaced through this call. It follows the app's
       * theme rather than the system's, because q2's theme is a preference that
       * can disagree with the operating system.
       */
      feedback.setTheme(isDark.value ? 'dark' : 'light')

      // The callbacks are handed to the form before the handle to it exists, and
      // they only run once somebody presses something — hence the holder.
      let dialog: Awaited<ReturnType<typeof feedback.createForm>> | null = null

      function close() {
        dialog?.removeFromDom()
        markHost(false)

        /*
         * The SDK does not only put the tags below on the feedback event: on
         * submit it also writes them onto the current scope, which in a browser
         * outlives the dialog and every navigation after it. Left there, the
         * next unrelated error in the tab would arrive in Sentry claiming to
         * have come from the feedback form. Setting it to undefined is how a
         * tag is taken off a scope again.
         */
        Sentry.getCurrentScope().setTag('q2.feedback_source', undefined)
      }

      const created = await feedback.createForm({
        ...feedbackTextOptions(t.value.feedback),

        // Which control this came from. A closed vocabulary, so it can be a tag
        // on the event without becoming somewhere free text ends up.
        tags: { 'q2.feedback_source': source },

        // Nothing accumulates in the DOM: the form is built per opening, so the
        // one that was just closed is also the one to take out.
        onFormClose: close,
        onFormSubmitted: close,
      })

      dialog = created
      created.appendToDom()
      created.open()
      markHost(true)
    }
    catch (error) {
      // Only reachable if the modal cannot be built at all. Saying so is still
      // better than a button that swallowed the tap.
      report(error, { feature: 'feedback', action: 'open' })
      toast.show(t.value.toast.feedbackUnavailable)
    }
    finally {
      isOpening.value = false
    }
  }

  return { isAvailable, isOpening, open }
}
