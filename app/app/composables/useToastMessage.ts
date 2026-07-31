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

  function show(message: ToastMessage) {
    toast.add({
      title: `${message.emoji} ${message.text}`,
      color: 'primary',
      duration: 2200,
    })
  }

  return { show }
}
