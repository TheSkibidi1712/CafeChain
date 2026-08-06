export const POS_TOKEN_KEY = 'pos_jwt_token'

const POS_CONTEXT_KEY = 'pos_context'
const POS_TERMINAL_KEY = 'CafeChain_POS_TerminalId'

const CLAIMS = {
  nameIdentifier: [
    'nameid',
    'sub',
    'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier',
  ],
  name: [
    'name',
    'unique_name',
    'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name',
  ],
  role: [
    'role',
    'http://schemas.microsoft.com/ws/2008/06/identity/claims/role',
  ],
}

export interface PosSession {
  token: string | null
  staffId: number | null
  storeId: number | null
  staffName: string
  role: string
  avatarUrl?: string
  expiresAt?: number
  workShiftId?: number | null
  terminalId?: string | null
  requiresOpeningCash?: boolean
}

export class PosSessionBootstrapError extends Error {
  readonly errorCode: string

  constructor(message: string, errorCode = 'POS_EXCHANGE_UNAVAILABLE') {
    super(message)
    this.name = 'PosSessionBootstrapError'
    this.errorCode = errorCode
  }
}

function readClaim(payload: Record<string, unknown>, keys: string[]): string | null {
  for (const key of keys) {
    const value = payload[key]
    if (Array.isArray(value)) return String(value[0] ?? '')
    if (value !== undefined && value !== null) return String(value)
  }
  return null
}

function parseJwtPayload(token: string): Record<string, unknown> | null {
  const [, encodedPayload] = token.split('.')
  if (!encodedPayload) return null

  try {
    const base64 = encodedPayload.replace(/-/g, '+').replace(/_/g, '/')
    const padded = base64.padEnd(base64.length + ((4 - base64.length % 4) % 4), '=')
    const binary = atob(padded)
    const bytes = Uint8Array.from(binary, (char) => char.charCodeAt(0))
    return JSON.parse(new TextDecoder().decode(bytes)) as Record<string, unknown>
  } catch {
    try {
      return JSON.parse(atob(encodedPayload)) as Record<string, unknown>
    } catch {
      return null
    }
  }
}

export function savePosToken(token: string): PosSession {
  localStorage.setItem(POS_TOKEN_KEY, token)

  const payload = parseJwtPayload(token) ?? {}
  const session: PosSession = {
    token,
    staffId: Number(payload.StaffId ?? readClaim(payload, CLAIMS.nameIdentifier)) || null,
    storeId: Number(payload.StoreId) || null,
    staffName: readClaim(payload, CLAIMS.name) ?? 'Nhân viên POS',
    role: readClaim(payload, CLAIMS.role) ?? 'POS',
    avatarUrl: typeof payload.AvatarUrl === 'string' ? payload.AvatarUrl : undefined,
    expiresAt: typeof payload.exp === 'number' ? payload.exp * 1000 : undefined,
    workShiftId: Number(payload.PosWorkShiftId) || null,
    terminalId: typeof payload.PosTerminalId === 'string' && payload.PosTerminalId.trim()
      ? payload.PosTerminalId.trim()
      : null,
    requiresOpeningCash: String(payload.RequiresOpeningCash).toLowerCase() === 'true',
  }

  if (session.terminalId) localStorage.setItem(POS_TERMINAL_KEY, session.terminalId)

  localStorage.setItem(POS_CONTEXT_KEY, JSON.stringify({
    staffId: session.staffId,
    storeId: session.storeId,
    staffName: session.staffName,
    role: session.role,
    avatarUrl: session.avatarUrl,
    expiresAt: session.expiresAt,
    workShiftId: session.workShiftId,
    terminalId: session.terminalId,
    requiresOpeningCash: session.requiresOpeningCash,
  }))

  window.dispatchEvent(new CustomEvent('pos-session-changed', { detail: session }))
  return session
}

export function clearPosAuthentication(): void {
  localStorage.removeItem(POS_TOKEN_KEY)
  localStorage.removeItem(POS_CONTEXT_KEY)
  window.dispatchEvent(new CustomEvent('pos-session-changed', {
    detail: getPosSession(),
  }))
}

export function completeOpeningCash(workShiftId: number): void {
  const session = getPosSession()
  session.requiresOpeningCash = false
  session.workShiftId = workShiftId
  localStorage.setItem(POS_CONTEXT_KEY, JSON.stringify({
    staffId: session.staffId,
    storeId: session.storeId,
    staffName: session.staffName,
    role: session.role,
    avatarUrl: session.avatarUrl,
    expiresAt: session.expiresAt,
    workShiftId,
    terminalId: session.terminalId,
    requiresOpeningCash: false,
  }))
  window.dispatchEvent(new CustomEvent('pos-session-changed', { detail: session }))
}

export function getPosSession(): PosSession {
  const token = localStorage.getItem(POS_TOKEN_KEY)
  const storedContext = localStorage.getItem(POS_CONTEXT_KEY)

  if (storedContext) {
    try {
      const context = JSON.parse(storedContext) as Omit<PosSession, 'token'>
      return { token, ...context }
    } catch {
      localStorage.removeItem(POS_CONTEXT_KEY)
    }
  }

  if (token) return savePosToken(token)

  return {
    token: null,
    staffId: null,
    storeId: null,
    staffName: 'Chưa đăng nhập POS',
    role: 'POS',
  }
}

export async function bootstrapPosTokenFromUrl(): Promise<PosSession> {
  const url = new URL(window.location.href)
  const hashParams = new URLSearchParams(url.hash.startsWith('#') ? url.hash.slice(1) : url.hash)
  const exchangeCode = hashParams.get('exchange_code')
  const exchangeUrlValue = hashParams.get('exchange_url')

  if (!exchangeCode || !exchangeUrlValue) return getPosSession()

  const exchangeUrl = new URL(exchangeUrlValue, window.location.origin)
  if (exchangeUrl.protocol !== 'https:' && exchangeUrl.protocol !== 'http:') {
    throw new PosSessionBootstrapError(
      'Địa chỉ đổi mã POS không hợp lệ.',
      'POS_EXCHANGE_CODE_INVALID',
    )
  }

  // Remove the bearer exchange code from browser history before doing network I/O.
  url.hash = ''
  window.history.replaceState({}, document.title, url.pathname + url.search)

  let response: Response
  try {
    response = await fetch(exchangeUrl.toString(), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
      body: JSON.stringify({ exchangeCode }),
    })
  } catch {
    throw new PosSessionBootstrapError(
      'Không thể kết nối máy chủ để đổi mã mở POS.',
    )
  }
  const payload = await response.json().catch(() => null) as {
    token?: string
    message?: string
    errorCode?: string
  } | null
  if (!response.ok || !payload?.token) {
    throw new PosSessionBootstrapError(
      payload?.message ?? 'Mã mở POS đã hết hạn hoặc đã được sử dụng.',
      payload?.errorCode ?? 'POS_EXCHANGE_CODE_INVALID',
    )
  }

  return savePosToken(payload.token)
}

export function getPosStoreId(defaultStoreId = 1): number {
  return getPosSession().storeId ?? defaultStoreId
}

export function getPosTerminalId(): string {
  const sessionTerminal = getPosSession().terminalId?.trim()
  if (sessionTerminal) return sessionTerminal
  const existing = localStorage.getItem(POS_TERMINAL_KEY)
  if (existing) return existing
  return ''
}
