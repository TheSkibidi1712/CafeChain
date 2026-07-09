import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import * as signalR from '@microsoft/signalr'
import { enqueueOrder } from './services/OfflineSyncService'
import { API_BASE_URL, apiClient } from './services/apiClient'
import {
  allowOfflineTemporaryDrinkLabel,
  printTemporaryDrinkLabels,
  printTemporaryReceipt,
} from './services/offlineTemporaryPrint'
import {
  clearActivePaymentCloseGuard,
  writeActivePaymentCloseGuard,
} from './services/posShiftCloseGuard'
import { getPosSession } from './services/posSession'
import { usePOSData } from './hooks/usePOSData'
import ProductModifierModal, {
  type ModifierSelection,
  type ToppingOption,
  type MenuItem,
} from './components/ProductModifierModal'
import type { CartSyncQueueItem } from './db/CafeChainPOSDB'

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
    pendingCashAmount?: number
    pendingVietQrAmount?: number
  }
  inventoryWarnings?: string[]
}

interface PendingPayment {
  status: 'collecting' | 'awaiting-vietqr'
  cartSnapshot: CartItem[]
  orderTypeSnapshot: 'dine-in' | 'take-away'
  totalAmount: number
  pendingCashAmount: number
  vietQrAmount: number
  orderId?: number
  checkoutUrl?: string
  qrCode?: string | null
  expiresAt?: number
}

interface CashPaymentConfirmation {
  clientOrderId: string
  soldAt: string
  cartSnapshot: CartItem[]
  orderTypeSnapshot: 'dine-in' | 'take-away'
  totalAmount: number
  receivedAmountInput: string
}

interface CancelPaymentApiResponse {
  success: boolean
  code?: string
  message?: string
}

const PAYMENT_TIMEOUT_SECONDS = 5 * 60

const formatVND = (amount: number): string =>
  new Intl.NumberFormat('vi-VN').format(amount) + 'đ'

const getUnavailableReason = (item: MenuItem): string =>
  item.availabilityReason?.trim() || 'Tạm hết hàng'

const formatCountdown = (seconds: number): string => {
  const minutes = Math.floor(seconds / 60)
  const remainingSeconds = seconds % 60
  return `${minutes.toString().padStart(2, '0')}:${remainingSeconds.toString().padStart(2, '0')}`
}

const createPaymentExpiryTimestamp = () =>
  new Date().getTime() + PAYMENT_TIMEOUT_SECONDS * 1000

function getClientOrderId() {
  if (typeof crypto !== 'undefined' && crypto.randomUUID) return crypto.randomUUID()
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0
    const v = c === 'x' ? r : (r & 0x3) | 0x8
    return v.toString(16)
  })
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
  const [pendingCashInput, setPendingCashInput] = useState('')
  const [cashConfirmation, setCashConfirmation] = useState<CashPaymentConfirmation | null>(null)
  const [paymentRemainingSeconds, setPaymentRemainingSeconds] = useState(PAYMENT_TIMEOUT_SECONDS)
  const [isCancellingPayment, setIsCancellingPayment] = useState(false)
  const [lastOfflineOrder, setLastOfflineOrder] = useState<CartSyncQueueItem | null>(null)
  const [temporaryPrintTarget, setTemporaryPrintTarget] = useState<'receipt' | 'labels' | null>(null)
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
  const hasPosIdentity = !!session.staffId && !!session.storeId
  const hasPendingPayment = pendingPayment !== null
  const isCartLocked = hasPendingPayment
  const parsedPendingCash = Math.max(0, Number(pendingCashInput) || 0)
  const pendingCashForCart = Math.min(parsedPendingCash, totalAmount)
  const remainingAfterPendingCash = Math.max(0, totalAmount - pendingCashForCart)
  const cashConfirmationReceivedAmount = Math.max(0, Number(cashConfirmation?.receivedAmountInput) || 0)
  const cashConfirmationChangeAmount = cashConfirmation
    ? Math.max(0, cashConfirmationReceivedAmount - cashConfirmation.totalAmount)
    : 0
  const canConfirmCashPayment = !!cashConfirmation
    && cashConfirmationReceivedAmount >= cashConfirmation.totalAmount
    && !isCheckingOut
  const cashQuickAmounts = useMemo(() => {
    if (!cashConfirmation) return []
    const baseAmount = cashConfirmation.totalAmount
    const roundedAmount = Math.ceil(baseAmount / 10000) * 10000
    return Array.from(new Set([
      baseAmount,
      roundedAmount,
      roundedAmount + 50000,
      roundedAmount + 100000,
    ].filter((amount) => amount >= baseAmount && amount > 0)))
  }, [cashConfirmation])

  useEffect(() => {
    if (!pendingPayment || !hasOpenShift || !shift?.shiftId || !session.staffId || !session.storeId) {
      clearActivePaymentCloseGuard()
      return
    }

    writeActivePaymentCloseGuard({
      status: pendingPayment.status,
      shiftId: shift.shiftId,
      staffId: session.staffId,
      storeId: session.storeId,
      orderId: pendingPayment.orderId,
      totalAmount: pendingPayment.totalAmount,
      pendingCashAmount: pendingPayment.pendingCashAmount,
      vietQrAmount: pendingPayment.vietQrAmount,
      expiresAt: pendingPayment.expiresAt,
    })
  }, [hasOpenShift, pendingPayment, session.staffId, session.storeId, shift?.shiftId])

  const showMessage = useCallback((message: string) => {
    setCheckoutMessage(message)
    window.setTimeout(() => setCheckoutMessage(null), 3500)
  }, [])

  const addToCartWithModifiers = (item: MenuItem, selection: ModifierSelection) => {
    if (isCartLocked) {
      showMessage('Đang thanh toán, hãy hủy giao dịch trước khi sửa giỏ.')
      return
    }

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
    if (isCartLocked) {
      showMessage('Đang thanh toán, hãy hủy giao dịch trước khi sửa giỏ.')
      return
    }

    if (item.isAvailable === false) {
      showMessage(`Không thể thêm món: ${getUnavailableReason(item)}.`)
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
    if (isCartLocked) {
      showMessage('Đang thanh toán, hãy hủy giao dịch trước khi sửa giỏ.')
      return
    }

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
    if (isCartLocked) {
      showMessage('Đang thanh toán, hãy hủy giao dịch trước khi sửa giỏ.')
      return
    }

    setCart((prev) => prev.filter((ci) => ci.cartId !== cartId))
  }

  const clearCart = useCallback(() => setCart([]), [])

  const resetCart = useCallback(() => {
    if (isCartLocked) {
      showMessage('Đang thanh toán, hãy hủy giao dịch trước khi sửa giỏ.')
      return
    }

    clearCart()
  }, [clearCart, isCartLocked, showMessage])

  const closePaymentModalManually = useCallback(() => {
    showMessage('Đang thanh toán. Hãy hủy giao dịch nếu muốn sửa giỏ.')
  }, [showMessage])

  const cancelPendingPayment = useCallback(async (reason: 'manual' | 'timeout') => {
    if (!pendingPayment || isCancellingPayment) return

    if (pendingPayment.status === 'collecting' || !pendingPayment.orderId) {
      setPendingPayment(null)
      setIsCancellingPayment(false)
      showMessage('Đã hủy thanh toán tạm. Kiểm tra lại tiền mặt đã nhận.')
      return
    }

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

    const cancelledCashAmount = pendingPayment.pendingCashAmount
    setPendingPayment(null)
    setIsCancellingPayment(false)
    if (cancelledCashAmount > 0) setPendingCashInput(String(cancelledCashAmount))

    if (response.ok && response.data?.code === 'ALREADY_PAID') {
      clearCart()
      showMessage('Thanh toán VietQR thành công. Lệnh in đã gửi tới Print Bridge.')
      return
    }

    if (response.ok && response.data?.success) {
      showMessage(reason === 'timeout'
        ? 'Giao dịch VietQR đã hết hạn. Kiểm tra lại tiền mặt đã nhận.'
        : 'Đã hủy giao dịch VietQR. Kiểm tra lại tiền mặt đã nhận.')
      return
    }

    showMessage(response.data?.message || response.error || 'Không thể hủy giao dịch VietQR.')
  }, [clearCart, isCancellingPayment, pendingPayment, showMessage])

  useEffect(() => {
    if (!pendingPayment || pendingPayment.status !== 'awaiting-vietqr' || !pendingPayment.expiresAt) return

    let didAutoCancel = false
    const updateCountdown = () => {
      if (didAutoCancel) return

      const nextSeconds = Math.max(0, Math.ceil((pendingPayment.expiresAt! - Date.now()) / 1000))
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
    if (pendingPayment?.status !== 'awaiting-vietqr' || !pendingPayment.orderId) return

    let isDisposed = false
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${API_BASE_URL}/orderHub`)
      .withAutomaticReconnect()
      .build()

    const completePayment = () => {
      if (isDisposed) return
      setPendingPayment(null)
      setPendingCashInput('')
      clearCart()
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
  }, [clearCart, pendingPayment, showMessage])

  const buildOfflineQueueItems = (items: CartItem[]) =>
    items.map((ci) => ({
      menuItemId: ci.id,
      name: ci.name,
      sizeId: ci.sizeId ?? null,
      quantity: ci.quantity,
      unitPrice: ci.price,
      note: ci.note,
      toppings: ci.selectedToppings.map((topping) => ({ toppingId: topping.id })),
    }))

  const buildOfflineCartSnapshot = (items: CartItem[]) =>
    items.map((ci) => ({
      cartId: ci.cartId,
      menuItemId: ci.id,
      name: ci.name,
      categoryId: ci.categoryId,
      sizeId: ci.sizeId ?? null,
      sizeName: ci.sizeName,
      quantity: ci.quantity,
      unitPrice: ci.price,
      note: ci.note,
      detailText: ci.detailText,
      toppings: ci.selectedToppings.map((topping) => ({
        toppingId: topping.id,
        name: topping.name,
        price: topping.price,
      })),
    }))

  const enqueueOrderFallback = async ({
    clientOrderId,
    soldAt,
    cartSnapshot,
    orderTypeSnapshot,
    orderTotal,
    receivedAmount,
  }: {
    clientOrderId: string
    soldAt: string
    cartSnapshot: CartItem[]
    orderTypeSnapshot: 'dine-in' | 'take-away'
    orderTotal: number
    receivedAmount: number
  }) => {
    if (!hasOpenShift || !shift?.shiftId) {
      showMessage('Bạn cần mở ca làm việc trước khi lưu đơn offline.')
      return false
    }

    if (!hasPosIdentity || !session.staffId || !session.storeId) {
      showMessage('Thiếu thông tin nhân viên hoặc cửa hàng, không thể lưu đơn offline.')
      return false
    }

    if (cartSnapshot.length === 0 || orderTotal <= 0) {
      showMessage('Giỏ hàng trống, không thể lưu đơn offline.')
      return false
    }

    const offlineOrder: Omit<CartSyncQueueItem, 'queueId' | 'syncStatus' | 'createdAt' | 'retryCount'> = {
      clientOrderId,
      storeId: session.storeId,
      staffId: session.staffId,
      workShiftId: shift.shiftId,
      soldAt,
      orderType: orderTypeSnapshot,
      items: buildOfflineQueueItems(cartSnapshot),
      cartSnapshot: buildOfflineCartSnapshot(cartSnapshot),
      paymentSnapshot: {
        method: 'cash',
        paymentMethodId: 1,
        amount: orderTotal,
        receivedAmount,
        changeAmount: Math.max(0, receivedAmount - orderTotal),
        capturedAt: soldAt,
      },
      totalAmount: orderTotal,
      paymentMethod: 'cash',
    }

    const queueId = await enqueueOrder(offlineOrder)
    setLastOfflineOrder({
      ...offlineOrder,
      queueId,
      syncStatus: 'Pending',
      createdAt: Date.parse(soldAt),
      retryCount: 0,
    })

    return true
  }

  const snapshotCart = (items: CartItem[]) =>
    items.map((item) => ({
      ...item,
      selectedToppings: [...item.selectedToppings],
    }))

  const buildCommitItems = (items: CartItem[]) =>
    items.map((ci) => ({
      drinkId: ci.id,
      sizeId: ci.sizeId,
      quantity: ci.quantity,
      note: ci.note,
      toppings: ci.selectedToppings.map((topping) => ({ toppingId: topping.id })),
    }))

  const handlePrintTemporaryReceipt = async () => {
    if (!lastOfflineOrder || temporaryPrintTarget) return

    setTemporaryPrintTarget('receipt')
    try {
      await printTemporaryReceipt(lastOfflineOrder)
      showMessage('Đã mở hộp thoại in phiếu tạm.')
    } catch (error) {
      console.error('[POS] Temporary receipt print failed:', error)
      showMessage('Không thể in phiếu tạm. Vui lòng thử lại.')
    } finally {
      setTemporaryPrintTarget(null)
    }
  }

  const handlePrintTemporaryLabels = async () => {
    if (!lastOfflineOrder || temporaryPrintTarget) return

    setTemporaryPrintTarget('labels')
    try {
      await printTemporaryDrinkLabels(lastOfflineOrder)
      showMessage('Đã mở hộp thoại in tem tạm.')
    } catch (error) {
      console.error('[POS] Temporary label print failed:', error)
      showMessage(error instanceof Error ? error.message : 'Không thể in tem tạm.')
    } finally {
      setTemporaryPrintTarget(null)
    }
  }

  const postOrderCommit = (
    items: CartItem[],
    orderTypeValue: 'dine-in' | 'take-away',
    payments: Array<{ paymentMethodId: number; amount: number }>,
    receivedAmount: number,
    clientOrderId = getClientOrderId()
  ) =>
    apiClient.post<POSCommitApiResponse>('/api/v1/pos/orders/commit', {
      items: buildCommitItems(items),
      clientOrderId,
      payments,
      receivedAmount,
      orderTypeId: orderTypeValue === 'dine-in' ? 1 : 2,
      note: '',
    })

  const beginSplitCashFlow = () => {
    if (checkoutInFlightRef.current || cart.length === 0 || hasPendingPayment) return
    if (!hasOpenShift) {
      showMessage('Bạn cần mở ca làm việc trước khi thanh toán.')
      return
    }
    if (!isOnline || !navigator.onLine) {
      showMessage('Không hỗ trợ tách thanh toán khi offline.')
      return
    }
    if (pendingCashForCart <= 0 || pendingCashForCart >= totalAmount) {
      showMessage('Tiền mặt tạm phải nhỏ hơn tổng đơn.')
      return
    }

    setPendingPayment({
      status: 'collecting',
      cartSnapshot: snapshotCart(cart),
      orderTypeSnapshot: orderType,
      totalAmount,
      pendingCashAmount: pendingCashForCart,
      vietQrAmount: remainingAfterPendingCash,
    })
    showMessage('Đang thanh toán: đã ghi nhận tiền mặt tạm.')
  }

  const createVietQrForPendingPayment = async () => {
    if (!pendingPayment || pendingPayment.status !== 'collecting') return
    if (checkoutInFlightRef.current) return
    if (!isOnline || !navigator.onLine) {
      showMessage('Cần kết nối mạng để tạo mã VietQR.')
      return
    }

    checkoutInFlightRef.current = true
    setIsCheckingOut(true)
    try {
      const response = await postOrderCommit(
        pendingPayment.cartSnapshot,
        pendingPayment.orderTypeSnapshot,
        [
          { paymentMethodId: 1, amount: pendingPayment.pendingCashAmount },
          { paymentMethodId: 2, amount: pendingPayment.vietQrAmount },
        ],
        pendingPayment.pendingCashAmount
      )

      if (
        response.ok &&
        response.data?.success &&
        response.data.data?.requiresPayment &&
        response.data.data.orderId &&
        response.data.data.checkoutUrl
      ) {
        const commitData = response.data.data
        setPaymentRemainingSeconds(PAYMENT_TIMEOUT_SECONDS)
        setPendingPayment({
          ...pendingPayment,
          status: 'awaiting-vietqr',
          orderId: commitData.orderId,
          checkoutUrl: commitData.checkoutUrl,
          qrCode: commitData.qrCode,
          pendingCashAmount: commitData.pendingCashAmount ?? pendingPayment.pendingCashAmount,
          vietQrAmount: commitData.pendingVietQrAmount ?? pendingPayment.vietQrAmount,
          expiresAt: createPaymentExpiryTimestamp(),
        })
        showMessage('Đã tạo mã VietQR cho phần còn lại.')
        return
      }

      showMessage(response.data?.message || response.error || 'Không thể tạo mã VietQR.')
    } catch (err) {
      console.error('[POS] Split VietQR error:', err)
      showMessage('Không thể tạo mã VietQR. Vui lòng thử lại.')
    } finally {
      checkoutInFlightRef.current = false
      setIsCheckingOut(false)
    }
  }

  const settlePendingPaymentWithCash = async () => {
    if (!pendingPayment || pendingPayment.status !== 'collecting') return
    if (checkoutInFlightRef.current) return
    if (!isOnline || !navigator.onLine) {
      showMessage('Không hỗ trợ hoàn tất tách thanh toán khi offline.')
      return
    }

    checkoutInFlightRef.current = true
    setIsCheckingOut(true)
    try {
      const response = await postOrderCommit(
        pendingPayment.cartSnapshot,
        pendingPayment.orderTypeSnapshot,
        [{ paymentMethodId: 1, amount: pendingPayment.totalAmount }],
        pendingPayment.totalAmount
      )

      if (response.ok && response.data?.success) {
        const warnings = response.data.inventoryWarnings
        const warningText = warnings?.length ? ` (${warnings.length} cảnh báo kho)` : ''
        setPendingPayment(null)
        setPendingCashInput('')
        clearCart()
        showMessage(`Thanh toán tiền mặt thành công. Lệnh in đã gửi tới Print Bridge.${warningText}`)
        return
      }

      showMessage(response.data?.message || response.error || 'Không thể thanh toán tiền mặt.')
    } catch (err) {
      console.error('[POS] Pending cash settlement error:', err)
      showMessage('Không thể thanh toán tiền mặt. Vui lòng thử lại.')
    } finally {
      checkoutInFlightRef.current = false
      setIsCheckingOut(false)
    }
  }

  const openCashPaymentConfirmation = () => {
    if (checkoutInFlightRef.current || cart.length === 0 || hasPendingPayment) return
    if (!hasOpenShift) {
      showMessage('Bạn cần mở ca làm việc trước khi thanh toán.')
      return
    }

    const initialReceivedAmount = parsedPendingCash > 0 ? parsedPendingCash : totalAmount
    setCashConfirmation({
      clientOrderId: getClientOrderId(),
      soldAt: new Date().toISOString(),
      cartSnapshot: snapshotCart(cart),
      orderTypeSnapshot: orderType,
      totalAmount,
      receivedAmountInput: String(initialReceivedAmount),
    })
  }

  const cancelCashPaymentConfirmation = () => {
    if (isCheckingOut) return
    setCashConfirmation(null)
  }

  const confirmCashPayment = async () => {
    if (!cashConfirmation || checkoutInFlightRef.current) return

    const receivedCashAmount = cashConfirmationReceivedAmount
    if (receivedCashAmount < cashConfirmation.totalAmount) {
      showMessage('Tiền khách đưa chưa đủ để thanh toán.')
      return
    }

    const {
      clientOrderId,
      soldAt,
      cartSnapshot,
      orderTypeSnapshot,
      totalAmount: orderTotal,
    } = cashConfirmation
    const isNetworkUnavailable = !isOnline || !navigator.onLine

    checkoutInFlightRef.current = true
    setIsCheckingOut(true)
    try {
      if (!isNetworkUnavailable) {
        const response = await postOrderCommit(
          cartSnapshot,
          orderTypeSnapshot,
          [{ paymentMethodId: 1, amount: orderTotal }],
          receivedCashAmount,
          clientOrderId
        )

        if (response.ok && response.data?.success) {
          const warnings = response.data.inventoryWarnings
          const warningText = warnings?.length ? ` (${warnings.length} cảnh báo kho)` : ''
          setCashConfirmation(null)
          setPendingCashInput('')
          clearCart()
          showMessage(`Thanh toán thành công. Lệnh in đã gửi tới Print Bridge.${warningText}`)
        } else if (!response.ok && response.status === 0) {
          console.warn('[POS] Network commit failed, saving offline:', response.error)
          const savedOffline = await enqueueOrderFallback({
            clientOrderId,
            soldAt,
            cartSnapshot,
            orderTypeSnapshot,
            orderTotal,
            receivedAmount: receivedCashAmount,
          })
          if (savedOffline) {
            setCashConfirmation(null)
            setPendingCashInput('')
            clearCart()
            showMessage('Đơn đã lưu offline, sẽ tự đồng bộ khi có mạng.')
          }
        } else {
          showMessage(response.data?.message || response.error || 'Không thể thanh toán. Vui lòng kiểm tra lại ca két tiền.')
        }
      } else {
        const savedOffline = await enqueueOrderFallback({
          clientOrderId,
          soldAt,
          cartSnapshot,
          orderTypeSnapshot,
          orderTotal,
          receivedAmount: receivedCashAmount,
        })
        if (savedOffline) {
          setCashConfirmation(null)
          setPendingCashInput('')
          clearCart()
          showMessage('Offline: đơn đã lưu và sẽ tự đồng bộ khi có mạng.')
        }
      }
    } catch (err) {
      console.error('[POS] Cash checkout error:', err)
      if ((!isOnline || !navigator.onLine)) {
        try {
          const savedOffline = await enqueueOrderFallback({
            clientOrderId,
            soldAt,
            cartSnapshot,
            orderTypeSnapshot,
            orderTotal,
            receivedAmount: receivedCashAmount,
          })
          if (savedOffline) {
            setCashConfirmation(null)
            setPendingCashInput('')
            clearCart()
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

  const handleCheckout = async (paymentMethod: 'cash' | 'banking') => {
    if (paymentMethod === 'cash') {
      openCashPaymentConfirmation()
      return
    }

    if (checkoutInFlightRef.current || cart.length === 0 || hasPendingPayment) return
    if (!hasOpenShift) {
      showMessage('Bạn cần mở ca làm việc trước khi thanh toán.')
      return
    }
    const isNetworkUnavailable = !isOnline || !navigator.onLine
    if (isNetworkUnavailable) {
      showMessage('Cần kết nối mạng để tạo mã thanh toán VietQR.')
      return
    }
    if (parsedPendingCash > 0 && parsedPendingCash < totalAmount) {
      beginSplitCashFlow()
      return
    }
    if (parsedPendingCash >= totalAmount) {
      showMessage('Tiền mặt đã đủ cho đơn này.')
      return
    }

    const clientOrderId = getClientOrderId()
    const cartSnapshot = snapshotCart(cart)
    const orderTypeSnapshot = orderType
    const orderTotal = totalAmount

    checkoutInFlightRef.current = true
    setIsCheckingOut(true)
    try {
      const response = await postOrderCommit(
        cartSnapshot,
        orderTypeSnapshot,
        [{ paymentMethodId: 2, amount: orderTotal }],
        orderTotal,
        clientOrderId
      )

      if (response.ok && response.data?.success) {
        const commitData = response.data.data
        if (
          commitData?.requiresPayment &&
          commitData.orderId &&
          commitData.checkoutUrl
        ) {
          setPaymentRemainingSeconds(PAYMENT_TIMEOUT_SECONDS)
          setPendingPayment({
            status: 'awaiting-vietqr',
            cartSnapshot,
            orderTypeSnapshot,
            totalAmount: orderTotal,
            pendingCashAmount: 0,
            vietQrAmount: commitData.pendingVietQrAmount ?? commitData.total ?? orderTotal,
            orderId: commitData.orderId,
            checkoutUrl: commitData.checkoutUrl,
            qrCode: commitData.qrCode,
            expiresAt: createPaymentExpiryTimestamp(),
          })
          showMessage('Đã tạo mã VietQR, đang chờ xác nhận thanh toán.')
          return
        }

        const warnings = response.data.inventoryWarnings
        const warningText = warnings?.length ? ` (${warnings.length} cảnh báo kho)` : ''
        setPendingCashInput('')
        clearCart()
        showMessage(`Thanh toán thành công. Lệnh in đã gửi tới Print Bridge.${warningText}`)
      } else {
        showMessage(response.data?.message || response.error || 'Không thể thanh toán. Vui lòng kiểm tra lại ca két tiền.')
      }
    } catch (err) {
      console.error('[POS] Checkout error:', err)
      showMessage('Không thể thanh toán. Vui lòng thử lại.')
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
              onClick={() => {
                if (isCartLocked) {
                  showMessage('Đang thanh toán, hãy hủy giao dịch trước khi đổi loại đơn.')
                  return
                }
                setOrderType('dine-in')
              }}
              disabled={isCartLocked}
              className={`flex-1 py-2 rounded-lg text-xs font-semibold transition-colors cursor-pointer ${
                orderType === 'dine-in'
                  ? 'bg-brand-orange text-white'
                  : 'bg-surface text-text-secondary hover:bg-surface-hover'
              } disabled:opacity-40 disabled:cursor-not-allowed`}
            >
              Tại quán
            </button>
            <button
              onClick={() => {
                if (isCartLocked) {
                  showMessage('Đang thanh toán, hãy hủy giao dịch trước khi đổi loại đơn.')
                  return
                }
                setOrderType('take-away')
              }}
              disabled={isCartLocked}
              className={`flex-1 py-2 rounded-lg text-xs font-semibold transition-colors cursor-pointer ${
                orderType === 'take-away'
                  ? 'bg-brand-orange text-white'
                  : 'bg-surface text-text-secondary hover:bg-surface-hover'
              } disabled:opacity-40 disabled:cursor-not-allowed`}
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
              Không có sản phẩm trong danh mục này
            </div>
          ) : (
            <div className="grid grid-cols-3 gap-3">
              {filteredItems.map((item) => {
                const qtyInCart = getQuantityInCart(item.id)
                const isUnavailable = item.isAvailable === false
                const isProductLocked = isCartLocked || isUnavailable
                const unavailableReason = isUnavailable ? getUnavailableReason(item) : ''
                return (
                  <div
                    key={item.id}
                    onClick={() => handleQuickAdd(item)}
                    className={`relative bg-surface-card rounded-xl border border-border p-4 flex flex-col items-center select-none shadow-[var(--shadow-card)] transition-all duration-200 min-h-[150px] ${
                      isProductLocked
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
                      <span
                        className="absolute top-2.5 left-2.5 max-w-[120px] truncate px-2 py-1 bg-surface border border-border text-text-secondary text-[9px] font-extrabold rounded-lg z-10"
                        title={unavailableReason}
                      >
                        {unavailableReason}
                      </span>
                    )}

                    <button
                      onClick={(event) => {
                        event.stopPropagation()
                        if (isUnavailable) {
                          showMessage(`Không thể thêm món: ${unavailableReason}.`)
                          return
                        }
                        if (isCartLocked) {
                          showMessage('Đang thanh toán, hãy hủy giao dịch trước khi sửa giỏ.')
                          return
                        }
                        setActiveItemForModifiers(item)
                      }}
                      disabled={isProductLocked}
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
                    {isUnavailable && (
                      <span className="mb-2 line-clamp-2 min-h-[24px] text-center text-[9px] font-semibold leading-3 text-red-600">
                        {unavailableReason}
                      </span>
                    )}
                    <div className="mt-auto text-[9px] text-text-secondary font-bold bg-surface px-2.5 py-1 rounded-md border border-border-light">
                      {isUnavailable ? 'Không thể bán' : 'Thêm nhanh'}
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
                disabled={isCartLocked}
                className="text-[10px] font-semibold text-danger hover:text-danger-hover border border-danger/30 px-2 py-0.5 rounded-full hover:bg-danger/5 transition-colors cursor-pointer disabled:opacity-40 disabled:cursor-not-allowed"
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
                      disabled={isCartLocked}
                      className="w-6 h-6 rounded-md bg-surface border border-border text-text-secondary hover:bg-brand-orange-light hover:text-brand-orange hover:border-brand-orange-border text-xs font-bold flex items-center justify-center cursor-pointer transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
                    >
                      -
                    </button>
                    <span className="w-5 text-center text-xs font-bold text-text-primary">
                      {item.quantity}
                    </span>
                    <button
                      onClick={() => {
                        if (isCartLocked) {
                          showMessage('Đang thanh toán, hãy hủy giao dịch trước khi sửa giỏ.')
                          return
                        }
                        setCart((prev) => prev.map((ci) =>
                          ci.cartId === item.cartId ? { ...ci, quantity: ci.quantity + 1 } : ci
                        ))
                      }}
                      disabled={isCartLocked}
                      className="w-6 h-6 rounded-md bg-brand-orange text-white hover:bg-brand-orange-hover text-xs font-bold flex items-center justify-center cursor-pointer transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
                    >
                      +
                    </button>
                  </div>

                  <button
                    onClick={() => removeFromCart(item.cartId)}
                    disabled={isCartLocked}
                    className="w-6 h-6 rounded-md border border-danger/30 text-danger hover:bg-danger hover:text-white hover:border-danger text-xs flex items-center justify-center cursor-pointer transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
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

          {cart.length > 0 && !hasPendingPayment && (
            <div className="rounded-lg border border-border bg-surface px-3 py-2 space-y-2">
              <label className="block text-[11px] font-bold text-text-secondary" htmlFor="pending-cash-input">
                Tiền mặt tạm
              </label>
              <div className="flex items-center gap-2">
                <input
                  id="pending-cash-input"
                  type="number"
                  min="0"
                  step="1000"
                  value={pendingCashInput}
                  onChange={(event) => setPendingCashInput(event.target.value)}
                  className="min-w-0 flex-1 rounded-lg border border-border bg-white px-3 py-2 text-xs font-bold text-text-primary outline-none focus:border-brand-orange"
                  placeholder="0"
                />
                <span className="text-[11px] font-extrabold text-text-secondary">VNĐ</span>
              </div>
              {parsedPendingCash > 0 && (
                <div className="flex items-center justify-between text-[11px] font-bold text-text-secondary">
                  <span>Còn lại</span>
                  <span className="text-brand-orange">{formatVND(remainingAfterPendingCash)}</span>
                </div>
              )}
              {pendingCashForCart > 0 && pendingCashForCart < totalAmount && (
                <button
                  type="button"
                  onClick={beginSplitCashFlow}
                  disabled={isCheckingOut || !hasOpenShift || !isOnline}
                  className="w-full rounded-lg border border-brand-orange-border bg-brand-orange-light px-3 py-2 text-xs font-extrabold text-brand-orange hover:bg-brand-orange hover:text-white disabled:opacity-40 disabled:cursor-not-allowed transition-colors cursor-pointer"
                >
                  Ghi nhận tạm
                </button>
              )}
            </div>
          )}

          {pendingPayment && (
            <div className="rounded-lg border border-brand-orange-border bg-brand-orange-light px-3 py-2 space-y-2">
              <div className="flex items-center justify-between gap-2">
                <span className="text-xs font-extrabold text-brand-orange">Đang thanh toán</span>
                <span className="rounded-full bg-white px-2 py-0.5 text-[10px] font-extrabold text-brand-orange">
                  {pendingPayment.status === 'awaiting-vietqr' ? 'Chờ VietQR' : 'Tiền mặt tạm'}
                </span>
              </div>
              <div className="grid grid-cols-2 gap-2 text-[11px] font-bold text-text-secondary">
                <div>
                  <p>Tiền mặt</p>
                  <p className="text-text-primary">{formatVND(pendingPayment.pendingCashAmount)}</p>
                </div>
                <div>
                  <p>Còn lại</p>
                  <p className="text-brand-orange">{formatVND(pendingPayment.vietQrAmount)}</p>
                </div>
              </div>
              {pendingPayment.status === 'collecting' && (
                <div className="grid grid-cols-3 gap-1.5">
                  <button
                    type="button"
                    onClick={() => void createVietQrForPendingPayment()}
                    disabled={isCheckingOut || !isOnline}
                    className="rounded-lg bg-text-primary px-2 py-2 text-[10px] font-extrabold text-white hover:bg-gray-700 disabled:opacity-40 disabled:cursor-not-allowed transition-colors cursor-pointer"
                  >
                    VietQR
                  </button>
                  <button
                    type="button"
                    onClick={() => void settlePendingPaymentWithCash()}
                    disabled={isCheckingOut || !isOnline}
                    className="rounded-lg bg-brand-orange px-2 py-2 text-[10px] font-extrabold text-white hover:bg-brand-orange-hover disabled:opacity-40 disabled:cursor-not-allowed transition-colors cursor-pointer"
                  >
                    Tiền mặt
                  </button>
                  <button
                    type="button"
                    onClick={() => void cancelPendingPayment('manual')}
                    disabled={isCheckingOut || isCancellingPayment}
                    className="rounded-lg border border-danger/40 bg-white px-2 py-2 text-[10px] font-extrabold text-danger hover:bg-danger hover:text-white disabled:opacity-40 disabled:cursor-not-allowed transition-colors cursor-pointer"
                  >
                    Hủy
                  </button>
                </div>
              )}
            </div>
          )}

          {!hasOpenShift && (
            <Link
              to="/shift"
              className="block text-center px-3 py-2 rounded-lg border border-brand-orange-border bg-brand-orange-light text-brand-orange text-xs font-bold"
            >
              Mở ca trước khi thanh toán
            </Link>
          )}

          {lastOfflineOrder && (
            <div className="rounded-lg border border-brand-orange-border bg-brand-orange-light px-3 py-2 space-y-2">
              <div className="flex items-start justify-between gap-2">
                <div className="min-w-0">
                  <p className="text-xs font-extrabold text-brand-orange">Đơn offline vừa lưu</p>
                  <p className="text-[10px] font-bold text-text-secondary truncate">
                    {lastOfflineOrder.clientOrderId}
                  </p>
                </div>
                <button
                  type="button"
                  onClick={() => setLastOfflineOrder(null)}
                  className="h-6 w-6 shrink-0 rounded-md border border-brand-orange-border bg-white text-[11px] font-extrabold text-brand-orange hover:bg-brand-orange hover:text-white transition-colors cursor-pointer"
                  title="Ẩn đơn offline vừa lưu"
                  aria-label="Ẩn đơn offline vừa lưu"
                >
                  x
                </button>
              </div>
              <div className="grid grid-cols-2 gap-2">
                <button
                  type="button"
                  onClick={() => void handlePrintTemporaryReceipt()}
                  disabled={temporaryPrintTarget !== null}
                  className="rounded-lg bg-brand-orange px-3 py-2 text-[11px] font-extrabold text-white hover:bg-brand-orange-hover disabled:opacity-40 disabled:cursor-not-allowed transition-colors cursor-pointer"
                >
                  {temporaryPrintTarget === 'receipt' ? 'Đang in' : 'In phiếu tạm'}
                </button>
                {allowOfflineTemporaryDrinkLabel && (
                  <button
                    type="button"
                    onClick={() => void handlePrintTemporaryLabels()}
                    disabled={temporaryPrintTarget !== null}
                    className="rounded-lg bg-text-primary px-3 py-2 text-[11px] font-extrabold text-white hover:bg-gray-700 disabled:opacity-40 disabled:cursor-not-allowed transition-colors cursor-pointer"
                  >
                    {temporaryPrintTarget === 'labels' ? 'Đang in' : 'In tem tạm'}
                  </button>
                )}
              </div>
            </div>
          )}

          <div className="flex gap-2">
            <button
              onClick={() => handleCheckout('cash')}
              disabled={cart.length === 0 || isCheckingOut || !hasOpenShift || hasPendingPayment || cashConfirmation !== null}
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
              disabled={cart.length === 0 || isCheckingOut || !hasOpenShift || hasPendingPayment || !isOnline || cashConfirmation !== null}
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

      {cashConfirmation && (
        <div className="fixed inset-0 z-[60] bg-black/55 flex items-center justify-center px-4">
          <div
            role="dialog"
            aria-modal="true"
            aria-labelledby="cash-payment-title"
            className="w-full max-w-md rounded-2xl bg-white shadow-2xl border border-border overflow-hidden"
          >
            <div className="px-5 py-4 border-b border-border">
              <h3 id="cash-payment-title" className="text-base font-extrabold text-text-primary">
                Xác nhận thanh toán tiền mặt
              </h3>
            </div>
            <div className="p-5 space-y-4">
              <div className="rounded-xl border border-border bg-surface px-4 py-3 space-y-2">
                <div className="flex items-center justify-between gap-3 text-sm">
                  <span className="font-bold text-text-secondary">Tổng tiền cần thanh toán</span>
                  <span className="font-extrabold text-brand-orange tabular-nums">
                    {formatVND(cashConfirmation.totalAmount)}
                  </span>
                </div>
                <div className="flex items-center justify-between gap-3 text-sm">
                  <span className="font-bold text-text-secondary">Tiền thừa</span>
                  <span className="font-extrabold text-text-primary tabular-nums">
                    {formatVND(cashConfirmationChangeAmount)}
                  </span>
                </div>
              </div>

              <div className="space-y-2">
                <label className="block text-xs font-extrabold text-text-secondary" htmlFor="cash-received-input">
                  Tiền khách đưa
                </label>
                <div className="flex items-center gap-2">
                  <input
                    id="cash-received-input"
                    type="number"
                    min="0"
                    step="1000"
                    value={cashConfirmation.receivedAmountInput}
                    onChange={(event) => setCashConfirmation((current) => current
                      ? { ...current, receivedAmountInput: event.target.value }
                      : current)}
                    className="min-w-0 flex-1 rounded-xl border border-border bg-white px-3 py-3 text-base font-extrabold text-text-primary outline-none focus:border-brand-orange"
                    autoFocus
                  />
                  <span className="text-xs font-extrabold text-text-secondary">VNĐ</span>
                </div>
                {cashConfirmationReceivedAmount < cashConfirmation.totalAmount && (
                  <p className="text-xs font-bold text-danger">
                    Tiền khách đưa chưa đủ để thanh toán.
                  </p>
                )}
              </div>

              <div className="grid grid-cols-2 gap-2">
                {cashQuickAmounts.map((amount) => (
                  <button
                    key={amount}
                    type="button"
                    onClick={() => setCashConfirmation((current) => current
                      ? { ...current, receivedAmountInput: String(amount) }
                      : current)}
                    className="rounded-lg border border-border bg-surface px-3 py-2 text-xs font-extrabold text-text-secondary hover:border-brand-orange-border hover:text-brand-orange transition-colors cursor-pointer"
                  >
                    {formatVND(amount)}
                  </button>
                ))}
              </div>
            </div>
            <div className="grid grid-cols-2 gap-2 px-5 py-4 bg-surface border-t border-border">
              <button
                type="button"
                onClick={cancelCashPaymentConfirmation}
                disabled={isCheckingOut}
                className="rounded-xl border border-border bg-white px-4 py-3 text-sm font-extrabold text-text-secondary hover:bg-surface-hover disabled:opacity-40 disabled:cursor-not-allowed transition-colors cursor-pointer"
              >
                Hủy
              </button>
              <button
                type="button"
                onClick={() => void confirmCashPayment()}
                disabled={!canConfirmCashPayment}
                className="rounded-xl bg-brand-orange px-4 py-3 text-sm font-extrabold text-white hover:bg-brand-orange-hover disabled:opacity-40 disabled:cursor-not-allowed transition-colors cursor-pointer"
              >
                {isCheckingOut ? 'Đang xử lý' : 'Xác nhận'}
              </button>
            </div>
          </div>
        </div>
      )}

      {pendingPayment?.status === 'awaiting-vietqr' && pendingPayment.checkoutUrl && pendingPayment.orderId && (
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
                  Đơn #{pendingPayment.orderId} · {formatVND(pendingPayment.vietQrAmount)}
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
          if (isCartLocked) {
            showMessage('Đang thanh toán, hãy hủy giao dịch trước khi sửa giỏ.')
            setActiveItemForModifiers(null)
            return
          }

          if (activeItemForModifiers) addToCartWithModifiers(activeItemForModifiers, selection)
          setActiveItemForModifiers(null)
        }}
      />
    </div>
  )
}
