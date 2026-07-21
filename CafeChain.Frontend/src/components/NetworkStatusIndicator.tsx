import { useState, useEffect } from 'react'
import { useLiveQuery } from 'dexie-react-hooks'
import { db } from '../db/CafeChainPOSDB'

// ============================================================
// NetworkStatusIndicator — Online/Offline Banner + Sync Badge
// Issue #53: Trực quan hóa trạng thái mạng + hàng đợi offline
// Branding: Solid colors only (Trắng-Cam-Đỏ), NO gradients
// ============================================================

/**
 * Component hiển thị trạng thái kết nối mạng và số đơn hàng chờ sync.
 *
 * 3 trạng thái UI:
 *   1. Online + queue trống → Icon Wi-Fi xám nhạt (compact, tinh tế)
 *   2. Offline → Banner đỏ nổi bật + icon Wi-Fi gạch chéo
 *   3. Online + queue > 0 → Icon Wi-Fi xám + badge cam với số lượng
 *
 * Nhúng vào: POSLayout → Category Header (top bar cột 2)
 */
export default function NetworkStatusIndicator() {
  const [isOnline, setIsOnline] = useState<boolean>(navigator.onLine)

  // ─── Reactive count từ IndexedDB (dexie liveQuery) ───
  const pendingCount = useLiveQuery(
    () => db.cartSyncQueue
      .where('syncStatus')
      .anyOf(['Pending', 'Failed'])
      .count(),
    [],
    0
  )

  // ─── Browser online/offline event listeners ───
  useEffect(() => {
    const handleOnline = () => setIsOnline(true)
    const handleOffline = () => setIsOnline(false)

    window.addEventListener('online', handleOnline)
    window.addEventListener('offline', handleOffline)

    return () => {
      window.removeEventListener('online', handleOnline)
      window.removeEventListener('offline', handleOffline)
    }
  }, [])

  // ═══════════════════════════════════════════════
  // STATE 1: OFFLINE — Banner cảnh báo nổi bật
  // ═══════════════════════════════════════════════
  if (!isOnline) {
    return (
      <div className="flex items-center gap-1 select-none" role="status" aria-label={`Mất kết nối mạng. ${pendingCount} đơn chờ đồng bộ`}>
        {/* Offline Banner */}
        <div className="pos-touch-target flex items-center justify-center gap-1.5 px-2 rounded-lg bg-[var(--pos-danger-soft)] text-danger border border-danger/30">
          {/* Wi-Fi Crossed Icon — SVG Solid White on Solid Red BG */}
          <svg
            className="w-4 h-4 text-danger shrink-0"
            fill="none"
            viewBox="0 0 24 24"
            strokeWidth={2.5}
            stroke="currentColor"
          >
            <path strokeLinecap="round" strokeLinejoin="round" d="M18.364 5.636a9 9 0 010 12.728M15.536 8.464a5 5 0 010 7.072M12 12v.01" />
            <line x1="4" y1="4" x2="20" y2="20" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
          </svg>
          <span className="hidden 2xl:inline text-xs font-bold whitespace-nowrap">
            Mất kết nối · Đang bán offline
          </span>
        </div>

        {/* Badge đơn chờ sync (nếu có) */}
        {pendingCount > 0 && (
          <span className="inline-flex items-center justify-center min-w-5 h-5 rounded-md bg-warning text-white text-xs font-bold" title="Số đơn hàng đang chờ đồng bộ">
            {pendingCount}
          </span>
        )}
      </div>
    )
  }

  // ═══════════════════════════════════════════════
  // STATE 2: ONLINE + có đơn chờ sync
  // ═══════════════════════════════════════════════
  if (pendingCount > 0) {
    return (
      <div className="pos-touch-target relative flex items-center gap-1.5 px-2 rounded-lg bg-[var(--pos-warning-soft)] border border-warning/30" role="status" aria-label={`${pendingCount} đơn đang chờ đồng bộ`}>
        {/* Wi-Fi Icon — Xám (đang online) */}
        <svg
          className="w-4 h-4 text-text-secondary"
          fill="none"
          viewBox="0 0 24 24"
          strokeWidth={2}
          stroke="currentColor"
        >
          <path strokeLinecap="round" strokeLinejoin="round" d="M8.288 15.038a5.25 5.25 0 017.424 0M5.106 11.856c3.807-3.808 9.98-3.808 13.788 0M1.924 8.674c5.565-5.565 14.587-5.565 20.152 0M12 18.75h.007v.008H12v-.008z" />
        </svg>

        {/* Pending count badge — Cam đặc */}
        <span className="hidden 2xl:inline text-xs font-bold text-warning whitespace-nowrap">
          Đồng bộ
        </span>
        <span className="inline-flex items-center justify-center min-w-5 h-5 rounded-md bg-warning text-white text-xs font-bold">
          {pendingCount}
        </span>
      </div>
    )
  }

  // ═══════════════════════════════════════════════
  // STATE 3: ONLINE + queue trống — compact icon
  // ═══════════════════════════════════════════════
  return (
    <div className="pos-touch-target flex items-center justify-center gap-1 px-2 rounded-lg" title="Kết nối ổn định" role="status" aria-label="Đang online, kết nối ổn định">
      {/* Wi-Fi Icon — Xám nhạt, tinh tế */}
      <svg
        className="w-3.5 h-3.5 text-text-muted"
        fill="none"
        viewBox="0 0 24 24"
        strokeWidth={2}
        stroke="currentColor"
      >
        <path strokeLinecap="round" strokeLinejoin="round" d="M8.288 15.038a5.25 5.25 0 017.424 0M5.106 11.856c3.807-3.808 9.98-3.808 13.788 0M1.924 8.674c5.565-5.565 14.587-5.565 20.152 0M12 18.75h.007v.008H12v-.008z" />
      </svg>
      <span className="text-xs text-text-muted hidden 2xl:inline">Online</span>
    </div>
  )
}
