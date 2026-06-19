import { db, type Category, type MenuItem, type CartSyncQueueItem } from '../db/CafeChainPOSDB'
import { apiClient } from './apiClient'

/** Số lần retry tối đa khi sync đơn offline */
const MAX_RETRY_COUNT = 5

/** Thời gian chờ giữa các lần retry (ms) — exponential backoff */
const RETRY_BASE_DELAY_MS = 2000

// ============================================================
// 1. CATALOG SYNC — Đồng bộ danh mục + menu từ Backend
// ============================================================

/**
 * Fetch danh mục đồ uống từ Backend API và ghi đè vào IndexedDB.
 * Gọi khi: App khởi động, hoặc khi online trở lại sau offline.
 *
 * @returns Số lượng categories đã sync
 * @throws Error nếu API không phản hồi (offline → giữ cache cũ)
 */
export async function syncCategories(): Promise<number> {
  try {
    const response = await apiClient.get<Category[]>('/api/v1/pos/categories')

    if (!response.ok || !response.data) {
      throw new Error(response.error || 'Failed to fetch categories')
    }

    const data = response.data
    const now = Date.now()

    // Ghi đè toàn bộ categories (clear + bulk add)
    await db.transaction('rw', db.categories, async () => {
      await db.categories.clear()
      await db.categories.bulkAdd(
        data.map((cat) => ({ ...cat, syncedAt: now }))
      )
    })

    console.log(`[OfflineSync] ✅ Synced ${data.length} categories`)
    return data.length
  } catch (error) {
    console.warn('[OfflineSync] ⚠️ Categories sync failed — using cached data', error)
    throw error
  }
}

export async function syncMenuItems(): Promise<number> {
  try {
    const response = await apiClient.get<MenuItem[]>('/api/v1/pos/menu-items')

    if (!response.ok || !response.data) {
      throw new Error(response.error || 'Failed to fetch menu items')
    }

    const data = response.data
    const now = Date.now()

    // Ghi đè toàn bộ menuItems (clear + bulk add)
    await db.transaction('rw', db.menuItems, async () => {
      await db.menuItems.clear()
      await db.menuItems.bulkAdd(
        data.map((item) => ({ ...item, syncedAt: now }))
      )
    })

    console.log(`[OfflineSync] ✅ Synced ${data.length} menu items`)
    return data.length
  } catch (error) {
    console.warn('[OfflineSync] ⚠️ Menu items sync failed — using cached data', error)
    throw error
  }
}

/**
 * Đồng bộ toàn bộ catalog (categories + menuItems) từ Backend.
 * Nếu online → fetch mới. Nếu offline → giữ cache IndexedDB cũ.
 *
 * @returns Object chứa số lượng đã sync
 */
export async function syncCatalog(): Promise<{ categories: number; menuItems: number }> {
  let categories = 0
  let menuItems = 0

  try {
    categories = await syncCategories()
  } catch { /* cache cũ vẫn ok */ }

  try {
    menuItems = await syncMenuItems()
  } catch { /* cache cũ vẫn ok */ }

  return { categories, menuItems }
}

// ============================================================
// 2. CART SYNC QUEUE — Quản lý đơn hàng offline
// ============================================================

/**
 * Tạo UUID v4 cho Idempotency Key (ADR-0002).
 * Không phụ thuộc thư viện ngoài — dùng crypto.randomUUID().
 */
function generateClientOrderId(): string {
  if (typeof crypto !== 'undefined' && crypto.randomUUID) {
    return crypto.randomUUID()
  }
  // Fallback cho browser cũ
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0
    const v = c === 'x' ? r : (r & 0x3) | 0x8
    return v.toString(16)
  })
}

/**
 * Lưu đơn hàng vào hàng đợi offline (cartSyncQueue).
 * Gọi khi: Nhấn "Thanh toán" — bất kể online/offline.
 * Đơn sẽ được sync lên Backend ngay nếu có mạng, hoặc nằm chờ nếu offline.
 *
 * @param order Thông tin đơn hàng (không cần queueId, clientOrderId, syncStatus)
 * @returns queueId của bản ghi vừa tạo
 */
export async function enqueueOrder(
  order: Omit<CartSyncQueueItem, 'queueId' | 'clientOrderId' | 'syncStatus' | 'createdAt' | 'retryCount'>
): Promise<number> {
  const queueItem: CartSyncQueueItem = {
    ...order,
    clientOrderId: generateClientOrderId(),
    syncStatus: 'Pending',
    createdAt: Date.now(),
    retryCount: 0,
  }

  const queueId = await db.cartSyncQueue.add(queueItem)
  console.log(`[OfflineSync] 📝 Order enqueued: queueId=${queueId}, clientOrderId=${queueItem.clientOrderId}`)

  // Attempt immediate sync nếu online
  if (navigator.onLine) {
    syncPendingOrders().catch(() => {
      /* silent — sẽ retry sau */
    })
  }

  return queueId as number
}

/**
 * Đồng bộ tất cả đơn hàng Pending lên Backend API.
 * Gọi khi: Online trở lại, hoặc theo interval định kỳ.
 *
 * Luồng xử lý mỗi đơn:
 *   1. Đổi status → Syncing
 *   2. POST lên Backend với clientOrderId (Idempotency Key)
 *   3. Thành công → status = Synced, ghi syncedAt
 *   4. Thất bại → status = Failed, tăng retryCount, ghi lastError
 *
 * @returns Số đơn sync thành công
 */
export async function syncPendingOrders(): Promise<number> {
  const pendingOrders = await db.cartSyncQueue
    .where('syncStatus')
    .anyOf(['Pending', 'Failed'])
    .and((item) => item.retryCount < MAX_RETRY_COUNT)
    .toArray()

  if (pendingOrders.length === 0) return 0

  console.log(`[OfflineSync] 🔄 Syncing ${pendingOrders.length} pending orders...`)
  let synced = 0

  for (const order of pendingOrders) {
    try {
      // Mark as Syncing
      await db.cartSyncQueue.update(order.queueId!, { syncStatus: 'Syncing' })

      const response = await apiClient.post<any>('/api/AdminPOS/sync-offline-orders', {
        orders: [{
          clientOrderId: order.clientOrderId,
          storeId: order.storeId,
          staffId: order.staffId,
          workShiftId: order.workShiftId,
          orderTypeId: order.orderType === 'dine-in' ? 1 : 2,
          items: order.items.map((i) => ({
            drinkId: i.menuItemId,
            quantity: i.quantity,
            unitPrice: i.unitPrice,
          })),
          paymentMethodId: order.paymentMethod === 'cash' ? 1 : 2,
          totalAmount: order.totalAmount,
        }],
      })

      if (response.ok) {
        await db.cartSyncQueue.update(order.queueId!, {
          syncStatus: 'Synced',
          syncedAt: Date.now(),
        })
        synced++
        console.log(`[OfflineSync] ✅ Order synced: ${order.clientOrderId}`)
      } else {
        await db.cartSyncQueue.update(order.queueId!, {
          syncStatus: 'Failed',
          retryCount: order.retryCount + 1,
          lastError: response.error || `HTTP ${response.status}`,
        })
        console.warn(`[OfflineSync] ❌ Order sync failed: ${order.clientOrderId}`, response.error)
      }
    } catch (error) {
      await db.cartSyncQueue.update(order.queueId!, {
        syncStatus: 'Failed',
        retryCount: order.retryCount + 1,
        lastError: error instanceof Error ? error.message : String(error),
      })
      console.warn(`[OfflineSync] ❌ Network error for: ${order.clientOrderId}`, error)
    }

    // Exponential backoff giữa các đơn
    if (pendingOrders.indexOf(order) < pendingOrders.length - 1) {
      await new Promise((r) => setTimeout(r, RETRY_BASE_DELAY_MS))
    }
  }

  console.log(`[OfflineSync] 📊 Sync complete: ${synced}/${pendingOrders.length} succeeded`)
  return synced
}

// ============================================================
// 3. ONLINE/OFFLINE LISTENERS — Auto-sync khi có mạng lại
// ============================================================

/**
 * Đăng ký event listeners cho online/offline.
 * Gọi 1 lần khi App mount (trong useEffect).
 *
 * - Online → sync catalog + flush pending orders
 * - Offline → log warning
 *
 * @returns Cleanup function để gỡ listeners
 */
export function registerConnectivityListeners(): () => void {
  const handleOnline = () => {
    console.log('[OfflineSync] 🌐 Online — starting sync...')
    syncCatalog().catch(() => {})
    syncPendingOrders().catch(() => {})
  }

  const handleOffline = () => {
    console.warn('[OfflineSync] 📴 Offline — orders will be queued locally')
  }

  window.addEventListener('online', handleOnline)
  window.addEventListener('offline', handleOffline)

  return () => {
    window.removeEventListener('online', handleOnline)
    window.removeEventListener('offline', handleOffline)
  }
}

// ============================================================
// 4. DATA ACCESS — Đọc dữ liệu từ IndexedDB cho UI
// ============================================================

/**
 * Đọc tất cả categories từ IndexedDB.
 * POSLayout sidebar gọi hàm này thay vì dùng mock data.
 */
export async function getCategories(): Promise<Category[]> {
  return db.categories.toArray()
}

/**
 * Đọc menu items theo categoryId từ IndexedDB.
 * POSLayout product grid gọi hàm này.
 *
 * @param categoryId Filter theo danh mục. Nếu undefined → lấy tất cả.
 */
export async function getMenuItemsByCategory(categoryId?: number): Promise<MenuItem[]> {
  if (categoryId !== undefined) {
    return db.menuItems.where('categoryId').equals(categoryId).toArray()
  }
  return db.menuItems.toArray()
}

/**
 * Đếm số đơn hàng đang chờ sync (Pending + Failed).
 * Hiển thị badge cảnh báo trên UI nếu > 0.
 */
export async function getPendingOrderCount(): Promise<number> {
  return db.cartSyncQueue
    .where('syncStatus')
    .anyOf(['Pending', 'Failed'])
    .count()
}

/**
 * Lấy tất cả đơn đang chờ sync (cho Admin debug view).
 */
export async function getPendingOrders(): Promise<CartSyncQueueItem[]> {
  return db.cartSyncQueue
    .where('syncStatus')
    .anyOf(['Pending', 'Failed'])
    .toArray()
}
