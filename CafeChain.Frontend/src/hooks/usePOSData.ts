import { useEffect, useState } from 'react'
import { useLiveQuery } from 'dexie-react-hooks'
import { db } from '../db/CafeChainPOSDB'
import type { Category, MenuItem } from '../db/CafeChainPOSDB'
import {
  syncCatalog,
  registerConnectivityListeners,
  getPendingOrderCount,
} from '../services/OfflineSyncService'

// ============================================================
// usePOSData — React Hook cho dữ liệu POS từ IndexedDB
// Reactive: Tự cập nhật UI khi IndexedDB thay đổi (dexie-react-hooks)
// ============================================================

/**
 * Hook cung cấp dữ liệu categories + menuItems từ IndexedDB.
 * Reactive — UI tự render lại khi IndexedDB thay đổi (qua Dexie liveQuery).
 *
 * Luồng khởi tạo:
 *   1. Mount → đăng ký connectivity listeners
 *   2. Sync catalog từ Backend (nếu online)
 *   3. Đọc dữ liệu từ IndexedDB → render ngay (zero-latency)
 *   4. Online/Offline events → auto re-sync
 *
 * @returns { categories, menuItems, isOnline, pendingOrders, isLoading }
 */
export function usePOSData() {
  const [isLoading, setIsLoading] = useState(true)
  const [isOnline, setIsOnline] = useState(navigator.onLine)
  const [pendingCount, setPendingCount] = useState(0)

  // ─── Reactive Queries — auto-update khi IndexedDB thay đổi ───
  const categories = useLiveQuery(() => db.categories.toArray(), []) ?? []
  const menuItems = useLiveQuery(() => db.menuItems.toArray(), []) ?? []

  // ─── Khởi tạo: Sync + Listeners ───
  useEffect(() => {
    let mounted = true

    const init = async () => {
      try {
        // Sync catalog nếu online, fallback IndexedDB cache nếu offline
        await syncCatalog()
      } catch {
        // Offline hoặc API error — dùng cache cũ
      } finally {
        if (mounted) setIsLoading(false)
      }
    }

    init()

    // Đăng ký online/offline listeners
    const cleanupConnectivity = registerConnectivityListeners()

    // Track online status
    const handleOnline = () => { setIsOnline(true); refreshPendingCount() }
    const handleOffline = () => setIsOnline(false)
    window.addEventListener('online', handleOnline)
    window.addEventListener('offline', handleOffline)

    // Refresh pending count mỗi 10s
    const refreshPendingCount = async () => {
      const count = await getPendingOrderCount()
      if (mounted) setPendingCount(count)
    }
    refreshPendingCount()
    const interval = setInterval(refreshPendingCount, 10000)

    return () => {
      mounted = false
      cleanupConnectivity()
      window.removeEventListener('online', handleOnline)
      window.removeEventListener('offline', handleOffline)
      clearInterval(interval)
    }
  }, [])

  return {
    /** Danh sách danh mục (reactive từ IndexedDB) */
    categories: categories as Category[],
    /** Danh sách sản phẩm (reactive từ IndexedDB) */
    menuItems: menuItems as MenuItem[],
    /** true = đang tải lần đầu */
    isLoading,
    /** true = có kết nối mạng */
    isOnline,
    /** Số đơn hàng đang chờ đồng bộ */
    pendingOrders: pendingCount,
  }
}
