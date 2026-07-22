export type CustomerDisplayState =
  | 'idle'
  | 'cart'
  | 'vietqr'
  | 'success'
  | 'cancelled'
  | 'expired'
  | 'offline'

export interface CustomerDisplayItem {
  name: string
  quantity: number
  lineTotal: number
  optionSummary?: string
}

export interface CustomerDisplaySnapshot {
  schemaVersion: 1
  messageId: string
  sessionId: string
  sequence: number
  validUntil: number
  state: CustomerDisplayState
  storeId: number | null
  workShiftId: number | null
  orderType: 'dine-in' | 'take-away'
  items: CustomerDisplayItem[]
  totalAmount: number
  orderId?: number
  qrCode?: string
  expiresAt?: number
  message?: string
  updatedAt: number
}

export type CustomerDisplayMessage = CustomerDisplaySnapshot

interface CustomerDisplaySubscriptionOptions {
  expectedWorkShiftId: number | null
  initialSnapshot: CustomerDisplaySnapshot | null
}

export const CUSTOMER_DISPLAY_SCHEMA_VERSION = 1 as const
export const CUSTOMER_DISPLAY_SNAPSHOT_TTL_MS = 5 * 60 * 1000

const CHANNEL_NAME = 'cafechain-pos-customer-display-v1'
const SNAPSHOT_STORAGE_KEY = 'cafechain_pos_customer_display_snapshot_v1'
const SEQUENCE_STORAGE_PREFIX = 'cafechain_pos_customer_display_sequence_v1:'
const ALLOWED_STATES = new Set<CustomerDisplayState>([
  'idle',
  'cart',
  'vietqr',
  'success',
  'cancelled',
  'expired',
  'offline',
])
const inMemorySequence = new Map<string, number>()

const getSessionId = (storeId: number | null, workShiftId: number | null): string =>
  `store:${storeId ?? 'none'}:shift:${workShiftId ?? 'none'}`

const readStoredSequence = (sessionId: string): number => {
  try {
    const stored = Number(sessionStorage.getItem(`${SEQUENCE_STORAGE_PREFIX}${sessionId}`))
    return Number.isSafeInteger(stored) && stored > 0 ? stored : 0
  } catch {
    return 0
  }
}

const nextSequence = (sessionId: string): number => {
  const sequence = Math.max(
    Date.now(),
    (inMemorySequence.get(sessionId) ?? 0) + 1,
    readStoredSequence(sessionId) + 1,
  )
  inMemorySequence.set(sessionId, sequence)
  try {
    sessionStorage.setItem(`${SEQUENCE_STORAGE_PREFIX}${sessionId}`, String(sequence))
  } catch {
    // In-memory monotonic ordering still protects the current POS tab.
  }
  return sequence
}

const isValidItem = (value: unknown): value is CustomerDisplayItem => {
  if (!value || typeof value !== 'object') return false
  const candidate = value as Partial<CustomerDisplayItem>
  return typeof candidate.name === 'string'
    && typeof candidate.quantity === 'number'
    && typeof candidate.lineTotal === 'number'
    && (candidate.optionSummary === undefined || typeof candidate.optionSummary === 'string')
}

const isValidSnapshot = (value: unknown): value is CustomerDisplaySnapshot => {
  if (!value || typeof value !== 'object') return false
  const candidate = value as Partial<CustomerDisplaySnapshot>
  return candidate.schemaVersion === CUSTOMER_DISPLAY_SCHEMA_VERSION
    && typeof candidate.messageId === 'string'
    && candidate.messageId.length > 0
    && typeof candidate.sessionId === 'string'
    && candidate.sessionId.length > 0
    && typeof candidate.sequence === 'number'
    && Number.isSafeInteger(candidate.sequence)
    && typeof candidate.validUntil === 'number'
    && typeof candidate.state === 'string'
    && ALLOWED_STATES.has(candidate.state as CustomerDisplayState)
    && Array.isArray(candidate.items)
    && candidate.items.every(isValidItem)
    && typeof candidate.totalAmount === 'number'
    && typeof candidate.updatedAt === 'number'
}

const belongsToWorkShift = (
  snapshot: CustomerDisplaySnapshot,
  expectedWorkShiftId: number | null,
): boolean => expectedWorkShiftId === null || snapshot.workShiftId === expectedWorkShiftId

export const isFreshCustomerDisplaySnapshot = (
  snapshot: CustomerDisplaySnapshot,
  expectedWorkShiftId: number | null,
  now = Date.now(),
): boolean => snapshot.validUntil >= now && belongsToWorkShift(snapshot, expectedWorkShiftId)

export const isNewerCustomerDisplaySnapshot = (
  current: CustomerDisplaySnapshot | null,
  candidate: CustomerDisplaySnapshot,
): boolean => !current
  || (candidate.sessionId !== current.sessionId && candidate.updatedAt > current.updatedAt)
  || (candidate.sessionId === current.sessionId && candidate.sequence > current.sequence)

export function publishCustomerDisplay(
  snapshot: Omit<
    CustomerDisplaySnapshot,
    'schemaVersion' | 'messageId' | 'sessionId' | 'sequence' | 'validUntil' | 'updatedAt'
  >,
): CustomerDisplaySnapshot {
  const now = Date.now()
  const sessionId = getSessionId(snapshot.storeId, snapshot.workShiftId)
  const sequence = nextSequence(sessionId)
  const message: CustomerDisplaySnapshot = {
    ...snapshot,
    schemaVersion: CUSTOMER_DISPLAY_SCHEMA_VERSION,
    messageId: `${sessionId}:${sequence}`,
    sessionId,
    sequence,
    updatedAt: now,
    validUntil: now + CUSTOMER_DISPLAY_SNAPSHOT_TTL_MS,
  }

  try {
    localStorage.setItem(SNAPSHOT_STORAGE_KEY, JSON.stringify(message))
  } catch {
    // BroadcastChannel may still deliver when storage is unavailable or full.
  }

  if ('BroadcastChannel' in window) {
    try {
      const channel = new BroadcastChannel(CHANNEL_NAME)
      channel.postMessage(message)
      channel.close()
    } catch {
      // localStorage remains the cross-window fallback.
    }
  }

  return message
}

export function readCustomerDisplaySnapshot(
  expectedWorkShiftId: number | null,
): CustomerDisplaySnapshot | null {
  if (expectedWorkShiftId === null) return null
  try {
    const raw = localStorage.getItem(SNAPSHOT_STORAGE_KEY)
    if (!raw) return null
    const parsed: unknown = JSON.parse(raw)
    if (!isValidSnapshot(parsed) || !isFreshCustomerDisplaySnapshot(parsed, expectedWorkShiftId)) return null
    return parsed
  } catch {
    return null
  }
}

export function subscribeCustomerDisplay(
  listener: (snapshot: CustomerDisplaySnapshot) => void,
  options: CustomerDisplaySubscriptionOptions,
): () => void {
  let latestSnapshot = options.initialSnapshot

  const accept = (value: unknown) => {
    if (!isValidSnapshot(value)
      || !isFreshCustomerDisplaySnapshot(value, options.expectedWorkShiftId)
      || !isNewerCustomerDisplaySnapshot(latestSnapshot, value)) {
      return
    }
    latestSnapshot = value
    listener(value)
  }

  const handleStorage = (event: StorageEvent) => {
    if (event.key !== SNAPSHOT_STORAGE_KEY || !event.newValue) return
    try {
      accept(JSON.parse(event.newValue) as unknown)
    } catch {
      // Ignore malformed data from another tab; the last valid snapshot stays visible.
    }
  }

  window.addEventListener('storage', handleStorage)
  let channel: BroadcastChannel | null = null
  if ('BroadcastChannel' in window) {
    try {
      channel = new BroadcastChannel(CHANNEL_NAME)
      channel.onmessage = (event: MessageEvent<unknown>) => accept(event.data)
    } catch {
      channel = null
    }
  }

  return () => {
    window.removeEventListener('storage', handleStorage)
    channel?.close()
  }
}
