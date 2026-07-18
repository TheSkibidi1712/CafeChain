import { useCallback, useEffect, useState } from 'react'
import { useLiveQuery } from 'dexie-react-hooks'
import { db } from '../db/CafeChainPOSDB'
import type { Category, MenuItem } from '../db/CafeChainPOSDB'
import {
  syncCatalog,
  registerConnectivityListeners,
  getPendingOrderCount,
} from '../services/OfflineSyncService'
import { getPosSession, type PosSession } from '../services/posSession'

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
  const [catalogError, setCatalogError] = useState<string | null>(null)
  const [isOnline, setIsOnline] = useState(navigator.onLine)
  const [pendingCount, setPendingCount] = useState(0)
  const [storeId, setStoreId] = useState<number | null>(() => getPosSession().storeId)

  // ─── Reactive Queries — auto-update khi IndexedDB thay đổi ───
  const categories = useLiveQuery(
    () => storeId ? db.categories.where('storeId').equals(storeId).toArray() : [],
    [storeId]
  ) ?? []
  const menuItems = useLiveQuery(
    () => storeId ? db.menuItems.where('storeId').equals(storeId).toArray() : [],
    [storeId]
  ) ?? []

  const refreshCatalog = useCallback(async () => {
    setIsLoading(true)
    setCatalogError(null)
    try {
      await syncCatalog()
    } catch (error) {
      console.error('[POS Catalog] Không thể đồng bộ catalog:', error)
      setCatalogError(error instanceof Error
        ? error.message
        : 'Không tải được catalog cửa hàng.')
    } finally {
      setIsLoading(false)
    }
  }, [])

  // ─── Khởi tạo: Sync + Listeners ───
  useEffect(() => {
    let mounted = true

    queueMicrotask(() => {
      if (mounted) void refreshCatalog()
    })

    // Đăng ký online/offline listeners
    const cleanupConnectivity = registerConnectivityListeners()

    // Track online status
    const handleOnline = () => { setIsOnline(true); refreshPendingCount() }
    const handleOffline = () => setIsOnline(false)
    window.addEventListener('online', handleOnline)
    window.addEventListener('offline', handleOffline)
    const handleSessionChanged = (event: Event) => {
      const session = (event as CustomEvent<PosSession>).detail ?? getPosSession()
      setStoreId(session.storeId)
      refreshCatalog()
    }
    window.addEventListener('pos-session-changed', handleSessionChanged)

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
      window.removeEventListener('pos-session-changed', handleSessionChanged)
      clearInterval(interval)
    }
  }, [refreshCatalog])

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
    /** Lỗi sync catalog gần nhất; cache cũ vẫn được giữ nếu đọc được. */
    catalogError,
    /** Thử đồng bộ lại catalog theo yêu cầu của thu ngân. */
    refreshCatalog,
  }
}
