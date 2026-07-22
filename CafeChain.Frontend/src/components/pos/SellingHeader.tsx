import { Link } from 'react-router-dom'
import NetworkStatusIndicator from '../NetworkStatusIndicator'
import PrinterStatusBadge from '../PrinterStatusBadge'
import type { PosSession } from '../../services/posSession'

type OrderType = 'dine-in' | 'take-away'

interface SellingHeaderProps {
  orderType: OrderType
  searchQuery: string
  resultCount: number
  isCartLocked: boolean
  hasOpenShift: boolean
  shiftId?: number | null
  session: PosSession
  onOrderTypeChange: (orderType: OrderType) => void
  onSearchChange: (value: string) => void
}

export default function SellingHeader({
  orderType,
  searchQuery,
  resultCount,
  isCartLocked,
  hasOpenShift,
  shiftId,
  session,
  onOrderTypeChange,
  onSearchChange,
}: SellingHeaderProps) {
  const cashierInitials = session.staffName
    .split(' ')
    .filter(Boolean)
    .slice(-2)
    .map((part) => part[0])
    .join('')
    .toUpperCase() || 'POS'

  return (
    <header className="pos-selling-header" aria-label="Thanh công cụ bán hàng">
      <div className="pos-selling-brand" aria-label={`CafeChain POS, chi nhánh ${session.storeId ?? 'chưa xác định'}`}>
        <span className="pos-selling-brand-mark" aria-hidden="true">CC</span>
        <span className="pos-selling-brand-copy">
          <strong>CafeChain POS</strong>
          <small>Chi nhánh #{session.storeId ?? '-'}</small>
        </span>
      </div>

      <div className="pos-order-type-control" role="group" aria-label="Loại đơn tại quầy">
        <button
          type="button"
          onClick={() => onOrderTypeChange('dine-in')}
          disabled={isCartLocked}
          aria-pressed={orderType === 'dine-in'}
          className="pos-order-type-button"
        >
          Dùng tại quán
        </button>
        <button
          type="button"
          onClick={() => onOrderTypeChange('take-away')}
          disabled={isCartLocked}
          aria-pressed={orderType === 'take-away'}
          className="pos-order-type-button"
        >
          Mang đi
        </button>
      </div>

      <label className="pos-selling-search" htmlFor="pos-product-search">
        <span className="sr-only">Tìm món</span>
        <span className="pos-selling-search-icon" aria-hidden="true">⌕</span>
        <input
          id="pos-product-search"
          type="search"
          value={searchQuery}
          onChange={(event) => onSearchChange(event.target.value)}
          placeholder="Tìm món theo tên..."
          autoComplete="off"
        />
        <span className="pos-selling-result-count" aria-label={`${resultCount} món phù hợp`}>
          {resultCount}
        </span>
        {searchQuery && (
          <button
            type="button"
            onClick={() => onSearchChange('')}
            className="pos-selling-search-clear"
            aria-label="Xóa nội dung tìm kiếm"
          >
            Xóa
          </button>
        )}
      </label>

      <div className="pos-selling-status" aria-label="Kết nối và máy in">
        <NetworkStatusIndicator />
        <PrinterStatusBadge storeId={session.storeId ?? 1} />
      </div>

      <div
        className="pos-selling-cashier"
        title={`${session.staffName} · ${session.role}`}
        aria-label={`Thu ngân ${session.staffName}, ${session.role}`}
      >
        <span aria-hidden="true">{cashierInitials}</span>
        <span className="pos-selling-cashier-copy">
          <strong>{session.staffName}</strong>
          <small>{session.role}</small>
        </span>
      </div>

      <details className="pos-selling-more">
        <summary className="pos-selling-more-trigger">Tác vụ</summary>
        <nav className="pos-selling-more-menu" aria-label="Tác vụ POS">
          <Link to="/history">Lịch sử đơn</Link>
          <Link to="/inventory">Kho chi nhánh</Link>
          <Link to="/notifications">Thông báo</Link>
          <Link to="/shift">
            {hasOpenShift && shiftId ? `Két tiền · Ca #${shiftId}` : 'Mở ca và két tiền'}
          </Link>
        </nav>
      </details>
    </header>
  )
}
