import { Link } from 'react-router-dom'
import NetworkStatusIndicator from '../NetworkStatusIndicator'
import PrinterStatusBadge from '../PrinterStatusBadge'
import type { PosSession } from '../../services/posSession'
import type { PosLayoutPreference, PosResolvedLayout } from '../../hooks/usePosLayoutMode'
import PreferenceControls from '../PreferenceControls'
import { usePreferences } from '../../contexts/PreferencesContext'

type OrderType = 'dine-in' | 'take-away'

interface SellingHeaderProps {
  orderType: OrderType
  searchQuery: string
  resultCount: number
  isCartLocked: boolean
  hasOpenShift: boolean
  shiftId?: number | null
  responsibleStaffName: string
  currentOperatorName: string
  session: PosSession
  layoutPreference: PosLayoutPreference
  resolvedLayout: PosResolvedLayout
  isLayoutSwitchLocked: boolean
  onOrderTypeChange: (orderType: OrderType) => void
  onSearchChange: (value: string) => void
  onOpenCustomerDisplay: () => void
  onLayoutPreferenceChange: (preference: PosLayoutPreference) => void
}

export default function SellingHeader({
  orderType,
  searchQuery,
  resultCount,
  isCartLocked,
  hasOpenShift,
  shiftId,
  responsibleStaffName,
  currentOperatorName,
  session,
  layoutPreference,
  resolvedLayout,
  isLayoutSwitchLocked,
  onOrderTypeChange,
  onSearchChange,
  onOpenCustomerDisplay,
  onLayoutPreferenceChange,
}: SellingHeaderProps) {
  const { t } = usePreferences()
  const cashierInitials = currentOperatorName
    .split(' ')
    .filter(Boolean)
    .slice(-2)
    .map((part) => part[0])
    .join('')
    .toUpperCase() || 'POS'

  return (
    <header className="pos-selling-header" aria-label={t('sales.toolbar')}>
      <div className="pos-selling-brand" aria-label={`CafeChain POS, ${t('common.branch', { id: session.storeId ?? t('common.notAvailable') })}`}>
        <span className="pos-selling-brand-mark" aria-hidden="true">CC</span>
        <span className="pos-selling-brand-copy">
          <strong>CafeChain POS</strong>
          <small>{t('common.branch', { id: session.storeId ?? '-' })}</small>
        </span>
      </div>

      <div className="pos-order-type-control" role="group" aria-label={t('sales.orderType')}>
        <button
          type="button"
          onClick={() => onOrderTypeChange('dine-in')}
          disabled={isCartLocked}
          aria-pressed={orderType === 'dine-in'}
          className="pos-order-type-button"
        >
          {t('sales.dineIn')}
        </button>
        <button
          type="button"
          onClick={() => onOrderTypeChange('take-away')}
          disabled={isCartLocked}
          aria-pressed={orderType === 'take-away'}
          className="pos-order-type-button"
        >
          {t('sales.takeAway')}
        </button>
      </div>

      <label className="pos-selling-search" htmlFor="pos-product-search">
        <span className="sr-only">{t('sales.search')}</span>
        <span className="pos-selling-search-icon" aria-hidden="true">⌕</span>
        <input
          id="pos-product-search"
          type="search"
          value={searchQuery}
          onChange={(event) => onSearchChange(event.target.value)}
          placeholder={t('sales.searchPlaceholder')}
          autoComplete="off"
        />
        <span className="pos-selling-result-count" aria-label={t('sales.resultCount', { count: resultCount })}>
          {resultCount}
        </span>
        {searchQuery && (
          <button
            type="button"
            onClick={() => onSearchChange('')}
            className="pos-selling-search-clear"
            aria-label={t('sales.clearSearch')}
          >
            {t('common.delete')}
          </button>
        )}
      </label>

      <div className="pos-selling-status" aria-label={t('sales.connectionAndPrinter')}>
        <NetworkStatusIndicator />
        <PrinterStatusBadge storeId={session.storeId ?? 1} />
      </div>

      <div
        className="pos-selling-cashier"
        title={`Người đang thao tác: ${currentOperatorName} · Chịu trách nhiệm két: ${responsibleStaffName}`}
        aria-label={`Người đang thao tác ${currentOperatorName}; người chịu trách nhiệm két ${responsibleStaffName}`}
      >
        <span aria-hidden="true">{cashierInitials}</span>
        <span className="pos-selling-cashier-copy">
          <strong>{currentOperatorName}</strong>
          <small>{t('sales.currentOperator')}</small>
        </span>
      </div>

      <details className="pos-selling-more">
        <summary className="pos-selling-more-trigger">{t('sales.actions')}</summary>
        <nav className="pos-selling-more-menu" aria-label={t('sales.actions')}>
          {resolvedLayout === 'tablet' && (
            <div className="pos-tablet-menu-context">
              <span className="pos-selling-brand-mark" aria-hidden="true">{cashierInitials}</span>
              <span>
                <strong>{currentOperatorName}</strong>
                <small>{t('sales.currentOperator')} · {t('common.branch', { id: session.storeId ?? '-' })}</small>
              </span>
            </div>
          )}
          <Link to="/history">{t('nav.history')}</Link>
          <Link to="/inventory">{t('nav.inventory')}</Link>
          <Link to="/notifications">{t('nav.notifications')}</Link>
          <button
            type="button"
            onClick={onOpenCustomerDisplay}
            disabled={!hasOpenShift}
            title={hasOpenShift ? t('sales.openCustomerDisplay') : t('sales.openShiftFirst')}
          >
            {t('sales.customerDisplay')}
          </button>
          <Link to="/shift">
            {hasOpenShift && shiftId ? t('sales.shiftDrawer', { id: shiftId }) : t('sales.openShiftAndDrawer')}
          </Link>
          <div className="border-t border-border px-2 py-2">
            <PreferenceControls />
          </div>
          <div className="pos-layout-selector" role="group" aria-label={t('sales.layout')}>
            <div className="pos-layout-selector-heading">
              <strong>{t('sales.layout')}</strong>
              <span>{resolvedLayout === 'desktop' ? t('sales.layout.desktop') : t('sales.layout.tablet')}</span>
            </div>
            <div className="pos-layout-options">
              {([
                ['auto', t('sales.layout.auto')],
                ['desktop', t('sales.layout.desktop')],
                ['tablet', t('sales.layout.tablet')],
              ] as const).map(([value, label]) => (
                <button
                  key={value}
                  type="button"
                  onClick={() => onLayoutPreferenceChange(value)}
                  disabled={isLayoutSwitchLocked}
                  aria-pressed={layoutPreference === value}
                >
                  {label}
                </button>
              ))}
            </div>
            {isLayoutSwitchLocked && (
              <p>{t('sales.layoutLocked')}</p>
            )}
          </div>
        </nav>
      </details>
    </header>
  )
}
