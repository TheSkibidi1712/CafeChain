import { Component, useEffect, useMemo, useState, type ErrorInfo, type ReactNode } from 'react'
import { useLiveQuery } from 'dexie-react-hooks'
import { db } from '../db/CafeChainPOSDB'
import { apiClient } from '../services/apiClient'
import {
  formatHistoryMoney,
  mapLocalOrdersSafe,
} from '../utils/orderHistoryLocalMapper'
import { formatIceLevel, type IceLevelPercent } from '../utils/iceLevel'

const LABELS = {
  paid: 'Đã thanh toán',
  paying: 'Đang thanh toán',
  pendingSync: 'Chờ đồng bộ',
  syncing: 'Đang đồng bộ',
  syncFailed: 'Đồng bộ lỗi',
  cancelled: 'Đã hủy',
  refunded: 'Đã hoàn tiền',
  officialReceiptReady: 'Hóa đơn chính thức đã sẵn sàng',
  officialLabelReady: 'Tem chính thức đã sẵn sàng',
  noPrintData: 'Chưa có dữ liệu in',
} as const

const formatVND = (amount: number | null | undefined): string =>
  formatHistoryMoney(amount)

const formatDateTime = (value: string): string => {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  return date.toLocaleString('vi-VN', {
    hour: '2-digit',
    minute: '2-digit',
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  })
}

const toDisplayError = (message?: string): string => {
  const raw = message?.trim()
  if (!raw) return 'Không tải được lịch sử đơn hàng.'
  if (raw.includes('<!DOCTYPE') || raw.includes('<html')) {
    return 'Backend đang lỗi khi tải lịch sử đơn hàng. Vui lòng kiểm tra console backend.'
  }
  return raw.length > 260 ? `${raw.slice(0, 260)}...` : raw
}

interface BackendPayment {
  paymentMethodId: number
  paymentMethod: string
  paymentStatusId: number
  paymentStatus: string
  amount: number
  receivedAmount?: number | null
  changeAmount?: number | null
  paidAt?: string | null
  transactionCode?: string | null
}

interface BackendOrderDetail {
  drinkName: string
  sizeName?: string | null
  quantity: number
  price: number
  lineTotal?: number
  iceLevelPercent?: IceLevelPercent | null
  baseIceQuantityBaseUnit?: number | null
  appliedIceQuantityBaseUnit?: number | null
  note?: string | null
  toppings: string[]
}

interface OrderHistoryItem {
  orderId: number
  clientOrderId?: string | null
  storeId?: number
  storeName?: string | null
  workShiftId?: number | null
  source?: string | null
  orderType: string
  createdAt: string
  paidAt?: string | null
  total: number
  paymentMethod: string
  orderStatusId?: number
  orderStatusName?: string | null
  paymentStatusId?: number
  paymentStatusName?: string | null
  staffName: string
  note?: string | null
  payments?: BackendPayment[]
  orderDetails: BackendOrderDetail[]
}

interface PaginationInfo {
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

interface OrderHistoryApiResponse {
  success: boolean
  message?: string
  data?: {
    items?: OrderHistoryItem[]
    pagination?: PaginationInfo
  }
}

type ReprintType = 'receipt' | 'drinkLabel'

interface ReprintApiResponse {
  success: boolean
  message?: string
  data?: {
    orderId: number
    type: string
  }
}

interface ReprintTarget {
  orderKey: string
  type: ReprintType
}

interface ReprintFeedback {
  tone: 'info' | 'success' | 'error'
  message: string
}

interface HistoryPaymentLine {
  method: string
  amount: number | null
  status: string
  paidAt?: string | null
  transactionCode?: string | null
  receivedAmount?: number | null
  changeAmount?: number | null
}

interface HistoryDetailLine {
  drinkName: string
  sizeName?: string | null
  quantity: number
  unitPrice: number
  lineTotal: number
  iceLevelPercent?: IceLevelPercent | null
  note?: string | null
  toppings: string[]
}

interface HistoryRow {
  key: string
  source: 'backend' | 'local'
  orderId?: number
  clientOrderId?: string | null
  code: string
  soldAt: string
  total: number | null
  paymentSummary: string
  orderState: string
  syncState?: string
  receiptState: string
  drinkLabelState: string
  workShiftId?: number | null
  staffName: string
  storeName: string
  orderType?: string | null
  note?: string | null
  retryCount?: number
  lastError?: string
  items: HistoryDetailLine[]
  payments: HistoryPaymentLine[]
  isDegraded?: boolean
  degradeReason?: string
}

const normalizePaymentMethod = (method?: string | null): string => {
  const raw = (method ?? '').trim()
  const lower = raw.toLowerCase()
  if (!raw || lower === 'n/a') return 'Chưa xác định'
  if (lower.includes('cash') || lower.includes('tiền')) return 'Tiền mặt'
  if (lower.includes('qr') || lower.includes('payos') || lower.includes('bank') || lower.includes('chuyển khoản')) {
    return 'Chuyển khoản VietQR'
  }
  if (lower.includes('momo')) return 'Ví điện tử — dữ liệu cũ'
  return 'Chưa xác định'
}

const normalizePaymentStatus = (statusId?: number, statusName?: string | null): string => {
  if (statusId === 2) return LABELS.paid
  if (statusId === 3) return LABELS.refunded
  if (statusId === 4) return LABELS.cancelled
  const lower = (statusName ?? '').toLowerCase()
  if (lower.includes('paid') || lower.includes('success') || lower.includes('thành công')) return LABELS.paid
  if (lower.includes('fail')) return LABELS.cancelled
  return LABELS.paying
}

const getBackendOrderState = (order: OrderHistoryItem): string => {
  if (order.orderStatusId === 6) return LABELS.cancelled
  if (order.paymentStatusId === 3) return LABELS.refunded
  if (order.paymentStatusId === 2 && order.orderStatusId === 5) return LABELS.paid
  return LABELS.paying
}

const getStatusTone = (label: string): string => {
  if (label === LABELS.paid || label === LABELS.officialReceiptReady || label === LABELS.officialLabelReady) {
    return 'bg-green-50 text-green-700 border-green-200'
  }
  if (label === LABELS.syncFailed || label === LABELS.cancelled || label === LABELS.refunded) {
    return 'bg-red-50 text-red-700 border-red-200'
  }
  if (label === LABELS.pendingSync || label === LABELS.syncing || label === LABELS.paying) {
    return 'bg-amber-50 text-amber-700 border-amber-200'
  }
  return 'bg-surface text-text-secondary border-border'
}

const summarizePayments = (payments: HistoryPaymentLine[], fallback: string): string => {
  if (payments.length === 0) return normalizePaymentMethod(fallback)
  const settledPayments = payments.filter((payment) => (
    payment.status === LABELS.paid || payment.status === LABELS.refunded
  ))
  const methods = Array.from(new Set(settledPayments.map((payment) => payment.method)))
  if (methods.length === 0) return normalizePaymentMethod(fallback)
  return methods.length > 1 ? 'Thanh toán kết hợp' : methods[0]
}

const buildItemSummary = (items: HistoryDetailLine[]): string => {
  if (items.length === 0) return 'Không có món'
  const head = items
    .slice(0, 2)
    .map((item) => `${item.quantity}x ${item.drinkName}${item.sizeName ? ` (${item.sizeName})` : ''}`)
    .join(', ')
  return items.length > 2 ? `${head} +${items.length - 2} món` : head
}

const safeMoney = (value: unknown): number | null => {
  if (typeof value === 'number' && Number.isFinite(value)) return value
  if (typeof value === 'string' && value.trim() !== '') {
    const n = Number(value)
    if (Number.isFinite(n)) return n
  }
  return null
}

const mapBackendOrder = (order: OrderHistoryItem): HistoryRow => {
  const payments = (order.payments ?? []).map((payment) => ({
    method: normalizePaymentMethod(payment?.paymentMethod),
    amount: safeMoney(payment?.amount),
    status: normalizePaymentStatus(payment?.paymentStatusId, payment?.paymentStatus),
    paidAt: payment?.paidAt,
    transactionCode: payment?.transactionCode,
    receivedAmount: safeMoney(payment?.receivedAmount),
    changeAmount: safeMoney(payment?.changeAmount),
  }))

  const items = (order.orderDetails ?? []).map((detail) => {
    const quantity = safeMoney(detail?.quantity) ?? 0
    const unitPrice = safeMoney(detail?.price) ?? 0
    const lineTotal = safeMoney(detail?.lineTotal) ?? unitPrice * quantity
    return {
      drinkName: detail?.drinkName?.trim() || 'Món',
      sizeName: detail?.sizeName,
      quantity,
      unitPrice,
      lineTotal,
      iceLevelPercent: detail?.iceLevelPercent,
      note: detail?.note,
      toppings: detail?.toppings ?? [],
    }
  })

  const isPaid = order.paymentStatusId === 2 || payments.some((payment) => payment.status === LABELS.paid)

  return {
    key: `backend-${order.orderId}`,
    source: 'backend',
    orderId: order.orderId,
    clientOrderId: order.clientOrderId,
    code: `#${order.orderId}`,
    soldAt: order.paidAt || order.createdAt,
    total: safeMoney(order.total),
    paymentSummary: summarizePayments(payments, order.paymentMethod),
    orderState: getBackendOrderState(order),
    receiptState: isPaid ? LABELS.officialReceiptReady : LABELS.noPrintData,
    drinkLabelState: isPaid ? LABELS.officialLabelReady : LABELS.noPrintData,
    workShiftId: order.workShiftId,
    staffName: order.staffName || 'POS',
    storeName: order.storeName || (order.storeId ? `Cửa hàng #${order.storeId}` : 'Cửa hàng POS'),
    orderType: order.orderType,
    note: order.note,
    items,
    payments,
  }
}

function StatusPill({ label }: { label: string }) {
  return (
    <span className={`inline-flex max-w-full items-center rounded-full border px-2 py-1 text-[11px] font-bold ${getStatusTone(label)}`}>
      <span className="truncate">{label}</span>
    </span>
  )
}

function DetailDrawer({
  order,
  onClose,
  onReprint,
  reprintTarget,
  reprintFeedback,
}: {
  order: HistoryRow | null
  onClose: () => void
  onReprint: (order: HistoryRow, type: ReprintType) => void
  reprintTarget: ReprintTarget | null
  reprintFeedback: ReprintFeedback | null
}) {
  if (!order) return null

  const canReprint = order.source === 'backend' && Boolean(order.orderId) && order.orderState === LABELS.paid
  const isSending = reprintTarget !== null
  const isReceiptSending = reprintTarget?.orderKey === order.key && reprintTarget.type === 'receipt'
  const isLabelSending = reprintTarget?.orderKey === order.key && reprintTarget.type === 'drinkLabel'
  const feedbackTone = reprintFeedback?.tone === 'success'
    ? 'border-green-200 bg-green-50 text-green-700'
    : reprintFeedback?.tone === 'error'
      ? 'border-red-200 bg-red-50 text-red-700'
      : 'border-amber-200 bg-amber-50 text-amber-700'

  return (
    <div className="fixed inset-0 z-40 flex max-h-[100dvh] min-h-0 justify-end overflow-hidden bg-black/25" role="dialog" aria-modal="true">
      <button
        aria-label="Đóng chi tiết đơn"
        className="absolute inset-0 cursor-default"
        onClick={onClose}
      />
      <aside className="relative flex h-full max-h-[100dvh] min-h-0 w-full max-w-[480px] flex-col overflow-y-auto overscroll-contain border-l border-border bg-surface-white shadow-2xl">
        <div className="sticky top-0 z-10 border-b border-border bg-surface-white px-5 py-4">
          <div className="flex items-start justify-between gap-4">
            <div className="min-w-0">
              <p className="text-[11px] font-semibold text-text-muted">
                {order.source === 'local' ? 'Đơn offline chưa đồng bộ' : 'Đơn từ backend'}
              </p>
              <h2 className="truncate text-lg font-extrabold text-text-primary">{order.code}</h2>
              {order.clientOrderId && (
                <p className="mt-1 truncate font-mono text-[11px] text-text-secondary" title={order.clientOrderId}>
                  {order.clientOrderId}
                </p>
              )}
            </div>
            <button
              type="button"
              onClick={onClose}
              className="h-9 w-9 shrink-0 rounded-lg border border-border bg-surface text-sm font-black text-text-secondary hover:bg-brand-orange-light hover:text-brand-orange"
              aria-label="Đóng"
            >
              X
            </button>
          </div>
        </div>

        <div className="space-y-5 p-5">
          {order.isDegraded && (
            <div className="rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-[11px] font-semibold text-amber-800">
              {order.degradeReason || 'Đơn cục bộ thiếu dữ liệu; hiển thị hạn chế.'}
            </div>
          )}

          <section className="space-y-3">
            <div className="flex flex-wrap gap-2">
              <StatusPill label={order.orderState} />
              <StatusPill label={order.receiptState} />
              <StatusPill label={order.drinkLabelState} />
            </div>
            <div className="grid grid-cols-2 gap-3 text-xs">
              <Info label="Thời gian bán" value={formatDateTime(order.soldAt)} />
              <Info label="Tổng tiền" value={formatVND(order.total)} strong />
              <Info label="WorkShiftId" value={order.workShiftId ? `#${order.workShiftId}` : 'Chưa xác định'} />
              <Info label="Loại đơn" value={order.orderType || 'Chưa xác định'} />
              <Info label="Nhân viên" value={order.staffName} />
              <Info label="Cửa hàng" value={order.storeName} />
            </div>
          </section>

          {order.source === 'local' && (
            <section className="rounded-lg border border-border bg-surface p-4">
              <h3 className="mb-3 text-xs font-extrabold text-text-primary">Đồng bộ</h3>
              <div className="space-y-2 text-xs text-text-secondary">
                <InfoRow label="Trạng thái" value={order.syncState ?? order.orderState} />
                <InfoRow label="Số lần thử" value={String(order.retryCount ?? 0)} />
                {order.lastError && <InfoRow label="Lỗi gần nhất" value={order.lastError} />}
              </div>
            </section>
          )}

          <section className="rounded-lg border border-border bg-surface p-4">
            <h3 className="mb-3 text-xs font-extrabold text-text-primary">Thanh toán</h3>
            <div className="space-y-2">
              {order.payments.length === 0 ? (
                <p className="text-xs text-text-muted">Chưa có dữ liệu</p>
              ) : (
                order.payments.map((payment, index) => (
                  <div key={`${payment.method}-${index}`} className="flex items-start justify-between gap-3 text-xs">
                    <div className="min-w-0">
                      <p className="truncate font-bold text-text-primary">{payment.method}</p>
                      <p className="truncate text-[11px] text-text-muted">
                        {payment.status}{payment.transactionCode ? ` • ${payment.transactionCode}` : ''}
                      </p>
                      {(payment.receivedAmount != null || payment.changeAmount != null) && (
                        <p className="mt-0.5 truncate text-[11px] text-text-muted">
                          {payment.receivedAmount != null ? `Nhận ${formatVND(payment.receivedAmount)}` : ''}
                          {payment.receivedAmount != null && payment.changeAmount != null ? ' • ' : ''}
                          {payment.changeAmount != null ? `Thối ${formatVND(payment.changeAmount)}` : ''}
                        </p>
                      )}
                    </div>
                    <p className="shrink-0 font-bold text-text-primary">{formatVND(payment.amount)}</p>
                  </div>
                ))
              )}
            </div>
          </section>

          <section className="rounded-lg border border-border bg-surface p-4">
            <h3 className="mb-3 text-xs font-extrabold text-text-primary">Món trong đơn</h3>
            <div className="space-y-3">
              {order.items.length === 0 ? (
                <p className="text-xs text-text-muted">Không có món</p>
              ) : (
                order.items.map((item, index) => (
                  <div key={`${item.drinkName}-${index}`} className="border-b border-border-light pb-3 last:border-b-0 last:pb-0">
                    <div className="flex items-start justify-between gap-3">
                      <div className="min-w-0">
                        <p className="truncate text-sm font-bold text-text-primary">
                          {item.quantity}x {item.drinkName}
                        </p>
                        <p className="truncate text-[11px] text-text-muted">
                          {item.sizeName ? `Size ${item.sizeName}` : 'Không chọn size'}
                          {formatIceLevel(item.iceLevelPercent) ? ` • ${formatIceLevel(item.iceLevelPercent)}` : ''}
                          {item.toppings.length > 0 ? ` • ${item.toppings.join(', ')}` : ''}
                        </p>
                        {item.note && (
                          <p className="mt-1 line-clamp-2 text-[11px] text-text-secondary">{item.note}</p>
                        )}
                      </div>
                      <div className="shrink-0 text-right text-xs">
                        <p className="font-bold text-text-primary">{formatVND(item.lineTotal)}</p>
                        <p className="text-[11px] text-text-muted">{formatVND(item.unitPrice)}/món</p>
                      </div>
                    </div>
                  </div>
                ))
              )}
            </div>
          </section>

          {order.note && (
            <section className="rounded-lg border border-border bg-surface p-4">
              <h3 className="mb-2 text-xs font-extrabold text-text-primary">Ghi chú</h3>
              <p className="whitespace-pre-wrap break-words text-xs text-text-secondary">{order.note}</p>
            </section>
          )}

          <section className="rounded-lg border border-dashed border-border bg-surface p-4">
            <h3 className="mb-3 text-xs font-extrabold text-text-primary">In lại</h3>
            <div className="grid grid-cols-2 gap-2">
              <button
                type="button"
                onClick={() => onReprint(order, 'receipt')}
                disabled={!canReprint || isSending}
                className="rounded-lg border border-brand-orange-border bg-brand-orange px-3 py-2 text-xs font-bold text-white hover:bg-brand-orange-hover disabled:cursor-not-allowed disabled:border-border disabled:bg-surface disabled:text-text-muted"
              >
                {isReceiptSending ? 'Đang gửi lệnh in...' : 'In lại hóa đơn'}
              </button>
              <button
                type="button"
                onClick={() => onReprint(order, 'drinkLabel')}
                disabled={!canReprint || isSending}
                className="rounded-lg border border-brand-orange-border bg-brand-orange-light px-3 py-2 text-xs font-bold text-brand-orange hover:bg-surface-white disabled:cursor-not-allowed disabled:border-border disabled:bg-surface disabled:text-text-muted"
              >
                {isLabelSending ? 'Đang gửi lệnh in...' : 'In lại tem'}
              </button>
            </div>
            {!canReprint && (
              <p className="mt-2 text-[11px] text-text-muted">
                Chỉ hỗ trợ in lại với đơn backend đã thanh toán.
              </p>
            )}
            {reprintFeedback && (
              <p className={`mt-3 rounded-lg border px-3 py-2 text-xs font-semibold ${feedbackTone}`}>
                {reprintFeedback.message}
              </p>
            )}
          </section>
        </div>
      </aside>
    </div>
  )
}

function Info({ label, value, strong = false }: { label: string; value: string; strong?: boolean }) {
  return (
    <div className="min-w-0 rounded-lg border border-border bg-surface px-3 py-2">
      <p className="truncate text-[10px] font-bold uppercase text-text-muted">{label}</p>
      <p className={`truncate ${strong ? 'text-sm font-extrabold text-text-primary' : 'text-xs font-semibold text-text-secondary'}`} title={value}>
        {value}
      </p>
    </div>
  )
}

function InfoRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="grid grid-cols-[92px_minmax(0,1fr)] gap-2">
      <span className="font-bold text-text-primary">{label}</span>
      <span className="break-words text-text-secondary">{value}</span>
    </div>
  )
}

/** Component-level safety net — does not hide root cause of mapping bugs. */
class OrderHistoryErrorBoundary extends Component<
  { children: ReactNode },
  { hasError: boolean }
> {
  state = { hasError: false }

  static getDerivedStateFromError(): { hasError: boolean } {
    return { hasError: true }
  }

  componentDidCatch(error: Error, info: ErrorInfo): void {
    console.warn('[OrderHistory] render error boundary', {
      message: error.message,
      componentStack: info.componentStack?.slice(0, 400),
    })
  }

  render() {
    if (this.state.hasError) {
      return (
        <div className="flex h-full w-full items-center justify-center bg-surface p-6">
          <div className="max-w-md rounded-lg border border-red-200 bg-red-50 p-6 text-center">
            <p className="text-sm font-bold text-red-700">Không thể tải lịch sử đơn hàng. Vui lòng thử lại.</p>
            <p className="mt-2 text-[11px] text-text-muted">
              Trang gặp lỗi hiển thị. Bấm tải lại trang hoặc quay lại POS.
            </p>
            <button
              type="button"
              onClick={() => this.setState({ hasError: false })}
              className="mt-4 rounded-lg border border-red-200 bg-white px-4 py-2 text-xs font-bold text-red-700 hover:bg-red-100"
            >
              Thử lại
            </button>
          </div>
        </div>
      )
    }
    return this.props.children
  }
}

function OrderHistoryPage() {
  const [orders, setOrders] = useState<OrderHistoryItem[]>([])
  const [pagination, setPagination] = useState<PaginationInfo>({ page: 1, pageSize: 20, totalCount: 0, totalPages: 1 })
  const [isLoading, setIsLoading] = useState(true)
  const [selectedOrder, setSelectedOrder] = useState<HistoryRow | null>(null)
  const [reprintTarget, setReprintTarget] = useState<ReprintTarget | null>(null)
  const [reprintFeedback, setReprintFeedback] = useState<ReprintFeedback | null>(null)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)

  const localOfflineOrders = useLiveQuery(
    () => db.cartSyncQueue
      .where('syncStatus')
      .anyOf(['Pending', 'Syncing', 'Failed'])
      .toArray(),
    [],
    []
  )

  const fetchOrders = async (page: number) => {
    setIsLoading(true)
    try {
      const res = await apiClient.get<OrderHistoryApiResponse>(`/api/v1/pos/orders?page=${page}&pageSize=20`)
      if (res.ok && res.data?.success && res.data?.data) {
        setOrders(res.data.data.items ?? [])
        setPagination(res.data.data.pagination ?? { page: 1, pageSize: 20, totalCount: 0, totalPages: 1 })
        setErrorMessage(null)
      } else {
        const displayError = toDisplayError(res.data?.message || res.error)
        console.warn('[OrderHistory] API failed:', res.data?.message || res.error)
        setOrders([])
        setErrorMessage(displayError)
      }
    } catch (err) {
      console.error('[OrderHistory] Fetch error:', err)
      setOrders([])
      setErrorMessage(toDisplayError(err instanceof Error ? err.message : String(err)))
    } finally {
      setIsLoading(false)
    }
  }

  useEffect(() => {
    queueMicrotask(() => {
      void fetchOrders(1)
    })
  }, [])

  const historyRows = useMemo(() => {
    let localMapWarning: string | null = null
    let localRows: HistoryRow[]
    try {
      localRows = mapLocalOrdersSafe(localOfflineOrders ?? []).map((row) => ({
        ...row,
        source: 'local' as const,
      }))
      if (localRows.some((r) => r.isDegraded)) {
        localMapWarning = 'Không thể đọc một số đơn hàng cục bộ.'
      }
    } catch (err) {
      console.warn('[OrderHistory] local map failed', err instanceof Error ? err.message : String(err))
      localMapWarning = 'Không thể đọc một số đơn hàng cục bộ.'
      localRows = []
    }

    const backendRows = orders.map(mapBackendOrder)
    return {
      rows: [...localRows, ...backendRows].sort((a, b) => (
        new Date(b.soldAt).getTime() - new Date(a.soldAt).getTime()
      )),
      localMapWarning,
    }
  }, [orders, localOfflineOrders])

  const displayRows = historyRows.rows
  const localMapWarning = historyRows.localMapWarning

  const handlePageChange = (newPage: number) => {
    if (newPage < 1 || newPage > pagination.totalPages) return
    setPagination(prev => ({ ...prev, page: newPage }))
    void fetchOrders(newPage)
  }

  const handleReprint = async (order: HistoryRow, type: ReprintType) => {
    if (order.source === 'local' || !order.orderId || order.orderState !== LABELS.paid || reprintTarget) {
      return
    }

    setReprintTarget({ orderKey: order.key, type })
    setReprintFeedback({ tone: 'info', message: 'Đang gửi lệnh in...' })

    try {
      const res = await apiClient.post<ReprintApiResponse>(
        `/api/v1/pos/orders/${order.orderId}/reprint`,
        { type }
      )

      if (res.ok && res.data?.success) {
        setReprintFeedback({
          tone: 'success',
          message: type === 'receipt'
            ? 'Đã gửi lệnh in lại hóa đơn.'
            : 'Đã gửi lệnh in lại tem.',
        })
        return
      }

      setReprintFeedback({
        tone: 'error',
        message: res.data?.message || res.error || 'Không gửi được lệnh in lại.',
      })
    } catch (error) {
      setReprintFeedback({
        tone: 'error',
        message: error instanceof Error ? error.message : 'Không gửi được lệnh in lại.',
      })
    } finally {
      setReprintTarget(null)
    }
  }

  return (
    <div className="h-full w-full overflow-y-auto bg-surface p-6 font-sans select-none">
      <div className="mx-auto max-w-7xl space-y-5">
        <header className="flex flex-col gap-3 rounded-lg border border-border bg-surface-white p-5 shadow-[var(--shadow-card)] sm:flex-row sm:items-center sm:justify-between">
          <div className="min-w-0">
            <h1 className="text-base font-bold text-text-primary">Lịch sử đơn hàng</h1>
            <p className="mt-1 truncate text-[11px] text-text-secondary">
              {pagination.totalCount > 0
                ? `Backend ${pagination.totalCount} đơn • Offline chưa đồng bộ ${localOfflineOrders?.length ?? 0} đơn`
                : `Offline chưa đồng bộ ${localOfflineOrders?.length ?? 0} đơn`}
            </p>
          </div>
          <button
            type="button"
            onClick={() => void fetchOrders(pagination.page)}
            disabled={isLoading}
            className="rounded-lg border border-brand-orange-border bg-brand-orange-light px-4 py-2 text-xs font-bold text-brand-orange hover:bg-surface-white disabled:opacity-50"
          >
            Làm mới
          </button>
        </header>

        <section className="overflow-hidden rounded-lg border border-border bg-surface-white shadow-[var(--shadow-card)]">
          {errorMessage && (
            <div className="border-b border-red-200 bg-red-50 px-4 py-3 text-xs font-semibold text-red-700">
              <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                <span>{errorMessage}</span>
                <button
                  type="button"
                  onClick={() => void fetchOrders(pagination.page)}
                  disabled={isLoading}
                  className="self-start rounded-lg border border-red-200 bg-white px-3 py-1.5 text-[11px] font-bold text-red-700 hover:bg-red-100 disabled:opacity-50 sm:self-auto"
                >
                  Thử lại
                </button>
              </div>
            </div>
          )}
          {localMapWarning && (
            <div className="border-b border-amber-200 bg-amber-50 px-4 py-2 text-[11px] font-semibold text-amber-800">
              {localMapWarning}
            </div>
          )}
          <div className="grid min-w-[1100px] grid-cols-[150px_145px_minmax(210px,1.5fr)_150px_130px_170px_150px_120px] gap-3 border-b border-border bg-surface px-4 py-3 text-[10px] font-extrabold uppercase text-text-muted">
            <span>Mã đơn</span>
            <span>Thời gian</span>
            <span>Sản phẩm</span>
            <span>Thanh toán</span>
            <span>Trạng thái</span>
            <span>Hóa đơn</span>
            <span>Tem</span>
            <span className="text-right">Tổng tiền</span>
          </div>

          {isLoading ? (
            <div className="p-16 text-center text-xs font-semibold text-text-muted">Đang tải lịch sử đơn hàng…</div>
          ) : errorMessage && displayRows.length === 0 ? (
            <div className="p-16 text-center">
              <p className="text-xs font-semibold text-red-700">Không thể tải lịch sử đơn hàng. Vui lòng thử lại.</p>
              <p className="mt-1 text-[10px] text-text-muted">Kiểm tra backend rồi bấm Làm mới.</p>
            </div>
          ) : displayRows.length === 0 ? (
            <div className="p-16 text-center">
              <p className="text-xs font-semibold text-text-muted">Chưa có đơn hàng.</p>
              <p className="mt-1 text-[10px] text-text-muted">Đơn backend và đơn offline chưa đồng bộ sẽ xuất hiện tại đây.</p>
            </div>
          ) : (
            <div className="overflow-x-auto">
              <div className="min-w-[1100px] divide-y divide-border">
                {displayRows.map((order) => (
                  <button
                    type="button"
                    key={order.key}
                    onClick={() => {
                      setSelectedOrder(order)
                      setReprintFeedback(null)
                    }}
                    className="grid w-full grid-cols-[150px_145px_minmax(210px,1.5fr)_150px_130px_170px_150px_120px] gap-3 px-4 py-3 text-left text-xs transition-colors hover:bg-brand-orange-light/35"
                  >
                    <div className="min-w-0">
                      <p className="truncate font-mono font-bold text-text-primary">{order.code}</p>
                      {order.clientOrderId && (
                        <p className="truncate font-mono text-[10px] text-text-muted" title={order.clientOrderId}>
                          {order.clientOrderId}
                        </p>
                      )}
                      {order.isDegraded && (
                        <p className="mt-0.5 text-[10px] font-semibold text-amber-700">Đơn cục bộ thiếu dữ liệu</p>
                      )}
                    </div>
                    <p className="truncate text-text-secondary">{formatDateTime(order.soldAt)}</p>
                    <div className="min-w-0">
                      <p className="truncate font-semibold text-text-primary" title={buildItemSummary(order.items)}>
                        {buildItemSummary(order.items)}
                      </p>
                      <p className="truncate text-[10px] text-text-muted">
                        {order.workShiftId ? `WorkShift #${order.workShiftId}` : 'Chưa có WorkShift'}
                      </p>
                    </div>
                    <p className="truncate font-semibold text-text-secondary">{order.paymentSummary}</p>
                    <StatusPill label={order.orderState} />
                    <StatusPill label={order.receiptState} />
                    <StatusPill label={order.drinkLabelState} />
                    <p className="truncate text-right font-extrabold text-text-primary">{formatVND(order.total)}</p>
                  </button>
                ))}
              </div>
            </div>
          )}
        </section>

        {pagination.totalPages > 1 && (
          <nav className="flex items-center justify-center gap-2">
            <button
              type="button"
              onClick={() => handlePageChange(pagination.page - 1)}
              disabled={pagination.page <= 1}
              className="rounded-lg border border-border bg-surface-white px-3 py-1.5 text-xs font-bold hover:bg-surface disabled:cursor-not-allowed disabled:opacity-30"
            >
              Trước
            </button>
            <span className="rounded-lg border border-border bg-surface-white px-3 py-1.5 text-xs font-bold text-text-secondary">
              Trang {pagination.page}/{pagination.totalPages}
            </span>
            <button
              type="button"
              onClick={() => handlePageChange(pagination.page + 1)}
              disabled={pagination.page >= pagination.totalPages}
              className="rounded-lg border border-border bg-surface-white px-3 py-1.5 text-xs font-bold hover:bg-surface disabled:cursor-not-allowed disabled:opacity-30"
            >
              Sau
            </button>
          </nav>
        )}
      </div>

      <DetailDrawer
        order={selectedOrder}
        onClose={() => setSelectedOrder(null)}
        onReprint={handleReprint}
        reprintTarget={reprintTarget}
        reprintFeedback={reprintFeedback}
      />
    </div>
  )
}

export default function OrderHistory() {
  return (
    <OrderHistoryErrorBoundary>
      <OrderHistoryPage />
    </OrderHistoryErrorBoundary>
  )
}
