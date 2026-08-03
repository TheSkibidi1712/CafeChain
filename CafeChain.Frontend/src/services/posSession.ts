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
  }

  localStorage.setItem(POS_CONTEXT_KEY, JSON.stringify({
    staffId: session.staffId,
    storeId: session.storeId,
    staffName: session.staffName,
    role: session.role,
    avatarUrl: session.avatarUrl,
    expiresAt: session.expiresAt,
  }))

  window.dispatchEvent(new CustomEvent('pos-session-changed', { detail: session }))
  return session
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
    throw new Error('Địa chỉ đổi mã POS không hợp lệ.')
  }

  // Remove the bearer exchange code from browser history before doing network I/O.
  url.hash = ''
  window.history.replaceState({}, document.title, url.pathname + url.search)

  const response = await fetch(exchangeUrl.toString(), {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
    body: JSON.stringify({ exchangeCode }),
  })
  const payload = await response.json().catch(() => null) as { token?: string; message?: string } | null
  if (!response.ok || !payload?.token) {
    throw new Error(payload?.message ?? 'Mã mở POS đã hết hạn hoặc đã được sử dụng.')
  }

  return savePosToken(payload.token)
}

export function getPosStoreId(defaultStoreId = 1): number {
  return getPosSession().storeId ?? defaultStoreId
}

export function getPosTerminalId(): string {
  const existing = localStorage.getItem(POS_TERMINAL_KEY)
  if (existing) return existing
  const generated = crypto.randomUUID()
  localStorage.setItem(POS_TERMINAL_KEY, generated)
  return generated
}
