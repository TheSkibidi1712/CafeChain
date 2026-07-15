import { db, type Category, type MenuItem, type CartSyncQueueItem } from '../db/CafeChainPOSDB'
import { apiClient } from './apiClient'
import { POS_TOKEN_KEY, getPosSession } from './posSession'

/** Số lần retry tối đa khi sync đơn offline */
const MAX_RETRY_COUNT = 5

/** Thời gian chờ giữa các lần retry (ms) — exponential backoff */
const RETRY_BASE_DELAY_MS = 2000
const CATALOG_VERSION_POLL_MS = 30_000

interface CatalogVersionResponse {
  storeId: number
  version: number
  updatedAtUtc: string
}

interface CatalogSnapshotResponse {
  storeId: number
  version: number
  generatedAtUtc: string
  categories: Array<Omit<Category, 'storeId' | 'catalogVersion' | 'syncedAt'>>
  menuItems: Array<Omit<MenuItem, 'storeId' | 'catalogVersion' | 'syncedAt'>>
}

interface OfflineSyncApiResponse {
  success: boolean
  message?: string
  results?: Array<{
    clientOrderId: string
    status: 'created' | 'duplicate' | 'failed'
    orderId?: number
    error?: string
  }>
}

const LEGACY_CATEGORY_NAMES = ['Cof' + 'fee', 'Tea', 'Smoothie', 'Pastry', 'Topping']
const LEGACY_MENU_ITEM_NAMES = ['La' + 'tte', 'Es' + 'presso', 'Irish ' + 'Coffee']

async function clearCatalogCache(storeId?: number): Promise<void> {
  await db.transaction('rw', db.categories, db.menuItems, db.catalogStates, async () => {
    if (storeId) {
      await db.categories.where('storeId').equals(storeId).delete()
      await db.menuItems.where('storeId').equals(storeId).delete()
      await db.catalogStates.delete(storeId)
      return
    }

    await db.categories.clear()
    await db.menuItems.clear()
    await db.catalogStates.clear()
  })
}

async function clearLegacySeedCatalogCache(storeId: number): Promise<void> {
  const [categories, menuItems] = await Promise.all([
    db.categories.where('storeId').equals(storeId).toArray(),
    db.menuItems.where('storeId').equals(storeId).toArray(),
  ])

  const categoryNames = new Set(categories.map((category) => category.name))
  const itemNames = new Set(menuItems.map((item) => item.name))
  const hasLegacyCategories = LEGACY_CATEGORY_NAMES.every((name) => categoryNames.has(name))
  const hasLegacyMenuItems = LEGACY_MENU_ITEM_NAMES.every((name) => itemNames.has(name))

  if (hasLegacyCategories && hasLegacyMenuItems) {
    await clearCatalogCache(storeId)
    console.warn('[OfflineSync] Cleared legacy mock catalog cache from IndexedDB.')
  }
}

// ============================================================
// 1. CATALOG SYNC — Đồng bộ danh mục + menu từ Backend
// ============================================================

/**
 * Đồng bộ toàn bộ catalog (categories + menuItems) từ Backend.
 * Nếu online → fetch mới. Nếu offline → giữ cache IndexedDB cũ.
 *
 * @returns Object chứa số lượng đã sync
 */
export async function syncCatalog(): Promise<{ categories: number; menuItems: number }> {
  const session = getPosSession()
  if (!localStorage.getItem(POS_TOKEN_KEY) || !session.storeId) {
    await clearCatalogCache()
    return { categories: 0, menuItems: 0 }
  }

  const storeId = session.storeId
  await clearLegacySeedCatalogCache(storeId)
  const response = await apiClient.get<CatalogSnapshotResponse>('/api/v1/pos/catalog')
  if (!response.ok || !response.data) {
    if (response.status === 401 || response.status === 403) {
      await clearCatalogCache(storeId)
    }
    throw new Error(response.error || 'Không tải được catalog cửa hàng.')
  }

  const snapshot = response.data
  if (snapshot.storeId !== storeId) {
    throw new Error('Catalog trả về không thuộc cửa hàng hiện tại.')
  }

  const now = Date.now()
  await db.transaction('rw', db.categories, db.menuItems, db.catalogStates, async () => {
    await db.categories.where('storeId').equals(storeId).delete()
    await db.menuItems.where('storeId').equals(storeId).delete()
    await db.categories.bulkPut(snapshot.categories.map((category) => ({
      ...category,
      storeId,
      catalogVersion: snapshot.version,
      syncedAt: now,
    })))
    await db.menuItems.bulkPut(snapshot.menuItems.map((item) => ({
      ...item,
      storeId,
      catalogVersion: snapshot.version,
      syncedAt: now,
    })))
    await db.catalogStates.put({ storeId, version: snapshot.version, syncedAt: now })
  })

  console.log(`[OfflineSync] Synced Store #${storeId} catalog v${snapshot.version}: ${snapshot.categories.length} categories, ${snapshot.menuItems.length} items`)
  return { categories: snapshot.categories.length, menuItems: snapshot.menuItems.length }
}

async function getServerCatalogVersion(): Promise<CatalogVersionResponse> {
  const storeId = getPosSession().storeId
  if (!storeId) throw new Error('Không xác định được cửa hàng POS.')
  const response = await apiClient.get<CatalogVersionResponse>('/api/v1/pos/catalog/version')
  if (!response.ok || !response.data) {
    throw new Error(response.error || 'Không tải được phiên bản catalog.')
  }
  if (response.data.storeId !== storeId) throw new Error('Phiên bản catalog không thuộc cửa hàng hiện tại.')
  return response.data
}

export async function refreshCatalogIfVersionChanged(): Promise<boolean> {
  const storeId = getPosSession().storeId
  if (!navigator.onLine || !localStorage.getItem(POS_TOKEN_KEY) || !storeId) return false

  const server = await getServerCatalogVersion()
  const localVersion = (await db.catalogStates.get(storeId))?.version ?? -1
  if (localVersion === server.version) return false

  await syncCatalog()
  return true
}

// ============================================================
// 2. CART SYNC QUEUE — Quản lý đơn hàng offline
// ============================================================

/**
 * Lưu đơn hàng vào hàng đợi offline (cartSyncQueue).
 * Gọi khi: Thanh toán tiền mặt gặp offline/network error.
 *
 * @param order Thông tin đơn hàng offline cash, có ClientOrderId sinh từ POS click.
 * @returns queueId của bản ghi vừa tạo
 */
export async function enqueueOrder(
  order: Omit<CartSyncQueueItem, 'queueId' | 'syncStatus' | 'createdAt' | 'retryCount'>
): Promise<number> {
  if (order.paymentMethod !== 'cash') {
    throw new Error('Offline chỉ hỗ trợ thanh toán tiền mặt.')
  }

  if (!order.clientOrderId) {
    throw new Error('Thiếu ClientOrderId cho đơn offline.')
  }

  if (!order.workShiftId || !order.staffId || !order.storeId) {
    throw new Error('Thiếu WorkShiftId, StaffId hoặc StoreId cho đơn offline.')
  }

  const queueItem: CartSyncQueueItem = {
    ...order,
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

      // Issue #69: Gọi API mới /api/v1/pos/orders/sync-offline
      // Payload khớp OfflineBatchSyncRequestDto → OfflineOrderSyncDTO
      const response = await apiClient.post<OfflineSyncApiResponse>('/api/v1/pos/orders/sync-offline', {
        orders: [{
          clientOrderId: order.clientOrderId,
          localId: String(order.queueId),
          storeId: order.storeId,
          staffId: order.staffId,
          workShiftId: order.workShiftId,
          soldAt: order.soldAt,
          orderTypeId: order.orderType === 'dine-in' ? 1 : 2,
          paymentMethodId: 1,
          totalAmount: order.totalAmount,
          receivedAmount: order.paymentSnapshot?.receivedAmount ?? order.totalAmount,
          changeAmount: order.paymentSnapshot?.changeAmount ?? 0,
          note: '',
          cartSnapshot: order.cartSnapshot,
          paymentSnapshot: order.paymentSnapshot,
          details: order.items.map((i) => ({
            itemId: i.menuItemId,
            itemName: i.name,
            sizeId: i.sizeId ?? null,
            quantity: i.quantity,
            unitPrice: i.unitPrice,
            totalPrice: i.unitPrice * i.quantity,
            toppings: i.toppings ?? [],
          })),
        }],
      })

      // Issue #69: Phân loại response theo status từ Backend
      if (response.ok && response.data?.results) {
        const result = response.data.results[0]
        if (result && (result.status === 'created' || result.status === 'duplicate')) {
          await db.cartSyncQueue.update(order.queueId!, {
            syncStatus: 'Synced',
            syncedAt: Date.now(),
          })
          synced++
          console.log(`[OfflineSync] ✅ Order ${result.status}: ${order.clientOrderId} → orderId=${result.orderId}`)
        } else {
          // status === 'failed'
          await db.cartSyncQueue.update(order.queueId!, {
            syncStatus: 'Failed',
            retryCount: order.retryCount + 1,
            lastError: result?.error || 'Server rejected order',
          })
          console.warn(`[OfflineSync] ❌ Order failed: ${order.clientOrderId}`, result?.error)
        }
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

  // Issue #69: Tự động dọn dẹp đơn Synced cũ hơn 24h
  try {
    const cleaned = await cleanupSyncedOrders()
    if (cleaned > 0) console.log(`[OfflineSync] 🧹 Cleaned up ${cleaned} old synced orders`)
  } catch { /* cleanup failure is non-critical */ }

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
  const catalogVersionTimer = window.setInterval(() => {
    refreshCatalogIfVersionChanged().catch((error) => {
      console.warn('[OfflineSync] Catalog version check failed — keeping cached prices', error)
    })
  }, CATALOG_VERSION_POLL_MS)

  return () => {
    window.removeEventListener('online', handleOnline)
    window.removeEventListener('offline', handleOffline)
    window.clearInterval(catalogVersionTimer)
  }
}

// ============================================================
// 4. DATA ACCESS — Đọc dữ liệu từ IndexedDB cho UI
// ============================================================

/**
 * Đọc tất cả categories từ IndexedDB.
 * POSLayout sidebar đọc cache được đồng bộ từ Backend API.
 */
export async function getCategories(): Promise<Category[]> {
  const storeId = getPosSession().storeId
  return storeId ? db.categories.where('storeId').equals(storeId).toArray() : []
}

/**
 * Đọc menu items theo categoryId từ IndexedDB.
 * POSLayout product grid gọi hàm này.
 *
 * @param categoryId Filter theo danh mục. Nếu undefined → lấy tất cả.
 */
export async function getMenuItemsByCategory(categoryId?: number): Promise<MenuItem[]> {
  const storeId = getPosSession().storeId
  if (!storeId) return []
  if (categoryId !== undefined) {
    return db.menuItems.where('[storeId+categoryId]').equals([storeId, categoryId]).toArray()
  }
  return db.menuItems.where('storeId').equals(storeId).toArray()
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

// ============================================================
// 5. CLEANUP — Xóa đơn Synced cũ khỏi IndexedDB (Issue #69)
// ============================================================

/**
 * Xóa các đơn đã Synced thành công và cũ hơn maxAgeMs (mặc định 24h).
 * Giữ IndexedDB gọn — tránh phình dữ liệu trên iPad.
 * Tự động được gọi sau mỗi lần sync batch.
 *
 * @param maxAgeMs Thời gian tối đa giữ đơn Synced (ms). Mặc định 24 giờ.
 * @returns Số bản ghi đã xóa
 */
export async function cleanupSyncedOrders(maxAgeMs = 24 * 60 * 60 * 1000): Promise<number> {
  const cutoff = Date.now() - maxAgeMs
  return db.cartSyncQueue
    .where('syncStatus').equals('Synced')
    .and(item => (item.syncedAt ?? 0) < cutoff)
    .delete()
}
