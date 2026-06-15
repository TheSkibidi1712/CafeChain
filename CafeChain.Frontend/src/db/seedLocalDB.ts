import { db } from '../db/CafeChainPOSDB'
import type { Category, MenuItem } from '../db/CafeChainPOSDB'

// ============================================================
// seedLocalDB — Fill mock data vào IndexedDB cho Development
// Gọi khi IndexedDB rỗng (chưa sync Backend lần nào)
// ============================================================

const SEED_CATEGORIES: Category[] = [
  { id: 1, name: 'Coffee', icon: '☕', count: 14, syncedAt: Date.now() },
  { id: 2, name: 'Tea', icon: '🍵', count: 8, syncedAt: Date.now() },
  { id: 3, name: 'Smoothie', icon: '🥤', count: 6, syncedAt: Date.now() },
  { id: 4, name: 'Pastry', icon: '🧁', count: 5, syncedAt: Date.now() },
  { id: 5, name: 'Topping', icon: '🧋', count: 4, syncedAt: Date.now() },
]

const SEED_MENU_ITEMS: MenuItem[] = [
  { id: 1, name: 'Cà Phê Sữa Đá', price: 35000, categoryId: 1, isAvailable: true, syncedAt: Date.now() },
  { id: 2, name: 'Cà Phê Đen', price: 29000, categoryId: 1, isAvailable: true, syncedAt: Date.now() },
  { id: 3, name: 'Bạc Xỉu', price: 39000, categoryId: 1, isAvailable: true, syncedAt: Date.now() },
  { id: 4, name: 'Latte', price: 49000, categoryId: 1, isAvailable: true, syncedAt: Date.now() },
  { id: 5, name: 'Cappuccino', price: 49000, categoryId: 1, isAvailable: true, syncedAt: Date.now() },
  { id: 6, name: 'Americano', price: 39000, categoryId: 1, isAvailable: true, syncedAt: Date.now() },
  { id: 7, name: 'Espresso', price: 35000, categoryId: 1, isAvailable: true, syncedAt: Date.now() },
  { id: 8, name: 'Mocha', price: 55000, categoryId: 1, isAvailable: true, syncedAt: Date.now() },
  { id: 9, name: 'Caramel Macchiato', price: 55000, categoryId: 1, isAvailable: true, syncedAt: Date.now() },
  { id: 10, name: 'Cold Brew', price: 45000, categoryId: 1, isAvailable: true, syncedAt: Date.now() },
  { id: 11, name: 'Vietnamese Drip', price: 29000, categoryId: 1, isAvailable: true, syncedAt: Date.now() },
  { id: 12, name: 'Flat White', price: 49000, categoryId: 1, isAvailable: true, syncedAt: Date.now() },
  { id: 13, name: 'Affogato', price: 55000, categoryId: 1, isAvailable: true, syncedAt: Date.now() },
  { id: 14, name: 'Irish Coffee', price: 65000, categoryId: 1, isAvailable: true, syncedAt: Date.now() },
  { id: 15, name: 'Trà Sen Vàng', price: 45000, categoryId: 2, isAvailable: true, syncedAt: Date.now() },
  { id: 16, name: 'Trà Đào', price: 45000, categoryId: 2, isAvailable: true, syncedAt: Date.now() },
  { id: 17, name: 'Trà Vải', price: 45000, categoryId: 2, isAvailable: true, syncedAt: Date.now() },
  { id: 18, name: 'Trà Ô Long', price: 35000, categoryId: 2, isAvailable: true, syncedAt: Date.now() },
  { id: 19, name: 'Trà Chanh', price: 30000, categoryId: 2, isAvailable: true, syncedAt: Date.now() },
  { id: 20, name: 'Trà Sữa Trân Châu', price: 45000, categoryId: 2, isAvailable: true, syncedAt: Date.now() },
  { id: 21, name: 'Trà Matcha', price: 50000, categoryId: 2, isAvailable: true, syncedAt: Date.now() },
  { id: 22, name: 'Trà Hoa Cúc', price: 35000, categoryId: 2, isAvailable: true, syncedAt: Date.now() },
  { id: 23, name: 'Sinh Tố Bơ', price: 55000, categoryId: 3, isAvailable: true, syncedAt: Date.now() },
  { id: 24, name: 'Sinh Tố Dâu', price: 55000, categoryId: 3, isAvailable: true, syncedAt: Date.now() },
  { id: 25, name: 'Sinh Tố Xoài', price: 50000, categoryId: 3, isAvailable: true, syncedAt: Date.now() },
  { id: 26, name: 'Sinh Tố Chuối', price: 45000, categoryId: 3, isAvailable: true, syncedAt: Date.now() },
  { id: 27, name: 'Sinh Tố Việt Quất', price: 60000, categoryId: 3, isAvailable: true, syncedAt: Date.now() },
  { id: 28, name: 'Sinh Tố Dưa Hấu', price: 45000, categoryId: 3, isAvailable: true, syncedAt: Date.now() },
  { id: 29, name: 'Bánh Mì', price: 25000, categoryId: 4, isAvailable: true, syncedAt: Date.now() },
  { id: 30, name: 'Croissant', price: 35000, categoryId: 4, isAvailable: true, syncedAt: Date.now() },
  { id: 31, name: 'Muffin', price: 30000, categoryId: 4, isAvailable: true, syncedAt: Date.now() },
  { id: 32, name: 'Tiramisu', price: 45000, categoryId: 4, isAvailable: true, syncedAt: Date.now() },
  { id: 33, name: 'Cookie', price: 20000, categoryId: 4, isAvailable: true, syncedAt: Date.now() },
  { id: 34, name: 'Trân Châu', price: 10000, categoryId: 5, isAvailable: true, syncedAt: Date.now() },
  { id: 35, name: 'Thạch', price: 10000, categoryId: 5, isAvailable: true, syncedAt: Date.now() },
  { id: 36, name: 'Pudding', price: 12000, categoryId: 5, isAvailable: true, syncedAt: Date.now() },
  { id: 37, name: 'Kem Tươi', price: 15000, categoryId: 5, isAvailable: true, syncedAt: Date.now() },
]

/**
 * Seed IndexedDB với mock data nếu chưa có dữ liệu.
 * Chỉ chạy khi: categories hoặc menuItems bảng rỗng (first launch / cleared).
 */
export async function seedLocalDB(): Promise<void> {
  const catCount = await db.categories.count()
  const menuCount = await db.menuItems.count()

  if (catCount === 0) {
    await db.categories.bulkAdd(SEED_CATEGORIES)
    console.log(`[Seed] 📦 Seeded ${SEED_CATEGORIES.length} categories`)
  }

  if (menuCount === 0) {
    await db.menuItems.bulkAdd(SEED_MENU_ITEMS)
    console.log(`[Seed] 📦 Seeded ${SEED_MENU_ITEMS.length} menu items`)
  }
}
