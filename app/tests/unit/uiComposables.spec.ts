import { computed, nextTick, ref } from 'vue'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { de } from '~/i18n/messages'

const getFeedback = vi.fn()
const setTag = vi.fn()

vi.mock('@sentry/nuxt', () => ({
  getFeedback: () => getFeedback(),
  getCurrentScope: () => ({ setTag }),
}))

const { useFeedback } = await import('~/composables/useFeedback')
const { useKeyboardViewport } = await import('~/composables/useKeyboardViewport')

/**
 * The two composables that talk to the browser rather than to the API.
 *
 * Neither has a rule of its own to check. What they have is a pile of states
 * the app is *in* — no Sentry client, a dialog that will not build, a keyboard
 * that is up, a page that never had a visual viewport — and each of those is a
 * branch that decides whether a control does something or quietly does nothing.
 */
const failure = {
  kind: 'network' as const,
  isExpected: true,
  status: null,
  fieldErrors: {},
  traceId: null,
  errorId: null,
  reason: null,
}

let mounted: Array<() => void>
let unmounted: Array<() => void>

function installGlobals(isDark = true) {
  const report = vi.fn(() => failure)
  const show = vi.fn()

  mounted = []
  unmounted = []

  vi.stubGlobal('ref', ref)
  vi.stubGlobal('computed', computed)
  vi.stubGlobal('onMounted', (fn: () => void) => mounted.push(fn))
  vi.stubGlobal('onUnmounted', (fn: () => void) => unmounted.push(fn))
  vi.stubGlobal('useMessages', () => ref(de))
  vi.stubGlobal('useTheme', () => ({ isDark: ref(isDark) }))
  vi.stubGlobal('useToastMessage', () => ({ show }))
  vi.stubGlobal('useErrorReporter', () => ({ report }))

  return { report, show }
}

function installDocument() {
  const host = { setAttribute: vi.fn(), removeAttribute: vi.fn() }

  vi.stubGlobal('document', {
    getElementById: vi.fn(() => host),
    documentElement: { style: { setProperty: vi.fn(), removeProperty: vi.fn() } },
  })

  return host
}

beforeEach(() => {
  vi.restoreAllMocks()
  vi.unstubAllGlobals()
  getFeedback.mockReset()
  setTag.mockReset()
})

describe('useFeedback', () => {
  function dialog() {
    return {
      appendToDom: vi.fn(),
      open: vi.fn(),
      removeFromDom: vi.fn(),
    }
  }

  function client(form = dialog()) {
    return {
      setTheme: vi.fn(),
      createForm: vi.fn().mockResolvedValue(form),
    }
  }

  /**
   * Without a DSN the client never sets its integrations up, so a "Send
   * feedback" row would be a control that does nothing when tapped.
   */
  it('is unavailable until something answers, and stays closed', async () => {
    installGlobals()
    installDocument()
    getFeedback.mockReturnValue(undefined)

    const feedback = useFeedback()
    expect(feedback.isAvailable.value).toBe(false)

    mounted.forEach(fn => fn())
    expect(feedback.isAvailable.value).toBe(false)

    await feedback.open('settings')
    expect(feedback.isOpening.value).toBe(false)
  })

  it('builds a fresh form per opening, in the app\'s own theme', async () => {
    installGlobals(true)
    const host = installDocument()
    const form = dialog()
    const sentry = client(form)
    getFeedback.mockReturnValue(sentry)

    const feedback = useFeedback()
    mounted.forEach(fn => fn())
    expect(feedback.isAvailable.value).toBe(true)

    await feedback.open('settings')

    expect(sentry.setTheme).toHaveBeenCalledWith('dark')
    expect(sentry.createForm).toHaveBeenCalledWith(
      expect.objectContaining({ tags: { 'q2.feedback_source': 'settings' } }),
    )
    expect(form.appendToDom).toHaveBeenCalled()
    expect(form.open).toHaveBeenCalled()

    // main.css cannot see into a shadow DOM, so the host is marked instead.
    expect(host.setAttribute).toHaveBeenCalledWith('data-q2-open', '')
  })

  it('follows the light theme when that is the one chosen', async () => {
    installGlobals(false)
    installDocument()
    const sentry = client()
    getFeedback.mockReturnValue(sentry)

    const feedback = useFeedback()
    await feedback.open('error')

    expect(sentry.setTheme).toHaveBeenCalledWith('light')
  })

  /**
   * The SDK writes its tags onto the current scope on submit, where they would
   * outlive the dialog and claim the next unrelated error came from the form.
   */
  it('takes its tag back off the scope when the form closes', async () => {
    installGlobals()
    const host = installDocument()
    const form = dialog()
    const sentry = client(form)
    getFeedback.mockReturnValue(sentry)

    await useFeedback().open('settings')

    const options = sentry.createForm.mock.calls[0]?.[0] as { onFormClose: () => void }
    options.onFormClose()

    expect(form.removeFromDom).toHaveBeenCalled()
    expect(host.removeAttribute).toHaveBeenCalledWith('data-q2-open')
    expect(setTag).toHaveBeenCalledWith('q2.feedback_source', undefined)
  })

  it('says so rather than swallowing the tap when the dialog cannot be built', async () => {
    const { report, show } = installGlobals()
    installDocument()
    const sentry = client()
    sentry.createForm.mockRejectedValueOnce(new Error('no modal'))
    getFeedback.mockReturnValue(sentry)

    const feedback = useFeedback()
    await feedback.open('settings')

    expect(report).toHaveBeenCalledWith(expect.any(Error), { feature: 'feedback', action: 'open' })
    expect(show).toHaveBeenCalledWith(de.toast.feedbackUnavailable)
    expect(feedback.isOpening.value).toBe(false)
  })

  it('opens one dialog at a time', async () => {
    installGlobals()
    installDocument()
    const sentry = client()
    let release: (value: unknown) => void = () => {}
    sentry.createForm.mockImplementation(() => new Promise((resolve) => {
      release = resolve
    }))
    getFeedback.mockReturnValue(sentry)

    const feedback = useFeedback()
    const first = feedback.open('settings')
    await nextTick()

    expect(feedback.isOpening.value).toBe(true)
    await feedback.open('settings')
    expect(sentry.createForm).toHaveBeenCalledTimes(1)

    release(dialog())
    await first
  })
})

describe('useKeyboardViewport', () => {
  function installViewport(viewportHeight: number | null, innerHeight = 800) {
    const listeners: Array<() => void> = []

    const visualViewport = viewportHeight === null
      ? null
      : {
          height: viewportHeight,
          addEventListener: vi.fn((_: string, fn: () => void) => listeners.push(fn)),
          removeEventListener: vi.fn(),
        }

    vi.stubGlobal('window', { innerHeight, visualViewport, scrollTo: vi.fn() })

    return { visualViewport, listeners }
  }

  /**
   * While the keyboard covers part of the screen the visual viewport is the
   * space actually left, and pinning the shell to it leaves WebKit nothing to
   * scroll.
   */
  it('publishes what is left and what is covered while the keyboard is up', () => {
    installGlobals()
    const document = installDocument()
    void document
    installViewport(520)

    useKeyboardViewport()
    mounted.forEach(fn => fn())

    const style = (globalThis as { document: { documentElement: { style: { setProperty: ReturnType<typeof vi.fn> } } } })
      .document.documentElement.style

    expect(style.setProperty).toHaveBeenCalledWith('--q2-viewport-height', '520px')
    expect(style.setProperty).toHaveBeenCalledWith('--q2-keyboard-inset', '280px')

    // WebKit may already have scrolled to reveal the field; it fits now.
    expect((globalThis as { window: { scrollTo: ReturnType<typeof vi.fn> } }).window.scrollTo)
      .toHaveBeenCalledWith(0, 0)
  })

  /** A fraction of a pixel is rounding on a zoomed-out page, not a keyboard. */
  it('leaves the shell alone when nothing is covered', () => {
    installGlobals()
    installDocument()
    installViewport(800)

    useKeyboardViewport()
    mounted.forEach(fn => fn())

    const style = (globalThis as { document: { documentElement: { style: { removeProperty: ReturnType<typeof vi.fn> } } } })
      .document.documentElement.style

    expect(style.removeProperty).toHaveBeenCalledWith('--q2-viewport-height')
    expect(style.removeProperty).toHaveBeenCalledWith('--q2-keyboard-inset')
  })

  it('reacts to the keyboard opening after mount', () => {
    installGlobals()
    installDocument()
    const { listeners } = installViewport(800)

    useKeyboardViewport()
    mounted.forEach(fn => fn())

    const style = (globalThis as { document: { documentElement: { style: { setProperty: ReturnType<typeof vi.fn> } } } })
      .document.documentElement.style
    style.setProperty.mockClear()

    const viewport = (globalThis as { window: { visualViewport: { height: number } } }).window.visualViewport
    viewport.height = 400
    listeners.forEach(fn => fn())

    expect(style.setProperty).toHaveBeenCalledWith('--q2-keyboard-inset', '400px')
  })

  it('puts everything back when the screen goes away', () => {
    installGlobals()
    installDocument()
    const { visualViewport } = installViewport(520)

    useKeyboardViewport()
    mounted.forEach(fn => fn())
    unmounted.forEach(fn => fn())

    expect(visualViewport?.removeEventListener).toHaveBeenCalled()
  })

  /** A browser without a visual viewport is not a failure, just nothing to do. */
  it('does nothing at all where there is no visual viewport', () => {
    installGlobals()
    installDocument()
    installViewport(null)

    useKeyboardViewport()
    mounted.forEach(fn => fn())
    unmounted.forEach(fn => fn())

    const style = (globalThis as { document: { documentElement: { style: { setProperty: ReturnType<typeof vi.fn> } } } })
      .document.documentElement.style

    expect(style.setProperty).not.toHaveBeenCalled()
  })
})
