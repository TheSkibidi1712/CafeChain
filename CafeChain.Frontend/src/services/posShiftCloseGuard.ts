export type ActivePaymentCloseGuardStatus = 'collecting' | 'awaiting-vietqr'

export interface ActivePaymentCloseGuard {
  status: ActivePaymentCloseGuardStatus
  shiftId: number
  staffId: number
  storeId: number
  orderId?: number
  totalAmount: number
  pendingCashAmount: number
  vietQrAmount: number
  expiresAt?: number
  updatedAt: number
}

export interface ActivePaymentCloseGuardContext {
  shiftId: number
  staffId: number
  storeId: number
}

const ACTIVE_PAYMENT_CLOSE_GUARD_KEY = 'CafeChain_POS_ActivePaymentCloseGuard'
const ACTIVE_PAYMENT_CLOSE_GUARD_MAX_AGE_MS = 30 * 60 * 1000

export const ACTIVE_PAYMENT_CLOSE_GUARD_CHANGED = 'CafeChain_POS_ActivePaymentCloseGuardChanged'

const isFiniteNumber = (value: unknown): value is number =>
  typeof value === 'number' && Number.isFinite(value)

const dispatchGuardChanged = () => {
  window.dispatchEvent(new Event(ACTIVE_PAYMENT_CLOSE_GUARD_CHANGED))
}

function parseGuard(value: unknown): ActivePaymentCloseGuard | null {
  if (!value || typeof value !== 'object') return null

  const record = value as Record<string, unknown>
  const status = record.status

  if (status !== 'collecting' && status !== 'awaiting-vietqr') return null
  if (!isFiniteNumber(record.shiftId) || !isFiniteNumber(record.staffId) || !isFiniteNumber(record.storeId)) return null
  if (!isFiniteNumber(record.totalAmount) || !isFiniteNumber(record.pendingCashAmount) || !isFiniteNumber(record.vietQrAmount)) return null
  if (!isFiniteNumber(record.updatedAt)) return null

  if (record.orderId !== undefined && !isFiniteNumber(record.orderId)) return null
  if (record.expiresAt !== undefined && !isFiniteNumber(record.expiresAt)) return null

  return {
    status,
    shiftId: record.shiftId,
    staffId: record.staffId,
    storeId: record.storeId,
    orderId: record.orderId,
    totalAmount: record.totalAmount,
    pendingCashAmount: record.pendingCashAmount,
    vietQrAmount: record.vietQrAmount,
    expiresAt: record.expiresAt,
    updatedAt: record.updatedAt,
  }
}

export function writeActivePaymentCloseGuard(
  guard: Omit<ActivePaymentCloseGuard, 'updatedAt'>
): void {
  sessionStorage.setItem(ACTIVE_PAYMENT_CLOSE_GUARD_KEY, JSON.stringify({
    ...guard,
    updatedAt: Date.now(),
  }))
  dispatchGuardChanged()
}

export function clearActivePaymentCloseGuard(): void {
  sessionStorage.removeItem(ACTIVE_PAYMENT_CLOSE_GUARD_KEY)
  dispatchGuardChanged()
}

export function readActivePaymentCloseGuard(): ActivePaymentCloseGuard | null {
  const raw = sessionStorage.getItem(ACTIVE_PAYMENT_CLOSE_GUARD_KEY)
  if (!raw) return null

  try {
    const guard = parseGuard(JSON.parse(raw) as unknown)
    const now = Date.now()

    if (!guard) {
      clearActivePaymentCloseGuard()
      return null
    }

    if (guard.expiresAt !== undefined && guard.expiresAt <= now) {
      clearActivePaymentCloseGuard()
      return null
    }

    if (now - guard.updatedAt > ACTIVE_PAYMENT_CLOSE_GUARD_MAX_AGE_MS) {
      clearActivePaymentCloseGuard()
      return null
    }

    return guard
  } catch {
    clearActivePaymentCloseGuard()
    return null
  }
}

export function getMatchingActivePaymentCloseGuard(
  context: ActivePaymentCloseGuardContext
): ActivePaymentCloseGuard | null {
  const guard = readActivePaymentCloseGuard()
  if (!guard) return null

  return guard.shiftId === context.shiftId &&
    guard.staffId === context.staffId &&
    guard.storeId === context.storeId
    ? guard
    : null
}
