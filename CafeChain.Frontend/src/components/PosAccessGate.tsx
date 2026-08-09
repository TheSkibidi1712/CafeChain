import { type ReactNode, useCallback, useEffect, useState } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { API_BASE_URL, apiClient } from '../services/apiClient'
import { clearPosAuthentication, getPosSession } from '../services/posSession'

type PosAccessMode = 'OPENING_CASH' | 'ACTIVE' | 'PENDING_CLOSE'

interface PosAccessSnapshot {
  accessMode: PosAccessMode
  workShiftId?: number | null
  workShiftStatus?: string | null
  terminalId: string
  serverNowUtc: string
  recommendedAction: string
}

interface CurrentSessionEnvelope {
  success: boolean
  data?: PosAccessSnapshot
  errorCode?: string
  message?: string
}

const redirectToStaffHub = (errorCode: string, message?: string) => {
  const session = getPosSession()
  clearPosAuthentication()
  const target = new URL('/StaffHub', API_BASE_URL)
  target.searchParams.set('openPos', '1')
  target.searchParams.set('posErrorCode', errorCode || 'POS_ACCESS_DENIED')
  if (session.terminalId) target.searchParams.set('terminalId', session.terminalId)
  if (message) sessionStorage.setItem('pos-access-message', message)
  window.location.replace(target.toString())
}

export default function PosAccessGate({ children }: { children: ReactNode }) {
  const location = useLocation()
  const [snapshot, setSnapshot] = useState<PosAccessSnapshot | null>(null)
  const [networkError, setNetworkError] = useState<string | null>(null)
  const [checking, setChecking] = useState(true)

  const validate = useCallback(async () => {
    const session = getPosSession()
    if (!session.token) {
      redirectToStaffHub('POS_SESSION_INVALID', 'Bạn cần mở POS từ StaffHub.')
      return
    }

    const response = await apiClient.get<CurrentSessionEnvelope>('/api/v1/pos/session/current')
    if (!response.ok || !response.data?.data) {
      const payload = response.data
      if (response.status === 0) {
        setNetworkError('Không thể xác minh quyền POS. Kiểm tra kết nối rồi thử lại.')
        setChecking(false)
        return
      }
      redirectToStaffHub(
        payload?.errorCode ?? (response.status === 401 ? 'POS_SESSION_INVALID' : 'POS_ACCESS_DENIED'),
        payload?.message ?? response.error,
      )
      return
    }

    setSnapshot(response.data.data)
    setNetworkError(null)
    setChecking(false)
  }, [])

  useEffect(() => {
    queueMicrotask(() => void validate())
    const onFocus = () => void validate()
    const onVisibility = () => {
      if (document.visibilityState === 'visible') void validate()
    }
    const timer = window.setInterval(() => void validate(), 30_000)
    window.addEventListener('focus', onFocus)
    document.addEventListener('visibilitychange', onVisibility)
    return () => {
      window.clearInterval(timer)
      window.removeEventListener('focus', onFocus)
      document.removeEventListener('visibilitychange', onVisibility)
    }
  }, [validate])

  if (checking) {
    return <div className="grid min-h-screen place-items-center bg-surface text-sm font-semibold text-text-secondary">Đang xác minh quyền truy cập POS…</div>
  }

  if (networkError) {
    return (
      <main className="grid min-h-screen place-items-center bg-surface p-6">
        <section className="max-w-md rounded-2xl border border-border bg-white p-6 text-center shadow-sm">
          <h1 className="text-lg font-bold text-text-primary">Chưa thể xác minh POS</h1>
          <p className="mt-2 text-sm text-text-secondary">{networkError}</p>
          <button type="button" onClick={() => { setChecking(true); void validate() }} className="mt-5 rounded-lg bg-brand-orange px-4 py-2 font-bold text-white">Thử lại</button>
        </section>
      </main>
    )
  }

  if (!snapshot) return null
  if (snapshot.accessMode !== 'ACTIVE' && location.pathname !== '/shift') {
    return <Navigate to="/shift" replace state={{ accessMode: snapshot.accessMode }} />
  }
  return children
}
