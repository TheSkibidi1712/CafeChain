import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react'
import { useLiveQuery } from 'dexie-react-hooks'
import { db, type CartSyncQueueItem } from '../db/CafeChainPOSDB'
import { apiClient } from '../services/apiClient'
import {
  ACTIVE_PAYMENT_CLOSE_GUARD_CHANGED,
  getMatchingActivePaymentCloseGuard,
} from '../services/posShiftCloseGuard'
import { getPosSession } from '../services/posSession'

interface ShiftSummaryDto {
  shiftId?: number | null
  storeId?: number
  staffName?: string | null
  startTime?: string | null
  endTime?: string | null
  startingCash: number
  expectedEndingCash: number
  actualEndingCash?: number | null
  cashDiscrepancy?: number | null
  totalCashSales: number
  totalBankingSales: number
  totalOrders: number
  status: 'Open' | 'Closed' | 'NoActiveShift' | string
}

type ShiftActionResponse = Partial<ShiftSummaryDto> & {
  success?: boolean
  message?: string
}

const readApiMessage = (value: unknown): string | null => {
  if (!value || typeof value !== 'object') return null

  const record = value as Record<string, unknown>
  for (const key of ['message', 'error', 'title', 'detail']) {
    const message = record[key]
    if (typeof message === 'string' && message.trim().length > 0) {
      return message
    }
  }

  return null
}

const getApiErrorMessage = (
  response: { data: unknown; error?: string },
  fallback: string
): string => {
  const dataMessage = readApiMessage(response.data)
  if (dataMessage) return dataMessage

  const errorText = response.error?.trim()
  if (!errorText) return fallback

  try {
    const parsed = JSON.parse(errorText) as unknown
    return readApiMessage(parsed) ?? errorText
  } catch {
    return errorText
  }
}

const getUnexpectedErrorMessage = (error: unknown, fallback: string): string =>
  error instanceof Error && error.message.trim().length > 0 ? error.message : fallback

const formatVND = (amount: number): string =>
  new Intl.NumberFormat('vi-VN').format(amount) + 'đ'

const formatDateTime = (value?: string | null) => {
  if (!value) return '--'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return '--'
  return date.toLocaleString('vi-VN', {
    hour: '2-digit',
    minute: '2-digit',
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  })
}

function getPosTerminalId() {
  const key = 'CafeChain_POS_TerminalId'
  const existing = localStorage.getItem(key)
  if (existing) return existing
  const generated = crypto.randomUUID()
  localStorage.setItem(key, generated)
  return generated
}

export default function ShiftSummary() {
  const [shift, setShift] = useState<ShiftSummaryDto | null>(null)
  const [startingCash, setStartingCash] = useState<number | ''>('')
  const [actualEndingCash, setActualEndingCash] = useState<number | ''>('')
  const [discrepancyReason, setDiscrepancyReason] = useState('')
  const [isLoading, setIsLoading] = useState(true)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null)
  const [guardVersion, setGuardVersion] = useState(0)

  const localSyncBlockers = useLiveQuery(
    () => db.cartSyncQueue
      .where('syncStatus')
      .anyOf(['Pending', 'Syncing', 'Failed'])
      .toArray(),
    [],
    [] as CartSyncQueueItem[]
  )

  const session = getPosSession()
  const hasOpenShift = shift?.status === 'Open' && !!shift.shiftId
  const currentShiftId = hasOpenShift ? shift.shiftId ?? null : null
  const currentStaffId = session.staffId
  const currentStoreId = session.storeId
  const expectedEndingCash = useMemo(() => {
    if (!shift) return 0
    return shift.startingCash + shift.totalCashSales
  }, [shift])
  const cashDiscrepancy = actualEndingCash === '' ? 0 : actualEndingCash - expectedEndingCash
  const needsReason = actualEndingCash !== '' && cashDiscrepancy !== 0
  const discrepancyTone =
    actualEndingCash === ''
      ? 'empty'
      : cashDiscrepancy < 0
        ? 'short'
        : cashDiscrepancy > 0
          ? 'over'
          : 'match'
  const discrepancyLabel =
    discrepancyTone === 'short'
      ? 'Thiếu tiền'
      : discrepancyTone === 'over'
        ? 'Thừa tiền'
        : discrepancyTone === 'match'
          ? 'Khớp két'
          : 'Chưa nhập'
  const closedDiscrepancy = shift?.cashDiscrepancy ?? 0
  const activePaymentGuard = useMemo(() => {
    if (!currentShiftId || !currentStaffId || !currentStoreId) return null
    void guardVersion

    return getMatchingActivePaymentCloseGuard({
      shiftId: currentShiftId,
      staffId: currentStaffId,
      storeId: currentStoreId,
    })
  }, [currentShiftId, currentStaffId, currentStoreId, guardVersion])

  const shiftLocalSyncBlockers = useMemo(() => {
    if (!currentShiftId || !currentStaffId || !currentStoreId) return []

    return localSyncBlockers.filter((order) =>
      order.workShiftId === currentShiftId &&
      order.staffId === currentStaffId &&
      order.storeId === currentStoreId
    )
  }, [currentShiftId, currentStaffId, currentStoreId, localSyncBlockers])

  const pendingOfflineCount = shiftLocalSyncBlockers.filter((order) => order.syncStatus === 'Pending').length
  const syncingOfflineCount = shiftLocalSyncBlockers.filter((order) => order.syncStatus === 'Syncing').length
  const failedOfflineCount = shiftLocalSyncBlockers.filter((order) => order.syncStatus === 'Failed').length
  const closeBlockerReasons = useMemo(() => {
    const reasons: string[] = []

    if (activePaymentGuard) {
      reasons.push('Đang có giao dịch thanh toán chưa hoàn tất. Vui lòng hoàn tất hoặc hủy giao dịch trước khi đóng ca.')
    }

    if (pendingOfflineCount > 0) {
      reasons.push(`Còn ${pendingOfflineCount} đơn offline chờ đồng bộ trong ca này.`)
    }

    if (syncingOfflineCount > 0) {
      reasons.push(`Còn ${syncingOfflineCount} đơn offline đang đồng bộ. Vui lòng chờ đồng bộ hoàn tất.`)
    }

    if (failedOfflineCount > 0) {
      reasons.push(`Có ${failedOfflineCount} đơn offline đồng bộ lỗi chưa xử lý.`)
    }

    return reasons
  }, [activePaymentGuard, failedOfflineCount, pendingOfflineCount, syncingOfflineCount])
  const hasCloseBlockers = closeBlockerReasons.length > 0

  const loadCurrentShift = useCallback(async () => {
    setIsLoading(true)
    try {
      const response = await apiClient.get<ShiftSummaryDto>('/api/v1/pos/shifts/current')
      if (response.ok && response.data) {
        setShift(response.data)
      } else {
        setShift({ status: 'NoActiveShift', startingCash: 0, expectedEndingCash: 0, totalCashSales: 0, totalBankingSales: 0, totalOrders: 0 })
        setMessage({
          type: 'error',
          text: getApiErrorMessage(response, 'Không tải được dữ liệu ca.'),
        })
      }
    } catch (error) {
      setShift({ status: 'NoActiveShift', startingCash: 0, expectedEndingCash: 0, totalCashSales: 0, totalBankingSales: 0, totalOrders: 0 })
      setMessage({
        type: 'error',
        text: getUnexpectedErrorMessage(error, 'Không tải được dữ liệu ca.'),
      })
    } finally {
      setIsLoading(false)
    }
  }, [])

  useEffect(() => {
    queueMicrotask(() => {
      void loadCurrentShift()
    })
  }, [loadCurrentShift])

  useEffect(() => {
    const refreshActivePaymentGuard = () => setGuardVersion((version) => version + 1)

    const intervalId = window.setInterval(refreshActivePaymentGuard, 5000)
    window.addEventListener('focus', refreshActivePaymentGuard)
    window.addEventListener('storage', refreshActivePaymentGuard)
    window.addEventListener(ACTIVE_PAYMENT_CLOSE_GUARD_CHANGED, refreshActivePaymentGuard)

    return () => {
      window.clearInterval(intervalId)
      window.removeEventListener('focus', refreshActivePaymentGuard)
      window.removeEventListener('storage', refreshActivePaymentGuard)
      window.removeEventListener(ACTIVE_PAYMENT_CLOSE_GUARD_CHANGED, refreshActivePaymentGuard)
    }
  }, [])

  const handleOpenShift = async (event: FormEvent) => {
    event.preventDefault()
    if (startingCash === '') return

    setIsSubmitting(true)
    setMessage(null)
    try {
      const response = await apiClient.post<ShiftActionResponse>('/api/v1/pos/shifts/open', {
        startingCash,
        posTerminalId: getPosTerminalId(),
      })

      if (response.ok && response.data?.status === 'Open') {
        setShift(response.data as ShiftSummaryDto)
        setStartingCash('')
        setMessage({ type: 'success', text: 'Mở ca thành công.' })
      } else {
        setMessage({
          type: 'error',
          text: getApiErrorMessage(response, 'Không thể mở ca.'),
        })
      }
    } catch (error) {
      setMessage({
        type: 'error',
        text: getUnexpectedErrorMessage(error, 'Không thể mở ca.'),
      })
    } finally {
      setIsSubmitting(false)
    }
  }

  const handleCloseShift = async (event: FormEvent) => {
    event.preventDefault()
    if (!shift?.shiftId || actualEndingCash === '') return
    if (hasCloseBlockers) {
      setMessage({
        type: 'error',
        text: 'Không thể đóng ca thường. Vui lòng xử lý các lý do trên trước khi đóng ca thường.',
      })
      return
    }
    if (needsReason && discrepancyReason.trim().length === 0) return

    setIsSubmitting(true)
    setMessage(null)
    try {
      const response = await apiClient.post<ShiftActionResponse>(`/api/v1/pos/shifts/${shift.shiftId}/close`, {
        actualEndingCash,
        discrepancyReason: discrepancyReason.trim() || null,
      })

      if (response.ok && response.data?.status === 'Closed') {
        setShift(response.data as ShiftSummaryDto)
        setActualEndingCash('')
        setDiscrepancyReason('')
        setMessage({ type: 'success', text: 'Đóng ca thành công.' })
      } else {
        setMessage({
          type: 'error',
          text: getApiErrorMessage(response, 'Không thể đóng ca.'),
        })
      }
    } catch (error) {
      setMessage({
        type: 'error',
        text: getUnexpectedErrorMessage(error, 'Không thể đóng ca.'),
      })
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <div className="h-full w-full overflow-y-auto bg-surface p-6 font-sans select-none">
      <div className="max-w-5xl mx-auto space-y-5">
        <div className="bg-surface-white p-5 rounded-xl border border-border shadow-[var(--shadow-card)] flex justify-between items-center">
          <div>
            <h1 className="text-base font-bold text-text-primary">Quản lý ca làm việc POS</h1>
            <p className="text-[11px] text-text-secondary mt-1">
              {session.staffName}{session.storeId ? ` • Cửa hàng #${session.storeId}` : ''}
            </p>
          </div>
          <span className={`px-3 py-1 rounded-full text-xs font-bold border ${
            hasOpenShift
              ? 'bg-green-50 text-green-700 border-green-200'
              : shift?.status === 'Closed'
                ? 'bg-gray-50 text-gray-700 border-gray-200'
                : 'bg-brand-orange-light text-brand-orange border-brand-orange-border'
          }`}>
            {hasOpenShift ? 'Đang mở' : shift?.status === 'Closed' ? 'Đã đóng' : 'Chưa mở ca'}
          </span>
        </div>

        {message && (
          <div className={`p-4 rounded-xl border text-xs font-bold ${
            message.type === 'success'
              ? 'bg-green-50 border-green-200 text-green-700'
              : 'bg-red-50 border-red-200 text-red-700'
          }`}>
            {message.text}
          </div>
        )}

        {isLoading ? (
          <div className="bg-surface-white p-12 rounded-xl border border-border text-center text-xs font-semibold text-text-muted">
            Đang tải dữ liệu ca...
          </div>
        ) : !hasOpenShift && shift?.status !== 'Closed' ? (
          <form onSubmit={handleOpenShift} className="bg-surface-white p-5 rounded-xl border border-border shadow-[var(--shadow-card)] space-y-4">
            <h2 className="text-xs font-bold text-text-primary uppercase tracking-wider">Mở ca mới</h2>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div>
                <label className="block text-xs font-semibold text-text-secondary mb-1">
                  Tiền mặt đầu ca
                </label>
                <div className="relative">
                  <input
                    type="number"
                    min={0}
                    step={1000}
                    value={startingCash}
                    onChange={(event) => setStartingCash(event.target.value === '' ? '' : Number(event.target.value))}
                    placeholder="Ví dụ: 1000000"
                    required
                    className="w-full px-3 py-2 border border-border rounded-lg text-xs outline-none focus:border-brand-orange text-text-primary bg-surface font-semibold"
                  />
                  <span className="absolute right-3 top-2 text-xs text-text-secondary font-bold">VNĐ</span>
                </div>
              </div>
            </div>
            <div className="flex flex-wrap gap-2">
              {[500000, 1000000, 1500000, 2000000].map((value) => (
                <button
                  key={value}
                  type="button"
                  onClick={() => setStartingCash(value)}
                  className="px-3 py-1.5 rounded-lg border border-border text-xs font-bold text-text-secondary hover:border-brand-orange-border hover:text-brand-orange"
                >
                  {formatVND(value)}
                </button>
              ))}
            </div>
            <button
              type="submit"
              disabled={isSubmitting || startingCash === ''}
              className="px-4 py-2.5 bg-brand-orange text-white text-xs font-bold rounded-lg cursor-pointer hover:bg-brand-orange-hover disabled:opacity-40 disabled:cursor-not-allowed active:scale-95 transition-all shadow-[var(--shadow-button)]"
            >
              {isSubmitting ? 'Đang mở ca...' : 'Xác nhận mở ca'}
            </button>
          </form>
        ) : (
          <>
            <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
              <div className="bg-surface-white p-4 rounded-xl border border-border shadow-[var(--shadow-card)]">
                <p className="text-[10px] font-bold text-text-secondary uppercase">Tiền đầu ca</p>
                <p className="text-lg font-bold text-text-primary mt-1">{formatVND(shift?.startingCash ?? 0)}</p>
              </div>
              <div className="bg-surface-white p-4 rounded-xl border border-border shadow-[var(--shadow-card)]">
                <p className="text-[10px] font-bold text-text-secondary uppercase">Tiền mặt</p>
                <p className="text-lg font-bold text-brand-orange mt-1">{formatVND(shift?.totalCashSales ?? 0)}</p>
              </div>
              <div className="bg-surface-white p-4 rounded-xl border border-border shadow-[var(--shadow-card)]">
                <p className="text-[10px] font-bold text-text-secondary uppercase">Chuyển khoản</p>
                <p className="text-lg font-bold text-text-primary mt-1">{formatVND(shift?.totalBankingSales ?? 0)}</p>
              </div>
              <div className="bg-surface-white p-4 rounded-xl border border-border shadow-[var(--shadow-card)]">
                <p className="text-[10px] font-bold text-text-secondary uppercase">Số đơn</p>
                <p className="text-lg font-bold text-text-primary mt-1">{shift?.totalOrders ?? 0}</p>
              </div>
            </div>

            <div className="bg-surface-white p-5 rounded-xl border border-border shadow-[var(--shadow-card)] space-y-3">
              <h2 className="text-xs font-bold text-text-primary uppercase tracking-wider">Thông tin ca</h2>
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 text-xs">
                <div>
                  <p className="text-text-secondary">Nhân viên</p>
                  <p className="font-bold text-text-primary mt-1">{shift?.staffName || session.staffName}</p>
                </div>
                <div>
                  <p className="text-text-secondary">Mở ca</p>
                  <p className="font-bold text-text-primary mt-1">{formatDateTime(shift?.startTime)}</p>
                </div>
                <div>
                  <p className="text-text-secondary">Tiền két hệ thống</p>
                  <p className="font-bold text-brand-orange mt-1">{formatVND(expectedEndingCash)}</p>
                </div>
              </div>
            </div>

            {hasOpenShift ? (
              <form onSubmit={handleCloseShift} className="bg-surface-white p-5 rounded-xl border border-border shadow-[var(--shadow-card)] space-y-4">
                <h2 className="text-xs font-bold text-text-primary uppercase tracking-wider">Đóng ca và kiểm két</h2>
                {hasCloseBlockers && (
                  <div className="rounded-xl border border-red-200 bg-red-50 p-4 text-xs text-red-700">
                    <p className="font-extrabold">Không thể đóng ca thường.</p>
                    <ul className="mt-2 space-y-1.5 font-semibold">
                      {closeBlockerReasons.map((reason) => (
                        <li key={reason} className="flex gap-2">
                          <span aria-hidden="true">•</span>
                          <span>{reason}</span>
                        </li>
                      ))}
                    </ul>
                    <p className="mt-3 font-extrabold">Vui lòng xử lý các lý do trên trước khi đóng ca thường.</p>
                  </div>
                )}
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <div>
                    <label className="block text-xs font-semibold text-text-secondary mb-1">
                      Tiền mặt thực tế trong két
                    </label>
                    <div className="relative">
                      <input
                        type="number"
                        min={0}
                        step={1000}
                        value={actualEndingCash}
                        onChange={(event) => setActualEndingCash(event.target.value === '' ? '' : Number(event.target.value))}
                        placeholder="Nhập số tiền đếm được"
                        required
                        className="w-full px-3 py-2 border border-border rounded-lg text-xs outline-none focus:border-brand-orange text-text-primary bg-surface font-semibold"
                      />
                      <span className="absolute right-3 top-2 text-xs text-text-secondary font-bold">VNĐ</span>
                    </div>
                  </div>
                  <div
                    className={`p-3 rounded-lg border ${
                      discrepancyTone === 'short'
                        ? 'bg-red-50 border-red-200'
                        : discrepancyTone === 'over'
                          ? 'bg-amber-50 border-amber-200'
                          : discrepancyTone === 'match'
                            ? 'bg-green-50 border-green-200'
                            : 'bg-surface border-border'
                    }`}
                  >
                    <p className="text-[10px] font-bold text-text-secondary uppercase">Chênh lệch tạm tính</p>
                    <p
                      className={`text-lg font-bold mt-1 ${
                        discrepancyTone === 'short'
                          ? 'text-danger'
                          : discrepancyTone === 'over'
                            ? 'text-amber-700'
                            : discrepancyTone === 'match'
                              ? 'text-green-700'
                              : 'text-text-muted'
                      }`}
                    >
                      {cashDiscrepancy > 0 ? '+' : ''}{formatVND(cashDiscrepancy)}
                    </p>
                    <p
                      className={`text-[10px] font-extrabold uppercase mt-1 ${
                        discrepancyTone === 'short'
                          ? 'text-danger'
                          : discrepancyTone === 'over'
                            ? 'text-amber-700'
                            : discrepancyTone === 'match'
                              ? 'text-green-700'
                              : 'text-text-secondary'
                      }`}
                    >
                      {discrepancyLabel}
                    </p>
                  </div>
                </div>

                {needsReason && (
                  <div>
                    <label className="block text-xs font-semibold text-text-secondary mb-1">
                      Lý do chênh lệch
                    </label>
                    <textarea
                      value={discrepancyReason}
                      onChange={(event) => setDiscrepancyReason(event.target.value)}
                      rows={3}
                      className="w-full px-3 py-2 border border-border rounded-lg text-xs outline-none focus:border-brand-orange text-text-primary bg-surface font-semibold resize-none"
                      placeholder="Ví dụ: thối nhầm tiền, bổ sung tiền lẻ, kiểm két thiếu..."
                      required
                    />
                  </div>
                )}

                <button
                  type="submit"
                  disabled={isSubmitting || actualEndingCash === '' || hasCloseBlockers || (needsReason && discrepancyReason.trim().length === 0)}
                  className="px-4 py-2.5 bg-brand-orange text-white text-xs font-bold rounded-lg cursor-pointer hover:bg-brand-orange-hover disabled:opacity-40 disabled:cursor-not-allowed active:scale-95 transition-all shadow-[var(--shadow-button)]"
                >
                  {isSubmitting ? 'Đang đóng ca...' : 'Xác nhận đóng ca'}
                </button>
              </form>
            ) : (
              <div className="bg-surface-white p-5 rounded-xl border border-border shadow-[var(--shadow-card)] space-y-3">
                <h2 className="text-xs font-bold text-text-primary uppercase tracking-wider">Kết quả đóng ca</h2>
                <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 text-xs">
                  <div>
                    <p className="text-text-secondary">Đóng ca lúc</p>
                    <p className="font-bold text-text-primary mt-1">{formatDateTime(shift?.endTime)}</p>
                  </div>
                  <div>
                    <p className="text-text-secondary">Thực tế bàn giao</p>
                    <p className="font-bold text-text-primary mt-1">{formatVND(shift?.actualEndingCash ?? 0)}</p>
                  </div>
                  <div>
                    <p className="text-text-secondary">Chênh lệch</p>
                    <p className={`font-bold mt-1 ${closedDiscrepancy < 0 ? 'text-danger' : closedDiscrepancy > 0 ? 'text-amber-700' : 'text-green-700'}`}>
                      {closedDiscrepancy > 0 ? '+' : ''}{formatVND(closedDiscrepancy)}
                    </p>
                    <p className={`text-[10px] font-extrabold uppercase mt-1 ${closedDiscrepancy < 0 ? 'text-danger' : closedDiscrepancy > 0 ? 'text-amber-700' : 'text-green-700'}`}>
                      {closedDiscrepancy < 0 ? 'Thiếu tiền' : closedDiscrepancy > 0 ? 'Thừa tiền' : 'Khớp két'}
                    </p>
                  </div>
                </div>
                <button
                  type="button"
                  onClick={loadCurrentShift}
                  className="px-4 py-2 border border-brand-orange text-brand-orange text-xs font-bold rounded-lg cursor-pointer hover:bg-brand-orange-light transition-all"
                >
                  Kiểm tra ca hiện tại
                </button>
              </div>
            )}
          </>
        )}
      </div>
    </div>
  )
}
