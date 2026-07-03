import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import * as signalR from '@microsoft/signalr'
import { enqueueOrder } from './services/OfflineSyncService'
import { API_BASE_URL, apiClient } from './services/apiClient'
import { getPosSession } from './services/posSession'
import { usePOSData } from './hooks/usePOSData'
import ProductModifierModal, {
  type ModifierSelection,
  type ToppingOption,
  type MenuItem,
} from './components/ProductModifierModal'

interface CartItem {
  id: number
  cartId: string
  name: string
  price: number
  categoryId: number
  quantity: number
  sizeId?: number | null
  sizeName?: string
  note: string
  selectedToppings: ToppingOption[]
  detailText: string
}

interface ShiftSummary {
  shiftId?: number | null
  status: 'Open' | 'Closed' | 'NoActiveShift' | string
}

interface POSCommitApiResponse {
  success: boolean
  message?: string
  data?: {
    orderId?: number
    total?: number
    checkoutUrl?: string
    qrCode?: string | null
    requiresPayment?: boolean
    paymentMethodId?: number
  }
  inventoryWarnings?: string[]
}

interface PendingPayment {
  orderId: number
  checkoutUrl: string
  qrCode?: string | null
  amount: number
  expiresAt: number
}

interface CancelPaymentApiResponse {
  success: boolean
  code?: string
  message?: string
}

const PAYMENT_TIMEOUT_SECONDS = 5 * 60

const formatVND = (amount: number): string =>
  new Intl.NumberFormat('vi-VN').format(amount) + 'đ'

const formatCountdown = (seconds: number): string => {
  const minutes = Math.floor(seconds / 60)
  const remainingSeconds = seconds % 60
  return `${minutes.toString().padStart(2, '0')}:${remainingSeconds.toString().padStart(2, '0')}`
}

const createPaymentExpiryTimestamp = () =>
  new Date().getTime() + PAYMENT_TIMEOUT_SECONDS * 1000

function getClientOrderId() {
  if (typeof crypto !== 'undefined' && crypto.randomUUID) return crypto.randomUUID()
  return `${Date.now()}-${Math.random().toString(16).slice(2)}`
}

interface ProductImageProps {
  src?: string | null
  name: string
  fallbackIcon: string
}

function ProductImage({ src, name, fallbackIcon }: ProductImageProps) {
  const [hasImageError, setHasImageError] = useState(false)

  if (src && !hasImageError) {
    return (
      <img
        src={src}
        alt={name}
        className="w-12 h-12 rounded-xl object-cover mb-2 mt-2 bg-brand-orange-light"
        loading="lazy"
        onError={() => setHasImageError(true)}
      />
    )
  }

  return (
    <div className="w-12 h-12 rounded-xl bg-brand-orange-light flex items-center justify-center text-lg mb-2 mt-2">
      {fallbackIcon}
    </div>
  )
}

export default function POSLayout() {
  const { categories, menuItems, isLoading, isOnline, pendingOrders } = usePOSData()
  const [selectedCategory, setSelectedCategory] = useState<number | null>(null)
  const [cart, setCart] = useState<CartItem[]>([])
  const [orderType, setOrderType] = useState<'dine-in' | 'take-away'>('dine-in')
  const [isCheckingOut, setIsCheckingOut] = useState(false)
  const [checkoutMessage, setCheckoutMessage] = useState<string | null>(null)
  const [activeItemForModifiers, setActiveItemForModifiers] = useState<MenuItem | null>(null)
  const [shift, setShift] = useState<ShiftSummary | null>(null)
  const [pendingPayment, setPendingPayment] = useState<PendingPayment | null>(null)
  const [paymentRemainingSeconds, setPaymentRemainingSeconds] = useState(PAYMENT_TIMEOUT_SECONDS)
  const [isCancellingPayment, setIsCancellingPayment] = useState(false)
  const checkoutInFlightRef = useRef(false)

  const session = getPosSession()

  useEffect(() => {
    let active = true

    const loadShift = async () => {
      const response = await apiClient.get<ShiftSummary>('/api/v1/pos/shifts/current')
      if (!active) return
      if (response.ok && response.data) setShift(response.data)
      else setShift({ status: 'NoActiveShift' })
    }

    loadShift()
    window.addEventListener('focus', loadShift)

    return () => {
      active = false
      window.removeEventListener('focus', loadShift)
    }
  }, [])

  const selectedCategoryId = categories.some((cat) => cat.id === selectedCategory)
    ? selectedCategory
    : categories[0]?.id ?? null

  const filteredItems = useMemo(() => (
    menuItems.filter((item) =>
      (selectedCategoryId === null || item.categoryId === selectedCategoryId)
    )
  ), [menuItems, selectedCategoryId])

  const totalAmount = cart.reduce((sum, item) => sum + item.price * item.quantity, 0)
  const totalItems = cart.reduce((sum, item) => sum + item.quantity, 0)
  const currentCategory = categories.find((cat) => cat.id === selectedCategoryId)
  const hasOpenShift = shift?.status === 'Open' && !!shift.shiftId
  const hasPendingPayment = pendingPayment !== null

  const showMessage = useCallback((message: string) => {
    setCheckoutMessage(message)
    window.setTimeout(() => setCheckoutMessage(null), 3500)
  }, [])

  const addToCartWithModifiers = (item: MenuItem, selection: ModifierSelection) => {
    const toppingKey = selection.selectedToppings
      .map((topping) => topping.id)
      .sort((a, b) => a - b)
      .join(',')
    const sizeKey = selection.size?.sizeId ?? 'default'
    const cartId = `${item.id}-${sizeKey}-${selection.ice}-${selection.sugar}-${toppingKey}`
    const toppingNames = selection.selectedToppings.map((topping) => topping.name).join(', ')
    const detailText = [
      selection.size ? `Size ${selection.size.sizeName}` : '',
      selection.note,
      toppingNames ? `+${toppingNames}` : '',
    ].filter(Boolean).join(', ')

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
          sizeId: selection.size?.sizeId ?? null,
          sizeName: selection.size?.sizeName,
          note: selection.note,
          selectedToppings: selection.selectedToppings,
          detailText,
        },
      ]
    })
  }

  const handleQuickAdd = (item: MenuItem) => {
    if (item.isAvailable === false) {
      showMessage('Món này chưa khả dụng do thiếu BOM hoặc tồn kho.')
      return
    }

    const defaultSize = item.sizes?.[0] ?? null
    addToCartWithModifiers(item, {
      size: defaultSize,
      ice: '100%',
      sugar: '100%',
      selectedToppings: [],
      totalPrice: defaultSize?.price ?? item.price,
      note: 'Đá 100%, Đường 100%',
    })
  }

  const getQuantityInCart = (itemId: number) =>
    cart.filter((ci) => ci.id === itemId).reduce((sum, ci) => sum + ci.quantity, 0)

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

  const removeFromCart = (cartId: string) => {
    setCart((prev) => prev.filter((ci) => ci.cartId !== cartId))
  }

  const resetCart = useCallback(() => setCart([]), [])

  const closePaymentModalManually = useCallback(() => {
    setPendingPayment(null)
    setIsCancellingPayment(false)
    showMessage('Đã đóng cửa sổ VietQR. Tải lại trang nếu cần đồng bộ trạng thái mới nhất.')
  }, [showMessage])

  const cancelPendingPayment = useCallback(async (reason: 'manual' | 'timeout') => {
    if (!pendingPayment?.orderId || isCancellingPayment) return

    setIsCancellingPayment(true)
    const response = await apiClient.post<CancelPaymentApiResponse>(
      '/api/v1/pos/payments/cancel-payment',
      {
        orderId: pendingPayment.orderId,
        reason: reason === 'timeout'
          ? 'Hết thời gian chờ thanh toán VietQR'
          : 'Thu ngân hủy giao dịch VietQR',
      }
    )

    setPendingPayment(null)
    setIsCancellingPayment(false)

    if (response.ok && response.data?.code === 'ALREADY_PAID') {
      resetCart()
      showMessage('Thanh toán VietQR thành công. Lệnh in đã gửi tới Print Bridge.')
      return
    }

    if (response.ok && response.data?.success) {
      showMessage(reason === 'timeout'
        ? 'Giao dịch VietQR đã hết hạn và được hủy.'
        : 'Đã hủy giao dịch VietQR.')
      return
    }

    showMessage(response.data?.message || response.error || 'Không thể hủy giao dịch VietQR.')
  }, [isCancellingPayment, pendingPayment, resetCart, showMessage])

  useEffect(() => {
    if (!pendingPayment) return

    let didAutoCancel = false
    const updateCountdown = () => {
      if (didAutoCancel) return

      const nextSeconds = Math.max(0, Math.ceil((pendingPayment.expiresAt - Date.now()) / 1000))
      setPaymentRemainingSeconds(nextSeconds)
      if (nextSeconds === 0) {
        didAutoCancel = true
        void cancelPendingPayment('timeout')
      }
    }

    const timerId = window.setInterval(updateCountdown, 1000)
    return () => window.clearInterval(timerId)
  }, [cancelPendingPayment, pendingPayment])

  useEffect(() => {
    if (!pendingPayment?.orderId) return

    let isDisposed = false
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${API_BASE_URL}/orderHub`)
      .withAutomaticReconnect()
      .build()

    const completePayment = () => {
      if (isDisposed) return
      setPendingPayment(null)
      resetCart()
      showMessage('Thanh toán VietQR thành công. Lệnh in đã gửi tới Print Bridge.')
    }

    connection.on('PaymentCompleted', (payload: number | { orderId?: number }) => {
      const paidOrderId = typeof payload === 'number' ? payload : payload?.orderId
      if (!paidOrderId || paidOrderId === pendingPayment.orderId) completePayment()
    })

    connection
      .start()
      .then(() => connection.invoke('JoinOrderGroup', pendingPayment.orderId))
      .catch((error) => {
        console.error('[POS Payment SignalR] Connection failed:', error)
        showMessage('Không thể lắng nghe trạng thái thanh toán. Vui lòng kiểm tra lại kết nối.')
      })

    return () => {
      isDisposed = true
      connection.stop().catch((error) => {
        console.warn('[POS Payment SignalR] Stop failed:', error)
      })
    }
  }, [pendingPayment, resetCart, showMessage])

  const enqueueOrderFallback = async (paymentMethod: 'cash' | 'banking') => {
    if (!hasOpenShift || !shift?.shiftId) {
      showMessage('Bạn cần mở ca làm việc trước khi lưu đơn offline.')
      return false
    }

    await enqueueOrder({
      storeId: session.storeId ?? 1,
      staffId: session.staffId ?? 0,
      workShiftId: shift.shiftId,
      orderType,
      items: cart.map((ci) => ({
        menuItemId: ci.id,
        name: ci.name,
        sizeId: ci.sizeId,
        quantity: ci.quantity,
        unitPrice: ci.price,
        toppings: ci.selectedToppings.map((topping) => ({ toppingId: topping.id })),
      })),
      totalAmount,
      paymentMethod,
    })

    return true
  }

  const handleCheckout = async (paymentMethod: 'cash' | 'banking') => {
    if (checkoutInFlightRef.current || cart.length === 0 || hasPendingPayment) return
    if (!hasOpenShift) {
      showMessage('Bạn cần mở ca làm việc trước khi thanh toán.')
      return
    }
    if (paymentMethod === 'banking' && !navigator.onLine) {
      showMessage('Cần kết nối mạng để tạo mã thanh toán VietQR.')
      return
    }

    checkoutInFlightRef.current = true
    setIsCheckingOut(true)
    try {
      if (navigator.onLine) {
        const response = await apiClient.post<POSCommitApiResponse>('/api/v1/pos/orders/commit', {
          items: cart.map((ci) => ({
            drinkId: ci.id,
            sizeId: ci.sizeId,
            quantity: ci.quantity,
            note: ci.note,
            toppings: ci.selectedToppings.map((topping) => ({ toppingId: topping.id })),
          })),
          clientOrderId: getClientOrderId(),
          payments: [{ paymentMethodId: paymentMethod === 'cash' ? 1 : 2, amount: totalAmount }],
          receivedAmount: totalAmount,
          orderTypeId: orderType === 'dine-in' ? 1 : 2,
          note: '',
        })

        if (response.ok && response.data?.success) {
          const commitData = response.data.data
          if (
            paymentMethod === 'banking' &&
            commitData?.requiresPayment &&
            commitData.orderId &&
            commitData.checkoutUrl
          ) {
            setPaymentRemainingSeconds(PAYMENT_TIMEOUT_SECONDS)
            setPendingPayment({
              orderId: commitData.orderId,
              checkoutUrl: commitData.checkoutUrl,
              qrCode: commitData.qrCode,
              amount: commitData.total ?? totalAmount,
              expiresAt: createPaymentExpiryTimestamp(),
            })
            showMessage('Đã tạo mã VietQR, đang chờ xác nhận thanh toán.')
            return
          }

          const warnings = response.data.inventoryWarnings
          const warningText = warnings?.length ? ` (${warnings.length} cảnh báo kho)` : ''
          resetCart()
          showMessage(`Thanh toán thành công. Lệnh in đã gửi tới Print Bridge.${warningText}`)
        } else if (!response.ok && response.status === 0 && paymentMethod === 'cash') {
          console.warn('[POS] Network commit failed, saving offline:', response.error)
          const savedOffline = await enqueueOrderFallback(paymentMethod)
          if (savedOffline) {
            resetCart()
            showMessage('Đơn đã lưu offline, sẽ tự đồng bộ khi có mạng.')
          }
        } else {
          showMessage(response.data?.message || response.error || 'Không thể thanh toán. Vui lòng kiểm tra lại ca két tiền.')
        }
      } else {
        const savedOffline = await enqueueOrderFallback(paymentMethod)
        if (savedOffline) {
          resetCart()
          showMessage('Offline: đơn đã lưu và sẽ tự đồng bộ khi có mạng.')
        }
      }
    } catch (err) {
      console.error('[POS] Checkout error:', err)
      if (!navigator.onLine && paymentMethod === 'cash') {
        try {
          const savedOffline = await enqueueOrderFallback(paymentMethod)
          if (savedOffline) {
            resetCart()
            showMessage('Đơn đã lưu offline do lỗi mạng.')
          }
        } catch {
          window.alert('Lỗi nghiêm trọng: Không thể lưu đơn hàng.')
        }
      } else {
        showMessage('Không thể thanh toán. Vui lòng thử lại.')
      }
    } finally {
      checkoutInFlightRef.current = false
      setIsCheckingOut(false)
    }
  }

  return (
    <div className="h-full w-full overflow-hidden flex bg-surface font-sans select-none">
      <aside className="w-2/12 bg-surface-white flex flex-col border-r border-border">
        <div className="px-3 py-3 border-b border-border">
          <div className="flex gap-1.5">
            <button
              onClick={() => setOrderType('dine-in')}
              className={`flex-1 py-2 rounded-lg text-xs font-semibold transition-colors cursor-pointer ${
                orderType === 'dine-in'
                  ? 'bg-brand-orange text-white'
                  : 'bg-surface text-text-secondary hover:bg-surface-hover'
              }`}
            >
              Tại quán
            </button>
            <button
              onClick={() => setOrderType('take-away')}
              className={`flex-1 py-2 rounded-lg text-xs font-semibold transition-colors cursor-pointer ${
                orderType === 'take-away'
                  ? 'bg-brand-orange text-white'
                  : 'bg-surface text-text-secondary hover:bg-surface-hover'
              }`}
            >
              Mang đi
            </button>
          </div>
        </div>

        <div className="px-3 pt-3 pb-1">
          <Link
            to="/shift"
            className={`w-full py-2.5 rounded-lg text-xs font-bold cursor-pointer transition-colors shadow-[var(--shadow-button)] flex items-center justify-center ${
              hasOpenShift
                ? 'bg-green-600 text-white hover:bg-green-700'
                : 'bg-brand-orange text-white hover:bg-brand-orange-hover'
            }`}
          >
            {hasOpenShift ? `Ca #${shift?.shiftId}` : 'Mở ca'}
          </Link>
        </div>

        <nav className="flex-1 flex flex-col gap-1 px-3 py-2 overflow-y-auto">
          {categories.map((cat) => (
            <button
              key={cat.id}
              onClick={() => setSelectedCategory(cat.id)}
              className={`flex items-center justify-between px-3 py-2.5 rounded-lg text-xs font-medium transition-all duration-150 cursor-pointer ${
                selectedCategoryId === cat.id
                  ? 'bg-brand-orange-light text-brand-orange border border-brand-orange-border'
                  : 'text-text-secondary hover:bg-surface-hover border border-transparent'
              }`}
            >
              <span className="flex items-center gap-2 min-w-0">
                <span className="text-base">{cat.icon || '•'}</span>
                <span className="truncate">{cat.name}</span>
              </span>
              <span className={`text-[10px] font-bold px-1.5 py-0.5 rounded-full ${
                selectedCategoryId === cat.id
                  ? 'bg-brand-orange text-white'
                  : 'bg-surface text-text-muted'
              }`}>
                {cat.count}
              </span>
            </button>
          ))}
        </nav>
      </aside>

      <main className="w-6/12 flex flex-col bg-surface">
        <header className="flex items-center justify-between px-5 py-3 bg-surface-white border-b border-border">
          <h2 className="text-base font-bold text-text-primary flex items-center gap-2 min-w-0">
            <span className="text-lg">{currentCategory?.icon || '•'}</span>
            <span className="truncate">{currentCategory?.name ?? 'Menu'}</span>
            <span className="text-xs font-semibold text-brand-orange bg-brand-orange-light px-2 py-0.5 rounded-full">
              {filteredItems.length}
            </span>
          </h2>
          <div className="flex items-center gap-2 text-[11px]">
            {!isOnline && (
              <span className="text-danger font-bold bg-red-50 px-2.5 py-1.5 rounded-full border border-red-100">
                Offline
              </span>
            )}
            {pendingOrders > 0 && (
              <span className="text-brand-orange font-bold bg-brand-orange-light px-2.5 py-1.5 rounded-full border border-brand-orange-border">
                {pendingOrders} đơn chờ sync
              </span>
            )}
            <span className="text-text-muted bg-surface px-3 py-1.5 rounded-full border border-border">
              {new Date().toLocaleDateString('vi-VN', {
                weekday: 'short',
                day: '2-digit',
                month: '2-digit',
                year: 'numeric',
              })}
            </span>
          </div>
        </header>

        <div className="flex-1 overflow-y-auto p-4">
          {isLoading ? (
            <div className="h-full flex items-center justify-center text-xs font-semibold text-text-muted">
              Đang tải menu...
            </div>
          ) : filteredItems.length === 0 ? (
            <div className="h-full flex items-center justify-center text-xs font-semibold text-text-muted">
              Không có sản phẩm khả dụng
            </div>
          ) : (
            <div className="grid grid-cols-3 gap-3">
              {filteredItems.map((item) => {
                const qtyInCart = getQuantityInCart(item.id)
                const isUnavailable = item.isAvailable === false
                return (
                  <div
                    key={item.id}
                    onClick={() => handleQuickAdd(item)}
                    className={`relative bg-surface-card rounded-xl border border-border p-4 flex flex-col items-center select-none shadow-[var(--shadow-card)] transition-all duration-200 min-h-[150px] ${
                      isUnavailable
                        ? 'opacity-60 cursor-not-allowed'
                        : 'cursor-pointer hover:shadow-[var(--shadow-card-hover)] hover:border-brand-orange-border'
                    }`}
                  >
                    {qtyInCart > 0 && (
                      <span className="absolute -top-1.5 -left-1.5 w-5 h-5 bg-brand-orange text-white text-[10px] font-extrabold rounded-full flex items-center justify-center shadow-sm z-10">
                        {qtyInCart}
                      </span>
                    )}

                    {isUnavailable && (
                      <span className="absolute top-2.5 left-2.5 px-2 py-1 bg-surface border border-border text-text-secondary text-[9px] font-extrabold rounded-lg z-10">
                        Chưa khả dụng
                      </span>
                    )}

                    <button
                      onClick={(event) => {
                        event.stopPropagation()
                        if (isUnavailable) {
                          showMessage('Món này chưa khả dụng do thiếu BOM hoặc tồn kho.')
                          return
                        }
                        setActiveItemForModifiers(item)
                      }}
                      disabled={isUnavailable}
                      className="absolute top-2.5 right-2.5 px-2 py-1 bg-brand-orange-light border border-brand-orange-border text-brand-orange text-[9px] font-extrabold rounded-lg hover:bg-brand-orange hover:text-white transition-colors cursor-pointer disabled:cursor-not-allowed disabled:bg-surface disabled:border-border disabled:text-text-muted z-10"
                    >
                      Tùy chỉnh
                    </button>

                    <ProductImage
                      src={item.image}
                      name={item.name}
                      fallbackIcon={currentCategory?.icon || '•'}
                    />

                    <span className="text-xs font-semibold text-text-primary text-center leading-tight mb-0.5 line-clamp-2">
                      {item.name}
                    </span>
                    <span className="text-[10px] font-bold text-brand-orange mb-3">
                      {formatVND(item.price)}
                    </span>
                    <div className="mt-auto text-[9px] text-text-secondary font-bold bg-surface px-2.5 py-1 rounded-md border border-border-light">
                      Thêm nhanh
                    </div>
                  </div>
                )
              })}
            </div>
          )}
        </div>
      </main>

      <aside className="w-4/12 bg-surface-white flex flex-col border-l border-border">
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
                className="text-[10px] font-semibold text-danger hover:text-danger-hover border border-danger/30 px-2 py-0.5 rounded-full hover:bg-danger/5 transition-colors cursor-pointer"
              >
                Xóa giỏ
              </button>
            )}
          </div>
        </div>

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
                  <span className="w-6 h-6 rounded-full bg-brand-orange-light text-brand-orange text-[10px] font-bold flex items-center justify-center shrink-0">
                    {index + 1}
                  </span>

                  <div className="flex-1 min-w-0">
                    <p className="text-xs font-semibold text-text-primary truncate">{item.name}</p>
                    <p className="text-[9px] text-brand-orange font-bold truncate leading-tight mt-0.5">
                      {item.detailText}
                    </p>
                    <p className="text-[10px] text-text-secondary mt-0.5">{formatVND(item.price)}</p>
                  </div>

                  <div className="flex items-center gap-1">
                    <button
                      onClick={() => decreaseFromCart(item.cartId)}
                      className="w-6 h-6 rounded-md bg-surface border border-border text-text-secondary hover:bg-brand-orange-light hover:text-brand-orange hover:border-brand-orange-border text-xs font-bold flex items-center justify-center cursor-pointer transition-colors"
                    >
                      -
                    </button>
                    <span className="w-5 text-center text-xs font-bold text-text-primary">
                      {item.quantity}
                    </span>
                    <button
                      onClick={() => setCart((prev) => prev.map((ci) =>
                        ci.cartId === item.cartId ? { ...ci, quantity: ci.quantity + 1 } : ci
                      ))}
                      className="w-6 h-6 rounded-md bg-brand-orange text-white hover:bg-brand-orange-hover text-xs font-bold flex items-center justify-center cursor-pointer transition-colors"
                    >
                      +
                    </button>
                  </div>

                  <button
                    onClick={() => removeFromCart(item.cartId)}
                    className="w-6 h-6 rounded-md border border-danger/30 text-danger hover:bg-danger hover:text-white hover:border-danger text-xs flex items-center justify-center cursor-pointer transition-colors"
                    title="Xóa món"
                  >
                    x
                  </button>
                </div>
              ))}
            </div>
          )}
        </div>

        <div className="border-t border-border p-4 space-y-3">
          <div className="space-y-1.5">
            <div className="flex justify-between text-xs text-text-secondary">
              <span>Tạm tính ({totalItems} món)</span>
              <span>{formatVND(totalAmount)}</span>
            </div>
            <div className="flex justify-between text-xs text-text-secondary">
              <span>VAT đã gồm trong giá</span>
              <span>{formatVND(Math.round(totalAmount * 0.08 / 1.08))}</span>
            </div>
            <div className="h-px bg-border" />
            <div className="flex justify-between text-lg font-bold text-text-primary">
              <span>Tổng cộng</span>
              <span className="text-brand-orange">{formatVND(totalAmount)}</span>
            </div>
          </div>

          {!hasOpenShift && (
            <Link
              to="/shift"
              className="block text-center px-3 py-2 rounded-lg border border-brand-orange-border bg-brand-orange-light text-brand-orange text-xs font-bold"
            >
              Mở ca trước khi thanh toán
            </Link>
          )}

          <div className="flex gap-2">
            <button
              onClick={() => handleCheckout('cash')}
              disabled={cart.length === 0 || isCheckingOut || !hasOpenShift || hasPendingPayment}
              className="flex-1 py-3 rounded-xl bg-brand-orange text-white font-bold text-sm shadow-[var(--shadow-button)] hover:bg-brand-orange-hover active:scale-[0.98] transition-all duration-150 cursor-pointer disabled:opacity-40 disabled:cursor-not-allowed"
            >
              {isCheckingOut ? (
                <span className="inline-flex items-center justify-center gap-2">
                  <span className="h-3 w-3 rounded-full border-2 border-white/40 border-t-white animate-spin" />
                  Đang xử lý
                </span>
              ) : (
                'Tiền mặt'
              )}
            </button>
            <button
              onClick={() => handleCheckout('banking')}
              disabled={cart.length === 0 || isCheckingOut || !hasOpenShift || hasPendingPayment}
              className="flex-1 py-3 rounded-xl bg-text-primary text-white font-bold text-sm hover:bg-gray-700 active:scale-[0.98] transition-all duration-150 cursor-pointer disabled:opacity-40 disabled:cursor-not-allowed"
            >
              {isCheckingOut ? (
                <span className="inline-flex items-center justify-center gap-2">
                  <span className="h-3 w-3 rounded-full border-2 border-white/40 border-t-white animate-spin" />
                  Đang xử lý
                </span>
              ) : (
                'Chuyển khoản'
              )}
            </button>
          </div>
        </div>
      </aside>

      {checkoutMessage && (
        <div className="absolute bottom-6 left-1/2 -translate-x-1/2 bg-brand-orange text-white font-bold text-xs py-3.5 px-6 rounded-xl shadow-lg border border-brand-orange-border z-50">
          {checkoutMessage}
        </div>
      )}

      {pendingPayment && (
        <div className="fixed inset-0 z-[60] bg-black/55 flex items-center justify-center px-4">
          <div
            role="dialog"
            aria-modal="true"
            className="w-full max-w-[420px] rounded-xl bg-surface-white border border-border shadow-2xl overflow-hidden"
          >
            <div className="px-5 py-4 border-b border-border flex items-center justify-between gap-3">
              <div className="min-w-0">
                <p className="text-sm font-extrabold text-text-primary">Thanh toán VietQR</p>
                <p className="text-[11px] font-semibold text-text-muted">
                  Đơn #{pendingPayment.orderId} · {formatVND(pendingPayment.amount)}
                </p>
              </div>
              <div className="flex items-center gap-3 shrink-0">
                <span className="rounded-full border border-brand-orange-border bg-brand-orange-light px-3 py-1 text-xs font-extrabold text-brand-orange tabular-nums">
                  {formatCountdown(paymentRemainingSeconds)}
                </span>
                <button
                  type="button"
                  onClick={closePaymentModalManually}
                  className="h-8 w-8 rounded-lg border border-border text-text-secondary hover:border-danger/40 hover:bg-danger/5 hover:text-danger transition-colors cursor-pointer"
                  aria-label="Đóng modal thanh toán"
                  title="Đóng modal"
                >
                  ×
                </button>
              </div>
            </div>

            <div className="p-5 space-y-4">
              <div className="flex items-center justify-between rounded-lg border border-brand-orange-border bg-brand-orange-light px-3 py-2">
                <span className="text-xs font-bold text-brand-orange">Thời gian giữ giao dịch</span>
                <span className="text-sm font-extrabold text-brand-orange tabular-nums">
                  {formatCountdown(paymentRemainingSeconds)}
                </span>
              </div>

              <div className="h-[360px] rounded-lg border border-border bg-surface overflow-hidden">
                <iframe
                  title={`PayOS checkout ${pendingPayment.orderId}`}
                  src={pendingPayment.checkoutUrl}
                  className="h-full w-full bg-white"
                />
              </div>

              <div className="flex items-center justify-between gap-3">
                <span className="text-xs font-bold text-text-secondary">
                  Đang chờ khách quét mã...
                </span>
                <div className="flex items-center gap-2">
                  <button
                    type="button"
                    onClick={() => void cancelPendingPayment('manual')}
                    disabled={isCancellingPayment}
                    className="rounded-lg border border-danger/40 bg-white px-3 py-2 text-xs font-extrabold text-danger hover:bg-danger hover:text-white disabled:opacity-50 disabled:cursor-not-allowed transition-colors cursor-pointer"
                  >
                    {isCancellingPayment ? 'Đang hủy' : 'Hủy giao dịch'}
                  </button>
                  <a
                    href={pendingPayment.checkoutUrl}
                    target="_blank"
                    rel="noreferrer"
                    className="rounded-lg border border-brand-orange-border bg-brand-orange-light px-3 py-2 text-xs font-extrabold text-brand-orange hover:bg-brand-orange hover:text-white transition-colors"
                  >
                    Mở PayOS
                  </a>
                </div>
              </div>
            </div>
          </div>
        </div>
      )}

      <ProductModifierModal
        key={activeItemForModifiers?.id ?? 'closed'}
        isOpen={activeItemForModifiers !== null}
        onClose={() => setActiveItemForModifiers(null)}
        menuItem={activeItemForModifiers}
        onConfirm={(selection) => {
          if (activeItemForModifiers) addToCartWithModifiers(activeItemForModifiers, selection)
        }}
      />
    </div>
  )
}
