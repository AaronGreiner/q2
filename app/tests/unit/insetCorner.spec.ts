import { computed, readonly, ref } from 'vue'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { clampDrag, insetTapSlop, settleSide, travelOf } from '~/utils/insetCorner'

/**
 * The small picture of a proof from both cameras, dragged between the two top
 * corners (#151). The rules: it never leaves its frame, it lands in whichever
 * corner is nearer, and a drag is never also the tap that swaps the pictures.
 */

describe('where the corner lands', () => {
  it('follows a drag only as far as the other corner, and never back past its own', () => {
    expect(clampDrag('start', 120, 200)).toBe(120)
    expect(clampDrag('start', 500, 200)).toBe(200)
    expect(clampDrag('start', -40, 200)).toBe(0)
    expect(clampDrag('end', -120, 200)).toBe(-120)
    expect(clampDrag('end', -500, 200)).toBe(-200)
    expect(clampDrag('end', 40, 200)).toBe(0)
  })

  it('lands in the nearer corner when let go', () => {
    expect(settleSide('start', 90, 200)).toBe('start')
    expect(settleSide('start', 110, 200)).toBe('end')
    expect(settleSide('end', -90, 200)).toBe('end')
    expect(settleSide('end', -110, 200)).toBe('start')
    expect(settleSide('start', 900, 200)).toBe('end')
  })

  it('measures the way across with the same margin on either side', () => {
    // 390 wide, a 109 pixel corner 10 pixels in.
    expect(travelOf({ offsetLeft: 10, offsetWidth: 109 }, 390, 'start')).toBe(261)
    expect(travelOf({ offsetLeft: 271, offsetWidth: 109 }, 390, 'end')).toBe(261)
    expect(travelOf({ offsetLeft: 10, offsetWidth: 500 }, 390, 'start')).toBe(0)
  })
})

describe('useInsetCorner', () => {
  let useInsetCorner: typeof import('~/composables/useInsetCorner').useInsetCorner

  beforeEach(async () => {
    vi.stubGlobal('ref', ref)
    vi.stubGlobal('computed', computed)
    vi.stubGlobal('readonly', readonly)
    vi.stubGlobal('requestAnimationFrame', (run: () => void) => run())
    ;({ useInsetCorner } = await import('~/composables/useInsetCorner'))
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  /** A corner 109 pixels wide, 10 pixels into a 390 pixel frame. */
  function cornerElement() {
    return { offsetLeft: 10, offsetWidth: 109, offsetParent: { clientWidth: 390 }, setPointerCapture: vi.fn() }
  }

  function pointer(type: string, x: number, y = 0, target = cornerElement()) {
    return {
      type,
      pointerId: 1,
      pointerType: 'touch',
      button: 0,
      clientX: x,
      clientY: y,
      currentTarget: target,
      stopPropagation: vi.fn(),
    } as unknown as PointerEvent
  }

  it('moves to the other corner on a drag past the middle, and takes the click that ends it', () => {
    const corner = useInsetCorner()
    const element = cornerElement()

    const down = pointer('pointerdown', 50, 50, element)
    corner.onPointerdown(down)
    // The card's swipe and the viewer's drag-to-close never see it.
    expect(down.stopPropagation).toHaveBeenCalled()
    expect(element.setPointerCapture).toHaveBeenCalledWith(1)

    corner.onPointermove(pointer('pointermove', 150, 52, element))
    expect(corner.style.value.transform).toBe('translateX(100px)')
    expect(corner.style.value.transition).toBe('none')

    corner.onPointerup(pointer('pointerup', 230, 52, element))
    expect(corner.side.value).toBe('end')
    expect(corner.style.value.transform).toBeUndefined()

    expect(corner.tookClick()).toBe(true)
    // Asking clears it: the next tap swaps again.
    expect(corner.tookClick()).toBe(false)
  })

  it('slides back to its own corner from a short drag', () => {
    const corner = useInsetCorner()
    const element = cornerElement()

    corner.onPointerdown(pointer('pointerdown', 50, 50, element))
    corner.onPointermove(pointer('pointermove', 120, 50, element))
    corner.onPointerup(pointer('pointerup', 120, 50, element))

    expect(corner.side.value).toBe('start')
    expect(corner.tookClick()).toBe(true)
  })

  /** Below the slop it was a tap: the click that follows is the swap. */
  it('leaves a tap to be the swap', () => {
    const corner = useInsetCorner()
    const element = cornerElement()

    corner.onPointerdown(pointer('pointerdown', 50, 50, element))
    corner.onPointermove(pointer('pointermove', 50 + insetTapSlop - 2, 50, element))
    corner.onPointerup(pointer('pointerup', 50 + insetTapSlop - 2, 50, element))

    expect(corner.side.value).toBe('start')
    expect(corner.style.value.transform).toBeUndefined()
    expect(corner.tookClick()).toBe(false)
  })

  /** The page took the touch over for a scroll: back where it was, and nothing swallowed. */
  it('goes back where it was when the touch is cancelled', () => {
    const corner = useInsetCorner()
    const element = cornerElement()

    corner.onPointerdown(pointer('pointerdown', 50, 50, element))
    corner.onPointermove(pointer('pointermove', 250, 50, element))
    corner.onPointerup(pointer('pointercancel', 250, 50, element))

    expect(corner.side.value).toBe('start')
    expect(corner.tookClick()).toBe(false)
  })

  it('moves with the arrow keys, and back to the start when reset', () => {
    const corner = useInsetCorner()
    const key = (name: string) => ({ key: name, preventDefault: vi.fn(), stopPropagation: vi.fn() }) as unknown as KeyboardEvent

    const right = key('ArrowRight')
    corner.onKeydown(right)
    expect(corner.side.value).toBe('end')
    expect(right.preventDefault).toHaveBeenCalled()

    const enter = key('Enter')
    corner.onKeydown(enter)
    expect(enter.preventDefault).not.toHaveBeenCalled()
    expect(corner.side.value).toBe('end')

    corner.onKeydown(key('ArrowLeft'))
    expect(corner.side.value).toBe('start')

    corner.onKeydown(key('ArrowRight'))
    corner.reset()
    expect(corner.side.value).toBe('start')
  })

  it('ignores another finger, and a mouse button that is not the main one', () => {
    const corner = useInsetCorner()
    const element = cornerElement()

    corner.onPointerdown({ ...pointer('pointerdown', 50, 50, element), pointerType: 'mouse', button: 2 } as PointerEvent)
    corner.onPointermove(pointer('pointermove', 300, 50, element))
    expect(corner.style.value.transform).toBeUndefined()

    corner.onPointerdown(pointer('pointerdown', 50, 50, element))
    corner.onPointermove({ ...pointer('pointermove', 300, 50, element), pointerId: 2 } as PointerEvent)
    expect(corner.style.value.transform).toBeUndefined()
  })
})
