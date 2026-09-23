import { mountSuspended } from '@nuxt/test-utils/runtime'
import { describe, expect, it } from 'vitest'
import ProofGalleryTile from '~/components/proofs/ProofGalleryTile.vue'
import ProofPhotoViewer from '~/components/proofs/ProofPhotoViewer.vue'
import type { OwnProof } from '~/api/types'

/**
 * Your own photographs, on your profile.
 *
 * The things worth pinning down: that a photograph never reaches Session
 * Replay, that an outcome other than "believed" is said in words rather than
 * left to a colour, and that the full-size view names the goal and leads to it.
 */
function proof(overrides: Partial<OwnProof> = {}): OwnProof {
  return {
    id: 'proof-1',
    imageId: 'image-1',
    status: 'Confirmed',
    createdAt: '2026-09-15T09:00:00+00:00',
    goalId: 'goal-1',
    goalTitle: 'Dreimal die Woche laufen',
    goalIcon: 'run',
    ...overrides,
  }
}

describe('ProofGalleryTile', () => {
  it('keeps the photograph out of Session Replay', async () => {
    const wrapper = await mountSuspended(ProofGalleryTile, { props: { proof: proof() } })

    expect(wrapper.get('[data-testid="proof-gallery-tile"]').attributes()).toHaveProperty('data-q2-block')
  })

  it('shows a believed photograph with its date and no label', async () => {
    const wrapper = await mountSuspended(ProofGalleryTile, { props: { proof: proof() } })

    expect(wrapper.text()).toContain('15.9.2026')
    expect(wrapper.find('[data-testid="proof-gallery-status"]').exists()).toBe(false)
  })

  it.each([
    ['Voting', 'Wird geprüft'],
    ['Rejected', 'Nicht anerkannt'],
  ] as const)('says in words that a photograph is %s', async (status, label) => {
    const wrapper = await mountSuspended(ProofGalleryTile, { props: { proof: proof({ status }) } })

    expect(wrapper.get('[data-testid="proof-gallery-status"]').text()).toBe(label)
  })

  it('names the goal, the day and the outcome for a screen reader, and opens on tap', async () => {
    const wrapper = await mountSuspended(ProofGalleryTile, { props: { proof: proof({ status: 'Rejected' }) } })
    const tile = wrapper.get('[data-testid="proof-gallery-tile"]')

    expect(tile.attributes('aria-label')).toBe('Dreimal die Woche laufen, 15.9.2026, Nicht anerkannt — groß ansehen')

    await tile.trigger('click')
    expect(wrapper.emitted('open')).toHaveLength(1)
  })
})

describe('ProofPhotoViewer', () => {
  it('shows the photograph with its goal, day and outcome, and leads to the goal', async () => {
    const wrapper = await mountSuspended(ProofPhotoViewer, {
      props: { open: true, proof: proof({ status: 'Voting' }) },
      attachTo: document.body,
    })

    const viewer = document.querySelector('[data-testid="proof-viewer"]')
    expect(viewer).not.toBeNull()
    expect(viewer!.hasAttribute('data-q2-block')).toBe(true)

    const dialog = document.querySelector('[role="dialog"]')
    expect(dialog?.textContent).toContain('Dreimal die Woche laufen')
    expect(dialog?.textContent).toContain('Geliefert am 15.9.2026 · Wird geprüft')

    const goal = document.querySelector<HTMLAnchorElement>('[data-testid="proof-viewer-goal"]')
    expect(goal?.getAttribute('href')).toBe('/goals/goal-1')

    const close = document.querySelector<HTMLButtonElement>('[data-testid="proof-viewer-close"]')
    expect(close?.getAttribute('aria-label')).toBe('Schließen')

    wrapper.unmount()
  })
})
