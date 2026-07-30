import { mountSuspended } from '@nuxt/test-utils/runtime'
import { describe, expect, it } from 'vitest'
import type { ApiFailure } from '~/api/errors'
import GoalCreateForm from '~/components/goals/GoalCreateForm.vue'
import type { CreateGoalRequest } from '~/api/types'

/** The create form: what it emits, and how it shows what the server rejected. */

function failure(overrides: Partial<ApiFailure> = {}): ApiFailure {
  return {
    kind: 'validation',
    message: 'ignored',
    status: 400,
    fieldErrors: {},
    traceId: null,
    errorId: null,
    isExpected: true,
    ...overrides,
  }
}

async function mountForm(props: Record<string, unknown> = {}) {
  return await mountSuspended(GoalCreateForm, { props })
}

function lastSubmit(wrapper: Awaited<ReturnType<typeof mountForm>>): CreateGoalRequest {
  const events = wrapper.emitted('submit')
  expect(events).toBeDefined()
  return events!.at(-1)![0] as CreateGoalRequest
}

describe('GoalCreateForm', () => {
  it('cannot be submitted without a title', async () => {
    const wrapper = await mountForm()

    expect(wrapper.find('[data-testid="goal-submit"]').attributes('disabled')).toBeDefined()

    await wrapper.find('form').trigger('submit')
    expect(wrapper.emitted('submit')).toBeUndefined()
  })

  it('treats a whitespace-only title as empty', async () => {
    const wrapper = await mountForm()

    await wrapper.find('[data-testid="goal-title-input"]').setValue('   ')
    await wrapper.find('form').trigger('submit')

    expect(wrapper.emitted('submit')).toBeUndefined()
  })

  it('emits a trimmed request for a title-only goal', async () => {
    const wrapper = await mountForm()

    await wrapper.find('[data-testid="goal-title-input"]').setValue('  Walk every day  ')
    await wrapper.find('form').trigger('submit')

    expect(lastSubmit(wrapper)).toEqual({
      title: 'Walk every day',
      description: null,
      targetDate: null,
      participants: null,
    })
  })

  it('splits participants on commas and drops the blanks', async () => {
    const wrapper = await mountForm()

    await wrapper.find('[data-testid="goal-title-input"]').setValue('Run a 10k')
    await wrapper.find('[data-testid="goal-participants-input"]')
      .setValue(' Robin Sample , ,Kim Example ,')
    await wrapper.find('form').trigger('submit')

    expect(lastSubmit(wrapper).participants).toEqual(['Robin Sample', 'Kim Example'])
  })

  it('passes description and target date through', async () => {
    const wrapper = await mountForm()

    await wrapper.find('[data-testid="goal-title-input"]').setValue('Read more')
    await wrapper.find('[data-testid="goal-description-input"]').setValue('Ten pages a day')
    await wrapper.find('[data-testid="goal-target-date-input"]').setValue('2026-09-30')
    await wrapper.find('form').trigger('submit')

    expect(lastSubmit(wrapper)).toMatchObject({
      title: 'Read more',
      description: 'Ten pages a day',
      targetDate: '2026-09-30',
    })
  })

  it('shows server-side field errors next to the field', async () => {
    const wrapper = await mountForm({
      error: failure({ fieldErrors: { Title: ['A title is required.'] } }),
    })

    // The API's wording is the wording the user sees — no second copy of the
    // rules in the browser to drift out of sync.
    expect(wrapper.text()).toContain('A title is required.')
  })

  it('matches field errors regardless of casing', async () => {
    const wrapper = await mountForm({
      error: failure({ fieldErrors: { participants: ['Participant names must not be empty.'] } }),
    })

    expect(wrapper.text()).toContain('Participant names must not be empty.')
  })

  it('shows a non-field error as an alert above the button', async () => {
    const wrapper = await mountForm({
      error: failure({ kind: 'network', message: 'We could not reach the server.', status: null }),
    })

    const alert = wrapper.find('[data-testid="goal-create-error"]')
    expect(alert.exists()).toBe(true)
    expect(alert.attributes('role')).toBe('alert')
    expect(alert.text()).toContain('We could not reach the server.')
  })

  it('blocks a second submit while one is in flight', async () => {
    const wrapper = await mountForm({ submitting: true })

    await wrapper.find('[data-testid="goal-title-input"]').setValue('Walk every day')
    await wrapper.find('form').trigger('submit')

    // Double-submitting a create would produce two goals.
    expect(wrapper.emitted('submit')).toBeUndefined()
  })

  it('clears the fields when the page resets it after a success', async () => {
    const wrapper = await mountForm()
    const title = wrapper.find('[data-testid="goal-title-input"]')

    await title.setValue('Walk every day')
    ;(wrapper.vm as unknown as { reset: () => void }).reset()
    await wrapper.vm.$nextTick()

    expect((title.element as HTMLInputElement).value).toBe('')
  })
})
