import { apiClient, type ApiResponse } from './apiClient'

/** Issue #89/#91 — OTP approval for cash discrepancy close shift. */
export const OTP_ACTION_CASH_DIFFERENCE = 'CASH_DIFFERENCE'
export const OTP_TARGET_SHIFTS = 'shifts'
export const OTP_ERROR_REQUIRED = 'OTP_REQUIRED'

export interface OtpChallengeData {
  otpChallengePublicId?: string | null
  status?: string | null
  expiresInSeconds?: number
  resendAvailableInSeconds?: number
  remainingAttempts?: number
}

export interface OtpApiEnvelope {
  success?: boolean
  message?: string
  errorCode?: string
  data?: OtpChallengeData | null
}

export interface OtpRequestPayload {
  actionType: string
  targetType: string
  targetId: number
  workShiftId: number
  reason: string
  oldValueJson?: string | null
  newValueJson?: string | null
}

export interface OtpVerifyPayload {
  otpChallengePublicId: string
  otpCode: string
}

export interface OtpResendPayload {
  otpChallengePublicId: string
}

const asRecord = (value: unknown): Record<string, unknown> | null => {
  if (!value || typeof value !== 'object') return null
  return value as Record<string, unknown>
}

const readString = (record: Record<string, unknown> | null, keys: string[]): string | null => {
  if (!record) return null
  for (const key of keys) {
    const value = record[key]
    if (typeof value === 'string' && value.trim().length > 0) return value.trim()
  }
  return null
}

const readNumber = (record: Record<string, unknown> | null, keys: string[]): number | undefined => {
  if (!record) return undefined
  for (const key of keys) {
    const value = record[key]
    if (typeof value === 'number' && Number.isFinite(value)) return value
    if (typeof value === 'string' && value.trim().length > 0) {
      const parsed = Number(value)
      if (Number.isFinite(parsed)) return parsed
    }
  }
  return undefined
}

export const parseApiJsonBody = (response: { data: unknown; error?: string }): OtpApiEnvelope | null => {
  const fromData = asRecord(response.data)
  if (fromData) {
    return normalizeEnvelope(fromData)
  }

  const errorText = response.error?.trim()
  if (!errorText) return null

  try {
    const parsed = JSON.parse(errorText) as unknown
    const record = asRecord(parsed)
    return record ? normalizeEnvelope(record) : null
  } catch {
    return { message: errorText }
  }
}

const normalizeChallenge = (value: unknown): OtpChallengeData | null => {
  const record = asRecord(value)
  if (!record) return null

  const publicId =
    readString(record, ['otpChallengePublicId', 'OtpChallengePublicId']) ??
    null

  return {
    otpChallengePublicId: publicId,
    status: readString(record, ['status', 'Status']),
    expiresInSeconds: readNumber(record, ['expiresInSeconds', 'ExpiresInSeconds']) ?? 0,
    resendAvailableInSeconds:
      readNumber(record, ['resendAvailableInSeconds', 'ResendAvailableInSeconds']) ?? 0,
    remainingAttempts: readNumber(record, ['remainingAttempts', 'RemainingAttempts']),
  }
}

const normalizeEnvelope = (record: Record<string, unknown>): OtpApiEnvelope => {
  const nestedData = record.data ?? record.Data
  return {
    success: typeof record.success === 'boolean' ? record.success : undefined,
    message: readString(record, ['message', 'Message', 'error', 'title', 'detail']) ?? undefined,
    errorCode: readString(record, ['errorCode', 'ErrorCode']) ?? undefined,
    data: normalizeChallenge(nestedData),
  }
}

export const isOtpRequiredError = (response: { data: unknown; error?: string }): boolean => {
  const envelope = parseApiJsonBody(response)
  if (envelope?.errorCode === OTP_ERROR_REQUIRED) return true

  const message = (envelope?.message ?? response.error ?? '').toLowerCase()
  return (
    message.includes('otp_required') ||
    message.includes('cần xác nhận otp') ||
    (message.includes('chênh lệch') && message.includes('otp'))
  )
}

export const mapOtpUserMessage = (
  rawMessage: string | null | undefined,
  fallback = 'Không thể xác nhận OTP. Vui lòng thử lại.'
): string => {
  const message = rawMessage?.trim()
  if (!message) return fallback

  const lower = message.toLowerCase()

  if (lower.includes('không gửi được otp') || lower.includes('không gửi lại được otp')) {
    return 'Không gửi được OTP ca trưởng. Vui lòng kiểm tra cấu hình email.'
  }

  if (lower.includes('hết hạn')) {
    return 'OTP đã hết hạn.'
  }

  if (lower.includes('bị khóa') || lower.includes('khóa do nhập sai')) {
    return 'Yêu cầu OTP đã bị khóa.'
  }

  if (
    lower.includes('đợi') &&
    (lower.includes('gửi lại') || lower.includes('trước khi'))
  ) {
    // Prefer backend wait-seconds detail when present.
    if (/\d+\s*giây/.test(message)) return message
    return 'Vui lòng chờ trước khi gửi lại OTP.'
  }

  if (lower.includes('otp không đúng') || lower.includes('sai')) {
    return message
  }

  return message
}

export async function requestOtp(
  payload: OtpRequestPayload
): Promise<ApiResponse<OtpApiEnvelope>> {
  return apiClient.post<OtpApiEnvelope>('/api/v1/otp/request', payload)
}

export async function verifyOtp(
  payload: OtpVerifyPayload
): Promise<ApiResponse<OtpApiEnvelope>> {
  return apiClient.post<OtpApiEnvelope>('/api/v1/otp/verify', payload)
}

export async function resendOtp(
  payload: OtpResendPayload
): Promise<ApiResponse<OtpApiEnvelope>> {
  return apiClient.post<OtpApiEnvelope>('/api/v1/otp/resend', payload)
}

export function extractOtpEnvelope(
  response: ApiResponse<OtpApiEnvelope>
): OtpApiEnvelope {
  if (response.ok && response.data) {
    // Success body is already the envelope { success, message, data }
    const record = asRecord(response.data)
    if (record && ('data' in record || 'success' in record || 'message' in record)) {
      return normalizeEnvelope(record)
    }
    // Or data field is the challenge itself
    return {
      success: true,
      data: normalizeChallenge(response.data),
    }
  }

  return parseApiJsonBody(response) ?? {
    success: false,
    message: response.error ?? 'Không thể xác nhận OTP. Vui lòng thử lại.',
  }
}

export function formatCountdown(totalSeconds: number): string {
  const safe = Math.max(0, Math.floor(totalSeconds))
  const minutes = Math.floor(safe / 60)
  const seconds = safe % 60
  return `${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`
}
