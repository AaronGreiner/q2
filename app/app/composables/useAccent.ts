export const accentNames = ['iris', 'sage', 'rose', 'ochre'] as const
export type AccentName = typeof accentNames[number]

export function normalizeAccent(value: unknown): AccentName {
  return accentNames.includes(value as AccentName) ? value as AccentName : 'iris'
}

/** Device appearance is a cookie so SSR paints the chosen palette immediately. */
export function useAccent() {
  const stored = useCookie<string>('q2-accent', {
    default: () => 'iris',
    maxAge: 60 * 60 * 24 * 365,
    sameSite: 'lax',
    path: '/',
  })
  return computed<AccentName>({
    get: () => normalizeAccent(stored.value),
    set: (value) => { stored.value = normalizeAccent(value) },
  })
}
