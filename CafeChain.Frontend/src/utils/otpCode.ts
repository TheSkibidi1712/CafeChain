export const OPERATIONAL_OTP_ALPHABET = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789'
export const OPERATIONAL_OTP_LENGTH = 6
export const OPERATIONAL_OTP_INPUT_ERROR =
  'OTP chỉ gồm 6 chữ cái in hoa và số; không dùng O/0/I/1 hoặc ký tự đặc biệt.'

const allowedCharacters = new Set(OPERATIONAL_OTP_ALPHABET.split(''))

export interface SanitizedOtpInput {
  value: string
  rejected: boolean
}

export function sanitizeOperationalOtpInput(rawValue: string): SanitizedOtpInput {
  const upper = rawValue.toUpperCase()
  let rejected = false
  let value = ''

  for (const character of upper) {
    if (!allowedCharacters.has(character)) {
      rejected = true
      continue
    }
    if (value.length < OPERATIONAL_OTP_LENGTH) value += character
    else rejected = true
  }

  return { value, rejected }
}

export function isValidOperationalOtp(value: string): boolean {
  return value.length === OPERATIONAL_OTP_LENGTH
    && [...value].every((character) => allowedCharacters.has(character))
}
