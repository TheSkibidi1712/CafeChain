import { useState } from 'react'
import { enqueueOrder } from './services/OfflineSyncService'
import ProductModifierModal, { type ModifierSelection, type ToppingOption } from './components/ProductModifierModal'

// ============================================================
// Mock Data — Thay bằng API call khi tích hợp Backend
// ============================================================

/** Danh mục đồ uống */
interface Category {
  id: number
  name: string
  icon: string
  count: number
}

/** Món trong menu */
interface MenuItem {
  id: number
  name: string
  price: number
  categoryId: number
}

/** Món trong giỏ hàng */
interface CartItem {
  id: number
  cartId: string
  name: string
  price: number
  categoryId: number
  quantity: number
  modifiers?: {
    size: 'S' | 'M' | 'L'
    ice: '0%' | '50%' | '100%'
    sugar: '0%' | '50%' | '100%'
    selectedToppings: ToppingOption[]
    detailText: string
  }
}

const CATEGORIES: Category[] = [
  { id: 1, name: 'Coffee', icon: '☕', count: 14 },
  { id: 2, name: 'Tea', icon: '🍵', count: 8 },
  { id: 3, name: 'Smoothie', icon: '🥤', count: 6 },
  { id: 4, name: 'Pastry', icon: '🧁', count: 5 },
  { id: 5, name: 'Topping', icon: '🧋', count: 4 },
]

const MENU_ITEMS: MenuItem[] = [
  { id: 1, name: 'Cà Phê Sữa Đá', price: 35000, categoryId: 1 },
  { id: 2, name: 'Cà Phê Đen', price: 29000, categoryId: 1 },
  { id: 3, name: 'Bạc Xỉu', price: 39000, categoryId: 1 },
  { id: 4, name: 'Latte', price: 49000, categoryId: 1 },
  { id: 5, name: 'Cappuccino', price: 49000, categoryId: 1 },
  { id: 6, name: 'Americano', price: 39000, categoryId: 1 },
  { id: 7, name: 'Espresso', price: 35000, categoryId: 1 },
  { id: 8, name: 'Mocha', price: 55000, categoryId: 1 },
  { id: 9, name: 'Caramel Macchiato', price: 55000, categoryId: 1 },
  { id: 10, name: 'Cold Brew', price: 45000, categoryId: 1 },
  { id: 11, name: 'Vietnamese Drip', price: 29000, categoryId: 1 },
  { id: 12, name: 'Flat White', price: 49000, categoryId: 1 },
  { id: 13, name: 'Affogato', price: 55000, categoryId: 1 },
  { id: 14, name: 'Irish Coffee', price: 65000, categoryId: 1 },
  { id: 15, name: 'Trà Sen Vàng', price: 45000, categoryId: 2 },
  { id: 16, name: 'Trà Đào', price: 45000, categoryId: 2 },
  { id: 17, name: 'Trà Vải', price: 45000, categoryId: 2 },
  { id: 18, name: 'Trà Ô Long', price: 35000, categoryId: 2 },
  { id: 19, name: 'Trà Chanh', price: 30000, categoryId: 2 },
  { id: 20, name: 'Trà Sữa Trân Châu', price: 45000, categoryId: 2 },
  { id: 21, name: 'Trà Matcha', price: 50000, categoryId: 2 },
  { id: 22, name: 'Trà Hoa Cúc', price: 35000, categoryId: 2 },
  { id: 23, name: 'Sinh Tố Bơ', price: 55000, categoryId: 3 },
  { id: 24, name: 'Sinh Tố Dâu', price: 55000, categoryId: 3 },
  { id: 25, name: 'Sinh Tố Xoài', price: 50000, categoryId: 3 },
  { id: 26, name: 'Sinh Tố Chuối', price: 45000, categoryId: 3 },
  { id: 27, name: 'Sinh Tố Việt Quất', price: 60000, categoryId: 3 },
  { id: 28, name: 'Sinh Tố Dưa Hấu', price: 45000, categoryId: 3 },
  { id: 29, name: 'Bánh Mì', price: 25000, categoryId: 4 },
  { id: 30, name: 'Croissant', price: 35000, categoryId: 4 },
  { id: 31, name: 'Muffin', price: 30000, categoryId: 4 },
  { id: 32, name: 'Tiramisu', price: 45000, categoryId: 4 },
  { id: 33, name: 'Cookie', price: 20000, categoryId: 4 },
  { id: 34, name: 'Trân Châu', price: 10000, categoryId: 5 },
  { id: 35, name: 'Thạch', price: 10000, categoryId: 5 },
  { id: 36, name: 'Pudding', price: 12000, categoryId: 5 },
  { id: 37, name: 'Kem Tươi', price: 15000, categoryId: 5 },
]

// ============================================================
// Utility: Format VND
// ============================================================

/** Format số tiền VND theo chuẩn Việt Nam */
const formatVND = (amount: number): string =>
  new Intl.NumberFormat('vi-VN').format(amount) + 'đ'

// ============================================================
// POSLayout — iPad Landscape 3-Column Layout
// Brand: Trắng sạch (#FFFFFF) + Cam đặc (#FF8C00)
// ============================================================

/**
 * Component layout chính cho POS iPad Landscape.
 * Chia màn hình thành 3 cột:
 *   - Cột 1 (w-2/12): Header + Category sidebar
 *   - Cột 2 (w-6/12): Product grid
 *   - Cột 3 (w-4/12): Cart + Payment
 *
 * Branding: Solid Orange (#FF8C00) trên nền trắng.
 * Không sử dụng gradient hay màu pha trộn.
 */
export default function POSLayout() {
  const [selectedCategory, setSelectedCategory] = useState<number>(1)
  const [cart, setCart] = useState<CartItem[]>([])
  const [orderType, setOrderType] = useState<'dine-in' | 'take-away'>('dine-in')
  const [isCheckingOut, setIsCheckingOut] = useState(false)
  const [checkoutMessage, setCheckoutMessage] = useState<string | null>(null)

  // Modifiers Modal State
  const [activeItemForModifiers, setActiveItemForModifiers] = useState<MenuItem | null>(null)

  const handleCheckout = async (paymentMethod: 'cash' | 'banking') => {
    if (cart.length === 0) return
    setIsCheckingOut(true)
    try {
      await enqueueOrder({
        storeId: 1,
        staffId: 1,
        workShiftId: 1,
        orderType,
        items: cart.map((ci) => ({
          menuItemId: ci.id,
          name: ci.name + (ci.modifiers ? ` (${ci.modifiers.detailText})` : ''),
          quantity: ci.quantity,
          unitPrice: ci.price,
        })),
        totalAmount,
        paymentMethod,
      })

      setCheckoutMessage(`Thanh toán ${paymentMethod === 'cash' ? 'tiền mặt' : 'chuyển khoản'} thành công! Đơn hàng đã được lưu.`)
      resetCart()
      setTimeout(() => setCheckoutMessage(null), 3500)
    } catch (err) {
      console.error(err)
      alert('Lỗi khi lưu đơn hàng offline!')
    } finally {
      setIsCheckingOut(false)
    }
  }

  const filteredItems = MENU_ITEMS.filter(
    (item) => item.categoryId === selectedCategory
  )

  /** Add customized/configured item to cart */
  const addToCartWithModifiers = (item: MenuItem, selection: ModifierSelection) => {
    const toppingsKey = selection.selectedToppings
      .map((t) => t.id)
      .sort((a, b) => a - b)
      .join(',')
    const cartId = `${item.id}-${selection.size}-${selection.ice}-${selection.sugar}-${toppingsKey}`

    const toppingsName = selection.selectedToppings.map(t => t.name).join(', ')
    const detailString = `Size ${selection.size}, ${selection.ice} Đá, ${selection.sugar} Đường${toppingsName ? `, +${toppingsName}` : ''}`

    setCart((prev) => {
      const existing = prev.find((ci) => ci.cartId === cartId)
      if (existing) {
        return prev.map((ci) =>
          ci.cartId === cartId ? { ...ci, quantity: ci.quantity + 1 } : ci
        )
      }
      return [
        ...prev,
        {
          id: item.id,
          cartId,
          name: item.name,
          price: selection.totalPrice,
          categoryId: item.categoryId,
          quantity: 1,
          modifiers: {
            size: selection.size,
            ice: selection.ice,
            sugar: selection.sugar,
            selectedToppings: selection.selectedToppings,
            detailText: detailString,
          },
        },
      ]
    })
  }

  /** Quick Add - adds item with default options */
  const handleQuickAdd = (item: MenuItem) => {
    addToCartWithModifiers(item, {
      size: 'S',
      ice: '100%',
      sugar: '100%',
      selectedToppings: [],
      totalPrice: item.price,
    })
  }

  /** Open modifier modal for custom configuring */
  const handleCustomAddClick = (item: MenuItem) => {
    setActiveItemForModifiers(item)
  }

  const handleConfirmModifiers = (selection: ModifierSelection) => {
    if (!activeItemForModifiers) return
    addToCartWithModifiers(activeItemForModifiers, selection)
  }

  /** Get count of this item in cart across all modifier variations */
  const getQuantityInCart = (itemId: number) => {
    return cart.filter((ci) => ci.id === itemId).reduce((sum, ci) => sum + ci.quantity, 0)
  }

  /** Giảm số lượng hoặc xóa khỏi giỏ hàng */
  const decreaseFromCart = (cartId: string) => {
    setCart((prev) => {
      const existing = prev.find((ci) => ci.cartId === cartId)
      if (existing && existing.quantity > 1) {
        return prev.map((ci) =>
          ci.cartId === cartId ? { ...ci, quantity: ci.quantity - 1 } : ci
        )
      }
      return prev.filter((ci) => ci.cartId !== cartId)
    })
  }

  /** Xóa hẳn món khỏi giỏ hàng */
  const removeFromCart = (cartId: string) => {
    setCart((prev) => prev.filter((ci) => ci.cartId !== cartId))
  }

  /** Reset toàn bộ giỏ hàng */
  const resetCart = () => setCart([])

  const totalAmount = cart.reduce(
    (sum, item) => sum + item.price * item.quantity, 0
  )
  const totalItems = cart.reduce((sum, item) => sum + item.quantity, 0)

  const currentCategory = CATEGORIES.find((c) => c.id === selectedCategory)

  return (
    <div className="h-full w-full overflow-hidden flex bg-surface font-sans select-none">

      {/* ═══════════════════════════════════════════════
          CỘT 1: Sidebar (w-2/12) — Header + Danh mục
          ═══════════════════════════════════════════════ */}
      <aside className="w-2/12 bg-surface-white flex flex-col border-r border-border">

        {/* ─── Order Type Toggle ─── */}
        <div className="px-3 py-3 border-b border-border">
          <div className="flex gap-1.5">
            <button
              onClick={() => setOrderType('dine-in')}
              className={`flex-1 py-2 rounded-lg text-xs font-semibold transition-colors cursor-pointer
                ${orderType === 'dine-in'
                  ? 'bg-brand-orange text-white'
                  : 'bg-surface text-text-secondary hover:bg-surface-hover'
                }`}
            >
              🍽 Dine In
            </button>
            <button
              onClick={() => setOrderType('take-away')}
              className={`flex-1 py-2 rounded-lg text-xs font-semibold transition-colors cursor-pointer
                ${orderType === 'take-away'
                  ? 'bg-brand-orange text-white'
                  : 'bg-surface text-text-secondary hover:bg-surface-hover'
                }`}
            >
              🥡 Take Away
            </button>
          </div>
        </div>

        {/* ─── Menu Button ─── */}
        <div className="px-3 pt-3 pb-1">
          <button className="w-full py-2.5 rounded-lg bg-brand-orange text-white text-xs font-bold cursor-pointer hover:bg-brand-orange-hover transition-colors shadow-[var(--shadow-button)]">
            ☰ Menu
          </button>
        </div>

        {/* ─── Category List ─── */}
        <nav className="flex-1 flex flex-col gap-1 px-3 py-2 overflow-y-auto">
          {CATEGORIES.map((cat) => (
            <button
              key={cat.id}
              onClick={() => setSelectedCategory(cat.id)}
              className={`
                flex items-center justify-between px-3 py-2.5 rounded-lg text-xs font-medium
                transition-all duration-150 cursor-pointer
                ${selectedCategory === cat.id
                  ? 'bg-brand-orange-light text-brand-orange border border-brand-orange-border'
                  : 'text-text-secondary hover:bg-surface-hover border border-transparent'
                }
              `}
            >
              <span className="flex items-center gap-2">
                <span className="text-base">{cat.icon}</span>
                <span>{cat.name}</span>
              </span>
              <span className={`
                text-[10px] font-bold px-1.5 py-0.5 rounded-full
                ${selectedCategory === cat.id
                  ? 'bg-brand-orange text-white'
                  : 'bg-surface text-text-muted'
                }
              `}>
                {cat.count}
              </span>
            </button>
          ))}
        </nav>

        {/* Redundant Staff Info removed (already in TopNavbar) */}
      </aside>

      {/* ═══════════════════════════════════════════════
          CỘT 2: Product Grid (w-6/12)
          ═══════════════════════════════════════════════ */}
      <main className="w-6/12 flex flex-col bg-surface">

        {/* ─── Category Header ─── */}
        <header className="flex items-center justify-between px-5 py-3 bg-surface-white border-b border-border">
          <div>
            <h2 className="text-base font-bold text-text-primary flex items-center gap-2">
              <span className="text-lg">{currentCategory?.icon}</span>
              {currentCategory?.name}
              <span className="text-xs font-semibold text-brand-orange bg-brand-orange-light px-2 py-0.5 rounded-full">
                {filteredItems.length}
              </span>
            </h2>
          </div>
          <div className="text-[11px] text-text-muted bg-surface px-3 py-1.5 rounded-full border border-border">
            {new Date().toLocaleDateString('vi-VN', {
              weekday: 'short',
              day: '2-digit',
              month: '2-digit',
              year: 'numeric',
            })}
          </div>
        </header>

        {/* ─── Product Grid ─── */}
        <div className="flex-1 overflow-y-auto p-4">
          <div className="grid grid-cols-3 gap-3">
            {filteredItems.map((item) => {
              const qtyInCart = getQuantityInCart(item.id)
              return (
                <div
                  key={item.id}
                  onClick={() => handleQuickAdd(item)}
                  className="relative bg-surface-card rounded-xl border border-border p-4 flex flex-col items-center cursor-pointer select-none
                             shadow-[var(--shadow-card)] hover:shadow-[var(--shadow-card-hover)]
                             hover:border-brand-orange-border transition-all duration-200"
                >
                  {/* Quantity Badge (Top-Left) */}
                  {qtyInCart > 0 && (
                    <span className="absolute -top-1.5 -left-1.5 w-5.5 h-5.5 bg-brand-orange text-white text-[10px] font-extrabold rounded-full flex items-center justify-center shadow-sm z-10">
                      {qtyInCart}
                    </span>
                  )}

                  {/* Custom Add Trigger (Top-Right) */}
                  <button
                    onClick={(e) => {
                      e.stopPropagation();
                      handleCustomAddClick(item);
                    }}
                    className="absolute top-2.5 right-2.5 px-2 py-1 bg-brand-orange-light border border-brand-orange-border text-brand-orange text-[9px] font-extrabold rounded-lg hover:bg-brand-orange hover:text-white transition-colors cursor-pointer z-10"
                    title="Tùy biến Size/Topping"
                  >
                    ⚙️ Tùy chỉnh
                  </button>

                  {/* Product Icon */}
                  <div className="w-11 h-11 rounded-xl bg-brand-orange-light flex items-center justify-center text-lg mb-2 mt-2">
                    {currentCategory?.icon}
                  </div>

                  {/* Product Info */}
                  <span className="text-xs font-semibold text-text-primary text-center leading-tight mb-0.5">
                    {item.name}
                  </span>
                  <span className="text-[10px] font-bold text-brand-orange mb-3">
                    {formatVND(item.price)}
                  </span>

                  {/* Bottom indicator/action label */}
                  <div className="text-[9px] text-text-secondary font-bold bg-surface px-2.5 py-1 rounded-md border border-border-light hover:bg-brand-orange-light hover:text-brand-orange hover:border-brand-orange-border transition-colors">
                    ⚡ Thêm nhanh
                  </div>
                </div>
              )
            })}
          </div>
        </div>
      </main>

      {/* ═══════════════════════════════════════════════
          CỘT 3: Cart + Payment (w-4/12)
          ═══════════════════════════════════════════════ */}
      <aside className="w-4/12 bg-surface-white flex flex-col border-l border-border">

        {/* ─── Cart Header ─── */}
        <div className="flex items-center justify-between px-5 py-3 border-b border-border">
          <div className="flex items-center gap-2">
            <span className="text-lg">🛒</span>
            <h2 className="text-base font-bold text-text-primary">Giỏ hàng</h2>
          </div>
          <div className="flex items-center gap-2">
            {totalItems > 0 && (
              <span className="bg-brand-orange text-white text-[10px] font-bold px-2 py-0.5 rounded-full">
                {totalItems} món
              </span>
            )}
            {cart.length > 0 && (
              <button
                onClick={resetCart}
                className="text-[10px] font-semibold text-danger hover:text-danger-hover
                           border border-danger/30 px-2 py-0.5 rounded-full
                           hover:bg-danger/5 transition-colors cursor-pointer"
              >
                Reset Order
              </button>
            )}
          </div>
        </div>

        {/* ─── Cart Items ─── */}
        <div className="flex-1 overflow-y-auto px-4 py-2">
          {cart.length === 0 ? (
            <div className="flex flex-col items-center justify-center h-full text-text-muted">
              <span className="text-4xl mb-3 opacity-20">🛒</span>
              <p className="text-sm font-medium">Chưa có sản phẩm</p>
              <p className="text-[11px] mt-1 opacity-60">Chọn món từ menu bên trái</p>
            </div>
          ) : (
            <div className="flex flex-col gap-2">
              {cart.map((item, index) => (
                <div
                  key={item.cartId}
                  className="flex items-center gap-3 p-3 bg-surface rounded-xl border border-border-light"
                >
                  {/* Order Number */}
                  <span className="w-6 h-6 rounded-full bg-brand-orange-light text-brand-orange text-[10px] font-bold flex items-center justify-center shrink-0">
                    {index + 1}
                  </span>

                  {/* Item Info */}
                  <div className="flex-1 min-w-0">
                    <p className="text-xs font-semibold text-text-primary truncate">{item.name}</p>
                    {item.modifiers && (
                      <p className="text-[9px] text-brand-orange font-bold truncate leading-tight mt-0.5">
                        {item.modifiers.detailText}
                      </p>
                    )}
                    <p className="text-[10px] text-text-secondary mt-0.5">{formatVND(item.price)}</p>
                  </div>

                  {/* Quantity Controls */}
                  <div className="flex items-center gap-1">
                    <button
                      onClick={() => decreaseFromCart(item.cartId)}
                      className="w-6 h-6 rounded-md bg-surface border border-border text-text-secondary
                                 hover:bg-brand-orange-light hover:text-brand-orange hover:border-brand-orange-border
                                 text-xs font-bold flex items-center justify-center cursor-pointer transition-colors"
                    >
                      −
                    </button>
                    <span className="w-5 text-center text-xs font-bold text-text-primary">
                      {item.quantity}
                    </span>
                    <button
                      onClick={() => addToCartWithModifiers(
                        { id: item.id, name: item.name, price: item.price, categoryId: item.categoryId },
                        {
                          size: item.modifiers?.size ?? 'S',
                          ice: item.modifiers?.ice ?? '100%',
                          sugar: item.modifiers?.sugar ?? '100%',
                          selectedToppings: item.modifiers?.selectedToppings ?? [],
                          totalPrice: item.price,
                        }
                      )}
                      className="w-6 h-6 rounded-md bg-brand-orange text-white
                                 hover:bg-brand-orange-hover
                                 text-xs font-bold flex items-center justify-center cursor-pointer transition-colors"
                    >
                      +
                    </button>
                  </div>

                  {/* Delete — Solid Red, NO gradient */}
                  <button
                    onClick={() => removeFromCart(item.cartId)}
                    className="w-6 h-6 rounded-md border border-danger/30 text-danger
                               hover:bg-danger hover:text-white hover:border-danger
                               text-xs flex items-center justify-center cursor-pointer transition-colors"
                    title="Xóa món"
                  >
                    🗑
                  </button>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* ─── Cart Footer: Summary + Payment ─── */}
        <div className="border-t border-border p-4 space-y-3">
          {/* Summary */}
          <div className="space-y-1.5">
            <div className="flex justify-between text-xs text-text-secondary">
              <span>Tạm tính ({totalItems} món)</span>
              <span>{formatVND(totalAmount)}</span>
            </div>
            <div className="flex justify-between text-xs text-text-secondary">
              <span>VAT (8%)</span>
              <span>{formatVND(Math.round(totalAmount * 0.08 / 1.08))}</span>
            </div>
            <div className="h-px bg-border" />
            <div className="flex justify-between text-lg font-bold text-text-primary">
              <span>Tổng cộng</span>
              <span className="text-brand-orange">{formatVND(totalAmount)}</span>
            </div>
          </div>

          {/* Payment Buttons — Solid Colors, NO gradients */}
          <div className="flex gap-2">
            <button
              onClick={() => handleCheckout('cash')}
              disabled={cart.length === 0 || isCheckingOut}
              className="flex-1 py-3 rounded-xl bg-brand-orange text-white font-bold text-sm
                         shadow-[var(--shadow-button)] hover:bg-brand-orange-hover active:scale-[0.98]
                         transition-all duration-150 cursor-pointer
                         disabled:opacity-40 disabled:cursor-not-allowed"
            >
              💵 Tiền mặt
            </button>
            <button
              onClick={() => handleCheckout('banking')}
              disabled={cart.length === 0 || isCheckingOut}
              className="flex-1 py-3 rounded-xl bg-text-primary text-white font-bold text-sm
                         hover:bg-gray-700 active:scale-[0.98]
                         transition-all duration-150 cursor-pointer
                         disabled:opacity-40 disabled:cursor-not-allowed"
            >
              📱 Chuyển khoản
            </button>
          </div>
        </div>
      </aside>

      {/* Checkout Success Toast Banner */}
      {checkoutMessage && (
        <div className="absolute bottom-6 left-1/2 -translate-x-1/2 bg-brand-orange text-white font-bold text-xs py-3.5 px-6 rounded-xl shadow-lg border border-brand-orange-border animate-bounce z-50">
          {checkoutMessage}
        </div>
      )}

      {/* Product Customization Modifier Modal */}
      <ProductModifierModal
        isOpen={activeItemForModifiers !== null}
        onClose={() => setActiveItemForModifiers(null)}
        menuItem={activeItemForModifiers}
        onConfirm={handleConfirmModifiers}
      />
    </div>
  )
}
