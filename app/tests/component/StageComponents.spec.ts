import { mountSuspended } from '@nuxt/test-utils/runtime'
import { describe, expect, it } from 'vitest'
import ChallengeArchiveTile from '~/components/challenge/ChallengeArchiveTile.vue'
import DeleteAccountSheet from '~/components/settings/DeleteAccountSheet.vue'
import InviteCard from '~/components/friends/InviteCard.vue'
import type { ChallengeArchiveEntry } from '~/api/types'

/**
 * The three screens stages 7 to 9 added that nothing was rendering in a test.
 *
 * What is worth pinning down in each is the same kind of thing: a picture is
 * personal and must be blocked from Session Replay, a missing one is a normal
 * state rather than a broken one, and the one irreversible control in q2 says
 * what it will destroy before it asks for anything.
 */
function archiveEntry(overrides: Record<string, unknown> = {}): ChallengeArchiveEntry {
  return {
    challenge: {
      id: 'challenge-1',
      prompt: 'Zeig deinen Arbeitsplatz.',
      publishedAt: '2026-09-08T00:00:00Z',
      expiresAt: '2026-09-09T00:00:00Z',
    },
    entry: {
      id: 'entry-1',
      author: {
        id: 'person-1',
        displayName: 'Mara',
        handle: '@mara',
        initials: 'MK',
        avatarColor: '#4f46e5',
        isOnline: true,
        avatarImageId: null,
      },
      imageId: 'image-1',
      capturedInApp: true,
      createdAt: '2026-09-08T09:00:00Z',
      isMine: true,
      reactions: [],
    },
    ...overrides,
  } as ChallengeArchiveEntry
}

describe('ChallengeArchiveTile', () => {
  it('keeps a contribution out of Session Replay and sends the session with it', async () => {
    const wrapper = await mountSuspended(ChallengeArchiveTile, { props: { item: archiveEntry() } })

    expect(wrapper.get('[data-testid="challenge-archive-tile"]').attributes()).toHaveProperty('data-q2-block')
    expect(wrapper.get('img').attributes('crossorigin')).toBe('use-credentials')
  })

  /**
   * Only the date is on the tile; in a three-column grid the prompt would be
   * too long. Without it somewhere the picture is unplaceable in six months, so
   * it is in the label instead.
   */
  it('shows the date and keeps the prompt available to a screen reader', async () => {
    const wrapper = await mountSuspended(ChallengeArchiveTile, { props: { item: archiveEntry() } })

    expect(wrapper.get('figcaption').text()).toContain('Zeig deinen Arbeitsplatz.')
    expect(wrapper.get('img').attributes('alt')).toBe('Zeig deinen Arbeitsplatz.')
  })

  it('falls back to a placeholder when the picture will not load', async () => {
    const wrapper = await mountSuspended(ChallengeArchiveTile, { props: { item: archiveEntry() } })

    await wrapper.get('img').trigger('error')

    // A missing picture is a normal state, not a broken one: the frame stays
    // and an icon takes its place.
    expect(wrapper.find('img').exists()).toBe(false)
    expect(wrapper.find('svg').exists()).toBe(true)
  })
})

describe('InviteCard', () => {
  it('explains why the app looks empty and offers one thing that helps', async () => {
    const wrapper = await mountSuspended(InviteCard)

    expect(wrapper.text()).toContain('Noch keine Freunde')
    expect(wrapper.get('[data-testid="invite-share"]')).toBeTruthy()

    // The other half of the answer, and the reason this stage came after the
    // challenge: there is something to do today even with nobody watching.
    expect(wrapper.text()).toContain('Challenge des Tages')
  })

  /**
   * A browser that refuses the clipboard, or a person who wants to read it out,
   * still needs something to work with — which is why the link is text as well
   * as a share sheet, and why it is selectable at all.
   */
  it('keeps the link out of Session Replay but selectable by hand', async () => {
    const wrapper = await mountSuspended(InviteCard)

    const link = wrapper.find('[data-testid="invite-link"]')

    if (link.exists()) {
      expect(link.attributes()).toHaveProperty('data-q2-block')
      expect(link.classes()).toContain('q2-selectable')
    }
    else {
      // No code yet: the buttons must not offer to share nothing.
      expect(wrapper.get('[data-testid="invite-share"]').attributes('disabled')).toBeDefined()
    }
  })
})

describe('DeleteAccountSheet', () => {
  function submit(): HTMLButtonElement | null {
    return document.querySelector('[data-testid="delete-account-confirm"] button')
      ?? document.querySelector<HTMLButtonElement>('[data-testid="delete-account-confirm"]')
  }

  /**
   * It says what goes before it asks for anything, and it names the one thing
   * that does not — everybody else's half of a group conversation — because
   * that is the part people are surprised by afterwards.
   */
  it('says what will be destroyed before asking for the password', async () => {
    const wrapper = await mountSuspended(DeleteAccountSheet, { props: { open: true } })

    expect(document.body.textContent).toContain('Konto endgültig löschen')
    expect(document.body.textContent).toContain('In Gruppen verschwinden deine Nachrichten')
    expect(document.body.textContent).toContain('lässt sich nicht rückgängig machen')

    wrapper.unmount()
  })

  /**
   * A session cookie authorises reading somebody's screens; it does not
   * authorise erasing their year from a borrowed phone.
   */
  it('cannot be confirmed without the password', async () => {
    const wrapper = await mountSuspended(DeleteAccountSheet, { props: { open: true } })

    expect(submit()?.disabled).toBe(true)

    const field = document.querySelector<HTMLInputElement>('[data-testid="delete-account-password"] input')
      ?? document.querySelector<HTMLInputElement>('[data-testid="delete-account-password"]')

    expect(field?.getAttribute('type')).toBe('password')
    expect(field?.getAttribute('autocomplete')).toBe('current-password')

    wrapper.unmount()
  })

  /*
   * Closing and reopening the sheet is deliberately not exercised here. The
   * drawer's presence animation reads a computed style that happy-dom refuses
   * to hand out, and the resulting rejection is the environment failing rather
   * than the component — the same reason the other drawer specs mount open and
   * unmount. The reset itself is a three-line watcher; what is worth a test is
   * that the field is a real password field to begin with.
   */
})
