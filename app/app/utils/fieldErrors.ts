import type { ApiFailure, FieldErrorCode } from '~/api/errors'
import type { Messages } from '~/i18n/messages'

/**
 * What to show under one field of a form, in the language the person reads q2
 * in (#80, docs/adr/0042-field-errors-as-codes.md).
 *
 * Worded from the code the server sends beside its message, never from the
 * message itself: that one is English, and somebody who chose German — the
 * default — would otherwise read "A password must be at least 10 characters
 * long." under a German label. A field the server names without a code, or
 * with one this client does not know yet, gets the generic sentence rather
 * than the English one.
 *
 * Field names are matched case-insensitively: the server names a field after
 * its C# property (`Password`), a form after its input (`password`).
 */
export function fieldErrorText(
  failure: ApiFailure | null | undefined,
  field: string,
  t: Messages,
): string | undefined {
  if (!failure) return undefined

  const wanted = field.toLowerCase()
  const named = (record: Record<string, unknown>) => Object.keys(record).find(key => key.toLowerCase() === wanted)

  const codeKey = named(failure.fieldCodes ?? {})
  const code = codeKey ? failure.fieldCodes?.[codeKey]?.[0] : undefined
  if (code) return wordFieldError(code, t)

  return named(failure.fieldErrors) ? t.fieldErrors.invalid : undefined
}

/** One code as a sentence; a code without the number its sentence needs is worded generically. */
export function wordFieldError(error: FieldErrorCode, t: Messages): string {
  const words = t.fieldErrors
  const { min, max } = error

  switch (error.code) {
    case 'required': return words.required
    case 'tooShort': return min === undefined ? words.invalid : words.tooShort(min)
    case 'tooLong': return max === undefined ? words.invalid : words.tooLong(max)
    case 'outOfRange': return min === undefined || max === undefined ? words.invalid : words.outOfRange(min, max)
    case 'tooMany': return max === undefined ? words.invalid : words.tooMany(max)
    case 'tooFarAhead': return max === undefined ? words.invalid : words.tooFarAhead(max)
    case 'email': return words.email
    case 'taken': return words.taken
    case 'unknownOption': return words.unknownOption
    case 'resetLink': return words.resetLink
    case 'noChecker': return words.noChecker
    case 'noWeekday': return words.noWeekday
    case 'pastDay': return words.pastDay
    case 'mustRepeat': return words.mustRepeat
    default: return words.invalid
  }
}
