import Dexie, { type EntityTable } from 'dexie'

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
  /** Primary Key — từ Backend (categories.CategoryId) */
  id: number
  /** Tên danh mục (VD: "Coffee", "Tea") */
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
  /** Trạng thái: true = đang bán, false = ngừng bán */
  isAvailable: boolean
  /** Timestamp lần sync gần nhất (epoch ms) */
  syncedAt: number
}

/**
 * Trạng thái đồng bộ của đơn hàng offline
 * - Pending: Chờ gửi lên server (mất mạng)
 * - Syncing: Đang gửi
 * - Synced: Đã gửi thành công
 * - Failed: Gửi thất bại (cần retry)
 */
export type SyncStatus = 'Pending' | 'Syncing' | 'Synced' | 'Failed'

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
  /** Loại order: 'dine-in' | 'take-away' */
  orderType: string
  /** Danh sách sản phẩm trong đơn */
  items: Array<{
    menuItemId: number
    name: string
    quantity: number
    unitPrice: number
  }>
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
  categories!: EntityTable<Category, 'id'>
  menuItems!: EntityTable<MenuItem, 'id'>
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
