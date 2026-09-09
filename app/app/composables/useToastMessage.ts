/**
 * The little confirmation that pops up after something good happens.
 *
 * A thin wrapper over Nuxt UI's toaster, for two reasons: every one of these
 * carries an icon and a sentence from the message catalogue, and they all share
 * a duration short enough not to sit on top of the bottom navigation.
 *
 * An icon rather than an emoji, like the rest of the interface: an emoji is a
 * picture drawn differently on every platform, with a name we do not control
 * and no way to colour it.
 */
export interface ToastMessage {
  icon: string
  text: string
}

export function useToastMessage() {
  const toast = useToast()

  function show(message: ToastMessage, options: { private?: boolean } = {}) {
    toast.add({
      title: message.text,
      icon: message.icon,
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
