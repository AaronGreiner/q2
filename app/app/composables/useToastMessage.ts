/**
 * The little confirmation for something that worked where nothing on screen
 * would otherwise say so — a link copied, a report received, something gone
 * for good.
 *
 * Deliberately rare. The toaster is where an open app shows what has arrived
 * for somebody (`useNotificationToast`), and a confirmation of something the
 * screen already shows — a button changing state, a card leaving, a goal
 * appearing — is noise on top of that. So there is none for those.
 *
 * A thin wrapper over Nuxt UI's toaster, so every one of these carries an icon
 * and a sentence from the message catalogue, the house radius, and a duration
 * short enough not to sit over the screen's header for long.
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

  function show(message: ToastMessage) {
    toast.add({
      title: message.text,
      icon: message.icon,
      color: 'primary',
      duration: 2200,
      ui: { root: 'rounded-(--q2-radius-lg)' },
    })
  }

  return { show }
}
