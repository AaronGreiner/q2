import { describe, expect, it } from 'vitest'
import type { ApiFailure } from '~/api/errors'
import { de, en } from '~/i18n/messages'
import { fieldErrorText, wordFieldError } from '~/utils/fieldErrors'

/**
 * A field error is worded from the code the server sends, in the reader's
 * language — never from the server's English message (#80).
 */
function failure(overrides: Partial<ApiFailure> = {}): ApiFailure {
  return {
    kind: 'validation',
    status: 400,
    fieldErrors: {},
    fieldCodes: {},
    reason: null,
    traceId: null,
    errorId: null,
    isExpected: true,
    ...overrides,
  }
}

describe('fieldErrorText', () => {
  const registration = failure({
    fieldErrors: {
      Name: ['A name is required.'],
      Password: ['A password must be at least 10 characters long.'],
    },
    fieldCodes: {
      Name: [{ code: 'required' }],
      Password: [{ code: 'tooShort', min: 10 }],
    },
  })

  it('words a field from its code in the reader\'s language', () => {
    expect(fieldErrorText(registration, 'name', de)).toBe('Bitte ausfüllen.')
    expect(fieldErrorText(registration, 'password', de)).toBe('Mindestens 10 Zeichen.')
    expect(fieldErrorText(registration, 'password', en)).toBe('At least 10 characters.')
  })

  it('never shows the server\'s English, even for a field without a code', () => {
    const uncoded = failure({ fieldErrors: { Email: ['Username \'x\' is already taken.'] } })

    expect(fieldErrorText(uncoded, 'email', de)).toBe(de.fieldErrors.invalid)
  })

  it('says nothing about a field the server did not name, or without a failure', () => {
    expect(fieldErrorText(registration, 'email', de)).toBeUndefined()
    expect(fieldErrorText(null, 'name', de)).toBeUndefined()
    expect(fieldErrorText(failure({ kind: 'network' }), 'name', de)).toBeUndefined()
  })
})

describe('wordFieldError', () => {
  it.each([
    [{ code: 'required' }, 'Bitte ausfüllen.'],
    [{ code: 'tooShort', min: 3 }, 'Mindestens 3 Zeichen.'],
    [{ code: 'tooLong', max: 120 }, 'Höchstens 120 Zeichen.'],
    [{ code: 'outOfRange', min: 1, max: 14 }, 'Bitte eine Zahl von 1 bis 14.'],
    [{ code: 'tooMany', max: 20 }, 'Höchstens 20.'],
    [{ code: 'tooFarAhead', max: 365 }, 'Höchstens 365 Tage im Voraus.'],
    [{ code: 'email' }, de.fieldErrors.email],
    [{ code: 'taken' }, de.fieldErrors.taken],
    [{ code: 'unknownOption' }, de.fieldErrors.unknownOption],
    [{ code: 'resetLink' }, de.fieldErrors.resetLink],
    [{ code: 'noChecker' }, de.fieldErrors.noChecker],
    [{ code: 'noWeekday' }, de.fieldErrors.noWeekday],
    [{ code: 'pastDay' }, de.fieldErrors.pastDay],
    [{ code: 'mustRepeat' }, de.fieldErrors.mustRepeat],
    [{ code: 'invalid' }, de.fieldErrors.invalid],
  ])('words %o', (code, expected) => {
    expect(wordFieldError(code, de)).toBe(expected)
  })

  it('falls back to the generic sentence for a code it does not know, or one missing its number', () => {
    expect(wordFieldError({ code: 'somethingNew' }, de)).toBe(de.fieldErrors.invalid)
    expect(wordFieldError({ code: 'tooShort' }, de)).toBe(de.fieldErrors.invalid)
    expect(wordFieldError({ code: 'outOfRange', min: 1 }, en)).toBe(en.fieldErrors.invalid)
  })
})
