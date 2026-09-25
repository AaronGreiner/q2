import type { CapacitorConfig } from '@capacitor/cli'
import { de } from './app/i18n/messages'
import { installedThemeColor } from './app/utils/themeColors'

/**
 * The iOS app: the same q2, built client-only and served from the bundle by a
 * WebView (docs/adr/0034-bearer-tokens-for-the-native-app.md).
 *
 * Built with `bun run app:ios` from the repository root, never with `cap`
 * directly: the bundle has to be the native build (scripts/ios.ts), and a
 * plain `nuxt build` copied in here would be the website — server-rendered,
 * with a service worker, signing in with a cookie the WebView cannot keep.
 */
const config: CapacitorConfig = {
  // Fixed for good once the app is in App Store Connect (#51).
  appId: 'dev.aarongreiner.q2',

  // What the home screen shows under the icon — the same name the web app
  // manifest gives an installed PWA.
  appName: de.app.name,

  webDir: '.output/public',

  // Behind the WebView until the first frame, so a launch goes from the
  // launch screen to the app without a white flash in between. The launch
  // screen itself is drawn in the same colour (scripts/generate-icons.ts).
  backgroundColor: installedThemeColor,

  ios: {
    // The page lays itself out under the notch and the home indicator with
    // viewport-fit=cover and env(safe-area-inset-*), exactly as the installed
    // PWA does (app/app.vue). Letting the WebView inset its content as well
    // would count the safe areas twice.
    contentInset: 'never',
  },

  plugins: {
    /*
     * The keyboard shrinks the WebView, the way it shrinks a native screen.
     *
     * Left to itself, WebKit in an app does what it does in Safari — keeps the
     * page full height and scrolls it until the focused field shows — except
     * that in the WebView the scroll lands after useKeyboardViewport has put
     * the shell back, so the app stays pushed up behind the status bar with
     * the form cut off. Resized natively there is nothing to scroll: 100dvh is
     * already the space above the keyboard (form bar included), the visual
     * viewport is the whole page, and useKeyboardViewport measures nothing
     * covered and stays out of the way (app/AGENTS.md section 9b).
     */
    Keyboard: {
      resize: 'native',
      resizeOnFullScreen: true,
    },
  },
}

export default config
