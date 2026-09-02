import { useEffect, useState } from 'react'
import { Link, useLocation } from 'react-router-dom'
import NetworkStatusIndicator from './NetworkStatusIndicator'
import PrinterStatusBadge from './PrinterStatusBadge'
import { getPosSession, type PosSession } from '../services/posSession'
import { fetchUnreadCount } from '../services/notificationService'
import {
  startNotificationRealtime,
  stopNotificationRealtime,
} from '../services/notificationRealtime'
import PreferenceControls from './PreferenceControls'
import { usePreferences } from '../contexts/PreferencesContext'

const POLL_MS = 60_000

export default function TopNavbar() {
  const { t } = usePreferences()
  const location = useLocation()
  const currentPath = location.pathname
  const [session, setSession] = useState<PosSession>(() => getPosSession())
  const [unreadCount, setUnreadCount] = useState(0)

  useEffect(() => {
    const refreshSession = () => setSession(getPosSession())
    window.addEventListener('pos-session-changed', refreshSession)
    window.addEventListener('storage', refreshSession)
    return () => {
      window.removeEventListener('pos-session-changed', refreshSession)
      window.removeEventListener('storage', refreshSession)
    }
  }, [])

  useEffect(() => {
    let cancelled = false

    const run = async () => {
      const token = getPosSession().token
      if (!token) {
        if (!cancelled) setUnreadCount(0)
        return
      }
      const result = await fetchUnreadCount()
      if (!cancelled && result.ok) setUnreadCount(result.unreadCount)
    }

    // Defer so setState is not synchronous in the effect body (eslint).
    const startTimer = window.setTimeout(() => {
      void run()
    }, 0)

    const onNotifyChanged = () => {
      window.setTimeout(() => {
        void run()
      }, 0)
    }
    window.addEventListener('pos-notifications-changed', onNotifyChanged)

    const interval = window.setInterval(() => {
      void run()
    }, POLL_MS)

    return () => {
      cancelled = true
      window.clearTimeout(startTimer)
      window.clearInterval(interval)
      window.removeEventListener('pos-notifications-changed', onNotifyChanged)
    }
  }, [session.token, currentPath])

  useEffect(() => {
    if (session.token) void startNotificationRealtime()
    else void stopNotificationRealtime()
    return () => {
      void stopNotificationRealtime()
    }
  }, [session.token])

  const isTabActive = (path: string) => {
    if (path === '/order') {
      return currentPath === '/order' || currentPath === '/'
    }
    return currentPath === path
  }

  const tabClass = (path: string) =>
    `pos-touch-target min-w-11 px-3 py-2 text-xs font-bold rounded-lg transition-colors cursor-pointer flex items-center justify-center gap-1.5 whitespace-nowrap ${
      isTabActive(path)
        ? 'bg-brand-orange text-white shadow-[var(--shadow-button)]'
        : 'text-text-secondary hover:bg-brand-orange-light hover:text-brand-orange border border-transparent'
    }`

  return (
    <header className="relative z-40 min-h-16 w-full shrink-0 bg-surface-white border-b border-border px-3 md:px-5 flex items-center justify-between gap-3 select-none">
      <div className="flex shrink-0 items-center gap-2.5">
        <div className="w-10 h-10 rounded-xl bg-brand-orange flex items-center justify-center shadow-[var(--shadow-button)]">
          <span className="text-white font-extrabold text-sm">CC</span>
        </div>
        <div className="hidden xl:block">
          <h1 className="font-extrabold text-sm text-text-primary leading-tight">CafeChain POS</h1>
          <p className="text-[11px] text-text-muted">{t('common.branch', { id: session.storeId ?? '-' })}</p>
        </div>
      </div>

      <nav aria-label={t('nav.label')} className="min-w-0 flex flex-1 justify-center gap-1 overflow-x-auto">
        <Link to="/order" className={tabClass('/order')} aria-current={isTabActive('/order') ? 'page' : undefined}>
          <span aria-hidden="true">☕</span><span className="hidden lg:inline">{t('nav.sales')}</span>
        </Link>
        <Link to="/history" className={tabClass('/history')} aria-current={isTabActive('/history') ? 'page' : undefined}>
          <span aria-hidden="true">▤</span><span className="hidden lg:inline">{t('nav.history')}</span>
        </Link>
        <Link to="/inventory" className={tabClass('/inventory')} aria-current={isTabActive('/inventory') ? 'page' : undefined}>
          <span aria-hidden="true">▣</span><span className="hidden xl:inline">{t('nav.inventory')}</span>
        </Link>
        <Link to="/notifications" className={tabClass('/notifications')} aria-current={isTabActive('/notifications') ? 'page' : undefined}>
          <span aria-hidden="true">●</span><span className="hidden xl:inline">{t('nav.notifications')}</span>
          {unreadCount > 0 && (
            <span
              className={`ml-0.5 min-w-[1.1rem] h-4 px-1 rounded-full text-[10px] font-extrabold flex items-center justify-center ${
                isTabActive('/notifications')
                  ? 'bg-white text-brand-orange'
                  : 'bg-danger text-white'
              }`}
            >
              {unreadCount > 99 ? '99+' : unreadCount}
            </span>
          )}
        </Link>
        <Link to="/shift" className={tabClass('/shift')} aria-current={isTabActive('/shift') ? 'page' : undefined}>
          <span aria-hidden="true">◷</span><span className="hidden lg:inline">{t('nav.shift')}</span>
        </Link>
      </nav>

      <div className="flex shrink-0 items-center gap-2">
        <PreferenceControls />
        <NetworkStatusIndicator />
        <div className="hidden sm:block"><PrinterStatusBadge storeId={session.storeId ?? 1} /></div>
        <div className="flex items-center gap-2 border-l border-border pl-2 md:pl-3">
          <div className="w-10 h-10 rounded-xl bg-brand-orange-light flex items-center justify-center">
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
          <div className="hidden 2xl:block">
            <p className="text-xs font-semibold text-text-primary leading-tight">{session.staffName}</p>
            <p className="text-[11px] text-text-muted">
              {session.role}
              {session.storeId ? ` · Chi nhánh #${session.storeId}` : ''}
            </p>
          </div>
        </div>
      </div>
    </header>
  )
}
