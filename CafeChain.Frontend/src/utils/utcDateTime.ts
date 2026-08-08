const HAS_EXPLICIT_OFFSET = /(Z|[+-]\d{2}:?\d{2})$/i

/**
 * API timestamps are UTC. Older servers serialized SQL datetime2 without an
 * offset, so keep this parser defensive while the wire contract is upgraded.
 */
export const normalizeUtcInstant = (value?: string | null): string | null => {
  const timestamp = value?.trim()
  if (!timestamp) return null
  return HAS_EXPLICIT_OFFSET.test(timestamp) ? timestamp : `${timestamp}Z`
}

export const parseUtcInstantMs = (value?: string | null): number => {
  const normalized = normalizeUtcInstant(value)
  return normalized ? Date.parse(normalized) : Number.NaN
}

export const parseUtcInstant = (value?: string | null): Date | null => {
  const milliseconds = parseUtcInstantMs(value)
  return Number.isFinite(milliseconds) ? new Date(milliseconds) : null
}
