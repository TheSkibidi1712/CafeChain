import { Link, useLocation } from 'react-router-dom'
import NetworkStatusIndicator from './NetworkStatusIndicator'
import PrinterStatusBadge from './PrinterStatusBadge'

export default function TopNavbar() {
  const location = useLocation()
  const currentPath = location.pathname

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
        <Link to="/shift" className={tabClass('/shift')}>
          ⏰ Ca làm việc
        </Link>
      </nav>

      {/* Right Side Info & Network Status */}
      <div className="flex items-center gap-4">
        <NetworkStatusIndicator />
        <PrinterStatusBadge />
        <div className="flex items-center gap-2 border-l border-border pl-4">
          <div className="w-8 h-8 rounded-full bg-brand-orange-light flex items-center justify-center">
            <span className="text-brand-orange text-xs font-bold">NV</span>
          </div>
          <div className="hidden sm:block">
            <p className="text-xs font-semibold text-text-primary leading-tight">Nguyễn Văn A</p>
            <p className="text-[9px] text-text-muted">Thu ngân • Ca sáng</p>
          </div>
        </div>
      </div>
    </header>
  )
}
