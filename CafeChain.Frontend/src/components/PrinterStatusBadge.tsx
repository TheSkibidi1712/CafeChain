import { usePrinterStatus } from '../hooks/usePrinterStatus'

export default function PrinterStatusBadge() {
  const status = usePrinterStatus(1) // Default Store ID is 1

  return (
    <div className="flex items-center gap-1.5 select-none" title="Trạng thái máy in">
      {status === 'ready' && (
        <div className="flex items-center gap-1.5 px-2.5 py-1.5 rounded-full bg-emerald-50 border border-emerald-200 text-emerald-700 shadow-sm">
          {/* Printer Icon - Solid Green */}
          <svg className="w-3.5 h-3.5 text-emerald-600 shrink-0" fill="currentColor" viewBox="0 0 24 24">
            <path d="M19 8H5c-1.66 0-3 1.34-3 3v6h4v4h12v-4h4v-6c0-1.66-1.34-3-3-3zm-3 11H8v-5h8v5zm3-7c-.55 0-1-.45-1-1s.45-1 1-1 1 .45 1 1-.45 1-1 1zm-1-9H6v4h12V3z"/>
          </svg>
          <span className="text-[10px] font-extrabold uppercase tracking-wide">Sẵn sàng in</span>
        </div>
      )}

      {status === 'error' && (
        <div className="flex items-center gap-1.5 px-2.5 py-1.5 rounded-full bg-red-50 border border-red-200 text-red-600 shadow-sm animate-pulse">
          {/* Printer Icon - Solid Red */}
          <svg className="w-3.5 h-3.5 text-red-600 shrink-0" fill="currentColor" viewBox="0 0 24 24">
            <path d="M19 8H5c-1.66 0-3 1.34-3 3v6h4v4h12v-4h4v-6c0-1.66-1.34-3-3-3zm-3 11H8v-5h8v5zm3-7c-.55 0-1-.45-1-1s.45-1 1-1 1 .45 1 1-.45 1-1 1zm-1-9H6v4h12V3z"/>
          </svg>
          <span className="text-[10px] font-extrabold uppercase tracking-wide">Lỗi máy in</span>
        </div>
      )}

      {status === 'offline' && (
        <div className="flex items-center gap-1.5 px-2.5 py-1.5 rounded-full bg-gray-50 border border-gray-200 text-gray-500 shadow-sm">
          {/* Printer Icon - Gray */}
          <svg className="w-3.5 h-3.5 text-gray-400 shrink-0" fill="currentColor" viewBox="0 0 24 24">
            <path d="M19 8H5c-1.66 0-3 1.34-3 3v6h4v4h12v-4h4v-6c0-1.66-1.34-3-3-3zm-3 11H8v-5h8v5zm3-7c-.55 0-1-.45-1-1s.45-1 1-1 1 .45 1 1-.45 1-1 1zm-1-9H6v4h12V3z"/>
          </svg>
          <span className="text-[10px] font-extrabold uppercase tracking-wide">Mất kết nối</span>
        </div>
      )}
    </div>
  )
}
