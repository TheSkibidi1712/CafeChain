import { useEffect, useState } from 'react'
import { Link, useLocation } from 'react-router-dom'
import NetworkStatusIndicator from './NetworkStatusIndicator'
import PrinterStatusBadge from './PrinterStatusBadge'
import { getPosSession, type PosSession } from '../services/posSession'

export default function TopNavbar() {
  const location = useLocation()
  const currentPath = location.pathname
  const [session, setSession] = useState<PosSession>(() => getPosSession())

  useEffect(() => {
    const refreshSession = () => setSession(getPosSession())
    window.addEventListener('pos-session-changed', refreshSession)
    window.addEventListener('storage', refreshSession)

    return () => {
      window.removeEventListener('pos-session-changed', refreshSession)
      window.removeEventListener('storage', refreshSession)
    }
  }, [])

  const isTabActive = (path: string) => {
    if (path === '/order') {
      return currentPath === '/order' || currentPath === '/'
    }
    return currentPath === path
  }

  const tabClass = (path: string) =>
    `px-4 py-2.5 text-xs font-bold rounded-lg transition-colors cursor-pointer flex items-center gap-1.5 ${
      isTabActive(path)
        ? 'bg-brand-orange text-white shadow-[var(--shadow-button)]'
        : 'text-text-secondary hover:bg-brand-orange-light hover:text-brand-orange border border-transparent'
    }`

  return (
    <header className="w-full bg-surface-white border-b border-border px-6 py-3 flex items-center justify-between select-none">
      {/* Brand Logo & Name */}
      <div className="flex items-center gap-2.5">
        <div className="w-9 h-9 rounded-xl bg-brand-orange flex items-center justify-center">
          <span className="text-white font-extrabold text-sm">CC</span>
        </div>
        <div>
          <h1 className="font-bold text-sm text-text-primary leading-tight">CafeChain</h1>
          <p className="text-[10px] text-text-muted">POS Terminal</p>
        </div>
      </div>

      {/* Navigation Tabs (Multi-tab structure) */}
      <nav className="flex gap-1">
        <Link to="/order" className={tabClass('/order')}>
          🍽 Bán hàng
        </Link>
        <Link to="/history" className={tabClass('/history')}>
          📜 Lịch sử đơn
        </Link>
        <Link to="/inventory" className={tabClass('/inventory')}>
          📦 Kho chi nhánh
        </Link>
        <Link to="/shift" className={tabClass('/shift')}>
          ⏰ Ca làm việc
        </Link>
      </nav>

      {/* Right Side Info & Network Status */}
      <div className="flex items-center gap-4">
        <NetworkStatusIndicator />
        <PrinterStatusBadge storeId={session.storeId ?? 1} />
        <div className="flex items-center gap-2 border-l border-border pl-4">
          <div className="w-8 h-8 rounded-full bg-brand-orange-light flex items-center justify-center">
            <span className="text-brand-orange text-xs font-bold">
              {session.staffName
                .split(' ')
                .filter(Boolean)
                .slice(-2)
                .map((part) => part[0])
                .join('')
                .toUpperCase() || 'POS'}
            </span>
          </div>
          <div className="hidden sm:block">
            <p className="text-xs font-semibold text-text-primary leading-tight">{session.staffName}</p>
            <p className="text-[9px] text-text-muted">
              {session.role}{session.storeId ? ` • Cửa hàng #${session.storeId}` : ''}
            </p>
          </div>
        </div>
      </div>
    </header>
  )
}
