import { useCallback, useEffect, useMemo, useRef, useState, type FormEvent } from 'react'
import { useLiveQuery } from 'dexie-react-hooks'
import * as signalR from '@microsoft/signalr'
import { db, type CartSyncQueueItem } from '../db/CafeChainPOSDB'
import { API_BASE_URL, apiClient } from '../services/apiClient'
import {
  ACTIVE_PAYMENT_CLOSE_GUARD_CHANGED,
  getMatchingActivePaymentCloseGuard,
} from '../services/posShiftCloseGuard'
import { completeOpeningCash, getPosSession, getPosTerminalId } from '../services/posSession'
import {
  extractOtpEnvelope,
  formatCountdown,
  isOtpRequiredError,
  mapOtpUserMessage,
  OTP_ACTION_CASH_DIFFERENCE,
  OTP_ACTION_CLOSE_SHIFT_EXCEPTION,
  OTP_TARGET_SHIFTS,
  requestOtp,
  resendOtp,
  verifyOtp,
  type OtpChallengeData,
} from '../services/otpApprovalService'
import {
  isValidOperationalOtp,
  OPERATIONAL_OTP_INPUT_ERROR,
  sanitizeOperationalOtpInput,
} from '../utils/otpCode'
import { parseUtcInstant, parseUtcInstantMs } from '../utils/utcDateTime'

interface ShiftSummaryDto {
  shiftId?: number | null
  storeId?: number
  terminalId?: string | null
  staffName?: string | null
  responsibleStaffId?: number | null
  currentOperatorStaffId?: number | null
  currentOperatorStaffName?: string | null
  operatorChangedAtUtc?: string | null
  startTime?: string | null
  endTime?: string | null
  startTimeUtc?: string | null
  endTimeUtc?: string | null
  businessDate?: string | null
  openContext?: string | null
  autoCloseAtUtc?: string | null
  serverNowUtc?: string | null
  closeType?: string | null
  rowVersion?: string | null
  startingCash: number
  expectedEndingCash: number
  actualEndingCash?: number | null
  cashDiscrepancy?: number | null
  isExceptionClosed?: boolean
  exceptionCloseReason?: string | null
  exceptionClosedByStaffId?: number | null
  exceptionClosedAt?: string | null
  offlineOrderCountAtClose?: number | null
  offlineEstimatedTotalAtClose?: number | null
  offlineCashTotalAtClose?: number | null
  requiresReconciliation?: boolean
  hasLateOfflineSync?: boolean
  lateOfflineSyncCount?: number
  lastLateOfflineSyncedAt?: string | null
  totalCashSales: number
  totalBankingSales: number
  totalOrders: number
  status: 'OPEN' | 'CLOSING' | 'EXPIRED_PENDING_CLOSE' | 'CLOSED' | 'RECONCILIATION_REQUIRED' | 'NoActiveShift' | string
}

interface PosOperatorCandidateDto {
  staffId: number
  fullName: string
}

type ShiftActionResponse = Partial<ShiftSummaryDto> & {
  success?: boolean
  message?: string
  errorCode?: string
  recommendedAction?: string
  staffHubUrl?: string
}

const redirectToStaffHub = (terminalId?: string | null, serverUrl?: string | null) => {
  const target = new URL(serverUrl || '/StaffHub', API_BASE_URL)
  target.searchParams.set('openPos', '1')
  if (terminalId) target.searchParams.set('terminalId', terminalId)
  window.location.assign(target.toString())
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

const parseCloseErrorEnvelope = (response: { data: unknown; error?: string }) => {
  if (response.data && typeof response.data === 'object') {
    return response.data as Record<string, unknown>
  }

  const errorText = response.error?.trim()
  if (!errorText) return null

  try {
    const parsed = JSON.parse(errorText) as unknown
    if (parsed && typeof parsed === 'object') return parsed as Record<string, unknown>
  } catch {
    // fall through
  }

  return null
}

const formatVND = (amount: number): string =>
  new Intl.NumberFormat('vi-VN').format(amount) + 'đ'

const CASH_DENOMINATION_STEP = 1000

const formatCashInput = (amount: number | ''): string =>
  amount === '' ? '' : new Intl.NumberFormat('vi-VN').format(amount)

const validateActualEndingCash = (amount: number | ''): string | null => {
  if (amount === '') return 'Vui lòng nhập tiền mặt thực tế trong két.'
  if (!Number.isSafeInteger(amount) || amount < 0) {
    return 'Tiền mặt thực tế trong két phải là số nguyên không âm.'
  }
  if (amount % CASH_DENOMINATION_STEP !== 0) {
    return 'Tiền mặt thực tế trong két phải là bội số của 1.000đ.'
  }
  return null
}

const formatDateTime = (value?: string | null) => {
  const date = parseUtcInstant(value)
  if (!date) return '--'
  return date.toLocaleString('vi-VN', {
    hour: '2-digit',
    minute: '2-digit',
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  })
}

export default function ShiftSummary() {
  const [shift, setShift] = useState<ShiftSummaryDto | null>(null)
  const [startingCash, setStartingCash] = useState<number | ''>('')
  const [actualEndingCash, setActualEndingCash] = useState<number | ''>('')
  const [discrepancyReason, setDiscrepancyReason] = useState('')
  const [exceptionReason, setExceptionReason] = useState('')
  const [reconcileReason, setReconcileReason] = useState('')
  const [clockTick, setClockTick] = useState(0)
  const [serverOffsetMs, setServerOffsetMs] = useState(0)
  const authoritativeOpenedShiftIdRef = useRef<number | null>(getPosSession().workShiftId ?? null)
  const closeRequestKeyRef = useRef(crypto.randomUUID())
  const exceptionCloseRequestKeyRef = useRef(crypto.randomUUID())
  const reconcileRequestKeyRef = useRef(crypto.randomUUID())
  const [exceptionOtpChallengePublicId, setExceptionOtpChallengePublicId] = useState<string | null>(null)
  const [verifiedExceptionOtpId, setVerifiedExceptionOtpId] = useState<string | null>(null)
  const [exceptionOtpCode, setExceptionOtpCode] = useState('')
  const [exceptionOtpMessage, setExceptionOtpMessage] = useState<string | null>(null)
  const [exceptionOtpBusy, setExceptionOtpBusy] = useState(false)
  const [isLoading, setIsLoading] = useState(true)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null)
  const [guardVersion, setGuardVersion] = useState(0)
  const [operatorCandidates, setOperatorCandidates] = useState<PosOperatorCandidateDto[]>([])
  const [selectedOperatorId, setSelectedOperatorId] = useState<number | ''>('')
  const [operatorPin, setOperatorPin] = useState('')
  const [operatorBusy, setOperatorBusy] = useState(false)
  const [showOperatorPanel, setShowOperatorPanel] = useState(false)
  const operatorRequestKeyRef = useRef(crypto.randomUUID())

  // Issue #91 — OTP ca trưởng khi lệch két vượt ngưỡng
  const [showOtpPanel, setShowOtpPanel] = useState(false)
  const [otpChallengePublicId, setOtpChallengePublicId] = useState<string | null>(null)
  const [verifiedOtpChallengePublicId, setVerifiedOtpChallengePublicId] = useState<string | null>(null)
  const [otpCode, setOtpCode] = useState('')
  const [otpStatus, setOtpStatus] = useState<string | null>(null)
  const [otpMessage, setOtpMessage] = useState<string | null>(null)
  const [otpBusy, setOtpBusy] = useState(false)
  const [expiresInSeconds, setExpiresInSeconds] = useState(0)
  const [resendAvailableInSeconds, setResendAvailableInSeconds] = useState(0)
  const [remainingAttempts, setRemainingAttempts] = useState<number | null>(null)

  const localSyncBlockers = useLiveQuery(
    () => db.cartSyncQueue
      .where('syncStatus')
      .anyOf(['Pending', 'Syncing', 'Failed'])
      .toArray(),
    [],
    [] as CartSyncQueueItem[]
  )

  const session = getPosSession()
  const normalizedStatus = shift?.status?.toUpperCase()
  const boundWorkShiftMismatch = session.workShiftId != null
    && shift?.shiftId != null
    && session.workShiftId !== shift.shiftId
  const hasOpenShift = !boundWorkShiftMismatch
    && !!shift?.shiftId
    && ['OPEN', 'CLOSING', 'EXPIRED_PENDING_CLOSE'].includes(normalizedStatus ?? '')
  const requiresOpeningCash = session.requiresOpeningCash === true
    && session.workShiftId === shift?.shiftId
  const canAcceptTransactions = normalizedStatus === 'OPEN' && !requiresOpeningCash
  const currentShiftId = hasOpenShift && !requiresOpeningCash ? shift.shiftId ?? null : null
  const currentStaffId = session.staffId
  const currentStoreId = session.storeId
  const expiryAtMs = parseUtcInstantMs(shift?.autoCloseAtUtc)
  const expiryRemainingSeconds = Number.isFinite(expiryAtMs)
    ? Math.max(0, Math.ceil((expiryAtMs - (clockTick + serverOffsetMs)) / 1000))
    : null
  const expectedEndingCash = useMemo(() => {
    if (!shift) return 0
    return shift.startingCash + shift.totalCashSales
  }, [shift])
  const actualEndingCashError = validateActualEndingCash(actualEndingCash)
  const hasValidActualEndingCash = actualEndingCashError === null
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
  const offlineBlockerCount = pendingOfflineCount + syncingOfflineCount + failedOfflineCount
  const offlineEstimatedTotal = useMemo(
    () => shiftLocalSyncBlockers.reduce((sum, order) => sum + order.totalAmount, 0),
    [shiftLocalSyncBlockers]
  )
  const offlineCashTotal = useMemo(
    () => shiftLocalSyncBlockers.reduce((sum, order) => sum + (order.paymentSnapshot?.amount ?? order.totalAmount), 0),
    [shiftLocalSyncBlockers]
  )
  const hasOfflineQueueBlockers = offlineBlockerCount > 0
  const canUseExceptionClose = hasOfflineQueueBlockers && !activePaymentGuard
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
      if (response.status === 401) {
        redirectToStaffHub(getPosSession().terminalId)
        return
      }
      if (response.ok && response.data) {
        const authoritativeShiftId = authoritativeOpenedShiftIdRef.current
        if (authoritativeShiftId !== null
          && response.data.status?.toUpperCase() === 'OPEN'
          && response.data.shiftId !== authoritativeShiftId) {
          return
        }
        setShift(response.data)
        setClockTick(Date.now())
        const serverNowMs = parseUtcInstantMs(response.data.serverNowUtc)
        setServerOffsetMs(Number.isFinite(serverNowMs) ? serverNowMs - Date.now() : 0)
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
    if (!session.token) redirectToStaffHub(session.terminalId)
  }, [session.terminalId, session.token])

  useEffect(() => {
    queueMicrotask(() => {
      void loadCurrentShift()
    })
  }, [loadCurrentShift])

  useEffect(() => {
    if (!hasOpenShift) return

    let cancelled = false
    void apiClient.get<PosOperatorCandidateDto[]>('/api/v1/pos/shifts/operator/candidates')
      .then((response) => {
        if (!cancelled && response.ok && Array.isArray(response.data)) {
          setOperatorCandidates(response.data)
        }
      })
    return () => { cancelled = true }
  }, [hasOpenShift])

  const handleSwitchOperator = async (event: FormEvent) => {
    event.preventDefault()
    if (!shift?.shiftId || selectedOperatorId === '' || !/^\d{6}$/.test(operatorPin)) {
      setMessage({ type: 'error', text: 'Vui lòng chọn nhân viên và nhập đúng PIN gồm 6 chữ số.' })
      return
    }

    setOperatorBusy(true)
    try {
      const response = await apiClient.post<ShiftSummaryDto>(
        `/api/v1/pos/shifts/${shift.shiftId}/operator/switch`,
        {
          operatorStaffId: selectedOperatorId,
          pin: operatorPin,
          requestKey: operatorRequestKeyRef.current,
          rowVersion: shift.rowVersion,
        }
      )
      if (!response.ok || !response.data) {
        setMessage({
          type: 'error',
          text: getApiErrorMessage(response, 'Không thể đổi người thao tác POS. Vui lòng kiểm tra PIN và thử lại.'),
        })
        return
      }

      setShift(response.data)
      setMessage({ type: 'success', text: 'Đã đổi người thao tác POS thành công.' })
      setShowOperatorPanel(false)
      setSelectedOperatorId('')
      operatorRequestKeyRef.current = crypto.randomUUID()
    } catch (error) {
      setMessage({
        type: 'error',
        text: getUnexpectedErrorMessage(error, 'Không thể đổi người thao tác POS. Vui lòng thử lại.'),
      })
    } finally {
      setOperatorPin('')
      setOperatorBusy(false)
    }
  }

  useEffect(() => {
    let active = true
    let connection: signalR.HubConnection | null = null
    const refresh = () => { void loadCurrentShift() }
    window.addEventListener('focus', refresh)
    const pollingId = window.setInterval(refresh, 30_000)
    if (!session.token) {
      return () => {
        window.removeEventListener('focus', refresh)
        window.clearInterval(pollingId)
      }
    }

    queueMicrotask(() => {
      if (!active) return
      connection = new signalR.HubConnectionBuilder()
        .withUrl(`${API_BASE_URL}/hubs/workshifts`, {
          accessTokenFactory: () => session.token ?? '',
        })
        .withAutomaticReconnect([0, 2_000, 5_000, 10_000, 30_000])
        .build()
      connection.on('WorkShiftChanged', (notification: { storeId?: number }) => {
        if (notification.storeId && session.storeId && notification.storeId !== session.storeId) return
        refresh()
      })
      connection.onreconnected(() => {
        const terminalId = getPosTerminalId()
        if (terminalId) void connection?.invoke('JoinTerminal', terminalId)
        refresh()
      })
      connection.start()
        .then(() => {
          if (!active) return undefined
          const terminalId = getPosTerminalId()
          return terminalId ? connection?.invoke('JoinTerminal', terminalId) : undefined
        })
        .catch((error) => {
          if (active) {
            console.warn('[ShiftSummary WorkShift SignalR] Polling fallback active.', error)
          }
        })
    })

    return () => {
      active = false
      window.removeEventListener('focus', refresh)
      window.clearInterval(pollingId)
      if (connection) void connection.stop()
    }
  }, [loadCurrentShift, session.storeId, session.token])

  useEffect(() => {
    if (!shift?.autoCloseAtUtc) return
    const intervalId = window.setInterval(() => setClockTick(Date.now()), 1000)
    return () => window.clearInterval(intervalId)
  }, [shift?.autoCloseAtUtc])

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

  // Local display timers only — backend remains source of truth on each API response.
  useEffect(() => {
    if (!otpChallengePublicId) return

    const intervalId = window.setInterval(() => {
      setExpiresInSeconds((value) => (value > 0 ? value - 1 : 0))
      setResendAvailableInSeconds((value) => (value > 0 ? value - 1 : 0))
    }, 1000)

    return () => window.clearInterval(intervalId)
  }, [otpChallengePublicId])

  const returnToStaffHub = useCallback(() => {
    window.location.href = `${API_BASE_URL}/StaffHub/Index`
  }, [])

  const resetOtpState = useCallback(() => {
    setShowOtpPanel(false)
    setOtpChallengePublicId(null)
    setVerifiedOtpChallengePublicId(null)
    setOtpCode('')
    setOtpStatus(null)
    setOtpMessage(null)
    setOtpBusy(false)
    setExpiresInSeconds(0)
    setResendAvailableInSeconds(0)
    setRemainingAttempts(null)
  }, [])

  const applyOtpChallengeData = useCallback((data: OtpChallengeData | null | undefined) => {
    if (!data) return

    if (data.otpChallengePublicId) {
      setOtpChallengePublicId(data.otpChallengePublicId)
    }
    if (data.status) {
      setOtpStatus(data.status)
    }
    if (typeof data.expiresInSeconds === 'number') {
      setExpiresInSeconds(Math.max(0, data.expiresInSeconds))
    }
    if (typeof data.resendAvailableInSeconds === 'number') {
      setResendAvailableInSeconds(Math.max(0, data.resendAvailableInSeconds))
    }
    if (typeof data.remainingAttempts === 'number') {
      setRemainingAttempts(data.remainingAttempts)
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
      })

      if (response.ok && response.data?.status?.toUpperCase() === 'OPEN') {
        if (!response.data.shiftId) {
          setMessage({ type: 'error', text: 'Mở ca thành công nhưng API không trả về WorkShiftId.' })
          return
        }
        authoritativeOpenedShiftIdRef.current = response.data.shiftId
        completeOpeningCash(response.data.shiftId)
        setShift(response.data as ShiftSummaryDto)
        setClockTick(Date.now())
        const responseServerNowMs = parseUtcInstantMs(response.data.serverNowUtc)
        setServerOffsetMs(Number.isFinite(responseServerNowMs) ? responseServerNowMs - Date.now() : 0)
        setStartingCash('')
        setMessage({ type: 'success', text: 'Mở ca thành công.' })
      } else {
        const errorCode = response.data?.errorCode
        if (errorCode === 'STAFFHUB_OPEN_REQUIRED'
          || errorCode === 'POS_OPEN_CONTEXT_REQUIRED'
          || errorCode === 'POS_OPEN_CONTEXT_INVALID'
          || response.data?.recommendedAction === 'OPEN_STAFFHUB') {
          redirectToStaffHub(session.terminalId, response.data?.staffHubUrl)
          return
        }
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

  const submitCloseShift = useCallback(async (otpPublicId?: string | null) => {
    if (!shift?.shiftId) return false
    if (!hasValidActualEndingCash || actualEndingCash === '') {
      setMessage({
        type: 'error',
        text: actualEndingCashError ?? 'Tiền mặt thực tế trong két không hợp lệ.',
      })
      return false
    }
    if (hasCloseBlockers) {
      setMessage({
        type: 'error',
        text: 'Không thể đóng ca thường. Vui lòng xử lý các lý do trên trước khi đóng ca thường.',
      })
      return false
    }
    if (needsReason && discrepancyReason.trim().length === 0) return false

    setIsSubmitting(true)
    setMessage(null)
    try {
      const response = await apiClient.post<ShiftActionResponse>(`/api/v1/pos/shifts/${shift.shiftId}/close`, {
        requestKey: closeRequestKeyRef.current,
        rowVersion: shift.rowVersion ?? null,
        actualEndingCash,
        discrepancyReason: discrepancyReason.trim() || null,
        offlineQueueSummary: {
          offlineOrderCount: offlineBlockerCount,
          estimatedTotal: offlineEstimatedTotal,
          localCashTotal: offlineCashTotal,
        },
        ...(otpPublicId ? { otpChallengePublicId: otpPublicId } : {}),
      })

      if (response.ok && response.data?.status?.toUpperCase() === 'CLOSED') {
        setShift(response.data as ShiftSummaryDto)
        closeRequestKeyRef.current = crypto.randomUUID()
        setActualEndingCash('')
        setDiscrepancyReason('')
        resetOtpState()
        setMessage({ type: 'success', text: 'Đóng ca thành công.' })
        return true
      }

      if (isOtpRequiredError(response)) {
        setShowOtpPanel(true)
        setOtpMessage(
          'Chênh lệch két vượt ngưỡng cho phép. Vui lòng gửi OTP cho ca trưởng để xác nhận.'
        )
        setMessage({
          type: 'error',
          text: 'Cần OTP ca trưởng để đóng ca do chênh lệch két vượt ngưỡng.',
        })
        return false
      }

      const envelope = parseCloseErrorEnvelope(response)
      const apiMessage =
        (typeof envelope?.message === 'string' && envelope.message) ||
        getApiErrorMessage(response, 'Không thể đóng ca.')

      // Keep verified OTP unless backend clearly rejected the challenge.
      const lower = apiMessage.toLowerCase()
      if (
        lower.includes('otp') &&
        (lower.includes('hết hạn') ||
          lower.includes('không hợp lệ') ||
          lower.includes('không tìm thấy') ||
          lower.includes('đã sử dụng') ||
          lower.includes('bị khóa') ||
          lower.includes('chưa được duyệt'))
      ) {
        setVerifiedOtpChallengePublicId(null)
        setOtpMessage(mapOtpUserMessage(apiMessage, apiMessage))
      }

      setMessage({ type: 'error', text: apiMessage })
      return false
    } catch (error) {
      setMessage({
        type: 'error',
        text: getUnexpectedErrorMessage(error, 'Không thể đóng ca.'),
      })
      return false
    } finally {
      setIsSubmitting(false)
    }
  }, [
    actualEndingCash,
    actualEndingCashError,
    discrepancyReason,
    hasCloseBlockers,
    hasValidActualEndingCash,
    needsReason,
    offlineBlockerCount,
    offlineCashTotal,
    offlineEstimatedTotal,
    resetOtpState,
    shift,
  ])

  const handleCloseShift = async (event: FormEvent) => {
    event.preventDefault()
    await submitCloseShift(verifiedOtpChallengePublicId)
  }

  const handleRequestOtp = async () => {
    if (!shift?.shiftId) return
    if (!hasValidActualEndingCash || actualEndingCash === '') {
      setOtpMessage(actualEndingCashError ?? 'Tiền mặt thực tế trong két không hợp lệ.')
      return
    }
    if (needsReason && discrepancyReason.trim().length === 0) {
      setOtpMessage('Vui lòng nhập lý do chênh lệch trước khi gửi OTP.')
      return
    }

    setOtpBusy(true)
    setOtpMessage(null)
    try {
      const response = await requestOtp({
        actionType: OTP_ACTION_CASH_DIFFERENCE,
        targetType: OTP_TARGET_SHIFTS,
        targetId: shift.shiftId,
        workShiftId: shift.shiftId,
        reason: discrepancyReason.trim() || 'Chênh lệch két vượt ngưỡng khi đóng ca.',
        actualEndingCash: Number(actualEndingCash),
        oldValueJson: JSON.stringify({ expectedEndingCash }),
        newValueJson: JSON.stringify({
          actualEndingCash,
          cashDiscrepancy,
        }),
      })

      const envelope = extractOtpEnvelope(response)
      if (response.ok && envelope.data?.otpChallengePublicId) {
        applyOtpChallengeData(envelope.data)
        setVerifiedOtpChallengePublicId(null)
        setOtpCode('')
        setOtpMessage(envelope.message || 'OTP đã được gửi đến email ca trưởng.')
        setMessage({ type: 'success', text: 'Đã gửi OTP cho ca trưởng.' })
      } else {
        applyOtpChallengeData(envelope.data)
        if (response.error || envelope.message) {
          console.warn('[ShiftSummary] OTP request failed', {
            status: response.status,
            error: response.error,
            message: envelope.message,
          })
        }
        setOtpMessage(
          mapOtpUserMessage(
            envelope.message ?? response.error,
            'Không gửi được OTP ca trưởng. Vui lòng kiểm tra cấu hình hệ thống hoặc cơ sở dữ liệu.',
            { status: response.status, operation: 'request' }
          )
        )
      }
    } catch (error) {
      console.warn('[ShiftSummary] OTP request unexpected error', error)
      setOtpMessage(
        'Không gửi được OTP ca trưởng. Vui lòng kiểm tra cấu hình hệ thống hoặc cơ sở dữ liệu.'
      )
    } finally {
      setOtpBusy(false)
    }
  }

  const handleResendOtp = async () => {
    if (!otpChallengePublicId) return
    if (resendAvailableInSeconds > 0) {
      setOtpMessage('Vui lòng chờ trước khi gửi lại OTP.')
      return
    }

    setOtpBusy(true)
    setOtpMessage(null)
    try {
      const response = await resendOtp({ otpChallengePublicId })
      const envelope = extractOtpEnvelope(response)

      if (response.ok && envelope.data) {
        applyOtpChallengeData(envelope.data)
        setVerifiedOtpChallengePublicId(null)
        setOtpCode('')
        setOtpMessage(envelope.message || 'OTP mới đã được gửi đến email ca trưởng.')
        setMessage({ type: 'success', text: 'Đã gửi lại OTP cho ca trưởng.' })
      } else {
        applyOtpChallengeData(envelope.data)
        if (response.error || envelope.message) {
          console.warn('[ShiftSummary] OTP resend failed', {
            status: response.status,
            error: response.error,
            message: envelope.message,
          })
        }
        setOtpMessage(
          mapOtpUserMessage(
            envelope.message ?? response.error,
            'Không thể gửi lại OTP. Vui lòng thử lại.',
            { status: response.status, operation: 'resend' }
          )
        )
      }
    } catch (error) {
      console.warn('[ShiftSummary] OTP resend unexpected error', error)
      setOtpMessage(
        'Không gửi được OTP ca trưởng. Vui lòng kiểm tra cấu hình hệ thống hoặc cơ sở dữ liệu.'
      )
    } finally {
      setOtpBusy(false)
    }
  }

  const handleVerifyOtp = async () => {
    if (!otpChallengePublicId) {
      setOtpMessage('Vui lòng gửi OTP cho ca trưởng trước.')
      return
    }
    const normalizedOtp = otpCode.trim().toUpperCase()
    if (!isValidOperationalOtp(normalizedOtp)) {
      setOtpMessage(OPERATIONAL_OTP_INPUT_ERROR)
      return
    }

    setOtpBusy(true)
    setOtpMessage(null)
    try {
      const response = await verifyOtp({
        otpChallengePublicId,
        otpCode: normalizedOtp,
      })
      const envelope = extractOtpEnvelope(response)

      if (response.ok && envelope.data?.otpChallengePublicId) {
        applyOtpChallengeData(envelope.data)
        const publicId = envelope.data.otpChallengePublicId
        setVerifiedOtpChallengePublicId(publicId)
        setOtpStatus(envelope.data.status || 'Approved')
        setOtpMessage(envelope.message || 'Xác nhận OTP thành công. Đang đóng ca...')
        setMessage({ type: 'success', text: 'Xác nhận OTP thành công. Đang đóng ca...' })

        // Resubmit close with verified challenge — do not close before verify.
        await submitCloseShift(publicId)
      } else {
        applyOtpChallengeData(envelope.data)
        if (response.error || envelope.message) {
          console.warn('[ShiftSummary] OTP verify failed', {
            status: response.status,
            error: response.error,
            message: envelope.message,
          })
        }
        const mapped = mapOtpUserMessage(
          envelope.message ?? response.error,
          'Không thể xác nhận OTP. Vui lòng thử lại.',
          { status: response.status, operation: 'verify' }
        )
        setOtpMessage(mapped)
        if (envelope.data?.status?.toLowerCase() === 'locked') {
          setOtpStatus('Locked')
        }
        if (envelope.data?.remainingAttempts != null) {
          setRemainingAttempts(envelope.data.remainingAttempts)
        }
      }
    } catch (error) {
      console.warn('[ShiftSummary] OTP verify unexpected error', error)
      setOtpMessage('Không thể xác nhận OTP. Vui lòng thử lại.')
    } finally {
      setOtpBusy(false)
    }
  }

  const requestExceptionOtp = async () => {
    if (!shift?.shiftId) return
    if (!hasValidActualEndingCash || actualEndingCash === '') {
      setExceptionOtpMessage(actualEndingCashError ?? 'Tiền mặt thực tế trong két không hợp lệ.')
      return
    }
    if (exceptionReason.trim().length === 0) {
      setExceptionOtpMessage('Vui lòng nhập lý do đóng ngoại lệ trước khi gửi OTP.')
      return
    }
    if (!navigator.onLine) {
      setExceptionOtpMessage('Cần kết nối mạng để gửi và xác nhận OTP của người phê duyệt.')
      return
    }
    setExceptionOtpBusy(true)
    setExceptionOtpMessage(null)
    try {
      const response = await requestOtp({
        actionType: OTP_ACTION_CLOSE_SHIFT_EXCEPTION,
        targetType: OTP_TARGET_SHIFTS,
        targetId: shift.shiftId,
        workShiftId: shift.shiftId,
        reason: exceptionReason.trim(),
        exceptionReason: exceptionReason.trim(),
        discrepancyReason: discrepancyReason.trim() || null,
        actualEndingCash: Number(actualEndingCash),
        offlineQueueSummary: {
          offlineOrderCount: offlineBlockerCount,
          estimatedTotal: offlineEstimatedTotal,
          localCashTotal: offlineCashTotal,
        },
      })
      const envelope = extractOtpEnvelope(response)
      if (response.ok && envelope.data?.otpChallengePublicId) {
        setExceptionOtpChallengePublicId(envelope.data.otpChallengePublicId)
        setVerifiedExceptionOtpId(null)
        setExceptionOtpCode('')
        setExceptionOtpMessage(envelope.message || 'OTP đã được gửi đến người phê duyệt.')
      } else {
        setExceptionOtpMessage(
          mapOtpUserMessage(envelope.message ?? response.error, 'Không gửi được OTP. Kiểm tra mạng/cấu hình.')
        )
      }
    } catch {
      setExceptionOtpMessage('Cần kết nối mạng để gửi và xác nhận OTP của người phê duyệt.')
    } finally {
      setExceptionOtpBusy(false)
    }
  }

  const verifyExceptionOtp = async () => {
    if (!exceptionOtpChallengePublicId) return
    const normalized = exceptionOtpCode.trim().toUpperCase()
    if (!isValidOperationalOtp(normalized)) {
      setExceptionOtpMessage(OPERATIONAL_OTP_INPUT_ERROR)
      return
    }
    setExceptionOtpBusy(true)
    try {
      const response = await verifyOtp({
        otpChallengePublicId: exceptionOtpChallengePublicId,
        otpCode: normalized,
      })
      const envelope = extractOtpEnvelope(response)
      if (response.ok && envelope.data?.otpChallengePublicId) {
        setVerifiedExceptionOtpId(envelope.data.otpChallengePublicId)
        setExceptionOtpMessage(envelope.message || 'OTP đã xác nhận. Có thể đóng ca ngoại lệ.')
      } else {
        setExceptionOtpMessage(mapOtpUserMessage(envelope.message ?? response.error, 'Không xác nhận được OTP.'))
      }
    } finally {
      setExceptionOtpBusy(false)
    }
  }

  const handleExceptionCloseShift = async () => {
    if (!shift?.shiftId) return
    if (!hasValidActualEndingCash || actualEndingCash === '') {
      setMessage({
        type: 'error',
        text: actualEndingCashError ?? 'Tiền mặt thực tế trong két không hợp lệ.',
      })
      return
    }
    if (!hasOfflineQueueBlockers) {
      setMessage({
        type: 'error',
        text: 'Đóng ca ngoại lệ chỉ dùng khi còn đơn offline chưa đồng bộ trong ca này.',
      })
      return
    }
    if (activePaymentGuard) {
      setMessage({
        type: 'error',
        text: 'Không thể đóng ca ngoại lệ khi còn giao dịch thanh toán chưa hoàn tất.',
      })
      return
    }
    if (needsReason && discrepancyReason.trim().length === 0) return
    if (exceptionReason.trim().length === 0) return
    if (!verifiedExceptionOtpId) {
      setMessage({ type: 'error', text: 'Vui lòng gửi và xác nhận OTP phê duyệt (online) trước khi đóng ca ngoại lệ.' })
      return
    }
    if (!navigator.onLine) {
      setMessage({ type: 'error', text: 'Cần kết nối mạng để gửi và xác nhận OTP của người phê duyệt.' })
      return
    }

    setIsSubmitting(true)
    setMessage(null)
    try {
      const response = await apiClient.post<ShiftActionResponse>(`/api/v1/pos/shifts/${shift.shiftId}/close-exception`, {
        requestKey: exceptionCloseRequestKeyRef.current,
        rowVersion: shift.rowVersion ?? null,
        actualEndingCash,
        discrepancyReason: discrepancyReason.trim() || null,
        exceptionReason: exceptionReason.trim(),
        otpChallengePublicId: verifiedExceptionOtpId,
        offlineQueueSummary: {
          offlineOrderCount: offlineBlockerCount,
          estimatedTotal: offlineEstimatedTotal,
          localCashTotal: offlineCashTotal,
        },
      })

      if (response.ok && ['RECONCILIATION_REQUIRED', 'CLOSED'].includes(response.data?.status?.toUpperCase() ?? '')) {
        setShift(response.data as ShiftSummaryDto)
        exceptionCloseRequestKeyRef.current = crypto.randomUUID()
        setActualEndingCash('')
        setDiscrepancyReason('')
        setExceptionReason('')
        setExceptionOtpChallengePublicId(null)
        setVerifiedExceptionOtpId(null)
        setExceptionOtpCode('')
        setMessage({ type: 'success', text: 'Đóng ca ngoại lệ thành công. Ca cần đối soát lại sau khi đồng bộ offline.' })
      } else {
        setMessage({
          type: 'error',
          text: getApiErrorMessage(response, 'Không thể đóng ca ngoại lệ.'),
        })
      }
    } catch (error) {
      setMessage({
        type: 'error',
        text: getUnexpectedErrorMessage(error, 'Không thể đóng ca ngoại lệ.'),
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
              : normalizedStatus === 'CLOSED' || normalizedStatus === 'RECONCILIATION_REQUIRED'
                ? 'bg-gray-50 text-gray-700 border-gray-200'
                : 'bg-brand-orange-light text-brand-orange border-brand-orange-border'
          }`}>
            {normalizedStatus === 'OPEN'
              ? 'Đang mở'
              : normalizedStatus === 'CLOSING'
                ? 'Đang chốt két'
                : normalizedStatus === 'EXPIRED_PENDING_CLOSE'
                  ? 'Hết hạn — chờ chốt két'
                  : normalizedStatus === 'RECONCILIATION_REQUIRED'
                    ? 'Cần đối soát lại'
                    : normalizedStatus === 'CLOSED'
                      ? 'Đã đóng'
                      : 'Chưa mở phiên POS'}
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

        {boundWorkShiftMismatch && (
          <div className="rounded-xl border border-red-200 bg-red-50 p-4 text-xs font-bold text-red-700">
            WorkShift hiện tại không khớp phiên được StaffHub cấp. Vui lòng quay lại StaffHub để mở lại đúng terminal.
          </div>
        )}

        {hasOpenShift && shift?.autoCloseAtUtc && expiryRemainingSeconds !== null && (
          <div className={`p-4 rounded-xl border text-xs font-bold ${
            canAcceptTransactions && expiryRemainingSeconds > 600
              ? 'bg-blue-50 border-blue-200 text-blue-700'
              : canAcceptTransactions && expiryRemainingSeconds > 0
                ? 'bg-amber-50 border-amber-200 text-amber-800'
                : 'bg-red-50 border-red-200 text-red-700'
          }`}>
            {canAcceptTransactions && expiryRemainingSeconds > 0
              ? `Phiên POS ngoài lịch còn ${formatCountdown(expiryRemainingSeconds)} — tự ngừng nhận giao dịch lúc ${formatDateTime(shift.autoCloseAtUtc)}.`
              : 'Phiên POS đã hết hạn và không nhận giao dịch mới. Vui lòng kiểm đếm, chốt két hoặc đóng ngoại lệ.'}
          </div>
        )}

        {isLoading ? (
          <div className="bg-surface-white p-12 rounded-xl border border-border text-center text-xs font-semibold text-text-muted">
            Đang tải dữ liệu ca...
          </div>
        ) : (requiresOpeningCash || !hasOpenShift)
          && normalizedStatus !== 'CLOSED'
          && normalizedStatus !== 'RECONCILIATION_REQUIRED' ? (
          <form onSubmit={handleOpenShift} className="bg-surface-white p-5 rounded-xl border border-border shadow-[var(--shadow-card)] space-y-4">
            <h2 className="text-xs font-bold text-text-primary uppercase tracking-wider">
              {requiresOpeningCash ? 'Xác nhận tiền đầu phiên' : 'Mở phiên POS'}
            </h2>
            <p className="text-xs text-text-secondary">
              Việc mở POS chỉ tạo phiên chịu trách nhiệm POS/két; hệ thống không tạo lịch làm việc hoặc dữ liệu chấm công.
            </p>
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
              <div className="grid grid-cols-1 sm:grid-cols-4 gap-4 text-xs">
                <div>
                  <p className="text-text-secondary">Nhân viên chịu trách nhiệm</p>
                  <p className="font-bold text-text-primary mt-1">{shift?.staffName || session.staffName}</p>
                </div>
                <div>
                  <p className="text-text-secondary">Người đang thao tác</p>
                  <p className="font-bold text-brand-orange mt-1">
                    {shift?.currentOperatorStaffName || shift?.staffName || session.staffName}
                  </p>
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
              {normalizedStatus === 'OPEN' && (
                <div className="border-t border-border pt-3">
                  {!showOperatorPanel ? (
                    <button type="button" onClick={() => setShowOperatorPanel(true)}
                      className="px-3 py-2 rounded-lg border border-border text-xs font-bold text-text-primary hover:border-brand-orange">
                      Đổi người thao tác
                    </button>
                  ) : (
                    <form onSubmit={handleSwitchOperator} className="grid grid-cols-1 sm:grid-cols-[1fr_180px_auto_auto] gap-2 items-end">
                      <label className="text-xs font-semibold text-text-secondary">
                        Nhân viên
                        <select value={selectedOperatorId}
                          onChange={(event) => setSelectedOperatorId(event.target.value ? Number(event.target.value) : '')}
                          disabled={operatorBusy}
                          className="mt-1 w-full rounded-lg border border-border bg-white px-3 py-2 text-text-primary" required>
                          <option value="">Chọn người thao tác</option>
                          {operatorCandidates.map((candidate) => (
                            <option key={candidate.staffId} value={candidate.staffId}>{candidate.fullName}</option>
                          ))}
                        </select>
                      </label>
                      <label className="text-xs font-semibold text-text-secondary">
                        PIN cá nhân
                        <input type="password" inputMode="numeric" autoComplete="off" maxLength={6} pattern="[0-9]{6}"
                          value={operatorPin}
                          onChange={(event) => setOperatorPin(event.target.value.replace(/\D/g, '').slice(0, 6))}
                          disabled={operatorBusy}
                          className="mt-1 w-full rounded-lg border border-border px-3 py-2 text-text-primary" required />
                      </label>
                      <button type="submit" disabled={operatorBusy || selectedOperatorId === '' || operatorPin.length !== 6}
                        className="px-3 py-2 rounded-lg bg-brand-orange text-white text-xs font-bold disabled:opacity-40">
                        {operatorBusy ? 'Đang xác thực...' : 'Xác nhận'}
                      </button>
                      <button type="button" disabled={operatorBusy}
                        onClick={() => { setShowOperatorPanel(false); setOperatorPin(''); setSelectedOperatorId('') }}
                        className="px-3 py-2 rounded-lg border border-border text-xs font-bold">
                        Hủy
                      </button>
                    </form>
                  )}
                </div>
              )}
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
                {hasOfflineQueueBlockers && (
                  <div className="rounded-xl border border-amber-200 bg-amber-50 p-4 text-xs text-amber-800 space-y-3">
                    <div className="flex flex-col gap-1 sm:flex-row sm:items-start sm:justify-between">
                      <div>
                        <p className="font-extrabold text-text-primary">Đóng ca ngoại lệ</p>
                        <p className="mt-1 font-semibold">
                          Dành cho ca mất mạng lâu, còn đơn offline chưa đồng bộ. Các đơn này vẫn giữ WorkShift #{currentShiftId} và sẽ cần đối soát lại sau khi sync.
                        </p>
                      </div>
                      <span className="shrink-0 rounded-full border border-amber-300 bg-white px-3 py-1 font-extrabold text-amber-700">
                        Cần đối soát lại
                      </span>
                    </div>

                    <div className="grid grid-cols-1 sm:grid-cols-3 gap-2">
                      <div className="rounded-lg bg-white/75 p-3 border border-amber-100">
                        <p className="text-[10px] uppercase font-extrabold text-text-secondary">Offline chưa sync</p>
                        <p className="mt-1 text-base font-extrabold text-text-primary">{offlineBlockerCount} đơn</p>
                      </div>
                      <div className="rounded-lg bg-white/75 p-3 border border-amber-100">
                        <p className="text-[10px] uppercase font-extrabold text-text-secondary">Tổng ước tính</p>
                        <p className="mt-1 text-base font-extrabold text-text-primary">{formatVND(offlineEstimatedTotal)}</p>
                      </div>
                      <div className="rounded-lg bg-white/75 p-3 border border-amber-100">
                        <p className="text-[10px] uppercase font-extrabold text-text-secondary">Tiền mặt local</p>
                        <p className="mt-1 text-base font-extrabold text-text-primary">{formatVND(offlineCashTotal)}</p>
                      </div>
                    </div>

                    {activePaymentGuard && (
                      <p className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 font-bold text-red-700">
                        Không thể đóng ca ngoại lệ khi còn giao dịch thanh toán chưa hoàn tất.
                      </p>
                    )}

                    <div className="space-y-3">
                      <div>
                        <label className="block text-xs font-semibold text-text-secondary mb-1">
                          Lý do đóng ngoại lệ
                        </label>
                        <textarea
                          value={exceptionReason}
                          onChange={(event) => {
                            setExceptionReason(event.target.value)
                            setVerifiedExceptionOtpId(null)
                          }}
                          rows={3}
                          className="w-full px-3 py-2 border border-amber-200 rounded-lg text-xs outline-none focus:border-brand-orange text-text-primary bg-white font-semibold resize-none"
                          placeholder="Ví dụ: mất mạng kéo dài, còn đơn offline chưa đồng bộ..."
                          disabled={!canUseExceptionClose || isSubmitting}
                        />
                      </div>
                      <p className="font-semibold text-amber-800">
                        Cần kết nối mạng để gửi và xác nhận OTP của người phê duyệt. Không dùng PIN cố định.
                      </p>
                      <div className="flex flex-wrap gap-2">
                        <button
                          type="button"
                          onClick={requestExceptionOtp}
                          disabled={exceptionOtpBusy || !canUseExceptionClose || !hasValidActualEndingCash || exceptionReason.trim().length === 0}
                          className="px-3 py-2 bg-white border border-amber-300 text-amber-900 text-xs font-bold rounded-lg disabled:opacity-40"
                        >
                          {exceptionOtpBusy ? 'Đang gửi OTP...' : 'Gửi OTP phê duyệt'}
                        </button>
                        <input
                          type="text"
                          maxLength={6}
                          autoCapitalize="characters"
                          autoComplete="one-time-code"
                          value={exceptionOtpCode}
                          onChange={(event) => {
                            const sanitized = sanitizeOperationalOtpInput(event.target.value)
                            setExceptionOtpCode(sanitized.value)
                            if (sanitized.rejected) setExceptionOtpMessage(OPERATIONAL_OTP_INPUT_ERROR)
                          }}
                          className="w-28 px-3 py-2 border border-amber-200 rounded-lg text-xs font-extrabold tracking-widest uppercase"
                          placeholder="OTP"
                          disabled={!exceptionOtpChallengePublicId || exceptionOtpBusy}
                        />
                        <button
                          type="button"
                          onClick={verifyExceptionOtp}
                          disabled={!exceptionOtpChallengePublicId || exceptionOtpBusy || !isValidOperationalOtp(exceptionOtpCode)}
                          className="px-3 py-2 bg-amber-100 border border-amber-300 text-amber-900 text-xs font-bold rounded-lg disabled:opacity-40"
                        >
                          Xác nhận OTP
                        </button>
                      </div>
                      {exceptionOtpMessage && (
                        <p className="text-xs font-semibold text-amber-900">{exceptionOtpMessage}</p>
                      )}
                      <p className="font-semibold text-amber-700">
                        Đơn offline không bị xóa khỏi máy này và sẽ sync lại vào WorkShift cũ.
                      </p>
                    </div>

                    <button
                      type="button"
                      onClick={handleExceptionCloseShift}
                      disabled={
                        isSubmitting ||
                        !hasValidActualEndingCash ||
                        !canUseExceptionClose ||
                        exceptionReason.trim().length === 0 ||
                        !verifiedExceptionOtpId ||
                        (needsReason && discrepancyReason.trim().length === 0)
                      }
                      className="px-4 py-2.5 bg-amber-600 text-white text-xs font-bold rounded-lg cursor-pointer hover:bg-amber-700 disabled:opacity-40 disabled:cursor-not-allowed active:scale-95 transition-all"
                    >
                      {isSubmitting ? 'Đang đóng ca ngoại lệ...' : 'Đóng ca ngoại lệ'}
                    </button>
                  </div>
                )}
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <div>
                    <label className="block text-xs font-semibold text-text-secondary mb-1">
                      Tiền mặt thực tế trong két
                    </label>
                    <div className="relative">
                      <input
                        type="text"
                        inputMode="numeric"
                        value={formatCashInput(actualEndingCash)}
                        onChange={(event) => {
                          const digits = event.target.value.replace(/\D/g, '').replace(/^0+(?=\d)/, '')
                          const nextValue = digits === '' ? '' : Number(digits)
                          if (nextValue !== actualEndingCash) {
                            resetOtpState()
                            setExceptionOtpChallengePublicId(null)
                            setVerifiedExceptionOtpId(null)
                            setExceptionOtpCode('')
                            setExceptionOtpMessage(null)
                          }
                          setActualEndingCash(nextValue)
                        }}
                        placeholder="Nhập số tiền đếm được"
                        aria-invalid={actualEndingCash !== '' && !!actualEndingCashError}
                        aria-describedby={actualEndingCash !== '' && actualEndingCashError ? 'actual-ending-cash-error' : undefined}
                        className={`w-full py-2 pl-3 pr-16 border rounded-lg text-xs outline-none text-text-primary bg-surface font-semibold ${
                          actualEndingCash !== '' && actualEndingCashError
                            ? 'border-red-400 focus:border-red-500'
                            : 'border-border focus:border-brand-orange'
                        }`}
                      />
                      <span className="pointer-events-none absolute inset-y-0 right-3 flex items-center text-xs text-text-secondary font-bold">
                        VNĐ
                      </span>
                    </div>
                    {actualEndingCash !== '' && actualEndingCashError && (
                      <p id="actual-ending-cash-error" className="mt-1.5 text-xs font-semibold text-danger" role="alert">
                        {actualEndingCashError}
                      </p>
                    )}
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

                {showOtpPanel && (
                  <div className="rounded-xl border border-violet-200 bg-violet-50 p-4 text-xs text-violet-900 space-y-3">
                    <div className="flex flex-col gap-1 sm:flex-row sm:items-start sm:justify-between">
                      <div>
                        <p className="font-extrabold text-text-primary text-sm">Cần OTP ca trưởng</p>
                        <p className="mt-1 font-semibold text-violet-800">
                          Chênh lệch két vượt ngưỡng cho phép. Vui lòng gửi OTP cho ca trưởng để xác nhận.
                        </p>
                      </div>
                      <span className="shrink-0 rounded-full border border-violet-300 bg-white px-3 py-1 font-extrabold text-violet-700">
                        OTP bắt buộc
                      </span>
                    </div>

                    <div className="grid grid-cols-1 sm:grid-cols-3 gap-2">
                      <div className="rounded-lg bg-white/80 p-3 border border-violet-100">
                        <p className="text-[10px] uppercase font-extrabold text-text-secondary">Chênh lệch</p>
                        <p className={`mt-1 text-base font-extrabold ${
                          cashDiscrepancy < 0 ? 'text-danger' : cashDiscrepancy > 0 ? 'text-amber-700' : 'text-text-primary'
                        }`}>
                          {cashDiscrepancy > 0 ? '+' : ''}{formatVND(cashDiscrepancy)}
                        </p>
                        <p className="mt-0.5 text-[10px] font-extrabold uppercase text-text-secondary">
                          {discrepancyLabel}
                        </p>
                      </div>
                      <div className="rounded-lg bg-white/80 p-3 border border-violet-100">
                        <p className="text-[10px] uppercase font-extrabold text-text-secondary">Hết hạn OTP</p>
                        <p className="mt-1 text-base font-extrabold text-text-primary">
                          {otpChallengePublicId ? formatCountdown(expiresInSeconds) : '--:--'}
                        </p>
                        {expiresInSeconds === 0 && otpChallengePublicId && (
                          <p className="mt-0.5 text-[10px] font-bold text-danger">OTP đã hết hạn.</p>
                        )}
                      </div>
                      <div className="rounded-lg bg-white/80 p-3 border border-violet-100">
                        <p className="text-[10px] uppercase font-extrabold text-text-secondary">Gửi lại sau</p>
                        <p className="mt-1 text-base font-extrabold text-text-primary">
                          {otpChallengePublicId ? formatCountdown(resendAvailableInSeconds) : '--:--'}
                        </p>
                        {remainingAttempts != null && (
                          <p className="mt-0.5 text-[10px] font-bold text-text-secondary">
                            Còn {remainingAttempts} lần thử
                          </p>
                        )}
                      </div>
                    </div>

                    {otpMessage && (
                      <p className="rounded-lg border border-violet-200 bg-white px-3 py-2 font-bold text-violet-800">
                        {otpMessage}
                      </p>
                    )}

                    {verifiedOtpChallengePublicId && (
                      <p className="rounded-lg border border-green-200 bg-green-50 px-3 py-2 font-bold text-green-700">
                        OTP đã xác nhận. Hệ thống sẽ gửi lại yêu cầu đóng ca kèm mã xác nhận.
                      </p>
                    )}

                    <div className="flex flex-wrap gap-2">
                      <button
                        type="button"
                        onClick={handleRequestOtp}
                        disabled={
                          otpBusy ||
                          isSubmitting ||
                          !hasValidActualEndingCash ||
                          (needsReason && discrepancyReason.trim().length === 0) ||
                          !!otpChallengePublicId
                        }
                        className="px-4 py-2.5 bg-violet-700 text-white text-xs font-bold rounded-lg cursor-pointer hover:bg-violet-800 disabled:opacity-40 disabled:cursor-not-allowed active:scale-95 transition-all"
                      >
                        {otpBusy && !otpChallengePublicId ? 'Đang gửi OTP...' : 'Gửi OTP cho ca trưởng'}
                      </button>
                      <button
                        type="button"
                        onClick={handleResendOtp}
                        disabled={
                          otpBusy ||
                          isSubmitting ||
                          !otpChallengePublicId ||
                          resendAvailableInSeconds > 0 ||
                          otpStatus === 'Locked' ||
                          !!verifiedOtpChallengePublicId
                        }
                        className="px-4 py-2.5 border border-violet-400 text-violet-800 bg-white text-xs font-bold rounded-lg cursor-pointer hover:bg-violet-100 disabled:opacity-40 disabled:cursor-not-allowed active:scale-95 transition-all"
                      >
                        {resendAvailableInSeconds > 0
                          ? `Gửi lại OTP (${formatCountdown(resendAvailableInSeconds)})`
                          : 'Gửi lại OTP'}
                      </button>
                    </div>

                    {otpChallengePublicId && !verifiedOtpChallengePublicId && otpStatus !== 'Locked' && (
                      <div className="grid grid-cols-1 sm:grid-cols-[1fr_auto] gap-3 items-end">
                        <div>
                          <label className="block text-xs font-semibold text-text-secondary mb-1">
                            Mã OTP 6 ký tự (chữ + số)
                          </label>
                          <input
                            type="text"
                            inputMode="text"
                            autoComplete="one-time-code"
                            autoCapitalize="characters"
                            maxLength={6}
                            value={otpCode}
                            onChange={(event) => {
                              const sanitized = sanitizeOperationalOtpInput(event.target.value)
                              setOtpCode(sanitized.value)
                              if (sanitized.rejected) setOtpMessage(OPERATIONAL_OTP_INPUT_ERROR)
                            }}
                            disabled={otpBusy || isSubmitting || expiresInSeconds === 0}
                            className="w-full px-3 py-2 border border-violet-200 rounded-lg text-sm tracking-[0.35em] outline-none focus:border-violet-500 text-text-primary bg-white font-extrabold uppercase"
                            placeholder="A2B3C4"
                          />
                        </div>
                        <button
                          type="button"
                          onClick={handleVerifyOtp}
                          disabled={
                            otpBusy ||
                            isSubmitting ||
                            !isValidOperationalOtp(otpCode) ||
                            expiresInSeconds === 0
                          }
                          className="px-4 py-2.5 bg-brand-orange text-white text-xs font-bold rounded-lg cursor-pointer hover:bg-brand-orange-hover disabled:opacity-40 disabled:cursor-not-allowed active:scale-95 transition-all shadow-[var(--shadow-button)]"
                        >
                          {otpBusy ? 'Đang xác nhận...' : 'Xác nhận OTP'}
                        </button>
                      </div>
                    )}

                    {otpStatus === 'Locked' && (
                      <p className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 font-bold text-red-700">
                        Yêu cầu OTP đã bị khóa.
                      </p>
                    )}
                  </div>
                )}

                <button
                  type="submit"
                  disabled={
                    isSubmitting ||
                    otpBusy ||
                    !hasValidActualEndingCash ||
                    hasCloseBlockers ||
                    (needsReason && discrepancyReason.trim().length === 0) ||
                    (showOtpPanel && !verifiedOtpChallengePublicId)
                  }
                  className="px-4 py-2.5 bg-brand-orange text-white text-xs font-bold rounded-lg cursor-pointer hover:bg-brand-orange-hover disabled:opacity-40 disabled:cursor-not-allowed active:scale-95 transition-all shadow-[var(--shadow-button)]"
                >
                  {isSubmitting
                    ? 'Đang đóng ca...'
                    : showOtpPanel && !verifiedOtpChallengePublicId
                      ? 'Cần OTP trước khi đóng ca'
                      : 'Xác nhận đóng ca'}
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
                {shift?.requiresReconciliation && (
                  <div className="rounded-xl border border-amber-200 bg-amber-50 p-4 text-xs font-semibold text-amber-800">
                    <p className="font-extrabold text-text-primary">Cần đối soát lại</p>
                    {shift.isExceptionClosed && (
                      <p className="mt-1">Ca này được đóng ngoại lệ vì: {shift.exceptionCloseReason || 'Không có ghi chú'}</p>
                    )}
                    {shift.hasLateOfflineSync && (
                      <p className="mt-1">
                        Có {shift.lateOfflineSyncCount ?? 0} đơn offline đồng bộ sau khi ca đã đóng.
                      </p>
                    )}
                    <div className="mt-3 space-y-2">
                      <textarea
                        value={reconcileReason}
                        onChange={(event) => setReconcileReason(event.target.value)}
                        rows={2}
                        maxLength={500}
                        className="w-full rounded-lg border border-amber-200 bg-white px-3 py-2 font-semibold"
                        placeholder="Lý do và kết quả đối soát (tối thiểu 10 ký tự)"
                      />
                      <button
                        type="button"
                        disabled={isSubmitting || reconcileReason.trim().length < 10 || !shift.shiftId}
                        onClick={async () => {
                          if (!shift.shiftId) return
                          setIsSubmitting(true)
                          try {
                            const response = await apiClient.post<{ success?: boolean; message?: string }>(
                              `/api/v1/pos/shifts/${shift.shiftId}/reconcile`,
                              {
                                requestKey: reconcileRequestKeyRef.current,
                                reason: reconcileReason.trim(),
                                rowVersion: shift.rowVersion,
                                actualEndingCash: shift.actualEndingCash ?? 0,
                                offlineQueueSummary: {
                                  offlineOrderCount: 0,
                                  estimatedTotal: 0,
                                  localCashTotal: 0,
                                },
                              }
                            )
                            if (response.ok) {
                              reconcileRequestKeyRef.current = crypto.randomUUID()
                              setShift((current) => current ? {
                                ...current,
                                status: 'CLOSED',
                                requiresReconciliation: false,
                              } : current)
                              setMessage({ type: 'success', text: response.data?.message || 'Đối soát phiên POS thành công.' })
                            } else {
                              setMessage({ type: 'error', text: getApiErrorMessage(response, 'Không thể đối soát phiên POS.') })
                            }
                          } finally {
                            setIsSubmitting(false)
                          }
                        }}
                        className="rounded-lg bg-amber-700 px-3 py-2 font-bold text-white disabled:opacity-40"
                      >
                        Xác nhận đã đối soát
                      </button>
                    </div>
                  </div>
                )}
                <div className="flex flex-wrap gap-2">
                  <button
                    type="button"
                    onClick={returnToStaffHub}
                    className="px-4 py-2 bg-brand-orange text-white text-xs font-bold rounded-lg cursor-pointer hover:bg-brand-orange-hover transition-all shadow-[var(--shadow-button)]"
                  >
                    Quay lại StaffHub
                  </button>
                  <button
                    type="button"
                    onClick={loadCurrentShift}
                    className="px-4 py-2 border border-brand-orange text-brand-orange text-xs font-bold rounded-lg cursor-pointer hover:bg-brand-orange-light transition-all"
                  >
                    Kiểm tra ca hiện tại
                  </button>
                </div>
              </div>
            )}
          </>
        )}
      </div>
    </div>
  )
}
