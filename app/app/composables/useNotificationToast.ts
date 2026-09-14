import type { NotificationLine } from '~/api/types'
import {
  isNotificationOnScreen,
  notificationIcon,
  notificationLink,
  notificationTag,
  notificationText,
} from '~/utils/display'

/** How long a banner stays: long enough to read the first line of a message. */
export const bannerDurationMs = 5000

/**
 * A notification, shown at the top of the open app.
 *
 * What arrives here is what a push would have carried, sent instead of one
 * because this person is looking (docs/adr/0025-banners-in-the-open-app.md).
 * The server has already decided that it may interrupt them — their switch,
 * their quiet hours, a muted chat — so the one thing decided here is what only
 * the app knows: where it is. Nothing is shown for the screen it would open, and
 * a banner goes as soon as its screen is reached some other way.
 *
 * The words come from `notificationText`, as they do in the bell and on a lock
 * screen, and `notificationTag` lets a second message in one chat replace the
 * banner for the first rather than stack on it.
 *
 * The whole banner is the link, the way a line in the bell is: a real `<a href>`
 * in the title, stretched over the banner, so a tap anywhere opens it and a
 * keyboard can reach it. Its handler navigates within the app, and only while
 * the banner is still open — a swipe that dismissed it ends in a click on the
 * same element, and that click must not open what was just waved away.
 *
 * No close button, like a banner on a phone: one target to tap rather than two
 * a thumb can confuse. It goes by itself, is swiped up, or closed with Escape.
 */
export function useNotificationToast() {
  const toast = useToast()
  const t = useMessages()
  const router = useRouter()

  /** The notification behind each banner on screen, so a navigation can find the ones it answered. */
  const shown = new Map<string, NotificationLine>()

  function isOpen(id: string) {
    return toast.toasts.value.some(entry => entry.id === id && entry.open !== false)
  }

  function dismiss(id: string) {
    toast.remove(id)
    shown.delete(id)
  }

  watch(() => router.currentRoute.value.path, (path) => {
    for (const [id, line] of shown) {
      if (isNotificationOnScreen(line, path)) dismiss(id)
    }
  })

  function announce(line: NotificationLine) {
    if (isNotificationOnScreen(line, router.currentRoute.value.path)) return

    // Banners that timed out or were swiped away are no longer the toaster's.
    for (const id of shown.keys()) {
      if (!toast.toasts.value.some(entry => entry.id === id)) shown.delete(id)
    }

    const id = `notification:${notificationTag(line)}`
    const text = notificationText(line, t.value)
    const to = notificationLink(line)

    shown.set(id, line)

    toast.add({
      id,
      title: () => h('a', {
        'href': to,
        'class': 'after:absolute after:inset-0 focus-visible:outline-none',
        'data-testid': 'notification-banner',
        'onClick': (event: MouseEvent) => {
          // Always: the href is there for a keyboard and a screen reader, and
          // following it would reload the whole app.
          event.preventDefault()

          if (!isOpen(id)) return

          dismiss(id)
          void navigateTo(to)
        },
      }, text.title),
      description: text.body,
      icon: notificationIcon(line),

      // News, not something to do this second: no accent
      // (docs/adr/0015-qdos-design-language.md).
      color: 'neutral',
      duration: bannerDurationMs,
      progress: false,
      close: false,
      ui: {
        root: 'rounded-(--q2-radius-lg) has-[a:focus-visible]:outline-2 has-[a:focus-visible]:outline-offset-2 has-[a:focus-visible]:outline-(--ui-primary)',

        // Nuxt UI renders a toast in a teleport, where no data-q2-* attribute
        // can be placed, so Replay's own classes stand in: a name and a goal
        // title are masked, and a message — somebody's words, whose length is
        // itself personal — is blocked the way a chat bubble is.
        title: 'sentry-mask',
        description: `line-clamp-2 ${line.kind === 'MessageReceived' ? 'sentry-block' : 'sentry-mask'}`,
      },
    })
  }

  return { announce }
}
