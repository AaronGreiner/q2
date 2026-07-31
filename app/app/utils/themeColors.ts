/**
 * The two background colours the platform needs before any CSS exists.
 *
 * Everything else in q2 is coloured from the design tokens in
 * app/assets/css/main.css, and that file says nothing outside it may name a
 * hex value. These are the exception it cannot serve, and this is the one file
 * that holds them:
 *
 *  - the web app manifest is JSON read by the operating system at install
 *    time, and paints the splash screen behind the icon (nuxt.config.ts);
 *  - the `theme-color` meta tags colour the status bar around an installed
 *    window, before the stylesheet has loaded (app/app.vue).
 *
 * They are `--ui-bg` from `:root` and from `.dark`, and they move with it.
 */
export const themeColors = {
  light: '#fafafa',
  dark: '#0a0a0a',
} as const
