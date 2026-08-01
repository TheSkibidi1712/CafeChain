import { useState } from 'react'

export default function PrinterStatusSimulator() {
  const [activeMock, setActiveMock] = useState<string | null>(null)

  // Only display in development environment
  if (!import.meta.env.DEV) {
    return null
  }

  const triggerMock = (status: 'ready' | 'error' | 'offline') => {
    setActiveMock(status)
    const event = new CustomEvent('mock-printer-status', {
      detail: { status }
    })
    window.dispatchEvent(event)
  }

  const resetMock = () => {
    setActiveMock(null)
    const event = new CustomEvent('mock-printer-status-reset')
    window.dispatchEvent(event)
  }

  return (
    <details className="pos-printer-simulator fixed bottom-4 left-4 z-50 w-72 select-none rounded-xl border border-border bg-white shadow-[var(--pos-shadow)]">
      <summary className="pos-printer-simulator-summary pos-touch-target flex cursor-pointer list-none items-center justify-between gap-3 px-4 py-3">
        <div className="flex items-center gap-1.5">
          <span className="h-2 w-2 rounded-full bg-brand-orange" aria-hidden="true" />
          <h3 className="text-xs font-extrabold text-text-primary uppercase tracking-wide">
            Printer Simulator
          </h3>
        </div>
        <span className="text-xs font-bold text-text-secondary" aria-hidden="true">Mở</span>
      </summary>

      <div className="border-t border-border px-4 pb-4 pt-3">
        {activeMock && (
          <button
            onClick={resetMock}
            className="mb-2 min-h-11 w-full rounded-lg border border-brand-orange-border px-3 text-left text-[11px] font-bold text-brand-orange hover:bg-brand-orange-light cursor-pointer"
          >
            Trở về trạng thái thật
          </button>
        )}

        <div className="flex flex-col gap-2">
        <button
          onClick={() => triggerMock('ready')}
          className={`w-full py-2 px-3 rounded-lg text-xs font-semibold text-left transition-all border flex items-center justify-between cursor-pointer ${
            activeMock === 'ready'
              ? 'bg-emerald-50 border-emerald-500 text-emerald-700 font-bold shadow-sm'
              : 'bg-surface hover:bg-emerald-50/50 hover:border-emerald-200 border-transparent text-text-secondary'
          }`}
        >
          <span>Mock: Sẵn sàng (Ready)</span>
          <span className="w-2.5 h-2.5 rounded-full bg-emerald-500"></span>
        </button>

        <button
          onClick={() => triggerMock('error')}
          className={`w-full py-2 px-3 rounded-lg text-xs font-semibold text-left transition-all border flex items-center justify-between cursor-pointer ${
            activeMock === 'error'
              ? 'bg-red-50 border-red-500 text-red-700 font-bold shadow-sm'
              : 'bg-surface hover:bg-red-50/50 hover:border-red-200 border-transparent text-text-secondary'
          }`}
        >
          <span>Mock: Kẹt giấy (Error)</span>
          <span className="w-2.5 h-2.5 rounded-full bg-red-500 animate-ping"></span>
        </button>

        <button
          onClick={() => triggerMock('offline')}
          className={`w-full py-2 px-3 rounded-lg text-xs font-semibold text-left transition-all border flex items-center justify-between cursor-pointer ${
            activeMock === 'offline'
              ? 'bg-gray-100 border-gray-500 text-gray-700 font-bold shadow-sm'
              : 'bg-surface hover:bg-gray-100 hover:border-gray-200 border-transparent text-text-secondary'
          }`}
        >
          <span>Mock: Đứt cáp (Offline)</span>
          <span className="w-2.5 h-2.5 rounded-full bg-gray-400"></span>
        </button>
        </div>

        <div className="mt-3 text-[9px] text-text-muted flex justify-between">
          <span>Môi trường: Development</span>
          <span>Active: {activeMock ? activeMock.toUpperCase() : 'REAL Hub'}</span>
        </div>
      </div>
    </details>
  )
}
