import { usePrinterStatus } from '../hooks/usePrinterStatus'

interface PrinterStatusBadgeProps {
  storeId: number
}

export default function PrinterStatusBadge({ storeId }: PrinterStatusBadgeProps) {
  const status = usePrinterStatus(storeId)
  const statusLabel = status === 'ready'
    ? 'Máy in sẵn sàng'
    : status === 'error'
      ? 'Máy in đang lỗi'
      : 'Máy in mất kết nối'

  return (
    <div className="flex items-center gap-1.5 select-none" title={statusLabel} role="status" aria-label={statusLabel}>
      {status === 'ready' && (
        <div className="pos-touch-target flex items-center justify-center gap-1.5 px-2 rounded-lg bg-[var(--pos-success-soft)] border border-success/30 text-success">
          {/* Printer Icon - Solid Green */}
          <svg className="w-4 h-4 text-success shrink-0" fill="currentColor" viewBox="0 0 24 24">
            <path d="M19 8H5c-1.66 0-3 1.34-3 3v6h4v4h12v-4h4v-6c0-1.66-1.34-3-3-3zm-3 11H8v-5h8v5zm3-7c-.55 0-1-.45-1-1s.45-1 1-1 1 .45 1 1-.45 1-1 1zm-1-9H6v4h12V3z"/>
          </svg>
          <span className="hidden 2xl:inline text-xs font-bold">Sẵn sàng in</span>
        </div>
      )}

      {status === 'error' && (
        <div className="pos-touch-target flex items-center justify-center gap-1.5 px-2 rounded-lg bg-[var(--pos-danger-soft)] border border-danger/30 text-danger">
          {/* Printer Icon - Solid Red */}
          <svg className="w-4 h-4 text-danger shrink-0" fill="currentColor" viewBox="0 0 24 24">
            <path d="M19 8H5c-1.66 0-3 1.34-3 3v6h4v4h12v-4h4v-6c0-1.66-1.34-3-3-3zm-3 11H8v-5h8v5zm3-7c-.55 0-1-.45-1-1s.45-1 1-1 1 .45 1 1-.45 1-1 1zm-1-9H6v4h12V3z"/>
          </svg>
          <span className="hidden 2xl:inline text-xs font-bold">Lỗi máy in</span>
        </div>
      )}

      {status === 'offline' && (
        <div className="pos-touch-target flex items-center justify-center gap-1.5 px-2 rounded-lg bg-[var(--pos-danger-soft)] border border-danger/30 text-danger">
          {/* Printer Icon - Offline Red */}
          <svg className="w-4 h-4 text-danger shrink-0" fill="currentColor" viewBox="0 0 24 24">
            <path d="M19 8H5c-1.66 0-3 1.34-3 3v6h4v4h12v-4h4v-6c0-1.66-1.34-3-3-3zm-3 11H8v-5h8v5zm3-7c-.55 0-1-.45-1-1s.45-1 1-1 1 .45 1 1-.45 1-1 1zm-1-9H6v4h12V3z"/>
          </svg>
          <span className="hidden 2xl:inline text-xs font-bold">Mất kết nối máy in</span>
        </div>
      )}
    </div>
  )
}
