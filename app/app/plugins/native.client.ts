import { Capacitor } from '@capacitor/core'
import { createAccountsApi } from '~/api/accounts'
import { createCaller, type ApiFetch } from '~/api/client'
import { createObjectUrlCache, type ObjectUrlCache } from '~/utils/objectUrlCache'
import { createTokenKeeper, type RefreshTokenStorage, type TokenKeeper } from '~/utils/sessionTokens'

/**
 * What the iOS app needs that the browser does not: somewhere to keep a
 * session, and somewhere to keep pictures.
 *
 * The browser signs in with an http-only cookie and loads pictures through
 * `<img>`, so both of these are `null` there and nothing else changes. Inside
 * the Capacitor shell the WebView cannot keep that cookie — the API is another
 * site — so it signs in for bearer tokens instead
 * (docs/adr/0034-bearer-tokens-for-the-native-app.md). `useQ2Api`,
 * `useSession`, `useLiveConnection` and `useImageSource` read these two and
 * take the token path when they are there.
 *
 * Provided per app rather than kept in a module, like everything else here
 * that holds state (AGENTS.md section 4). Client-only: the native build is
 * never server-rendered, and the web build's server has no business with it.
 */
export default defineNuxtPlugin(() => {
  if (!Capacitor.isNativePlatform()) {
    return {
      provide: {
        sessionTokens: null as TokenKeeper | null,
        imageCache: null as ObjectUrlCache | null,
      },
    }
  }

  const { public: config } = useRuntimeConfig()

  // Its own client, without the token: refreshing is what gets the token, so
  // it cannot go through the client that needs one.
  const accounts = createAccountsApi(createCaller($fetch.create({
    baseURL: config.apiBaseUrl,
    credentials: 'omit',
    retry: 0,
    timeout: 10_000,
    headers: { Accept: 'application/json' },
  }) as ApiFetch))

  return {
    provide: {
      sessionTokens: createTokenKeeper({
        storage: keychain(),
        refresh: refreshToken => accounts.refreshTokens(refreshToken),
      }) as TokenKeeper | null,
      imageCache: createObjectUrlCache() as ObjectUrlCache | null,
    },
  }
})

/** The key the refresh token is kept under, after the plugin's own prefix. */
const refreshTokenKey = 'refresh-token'

/**
 * The refresh token in the iOS Keychain.
 *
 * Never synchronised through iCloud, and "this device only": a refresh token
 * is a key to somebody's account, and it must not arrive on a new phone from
 * a backup. Imported here, on the native path only, so the browser never
 * loads the plugin.
 */
function keychain(): RefreshTokenStorage {
  const plugin = import('@aparajita/capacitor-secure-storage')

  return {
    read: async () => {
      const { SecureStorage } = await plugin
      const value = await SecureStorage.get(refreshTokenKey, false, false)
      return typeof value === 'string' && value ? value : null
    },
    write: async (refreshToken) => {
      const { SecureStorage, KeychainAccess } = await plugin
      await SecureStorage.set(refreshTokenKey, refreshToken, false, false, KeychainAccess.whenUnlockedThisDeviceOnly)
    },
    clear: async () => {
      const { SecureStorage } = await plugin
      await SecureStorage.remove(refreshTokenKey, false)
    },
  }
}
