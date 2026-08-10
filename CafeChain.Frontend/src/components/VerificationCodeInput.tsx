import { useRef } from 'react'
import { OPERATIONAL_OTP_ALPHABET } from '../utils/otpCode'

interface VerificationCodeInputProps {
  value: string
  onChange: (value: string) => void
  mode: 'otp' | 'pin'
  disabled?: boolean
  error?: boolean
  label: string
  onRejected?: () => void
}

export default function VerificationCodeInput({
  value,
  onChange,
  mode,
  disabled = false,
  error = false,
  label,
  onRejected,
}: VerificationCodeInputProps) {
  const refs = useRef<Array<HTMLInputElement | null>>([])
  const alphabet = mode === 'pin' ? '0123456789' : OPERATIONAL_OTP_ALPHABET
  const allowed = new Set(alphabet)
  const cells = Array.from({ length: 6 }, (_, index) => value[index] ?? '')

  const normalize = (raw: string) => {
    const upper = raw.toUpperCase()
    const accepted = [...upper].filter((character) => allowed.has(character)).slice(0, 6)
    if (accepted.length !== [...upper].length) onRejected?.()
    return accepted
  }

  const writeFrom = (index: number, raw: string) => {
    const incoming = normalize(raw)
    if (incoming.length === 0) return
    const next = [...cells]
    incoming.forEach((character, offset) => {
      if (index + offset < 6) next[index + offset] = character
    })
    onChange(next.join('').slice(0, 6))
    refs.current[Math.min(5, index + incoming.length)]?.focus()
  }

  return (
    <div className="flex max-w-full gap-1.5 sm:gap-2" role="group" aria-label={label}>
      {cells.map((cell, index) => (
        <input
          key={index}
          ref={(element) => { refs.current[index] = element }}
          type={mode === 'pin' ? 'password' : 'text'}
          inputMode={mode === 'pin' ? 'numeric' : 'text'}
          autoComplete={index === 0 && mode === 'otp' ? 'one-time-code' : 'off'}
          value={cell}
          maxLength={1}
          disabled={disabled}
          aria-label={`${label}, ô ${index + 1}`}
          aria-invalid={error}
          onFocus={(event) => event.currentTarget.select()}
          onChange={(event) => {
            const raw = event.target.value
            if (!raw) {
              const next = [...cells]
              next[index] = ''
              onChange(next.join(''))
              return
            }
            writeFrom(index, raw)
          }}
          onKeyDown={(event) => {
            if (event.key === 'Backspace' && !cells[index] && index > 0) {
              event.preventDefault()
              const next = [...cells]
              next[index - 1] = ''
              onChange(next.join(''))
              refs.current[index - 1]?.focus()
            } else if (event.key === 'ArrowLeft' && index > 0) {
              refs.current[index - 1]?.focus()
            } else if (event.key === 'ArrowRight' && index < 5) {
              refs.current[index + 1]?.focus()
            }
          }}
          onPaste={(event) => {
            event.preventDefault()
            writeFrom(index, event.clipboardData.getData('text'))
          }}
          className={`aspect-square min-w-0 flex-1 rounded-lg border bg-white text-center text-base font-extrabold uppercase outline-none transition sm:max-w-12 ${
            error ? 'border-red-400 focus:border-red-500 focus:ring-2 focus:ring-red-100' : 'border-border focus:border-brand-orange focus:ring-2 focus:ring-brand-orange/20'
          } disabled:bg-gray-100 disabled:text-gray-400`}
        />
      ))}
    </div>
  )
}
