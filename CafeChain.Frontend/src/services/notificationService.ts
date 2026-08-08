import { apiClient } from './apiClient'

export type EmailDeliveryHint = 'none' | 'pending' | 'sent' | 'failed' | string

export interface StaffNotificationItem {
  notificationId: number
  type: string
  title: string
  body: string
  entityType: string
  entityId: number
  isRead: boolean
  readAt?: string | null
  createdAt: string
  emailAttempted: boolean
  emailSent: boolean
  emailDeliveryHint: EmailDeliveryHint
  targetUrl?: string | null
  activeOtp?: {
    code: string
    expiresAtUtc: string
  } | null
  operationalOtp?: OperationalOtpNotification | null
}

export interface OperationalOtpNotification {
  challengePublicId: string
  actionType: string
  terminalId: string
  terminalName: string
  storeName: string
  requestedByStaffId: number
  requestedByName: string
  approverStaffId: number
  approverName: string
  confirmedByStaffId?: number | null
  confirmedByName?: string | null
  sentAtUtc: string
  expiresAtUtc: string
  serverNowUtc: string
  status: 'Waiting' | 'Used' | 'Expired' | 'Cancelled' | string
  remainingSeconds: number
  canRevealOtp: boolean
  canContinueTerminalConfirmation: boolean
}

export interface NotificationListData {
  page: number
  pageSize: number
  total: number
  unreadCount: number
  items: StaffNotificationItem[]
}

interface Envelope<T> {
  success?: boolean
  message?: string
  data?: T
}

function parseError(raw?: string, fallback = 'Lỗi thông báo'): string {
  if (!raw) return fallback
  try {
    const parsed = JSON.parse(raw) as { message?: string }
    if (parsed.message) return parsed.message
  } catch {
    // ignore
  }
  return raw.length > 200 ? `${raw.slice(0, 200)}...` : raw
}

export async function fetchUnreadCount(): Promise<{
  ok: boolean
  unreadCount: number
  error?: string
}> {
  const res = await apiClient.get<Envelope<{ unreadCount: number }>>(
    '/api/v1/pos/notifications/unread-count'
  )
  if (!res.ok || !res.data?.data) {
    return { ok: false, unreadCount: 0, error: parseError(res.error) }
  }
  return { ok: true, unreadCount: res.data.data.unreadCount ?? 0 }
}

export async function fetchNotifications(params?: {
  page?: number
  pageSize?: number
}): Promise<{ ok: boolean; data: NotificationListData | null; error?: string }> {
  const query = new URLSearchParams()
  if (params?.page) query.set('page', String(params.page))
  if (params?.pageSize) query.set('pageSize', String(params.pageSize))
  const qs = query.toString()
  const path = `/api/v1/pos/notifications${qs ? `?${qs}` : ''}`
  const res = await apiClient.get<Envelope<NotificationListData>>(path)
  if (!res.ok || !res.data?.data) {
    return { ok: false, data: null, error: parseError(res.error, 'Không tải được thông báo.') }
  }
  return { ok: true, data: res.data.data }
}

export async function markNotificationRead(id: number): Promise<{ ok: boolean; error?: string }> {
  const res = await apiClient.post<Envelope<{ markedCount: number }>>(
    `/api/v1/pos/notifications/${id}/read`,
    {}
  )
  if (!res.ok) {
    return { ok: false, error: parseError(res.error, 'Không đánh dấu đã đọc.') }
  }
  return { ok: true }
}

export async function markAllNotificationsRead(): Promise<{
  ok: boolean
  markedCount: number
  error?: string
}> {
  const res = await apiClient.post<Envelope<{ markedCount: number }>>(
    '/api/v1/pos/notifications/read-all',
    {}
  )
  if (!res.ok || !res.data) {
    return { ok: false, markedCount: 0, error: parseError(res.error) }
  }
  return { ok: true, markedCount: res.data.data?.markedCount ?? 0 }
}

export async function revealTerminalOtp(id: number): Promise<{
  ok: boolean
  data?: { code: string; expiresAtUtc: string; serverNowUtc: string }
  error?: string
}> {
  const res = await apiClient.get<Envelope<{ code: string; expiresAtUtc: string; serverNowUtc: string }>>(
    `/api/v1/pos/notifications/${id}/terminal-otp`
  )
  if (!res.ok || !res.data?.data) {
    return { ok: false, error: parseError(res.error, 'Không thể xem OTP.') }
  }
  return { ok: true, data: res.data.data }
}

export async function confirmTerminalFromNotification(
  id: number,
  otpCode: string,
  requestKey: string
): Promise<{ ok: boolean; error?: string }> {
  const res = await apiClient.post<Envelope<unknown>>(
    `/api/v1/pos/notifications/${id}/terminal-confirm`,
    { otpCode, requestKey }
  )
  return res.ok
    ? { ok: true }
    : { ok: false, error: parseError(res.error, 'Không thể xác nhận Terminal.') }
}
