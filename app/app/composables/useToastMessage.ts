/**
 * The little confirmation that pops up after something good happens.
 *
 * A thin wrapper over Nuxt UI's toaster, for two reasons: every one of these
 * carries an emoji and a sentence from the message catalogue, and they all
 * share a duration short enough not to sit on top of the bottom navigation.
 */
export interface ToastMessage {
  emoji: string
  text: string
}

export function useToastMessage() {
  const toast = useToast()

  function show(message: ToastMessage, options: { private?: boolean } = {}) {
    toast.add({
      title: `${message.emoji} ${message.text}`,
      color: 'primary',
      duration: 2200,

      // Nuxt UI renders toasts in a teleport outside the calling component,
      // where a data attribute cannot be placed. `.sentry-mask` is Replay's
      // built-in equivalent and only goes on the two messages containing a
      // person's name.
      ui: options.private ? { title: 'sentry-mask' } : undefined,
    })
  }

  return { show }
}
