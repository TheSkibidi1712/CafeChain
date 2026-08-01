/**
 * Safe mappers for offline/local queue orders in Order History.
 * One corrupt IndexedDB record must never crash the whole /history page.
 *
 * Money field semantics (view model):
 * - LocalHistoryRow.total  → order total (tổng tiền đơn), never cash tendered/change
 * - HistoryPaymentLineSafe.amount → amount of one payment line only
 * - receivedAmount / changeAmount → cash tender meta only; never used as order total
 */

import type {
  CartQueueItemSnapshot,
  CartQueuePaymentSnapshot,
  CartSyncQueueItem,
  SyncStatus,
} from '../db/CafeChainPOSDB'
import type { IceLevelPercent } from './iceLevel'

export const ORDER_HISTORY_LABELS = {
  paid: 'Đã thanh toán',
  pendingSync: 'Chờ đồng bộ',
  syncing: 'Đang đồng bộ',
  syncFailed: 'Đồng bộ lỗi',
  noPrintData: 'Chưa có dữ liệu in',
  noMoneyData: 'Chưa có dữ liệu',
  noItems: 'Không có món',
  corruptLocal: 'Đơn cục bộ không đọc được',
  cash: 'Tiền mặt',
  banking: 'Chuyển khoản',
} as const

export type HistoryPaymentLineSafe = {
  method: string
  /**
   * Payment-line amount only (C).
   * null = no trustworthy line amount (do not invent 0).
   */
  amount: number | null
  status: string
  paidAt?: string | null
  transactionCode?: string | null
  /** Cash received (tendered) — not order total */
  receivedAmount?: number | null
  /** Cash change — not order total / not payment amount */
  changeAmount?: number | null
}

export type HistoryDetailLineSafe = {
  drinkName: string
  sizeName?: string | null
  quantity: number
  unitPrice: number
  lineTotal: number
  iceLevelPercent?: IceLevelPercent | null
  note?: string | null
  toppings: string[]
}

export type LocalHistoryRow = {
  key: string
  source: 'local'
  clientOrderId: string
  code: string
  soldAt: string
  /**
   * Order total (A) — tổng tiền đơn hàng.
   * null when not trustworthy.
   */
  total: number | null
  paymentSummary: string
  orderState: string
  syncState: string
  receiptState: string
  drinkLabelState: string
  workShiftId?: number | null
  staffName: string
  storeName: string
  orderType?: string | null
  retryCount?: number
  lastError?: string
  items: HistoryDetailLineSafe[]
  payments: HistoryPaymentLineSafe[]
  /** true when record was incomplete/corrupt but rendered as safe card */
  isDegraded?: boolean
  degradeReason?: string
}

/** Loose / legacy local queue shapes (IndexedDB may predate current schema). */
export type LooseLocalOrder = Partial<CartSyncQueueItem> & {
  queueId?: number
  /** Legacy single-payment alias */
  payment?: Partial<CartQueuePaymentSnapshot> | null
  /** Optional multi-payment list (split / future) */
  payments?: Array<Partial<CartQueuePaymentSnapshot> | Record<string, unknown> | null | undefined> | null
  cart?: CartQueueItemSnapshot[] | null
  /** Legacy order total alias */
  total?: number
}

const isFiniteNumber = (value: unknown): value is number =>
  typeof value === 'number' && Number.isFinite(value)

/** Read a finite money value; never invent. */
export const pickMoney = (...candidates: unknown[]): number | null => {
  for (const c of candidates) {
    if (isFiniteNumber(c)) return c
    if (typeof c === 'string' && c.trim() !== '') {
      const n = Number(c)
      if (Number.isFinite(n)) return n
    }
  }
  return null
}

export const formatHistoryMoney = (amount: number | null | undefined): string => {
  if (amount === null || amount === undefined || !Number.isFinite(amount)) {
    return ORDER_HISTORY_LABELS.noMoneyData
  }
  return `${new Intl.NumberFormat('vi-VN').format(Math.max(0, amount))}đ`
}

const getLocalSyncLabel = (status: SyncStatus | string | undefined): string => {
  if (status === 'Syncing') return ORDER_HISTORY_LABELS.syncing
  if (status === 'Failed') return ORDER_HISTORY_LABELS.syncFailed
  return ORDER_HISTORY_LABELS.pendingSync
}

const normalizePaymentMethod = (method?: string | null, paymentMethod?: string | null): string => {
  const raw = (method ?? paymentMethod ?? '').trim()
  const lower = raw.toLowerCase()
  if (!raw) return ORDER_HISTORY_LABELS.cash
  if (lower.includes('cash') || lower.includes('tiền')) return ORDER_HISTORY_LABELS.cash
  if (lower.includes('bank') || lower.includes('qr') || lower.includes('transfer')) {
    return ORDER_HISTORY_LABELS.banking
  }
  return raw
}

const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null && !Array.isArray(value)

const asLooseOrder = (value: unknown): LooseLocalOrder | null => {
  if (!isRecord(value)) return null
  return value as LooseLocalOrder
}

/** Safe ISO soldAt — never throws on invalid dates. */
export const normalizeSoldAt = (order: LooseLocalOrder): string => {
  if (typeof order.soldAt === 'string' && order.soldAt.trim()) {
    const d = new Date(order.soldAt)
    if (!Number.isNaN(d.getTime())) return order.soldAt
  }
  if (order.createdAt != null) {
    const n = typeof order.createdAt === 'number' ? order.createdAt : Number(order.createdAt)
    if (Number.isFinite(n)) {
      const d = new Date(n)
      if (!Number.isNaN(d.getTime())) return d.toISOString()
    }
  }
  return new Date(0).toISOString()
}

/**
 * Collect raw payment source objects without assuming shape.
 * Does not invent payments from order total alone (that is done in mapLocalPayments).
 */
export const collectPaymentSources = (
  order: LooseLocalOrder,
): Array<Partial<CartQueuePaymentSnapshot> & Record<string, unknown>> => {
  const sources: Array<Partial<CartQueuePaymentSnapshot> & Record<string, unknown>> = []

  const snapshot = order.paymentSnapshot ?? order.payment ?? null
  if (isRecord(snapshot)) {
    sources.push(snapshot as Partial<CartQueuePaymentSnapshot> & Record<string, unknown>)
  }

  if (Array.isArray(order.payments)) {
    for (const item of order.payments) {
      if (isRecord(item)) {
        sources.push(item as Partial<CartQueuePaymentSnapshot> & Record<string, unknown>)
      }
    }
  }

  return sources
}

/**
 * Order total (A) precedence:
 * 1. order.totalAmount
 * 2. legacy order.total
 * 3. single payment line amount ONLY when no order total and exactly one payment with amount
 *    (full-order single-payment legacy semantics)
 * 4. otherwise null — never use first of split, receivedAmount, changeAmount, or sum of payments
 */
export const resolveOrderTotal = (order: LooseLocalOrder): number | null => {
  const fromOrder = pickMoney(order.totalAmount, order.total)
  if (fromOrder !== null) return fromOrder

  const sources = collectPaymentSources(order)
  if (sources.length !== 1) return null

  // Single payment only — never use received/change as total
  return pickMoney(sources[0]?.amount)
}

/**
 * Resolve payment lines.
 * Line `amount` is payment-line money only (C).
 * Does not fall back a line amount to order total when multiple payments exist.
 * Single payment missing amount may use order total (full-order payment semantics).
 * Zero sources → one synthetic line with amount = order total if known (display only).
 */
export const mapLocalPayments = (order: LooseLocalOrder): HistoryPaymentLineSafe[] => {
  const sources = collectPaymentSources(order)
  const orderTotal = pickMoney(order.totalAmount, order.total)

  if (sources.length === 0) {
    return [{
      method: normalizePaymentMethod(
        typeof order.paymentMethod === 'string' ? order.paymentMethod : null,
      ),
      amount: orderTotal,
      status: ORDER_HISTORY_LABELS.paid,
      paidAt: typeof order.soldAt === 'string' ? order.soldAt : null,
      receivedAmount: null,
      changeAmount: null,
    }]
  }

  return sources.map((pay) => {
    // Payment-line amount: never use receivedAmount / changeAmount
    let amount = pickMoney(pay.amount)
    // Single full-order payment missing amount → order total is equivalent
    if (amount === null && sources.length === 1) {
      amount = orderTotal
    }
    // Multi-payment missing a line amount → null (do not borrow order total onto one line)

    return {
      method: normalizePaymentMethod(
        typeof pay.method === 'string' ? pay.method : null,
        typeof order.paymentMethod === 'string' ? order.paymentMethod : null,
      ),
      amount,
      status: ORDER_HISTORY_LABELS.paid,
      paidAt: (typeof pay.capturedAt === 'string' ? pay.capturedAt : null)
        ?? (typeof order.soldAt === 'string' ? order.soldAt : null),
      receivedAmount: pickMoney(pay.receivedAmount),
      changeAmount: pickMoney(pay.changeAmount),
    }
  })
}

const mapItems = (order: LooseLocalOrder): HistoryDetailLineSafe[] => {
  const cartSnapshot = Array.isArray(order.cartSnapshot)
    ? order.cartSnapshot
    : Array.isArray(order.cart)
      ? order.cart
      : []
  const fallbackItems = Array.isArray(order.items) ? order.items : []

  if (cartSnapshot.length > 0) {
    return cartSnapshot.map((item, index) => {
      const raw: Record<string, unknown> = isRecord(item) ? item : {}
      const quantity = pickMoney(raw.quantity) ?? 0
      const unitPrice = pickMoney(raw.unitPrice) ?? 0
      const toppingsRaw = Array.isArray(raw.toppings) ? raw.toppings : []
      const toppingNames = toppingsRaw
        .map((t: unknown) => {
          if (!isRecord(t)) return 'Topping'
          if (typeof t.name === 'string' && t.name.trim()) return t.name
          if (t.toppingId != null) return `Topping #${String(t.toppingId)}`
          return 'Topping'
        })
        .filter((name: string): name is string => Boolean(name))
      return {
        drinkName: (typeof raw.name === 'string' && raw.name.trim()) ? raw.name.trim() : `Món #${index + 1}`,
        sizeName: typeof raw.sizeName === 'string' ? raw.sizeName : null,
        quantity,
        unitPrice,
        lineTotal: unitPrice * quantity,
        iceLevelPercent: raw.iceLevelPercent === 0 || raw.iceLevelPercent === 50 || raw.iceLevelPercent === 100
          ? raw.iceLevelPercent
          : null,
        note: typeof raw.note === 'string' ? raw.note : undefined,
        toppings: toppingNames,
      }
    })
  }

  return fallbackItems.map((item, index) => {
    const raw: Record<string, unknown> = isRecord(item) ? item : {}
    const quantity = pickMoney(raw.quantity) ?? 0
    const unitPrice = pickMoney(raw.unitPrice) ?? 0
    const toppingsRaw = Array.isArray(raw.toppings) ? raw.toppings : []
    const toppingNames = toppingsRaw.map((t: unknown) => {
      if (!isRecord(t)) return 'Topping'
      if (t.toppingId != null) return `Topping #${String(t.toppingId)}`
      return 'Topping'
    })
    return {
      drinkName: (typeof raw.name === 'string' && raw.name.trim()) ? raw.name.trim() : `Món #${index + 1}`,
      sizeName: null,
      quantity,
      unitPrice,
      lineTotal: unitPrice * quantity,
      iceLevelPercent: raw.iceLevelPercent === 0 || raw.iceLevelPercent === 50 || raw.iceLevelPercent === 100
        ? raw.iceLevelPercent
        : null,
      note: typeof raw.note === 'string' ? raw.note : undefined,
      toppings: toppingNames,
    }
  })
}

const resolveClientOrderId = (order: LooseLocalOrder): string => {
  if (typeof order.clientOrderId === 'string' && order.clientOrderId.trim()) {
    return order.clientOrderId.trim()
  }
  if (order.queueId != null && Number.isFinite(Number(order.queueId))) {
    return `queue-${order.queueId}`
  }
  // Stable render key only — not a business/idempotency id
  return 'local-offline-order'
}

const buildDegradedRow = (clientOrderId: string, reason: string): LocalHistoryRow => ({
  key: `local-corrupt-${clientOrderId}`,
  source: 'local',
  clientOrderId,
  code: ORDER_HISTORY_LABELS.corruptLocal,
  soldAt: new Date(0).toISOString(),
  total: null,
  paymentSummary: ORDER_HISTORY_LABELS.noMoneyData,
  orderState: ORDER_HISTORY_LABELS.syncFailed,
  syncState: ORDER_HISTORY_LABELS.syncFailed,
  receiptState: ORDER_HISTORY_LABELS.noPrintData,
  drinkLabelState: ORDER_HISTORY_LABELS.noPrintData,
  staffName: '—',
  storeName: '—',
  items: [],
  payments: [],
  isDegraded: true,
  degradeReason: reason,
})

/**
 * Map one local queue record safely.
 * Accepts unknown; never mutates input; never throws.
 */
export const mapLocalOrderSafe = (input: unknown): LocalHistoryRow => {
  try {
    const order = asLooseOrder(input)
    if (!order) {
      return buildDegradedRow('unknown', 'Bản ghi cục bộ rỗng hoặc không hợp lệ.')
    }

    const clientOrderId = resolveClientOrderId(order)
    const soldAt = normalizeSoldAt(order)
    const items = mapItems(order)
    const payments = mapLocalPayments(order)
    const total = resolveOrderTotal(order)
    const syncState = getLocalSyncLabel(
      typeof order.syncStatus === 'string' ? order.syncStatus : undefined,
    )
    const hasPaymentShape = Boolean(
      order.paymentSnapshot || order.payment || (Array.isArray(order.payments) && order.payments.some(isRecord)),
    )
    const degraded = !hasPaymentShape
    const missingItems = items.length === 0

    return {
      key: `local-${order.queueId ?? clientOrderId}`,
      source: 'local',
      clientOrderId,
      code: clientOrderId === 'local-offline-order'
        ? ORDER_HISTORY_LABELS.corruptLocal
        : `Tạm ${clientOrderId.slice(0, 8)}`,
      soldAt,
      total,
      paymentSummary: payments.map((p) => p.method).filter(Boolean).join(' + ')
        || normalizePaymentMethod(typeof order.paymentMethod === 'string' ? order.paymentMethod : null),
      orderState: syncState,
      syncState,
      receiptState: ORDER_HISTORY_LABELS.noPrintData,
      drinkLabelState: ORDER_HISTORY_LABELS.noPrintData,
      workShiftId: typeof order.workShiftId === 'number' ? order.workShiftId : null,
      staffName: order.staffId != null ? `Nhân viên #${order.staffId}` : 'POS',
      storeName: order.storeId != null ? `Cửa hàng #${order.storeId}` : 'Cửa hàng POS',
      orderType: order.orderType === 'take-away'
        ? 'Mang đi'
        : order.orderType === 'dine-in'
          ? 'Tại quán'
          : (typeof order.orderType === 'string' ? order.orderType : null),
      retryCount: typeof order.retryCount === 'number' ? order.retryCount : undefined,
      lastError: typeof order.lastError === 'string' ? order.lastError : undefined,
      items,
      payments,
      isDegraded: degraded || missingItems || total === null,
      degradeReason: degraded
        ? 'Thiếu paymentSnapshot (schema cũ hoặc bản ghi không đầy đủ).'
        : missingItems
          ? 'Thiếu cartSnapshot/items.'
          : total === null
            ? 'Không xác định được tổng tiền đơn.'
            : undefined,
    }
  } catch (err) {
    const id = isRecord(input) && typeof input.clientOrderId === 'string' && input.clientOrderId.trim()
      ? input.clientOrderId.trim()
      : isRecord(input) && input.queueId != null
        ? `queue-${String(input.queueId)}`
        : 'unknown'
    console.warn('[OrderHistory] mapLocalOrderSafe failed', {
      clientOrderId: id,
      error: err instanceof Error ? err.message : String(err),
    })
    return buildDegradedRow(id, 'Không thể đọc đơn hàng cục bộ.')
  }
}

/** Map many local queue rows; never throws; never mutates input. */
export const mapLocalOrdersSafe = (orders: unknown): LocalHistoryRow[] => {
  if (!Array.isArray(orders)) return []
  return orders.map((o) => mapLocalOrderSafe(o))
}
