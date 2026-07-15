import Dexie, { type EntityTable, type Table } from 'dexie'

// ============================================================
// CafeChainPOS_DB — IndexedDB Schema Definition
// Issue #51: Offline-first architecture cho POS iPad
// Thay thế localStorage bằng IndexedDB qua Dexie.js
// ============================================================

/**
 * Danh mục đồ uống — đồng bộ từ Backend API
 * Endpoint: GET /api/v1/pos/categories
 */
export interface Category {
  storeId: number
  catalogVersion: number
  /** Primary Key — từ Backend (categories.CategoryId) */
  id: number
  /** Tên danh mục từ Backend */
  name: string
  /** Emoji icon hiển thị trên sidebar */
  icon: string
  /** Số lượng món trong danh mục */
  count: number
  /** Timestamp lần sync gần nhất (epoch ms) */
  syncedAt: number
}

/**
 * Món ăn / đồ uống trong menu — đồng bộ từ Backend API
 * Endpoint: GET /api/v1/pos/menu-items
 */
export interface MenuItem {
  storeId: number
  catalogVersion: number
  /** Primary Key — từ Backend (drinks.DrinkId) */
  id: number
  /** Tên món hiển thị trên thẻ sản phẩm */
  name: string
  /** Giá bán (VNĐ) */
  price: number
  /** FK → Category.id */
  categoryId: number
  /** URL ảnh sản phẩm (optional) */
  image?: string
  /** true = co the them vao gio; false = hien thi disabled kem ly do */
  isAvailable: boolean
  /** Ma trang thai kha dung tu backend */
  availabilityStatus?: string
  /** Ly do tieng Viet khi mon tam thoi khong ban duoc */
  availabilityReason?: string | null
  /** Danh sách size khả dụng từ Backend */
  sizes?: MenuItemSize[]
  /** Danh sách topping khả dụng cho món này từ Backend */
  availableToppings?: ToppingOption[]
  /** Timestamp lần sync gần nhất (epoch ms) */
  syncedAt: number
}

export interface MenuItemSize {
  storeMenuItemId: number
  drinkSizeId: number
  sizeId: number
  sizeName: string
  price: number
  globalPrice: number
  storeOverride?: number | null
  priceSource: string
  isAvailable: boolean
  availabilityStatus: string
  availabilityReason?: string | null
}

export interface CatalogState {
  storeId: number
  version: number
  syncedAt: number
}

export interface ToppingOption {
  id: number
  name: string
  price: number
  imageUrl?: string
}

/**
 * Trạng thái đồng bộ của đơn hàng offline
 * - Pending: Chờ gửi lên server (mất mạng)
 * - Syncing: Đang gửi
 * - Synced: Đã gửi thành công
 * - Failed: Gửi thất bại (cần retry)
 */
export type SyncStatus = 'Pending' | 'Syncing' | 'Synced' | 'Failed'

export interface CartQueueToppingSnapshot {
  toppingId: number
  name?: string
  price?: number
}

export interface CartQueueItemSnapshot {
  cartId?: string
  menuItemId: number
  name: string
  categoryId?: number
  sizeId?: number | null
  sizeName?: string
  quantity: number
  unitPrice: number
  note?: string
  detailText?: string
  toppings?: CartQueueToppingSnapshot[]
}

export interface CartQueuePaymentSnapshot {
  method: 'cash'
  paymentMethodId: 1
  amount: number
  receivedAmount: number
  changeAmount: number
  capturedAt: string
}

/**
 * Đơn hàng trong hàng đợi đồng bộ — tạo khi thanh toán offline
 * Lưu toàn bộ payload cần thiết để replay lên Backend khi có mạng lại.
 */
export interface CartSyncQueueItem {
  /** Auto-increment primary key (local) */
  queueId?: number
  /**
   * UUID v4 sinh tại client lúc nhấn "Thanh toán" — Idempotency Key
   * Đảm bảo Backend không tạo trùng đơn khi retry (ADR-0002)
   */
  clientOrderId: string
  /** Mã cửa hàng */
  storeId: number
  /** Mã nhân viên thu ngân */
  staffId: number
  /** Mã ca làm việc POS */
  workShiftId: number
  /** Thời điểm bán thực tế tại POS (ISO string) */
  soldAt: string
  /** Loại order: 'dine-in' | 'take-away' */
  orderType: string
  /** Danh sách sản phẩm trong đơn */
  items: Array<{
    menuItemId: number
    name: string
    sizeId?: number | null
    quantity: number
    unitPrice: number
    note?: string
    toppings?: Array<{ toppingId: number }>
  }>
  /** Snapshot đầy đủ của giỏ hàng lúc thu ngân bấm thanh toán */
  cartSnapshot: CartQueueItemSnapshot[]
  /** Snapshot thanh toán tiền mặt lúc thu ngân bấm thanh toán */
  paymentSnapshot: CartQueuePaymentSnapshot
  /** Tổng tiền */
  totalAmount: number
  /** Phương thức thanh toán: 'cash' | 'banking' */
  paymentMethod: string
  /** Trạng thái đồng bộ */
  syncStatus: SyncStatus
  /** Thời điểm tạo đơn (epoch ms) */
  createdAt: number
  /** Thời điểm đồng bộ thành công (epoch ms, nullable) */
  syncedAt?: number
  /** Số lần retry đã thực hiện */
  retryCount: number
  /** Thông báo lỗi lần gửi gần nhất (nullable) */
  lastError?: string
}

// ============================================================
// Database Class — Dexie v4 typed
// ============================================================

/**
 * CafeChainPOS_DB — IndexedDB database cho POS iPad.
 *
 * Bảng:
 * - `categories`: Danh mục đồ uống (sync từ Backend)
 * - `menuItems`: Menu sản phẩm (sync từ Backend)
 * - `cartSyncQueue`: Hàng đợi đơn hàng offline chờ đồng bộ
 *
 * Schema design:
 * - categories & menuItems: Cache local, luôn được ghi đè khi sync
 * - cartSyncQueue: Append-only queue, chỉ xóa sau khi sync thành công
 */
export class CafeChainPOSDB extends Dexie {
  categories!: Table<Category, [number, number]>
  menuItems!: Table<MenuItem, [number, number]>
  catalogStates!: EntityTable<CatalogState, 'storeId'>
  cartSyncQueue!: EntityTable<CartSyncQueueItem, 'queueId'>

  constructor() {
    super('CafeChainPOS_DB')

    this.version(1).stores({
      // ─── categories ───
      // PK: id (from Backend)
      // Index: name (cho search)
      categories: 'id, name',

      // ─── menuItems ───
      // PK: id (from Backend)
      // Index: categoryId (filter theo danh mục), name (search), isAvailable (filter)
      menuItems: 'id, categoryId, name, isAvailable',

      // ─── cartSyncQueue ───
      // PK: ++queueId (auto-increment local)
      // Index: clientOrderId (unique idempotency key), syncStatus (filter pending), createdAt (sort)
      cartSyncQueue: '++queueId, clientOrderId, syncStatus, createdAt',
    })

    this.version(2).stores({
      categories: '[storeId+id], storeId, [storeId+name]',
      menuItems: '[storeId+id], storeId, [storeId+categoryId], name, isAvailable',
      catalogStates: 'storeId, version',
      cartSyncQueue: '++queueId, clientOrderId, syncStatus, createdAt',
    }).upgrade(async (transaction) => {
      await transaction.table('categories').clear()
      await transaction.table('menuItems').clear()
    })
  }
}

/**
 * Singleton instance — import db từ bất kỳ đâu trong app
 * ```ts
 * import { db } from '@/db/CafeChainPOSDB'
 * const items = await db.menuItems.where('categoryId').equals(1).toArray()
 * ```
 */
export const db = new CafeChainPOSDB()
