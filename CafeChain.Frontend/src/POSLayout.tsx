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
import CartLine from './components/pos/CartLine'
import SellingHeader from './components/pos/SellingHeader'
import type { CartSyncQueueItem } from './db/CafeChainPOSDB'

interface CartItem {
  id: number
  cartId: string
  name: string
  price: number
  acceptedBasePrice: number
  storeMenuItemId: number
  drinkSizeId: number
  priceSource: string
  catalogVersion: number
  categoryId: number
  quantity: number
  sizeId?: number | null
  sizeName?: string
  ice: '0%' | '50%' | '100%'
  sugar: '0%' | '50%' | '100%'
  customerNote: string
  note: string
  selectedToppings: ToppingOption[]
  optionSummary: string
  detailText: string
}

interface ActiveModifier {
  item: MenuItem
  editingCartId?: string
  initialSelection?: ModifierSelection
}

interface ShiftSummary {
  shiftId?: number | null
  status: 'Open' | 'Closed' | 'NoActiveShift' | string
}

interface POSCommitApiResponse {
  success: boolean
  message?: string
  errorCode?: string
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
  clientOrderId: string
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

interface CashReturnConfirmation {
  reason: 'manual' | 'timeout'
  requestKey: string
}

const PAYMENT_TIMEOUT_SECONDS = 5 * 60
const CASH_DENOMINATION_STEP = 1000

const validateCashVnd = (amount: number, allowZero = false): string | null => {
  if (!Number.isSafeInteger(amount) || amount < 0 || (!allowZero && amount === 0)) {
    return allowZero ? 'Số tiền mặt không hợp lệ.' : 'Số tiền mặt phải lớn hơn 0.'
  }
  return amount % CASH_DENOMINATION_STEP === 0
    ? null
    : 'Số tiền mặt phải là bội số của 1.000đ.'
}

const formatCashInput = (value: string): string =>
  value ? new Intl.NumberFormat('vi-VN').format(Number(value)) : ''

const formatVND = (amount: number): string =>
  new Intl.NumberFormat('vi-VN').format(amount) + 'đ'

const getUnavailableReason = (item: MenuItem): string =>
  item.availabilityReason?.trim() || 'Tạm hết hàng'

const requiresProductOptions = (item: MenuItem): boolean =>
  (item.sizes?.filter((size) => size.isAvailable).length ?? 0) > 1
  || (item.availableToppings?.length ?? 0) > 0

const applyToppingPolicy = (
  topping: ToppingOption,
  size: NonNullable<MenuItem['sizes']>[number]
): ToppingOption => {
  const policy = size.toppingPolicies?.find((item) => item.toppingId === topping.id)
  return {
    ...topping,
    acceptedPrice: policy?.priceTreatment === 'INCLUDED_IN_BASE_PRICE'
      ? 0
      : topping.price * (policy?.quantityPerDrink ?? 1),
    isRequired: policy?.isRequired ?? false,
    isDefaultSelected: policy?.isDefaultSelected ?? false,
    priceTreatment: policy?.priceTreatment,
    quantityPerDrink: policy?.quantityPerDrink ?? 1,
  }
}

const getDefaultToppings = (
  item: MenuItem,
  size: NonNullable<MenuItem['sizes']>[number]
): ToppingOption[] => {
  const defaultIds = new Set(
    (size.toppingPolicies ?? [])
      .filter((policy) => policy.isDefaultSelected || policy.isRequired)
      .map((policy) => policy.toppingId)
  )
  return (item.availableToppings ?? [])
    .filter((topping) => defaultIds.has(topping.id))
    .map((topping) => applyToppingPolicy(topping, size))
}

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
        className="aspect-[4/3] w-full rounded-lg object-cover bg-brand-orange-light"
        loading="lazy"
        width="320"
        height="240"
        onError={() => setHasImageError(true)}
      />
    )
  }

  return (
    <div className="aspect-[4/3] w-full rounded-lg bg-brand-orange-light flex items-center justify-center text-3xl text-brand-orange" role="img" aria-label={`Chưa có ảnh ${name}`}>
      <span aria-hidden="true">{fallbackIcon}</span>
    </div>
  )
}

export default function POSLayout() {
  const {
    categories,
    menuItems,
    isLoading,
    isOnline,
    catalogError,
    refreshCatalog,
  } = usePOSData()
  const [selectedCategory, setSelectedCategory] = useState<number | null>(null)
  const [searchQuery, setSearchQuery] = useState('')
  const [isCartOpen, setIsCartOpen] = useState(false)
  const [cart, setCart] = useState<CartItem[]>([])
  const [orderType, setOrderType] = useState<'dine-in' | 'take-away'>('dine-in')
  const [isCheckingOut, setIsCheckingOut] = useState(false)
  const [checkoutMessage, setCheckoutMessage] = useState<string | null>(null)
  const [activeModifier, setActiveModifier] = useState<ActiveModifier | null>(null)
  const [shift, setShift] = useState<ShiftSummary | null>(null)
  const [pendingPayment, setPendingPayment] = useState<PendingPayment | null>(null)
  const [pendingCashInput, setPendingCashInput] = useState('')
  const [cashConfirmation, setCashConfirmation] = useState<CashPaymentConfirmation | null>(null)
  const [cashReturnConfirmation, setCashReturnConfirmation] = useState<CashReturnConfirmation | null>(null)
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

  const selectedCategoryId = selectedCategory !== null
    && categories.some((cat) => cat.id === selectedCategory)
    ? selectedCategory
    : null

  const filteredItems = useMemo(() => {
    const normalizedSearch = searchQuery.trim().toLocaleLowerCase('vi-VN')
    return menuItems.filter((item) =>
      (selectedCategoryId === null || item.categoryId === selectedCategoryId)
      && (!normalizedSearch || item.name.toLocaleLowerCase('vi-VN').includes(normalizedSearch))
    )
  }, [menuItems, searchQuery, selectedCategoryId])

  const totalAmount = cart.reduce((sum, item) => sum + item.price * item.quantity, 0)
  const totalItems = cart.reduce((sum, item) => sum + item.quantity, 0)
  const currentCategory = categories.find((cat) => cat.id === selectedCategoryId)
  const hasOpenShift = shift?.status === 'Open' && !!shift.shiftId
  const hasPosIdentity = !!session.staffId && !!session.storeId
  const hasPendingPayment = pendingPayment !== null
  const isCartLocked = hasPendingPayment
  const staleCartItems = useMemo(() => {
    if (isCartLocked) return []

    return cart.filter((line) => {
      const currentItem = menuItems.find((item) => item.id === line.id)
      const currentSize = currentItem?.sizes?.find((size) => size.drinkSizeId === line.drinkSizeId)
      if (!currentItem || !currentSize || !currentSize.isAvailable) return true
      if (currentItem.catalogVersion !== line.catalogVersion
        || currentSize.storeMenuItemId !== line.storeMenuItemId
        || currentSize.price !== line.acceptedBasePrice
        || currentSize.priceSource !== line.priceSource) return true

      const selectedIds = new Set(line.selectedToppings.map((topping) => topping.id))
      if ((currentSize.toppingPolicies ?? []).some((policy) => policy.isRequired && !selectedIds.has(policy.toppingId))) {
        return true
      }

      return line.selectedToppings.some((selected) => {
        const current = currentItem.availableToppings?.find((topping) => topping.id === selected.id)
        if (!current) return true
        const expected = applyToppingPolicy(current, currentSize)
        return expected.acceptedPrice !== (selected.acceptedPrice ?? selected.price)
      })
    })
  }, [cart, isCartLocked, menuItems])
  const hasStaleCart = staleCartItems.length > 0
  const parsedPendingCash = Math.max(0, Number(pendingCashInput) || 0)
  const pendingCashForCart = parsedPendingCash
  const remainingAfterPendingCash = Math.max(0, totalAmount - parsedPendingCash)
  const pendingCashValidation = pendingCashInput
    ? validateCashVnd(parsedPendingCash)
    : null
  const cashConfirmationReceivedAmount = Math.max(0, Number(cashConfirmation?.receivedAmountInput) || 0)
  const cashConfirmationChangeAmount = cashConfirmation
    ? Math.max(0, cashConfirmationReceivedAmount - cashConfirmation.totalAmount)
    : 0
  const canConfirmCashPayment = !!cashConfirmation
    && cashConfirmationReceivedAmount >= cashConfirmation.totalAmount
    && validateCashVnd(cashConfirmationReceivedAmount) === null
    && !isCheckingOut
  const cashQuickAmounts = useMemo(() => {
    if (!cashConfirmation) return []
    const baseAmount = cashConfirmation.totalAmount
    return Array.from(new Set([
      baseAmount,
      50000,
      100000,
      200000,
      500000,
    ].filter((amount) => amount >= baseAmount && amount > 0)))
  }, [cashConfirmation])

  const updateCashReceivedInput = (value: string) => {
    const digitsOnly = value.replace(/\D/g, '').replace(/^0+(?=\d)/, '')
    setCashConfirmation((current) => current
      ? { ...current, receivedAmountInput: digitsOnly }
      : current)
  }

  const appendCashDigit = (digit: string) => {
    setCashConfirmation((current) => {
      if (!current) return current
      const nextValue = `${current.receivedAmountInput}${digit}`.replace(/^0+(?=\d)/, '')
      return { ...current, receivedAmountInput: nextValue }
    })
  }

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

  const closeModifierModal = useCallback(() => {
    setActiveModifier(null)
  }, [])

  const changeOrderType = useCallback((nextOrderType: 'dine-in' | 'take-away') => {
    if (isCartLocked) {
      showMessage('Đang thanh toán, hãy hủy giao dịch trước khi đổi loại đơn.')
      return
    }
    setOrderType(nextOrderType)
  }, [isCartLocked, showMessage])

  useEffect(() => {
    const closeCartOnEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setIsCartOpen(false)
    }
    window.addEventListener('keydown', closeCartOnEscape)
    return () => window.removeEventListener('keydown', closeCartOnEscape)
  }, [])

  const applyModifierSelection = (
    item: MenuItem,
    selection: ModifierSelection,
    editingCartId?: string
  ) => {
    if (isCartLocked) {
      showMessage('Đang thanh toán, hãy hủy giao dịch trước khi sửa giỏ.')
      return
    }

    if (!selection.size || !selection.size.isAvailable) {
      showMessage('Size đã chọn hiện không khả dụng. Vui lòng chọn size khác.')
      return
    }
    const selectedSize = selection.size

    const toppingKey = selection.selectedToppings
      .map((topping) => topping.id)
      .sort((a, b) => a - b)
      .join(',')
    const sizeKey = selection.size?.sizeId ?? 'default'
    const noteKey = encodeURIComponent(selection.customerNote.trim().toLocaleLowerCase('vi-VN'))
    const cartId = `${item.id}-${sizeKey}-${selection.ice}-${selection.sugar}-${toppingKey}-${noteKey}`
    const toppingNames = selection.selectedToppings.map((topping) => topping.name).join(', ')
    const optionSummary = [
      selection.size ? `Size ${selection.size.sizeName}` : '',
      `Đá ${selection.ice}`,
      `Đường ${selection.sugar}`,
      toppingNames ? `+${toppingNames}` : '',
    ].filter(Boolean).join(', ')
    const detailText = [optionSummary, selection.customerNote].filter(Boolean).join('. ')

    const nextLine: CartItem = {
      id: item.id,
      cartId,
      name: item.name,
      price: selection.totalPrice,
      acceptedBasePrice: selectedSize.price,
      storeMenuItemId: selectedSize.storeMenuItemId,
      drinkSizeId: selectedSize.drinkSizeId,
      priceSource: selectedSize.priceSource,
      catalogVersion: item.catalogVersion,
      categoryId: item.categoryId,
      quantity: selection.quantity,
      sizeId: selection.size?.sizeId ?? null,
      sizeName: selection.size?.sizeName,
      ice: selection.ice,
      sugar: selection.sugar,
      customerNote: selection.customerNote,
      note: selection.note,
      selectedToppings: selection.selectedToppings,
      optionSummary,
      detailText,
    }

    setCart((prev) => {
      if (editingCartId) {
        const collision = prev.find((line) => line.cartId === cartId && line.cartId !== editingCartId)
        if (collision) {
          return prev
            .filter((line) => line.cartId !== editingCartId)
            .map((line) => line.cartId === cartId
              ? { ...line, quantity: line.quantity + selection.quantity }
              : line)
        }

        return prev.map((line) => line.cartId === editingCartId ? nextLine : line)
      }

      const existing = prev.find((ci) => ci.cartId === cartId)
      if (existing) {
        return prev.map((ci) =>
          ci.cartId === cartId ? { ...ci, quantity: ci.quantity + selection.quantity } : ci
        )
      }

      return [...prev, nextLine]
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

    const defaultSize = item.sizes?.find((size) => size.isAvailable) ?? null
    if (!defaultSize) {
      showMessage(`Không thể thêm món: ${getUnavailableReason(item)}.`)
      return
    }
    const defaultToppings = getDefaultToppings(item, defaultSize)
    const toppingTotal = defaultToppings.reduce(
      (sum, topping) => sum + (topping.acceptedPrice ?? topping.price),
      0
    )
    applyModifierSelection(item, {
      size: defaultSize,
      ice: '100%',
      sugar: '100%',
      selectedToppings: defaultToppings,
      quantity: 1,
      customerNote: '',
      totalPrice: defaultSize.price + toppingTotal,
      note: 'Đá 100%, Đường 100%',
    })
  }

  const handleProductSelection = (item: MenuItem) => {
    if (isCartLocked) {
      showMessage('Đang thanh toán, hãy hủy giao dịch trước khi sửa giỏ.')
      return
    }

    if (item.isAvailable === false) {
      showMessage(`Không thể thêm món: ${getUnavailableReason(item)}.`)
      return
    }

    if (requiresProductOptions(item)) {
      setActiveModifier({ item })
      return
    }

    handleQuickAdd(item)
  }

  const editCartLine = (line: CartItem) => {
    if (isCartLocked) {
      showMessage('Đang thanh toán, hãy hủy giao dịch trước khi sửa giỏ.')
      return
    }

    const item = menuItems.find((menuItem) => menuItem.id === line.id)
    const size = item?.sizes?.find((option) => option.drinkSizeId === line.drinkSizeId) ?? null
    if (!item || !size?.isAvailable) {
      showMessage('Món hoặc size này không còn khả dụng. Hãy cập nhật lại giỏ trước khi sửa.')
      return
    }

    const selectedToppings = line.selectedToppings
      .map((selected) => item.availableToppings?.find((topping) => topping.id === selected.id))
      .filter((topping): topping is ToppingOption => Boolean(topping))
      .map((topping) => applyToppingPolicy(topping, size))
    const toppingTotal = selectedToppings.reduce(
      (sum, topping) => sum + (topping.acceptedPrice ?? topping.price),
      0
    )

    setActiveModifier({
      item,
      editingCartId: line.cartId,
      initialSelection: {
        size,
        ice: line.ice,
        sugar: line.sugar,
        selectedToppings,
        quantity: line.quantity,
        customerNote: line.customerNote,
        totalPrice: size.price + toppingTotal,
        note: line.note,
      },
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

  const confirmClearCart = useCallback(() => {
    if (cart.length === 0 || isCartLocked) return
    if (!window.confirm('Xóa toàn bộ món khỏi giỏ hàng?')) return
    resetCart()
  }, [cart.length, isCartLocked, resetCart])

  const refreshStaleCart = () => {
    if (isCartLocked || !hasStaleCart) return
    if (!window.confirm('Catalog hoặc giá bán đã thay đổi. Cập nhật lại các món trong giỏ theo menu mới?')) return

    let unresolvedCount = 0
    const refreshed = cart.map((line) => {
      const currentItem = menuItems.find((item) => item.id === line.id)
      const currentSize = currentItem?.sizes?.find((size) => size.drinkSizeId === line.drinkSizeId)
      if (!currentItem || !currentSize || !currentSize.isAvailable) {
        unresolvedCount++
        return line
      }

      const selectedIds = new Set(line.selectedToppings.map((topping) => topping.id))
      const selected = (currentItem.availableToppings ?? [])
        .filter((topping) => selectedIds.has(topping.id))
        .map((topping) => applyToppingPolicy(topping, currentSize))
      const defaults = getDefaultToppings(currentItem, currentSize)
      const merged = Array.from(
        new Map([...selected, ...defaults].map((topping) => [topping.id, topping])).values()
      )
      const toppingTotal = merged.reduce(
        (sum, topping) => sum + (topping.acceptedPrice ?? topping.price),
        0
      )
      const toppingNames = merged.map((topping) => topping.name).join(', ')

      return {
        ...line,
        name: currentItem.name,
        price: currentSize.price + toppingTotal,
        acceptedBasePrice: currentSize.price,
        storeMenuItemId: currentSize.storeMenuItemId,
        drinkSizeId: currentSize.drinkSizeId,
        priceSource: currentSize.priceSource,
        catalogVersion: currentItem.catalogVersion,
        sizeId: currentSize.sizeId,
        sizeName: currentSize.sizeName,
        selectedToppings: merged,
        optionSummary: [
          `Size ${currentSize.sizeName}`,
          `Đá ${line.ice}`,
          `Đường ${line.sugar}`,
          toppingNames ? `+${toppingNames}` : '',
        ].filter(Boolean).join(', '),
        detailText: [
          `Size ${currentSize.sizeName}, Đá ${line.ice}, Đường ${line.sugar}`,
          toppingNames ? `+${toppingNames}` : '',
          line.customerNote,
        ].filter(Boolean).join('. '),
      }
    })

    setCart(refreshed)
    showMessage(unresolvedCount > 0
      ? `Đã cập nhật giá. Còn ${unresolvedCount} món không khả dụng cần xóa hoặc thay thế.`
      : 'Đã cập nhật giỏ theo catalog mới. Vui lòng kiểm tra lại tổng tiền.')
  }

  const closePaymentModalManually = useCallback(() => {
    showMessage('Đang thanh toán. Hãy hủy giao dịch nếu muốn sửa giỏ.')
  }, [showMessage])

  const cancelPendingPayment = useCallback(async (
    reason: 'manual' | 'timeout',
    cashReturnedConfirmed = false
  ) => {
    if (!pendingPayment || isCancellingPayment) return

    const hasPhysicalCash = pendingPayment.pendingCashAmount > 0
    if (reason === 'manual' && hasPhysicalCash && !cashReturnedConfirmed) {
      setCashReturnConfirmation({ reason, requestKey: crypto.randomUUID() })
      return
    }

    if (pendingPayment.status === 'collecting' || !pendingPayment.orderId) {
      if (hasPhysicalCash) {
        setIsCancellingPayment(true)
        const auditResponse = await apiClient.post<CancelPaymentApiResponse>(
          '/api/v1/pos/payments/temporary-cash/cancel',
          {
            clientOrderId: pendingPayment.clientOrderId,
            pendingCashAmount: pendingPayment.pendingCashAmount,
            returnedAmount: pendingPayment.pendingCashAmount,
            cashReturnedConfirmed,
            reason: 'Thu ngân hủy thanh toán tạm',
            requestKey: cashReturnConfirmation?.requestKey,
          }
        )
        setIsCancellingPayment(false)
        if (!auditResponse.ok || !auditResponse.data?.success) {
          showMessage(auditResponse.data?.message || auditResponse.error || 'Không thể ghi nhận hoàn tiền tạm.')
          return
        }
      }
      setPendingPayment(null)
      setCashReturnConfirmation(null)
      setIsCancellingPayment(false)
      showMessage(hasPhysicalCash
        ? 'Đã xác nhận hoàn tiền và hủy thanh toán tạm.'
        : 'Đã hủy thanh toán tạm.')
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
        cashReturnedConfirmed,
        keepTemporaryCash: reason === 'timeout' && hasPhysicalCash,
        returnedAmount: cashReturnedConfirmed ? pendingPayment.pendingCashAmount : 0,
        requestKey: cashReturnedConfirmed ? cashReturnConfirmation?.requestKey : undefined,
      }
    )

    const cancelledCashAmount = pendingPayment.pendingCashAmount
    setIsCancellingPayment(false)

    if (response.ok && response.data?.code === 'ALREADY_PAID') {
      setCashReturnConfirmation(null)
      setPendingPayment(null)
      clearCart()
      showMessage('Thanh toán VietQR thành công. Lệnh in đã gửi tới Print Bridge.')
      return
    }

    if (response.ok && response.data?.success) {
      setCashReturnConfirmation(null)
      if (reason === 'timeout' && cancelledCashAmount > 0) {
        setPendingPayment({
          ...pendingPayment,
          clientOrderId: crypto.randomUUID(),
          status: 'collecting',
          orderId: undefined,
          checkoutUrl: undefined,
          qrCode: null,
          expiresAt: undefined,
        })
        showMessage('VietQR đã hết hạn. Tiền mặt vẫn đang tạm giữ; chọn phương thức khác hoặc hủy và hoàn tiền.')
        return
      }
      setPendingPayment(null)
      showMessage(reason === 'timeout'
        ? 'Giao dịch VietQR đã hết hạn.'
        : cancelledCashAmount > 0
          ? 'Đã xác nhận hoàn tiền và hủy giao dịch VietQR.'
          : 'Đã hủy giao dịch VietQR.')
      return
    }

    showMessage(response.data?.message || response.error || 'Không thể hủy giao dịch VietQR.')
  }, [cashReturnConfirmation?.requestKey, clearCart, isCancellingPayment, pendingPayment, showMessage])

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
      storeMenuItemId: ci.storeMenuItemId,
      drinkSizeId: ci.drinkSizeId,
      name: ci.name,
      sizeId: ci.sizeId ?? null,
      quantity: ci.quantity,
      unitPrice: ci.price,
      effectivePrice: ci.acceptedBasePrice,
      priceSource: ci.priceSource,
      catalogVersion: ci.catalogVersion,
      note: ci.note,
      toppings: ci.selectedToppings.map((topping) => ({
        toppingId: topping.id,
        name: topping.name,
        acceptedPrice: topping.acceptedPrice ?? topping.price,
      })),
    }))

  const buildOfflineCartSnapshot = (items: CartItem[]) =>
    items.map((ci) => ({
      cartId: ci.cartId,
      menuItemId: ci.id,
      storeMenuItemId: ci.storeMenuItemId,
      drinkSizeId: ci.drinkSizeId,
      name: ci.name,
      categoryId: ci.categoryId,
      sizeId: ci.sizeId ?? null,
      sizeName: ci.sizeName,
      quantity: ci.quantity,
      unitPrice: ci.price,
      effectivePrice: ci.acceptedBasePrice,
      priceSource: ci.priceSource,
      catalogVersion: ci.catalogVersion,
      note: ci.note,
      detailText: ci.detailText,
      toppings: ci.selectedToppings.map((topping) => ({
        toppingId: topping.id,
        name: topping.name,
        price: topping.acceptedPrice ?? topping.price,
        acceptedPrice: topping.acceptedPrice ?? topping.price,
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
      storeMenuItemId: ci.storeMenuItemId,
      drinkSizeId: ci.drinkSizeId,
      acceptedBasePrice: ci.acceptedBasePrice,
      acceptedUnitPrice: ci.price,
      priceSource: ci.priceSource,
      catalogVersion: ci.catalogVersion,
      quantity: ci.quantity,
      note: ci.note,
      toppings: ci.selectedToppings.map((topping) => ({
        toppingId: topping.id,
        name: topping.name,
        acceptedPrice: topping.acceptedPrice ?? topping.price,
      })),
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
    if (hasStaleCart) {
      showMessage('Giỏ hàng cần được cập nhật theo catalog mới trước khi thanh toán.')
      return
    }
    if (!hasOpenShift) {
      showMessage('Bạn cần mở ca làm việc trước khi thanh toán.')
      return
    }
    if (!isOnline || !navigator.onLine) {
      showMessage('Không hỗ trợ tách thanh toán khi offline.')
      return
    }
    if (pendingCashForCart <= 0) {
      showMessage('Tiền mặt tạm phải lớn hơn 0.')
      return
    }
    if (pendingCashValidation) {
      showMessage(pendingCashValidation)
      return
    }
    if (pendingCashForCart >= totalAmount) {
      openCashPaymentConfirmation()
      return
    }

    setPendingPayment({
      clientOrderId: getClientOrderId(),
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
        pendingPayment.pendingCashAmount,
        pendingPayment.clientOrderId
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
        pendingPayment.totalAmount,
        pendingPayment.clientOrderId
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
    if (hasStaleCart) {
      showMessage('Giỏ hàng cần được cập nhật theo catalog mới trước khi thanh toán.')
      return
    }
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
    const receivedCashError = validateCashVnd(receivedCashAmount)
    if (receivedCashError) {
      showMessage(receivedCashError)
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
    if (hasStaleCart) {
      showMessage('Giỏ hàng cần được cập nhật theo catalog mới trước khi thanh toán.')
      return
    }
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
      if (pendingCashValidation) {
        showMessage(pendingCashValidation)
        return
      }
      beginSplitCashFlow()
      return
    }
    if (parsedPendingCash >= totalAmount) {
      openCashPaymentConfirmation()
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
            clientOrderId,
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
    <div className="pos-shell font-sans select-none" id="pos-main-content">
      <SellingHeader
        orderType={orderType}
        searchQuery={searchQuery}
        resultCount={filteredItems.length}
        isCartLocked={isCartLocked}
        hasOpenShift={hasOpenShift}
        shiftId={shift?.shiftId}
        session={session}
        onOrderTypeChange={changeOrderType}
        onSearchChange={setSearchQuery}
      />

      <aside className="pos-category-panel bg-surface-white flex flex-col border-r border-border" aria-label="Danh mục sản phẩm">

        <nav className="pos-category-list flex-1 flex flex-col gap-1 px-3 py-2 overflow-y-auto" aria-label="Danh mục sản phẩm">
          <button
            type="button"
            onClick={() => setSelectedCategory(null)}
            aria-pressed={selectedCategoryId === null}
            className={`pos-touch-target flex items-center justify-between px-3 py-2.5 rounded-lg text-xs font-semibold transition-colors cursor-pointer ${
              selectedCategoryId === null
                ? 'bg-brand-orange text-white'
                : 'text-text-secondary hover:bg-surface-hover border border-transparent'
            }`}
          >
            <span className="flex items-center gap-2"><span aria-hidden="true">☕</span><span>Tất cả</span></span>
            <span className="tabular-nums">{menuItems.length}</span>
          </button>
          {categories.map((cat) => (
            <button
              key={cat.id}
              onClick={() => setSelectedCategory(cat.id)}
              aria-pressed={selectedCategoryId === cat.id}
              className={`pos-touch-target flex items-center justify-between px-3 py-2.5 rounded-lg text-xs font-semibold transition-colors cursor-pointer ${
                selectedCategoryId === cat.id
                  ? 'bg-brand-orange text-white border border-brand-orange'
                  : 'text-text-secondary hover:bg-surface-hover border border-transparent'
              }`}
            >
              <span className="flex items-center gap-2 min-w-0">
                <span className="text-base">{cat.icon || '•'}</span>
                <span className="truncate">{cat.name}</span>
              </span>
              <span className={`text-[10px] font-bold px-1.5 py-0.5 rounded-full ${
                selectedCategoryId === cat.id
                  ? 'bg-white/20 text-white'
                  : 'bg-surface text-text-muted'
              }`}>
                {cat.count}
              </span>
            </button>
          ))}
        </nav>
      </aside>

      <main className="pos-catalog-panel flex flex-col bg-surface">
        <div className="flex-1 min-h-0 overflow-y-auto p-3 md:p-4">
          {isLoading ? (
            <div className="h-full flex items-center justify-center text-xs font-semibold text-text-muted">
              Đang tải menu...
            </div>
          ) : catalogError && categories.length === 0 && menuItems.length === 0 ? (
            <div className="h-full flex flex-col items-center justify-center gap-3 px-6 text-center">
              <p className="text-sm font-bold text-danger">Không tải được menu cửa hàng</p>
              <p className="max-w-md text-xs text-text-secondary">{catalogError}</p>
              <button
                type="button"
                onClick={() => void refreshCatalog()}
                className="px-4 py-2 rounded-lg bg-brand-orange text-white text-xs font-bold hover:bg-brand-orange-hover transition-colors"
              >
                Thử tải lại
              </button>
            </div>
          ) : filteredItems.length === 0 ? (
            <div className="h-full flex items-center justify-center text-xs font-semibold text-text-muted">
              {searchQuery ? 'Không tìm thấy món phù hợp' : 'Không có sản phẩm trong danh mục này'}
            </div>
          ) : (
            <div className="pos-product-grid">
              {filteredItems.map((item) => {
                const qtyInCart = getQuantityInCart(item.id)
                const isUnavailable = item.isAvailable === false
                const isProductLocked = isCartLocked || isUnavailable
                const unavailableReason = isUnavailable ? getUnavailableReason(item) : ''
                const requiresOptions = requiresProductOptions(item)
                return (
                  <article
                    key={item.id}
                    onClick={() => handleProductSelection(item)}
                    onKeyDown={(event) => {
                      if (event.target !== event.currentTarget) return

                      if (event.key === 'Enter' || event.key === ' ') {
                        event.preventDefault()
                        handleProductSelection(item)
                      }
                    }}
                    role="button"
                    tabIndex={isProductLocked ? -1 : 0}
                    aria-disabled={isProductLocked}
                    aria-label={`${item.name}, ${formatVND(item.price)}${
                      isUnavailable
                        ? `, ${unavailableReason}`
                        : requiresOptions ? ', chạm để chọn tùy chọn' : ', chạm để thêm nhanh'
                    }`}
                    className={`pos-product-card relative bg-surface-card rounded-xl border border-border p-3 flex flex-col select-none shadow-[var(--shadow-card)] transition-[border-color,box-shadow,transform] duration-150 ${
                      isProductLocked
                        ? 'opacity-65 cursor-not-allowed'
                        : 'cursor-pointer active:scale-[0.985]'
                    }`}
                  >
                    {qtyInCart > 0 && (
                      <span className="absolute -top-1.5 -left-1.5 w-5 h-5 bg-brand-orange text-white text-[10px] font-extrabold rounded-full flex items-center justify-center shadow-sm z-10">
                        {qtyInCart}
                      </span>
                    )}

                    {isUnavailable && (
                      <span
                        className="absolute top-2 left-2 max-w-[calc(100%-4rem)] truncate px-2 py-1 bg-white/95 border border-warning/30 text-warning text-xs font-bold rounded-md z-10"
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
                        setActiveModifier({ item })
                      }}
                      disabled={isProductLocked}
                      className="pos-touch-target absolute top-1 right-1 px-2 bg-white/95 border border-brand-orange-border text-brand-orange text-xs font-bold rounded-lg hover:bg-brand-orange-light transition-colors cursor-pointer disabled:cursor-not-allowed disabled:bg-surface disabled:border-border disabled:text-text-muted z-10"
                      aria-label={`Tùy chỉnh ${item.name}`}
                    >
                      Tùy chỉnh
                    </button>

                    <ProductImage
                      src={item.image}
                      name={item.name}
                      fallbackIcon={currentCategory?.icon || '•'}
                    />

                    <span className="mt-3 min-h-10 text-base font-bold text-text-primary leading-5 line-clamp-2">
                      {item.name}
                    </span>
                    <span className="mt-1 text-base font-extrabold text-brand-orange tabular-nums">
                      {formatVND(item.price)}
                    </span>
                    {isUnavailable && (
                      <span className="mt-1 line-clamp-2 min-h-9 text-sm font-semibold leading-4 text-danger">
                        {unavailableReason}
                      </span>
                    )}
                    <div className="mt-auto pt-2 text-sm text-text-secondary font-semibold">
                      {isUnavailable
                        ? 'Không thể bán tại lúc này'
                        : requiresOptions ? 'Chạm để chọn tùy chọn' : 'Chạm để thêm nhanh'}
                    </div>
                  </article>
                )
              })}
            </div>
          )}
        </div>
      </main>

      {isCartOpen && (
        <button
          type="button"
          className="pos-cart-backdrop"
          onClick={() => setIsCartOpen(false)}
          aria-label="Đóng giỏ hàng"
        />
      )}

      <aside id="pos-cart-panel" className="pos-cart-panel bg-surface-white flex flex-col border-l border-border" data-open={isCartOpen} aria-label="Giỏ hàng">
        <div className="min-h-16 flex items-center justify-between px-4 py-2 border-b border-border">
          <div className="flex items-center gap-2">
            <span className="text-lg" aria-hidden="true">▣</span>
            <div>
              <h2 className="text-base font-extrabold text-text-primary">Giỏ hàng</h2>
              <p className="text-xs text-text-secondary">{orderType === 'dine-in' ? 'Tại quán' : 'Mang đi'}</p>
            </div>
          </div>
          <div className="flex items-center gap-2">
            {totalItems > 0 && (
              <span className="bg-brand-orange-light text-brand-orange text-xs font-bold px-2 py-1 rounded-md tabular-nums">
                {totalItems} món
              </span>
            )}
            {cart.length > 0 && (
              <button
                onClick={confirmClearCart}
                disabled={isCartLocked}
                className="pos-touch-target text-xs font-bold text-danger px-2 rounded-lg hover:bg-[var(--pos-danger-soft)] transition-colors cursor-pointer disabled:opacity-40 disabled:cursor-not-allowed"
              >
                Xóa giỏ
              </button>
            )}
            <button
              type="button"
              onClick={() => setIsCartOpen(false)}
              className="pos-cart-close pos-touch-target items-center justify-center rounded-lg border border-border text-xl text-text-secondary"
              aria-label="Đóng giỏ hàng"
            >
              ×
            </button>
          </div>
        </div>

        {hasStaleCart && !isCartLocked && (
          <div className="mx-4 mt-3 rounded-lg border border-amber-300 bg-amber-50 px-3 py-2 text-amber-900">
            <p className="text-[11px] font-extrabold">Catalog hoặc giá bán đã thay đổi</p>
            <p className="mt-0.5 text-[10px] font-semibold leading-4">
              Có {staleCartItems.length} món cần kiểm tra lại trước khi thanh toán.
            </p>
            <button
              type="button"
              onClick={refreshStaleCart}
              className="mt-2 rounded-md border border-amber-400 bg-white px-2.5 py-1.5 text-[10px] font-extrabold text-amber-900 hover:bg-amber-100 transition-colors cursor-pointer"
            >
              Cập nhật và xác nhận lại
            </button>
          </div>
        )}

        <div className="pos-cart-scroll flex-1 min-h-0 overflow-y-auto px-3 py-3">
          {cart.length === 0 ? (
            <div className="flex flex-col items-center justify-center h-full text-text-muted">
              <span className="text-4xl mb-3 opacity-20">🛒</span>
              <p className="text-sm font-medium">Chưa có sản phẩm</p>
              <p className="text-sm mt-1 opacity-70">Chọn món từ danh sách để bắt đầu đơn hàng.</p>
            </div>
          ) : (
            <div className="flex flex-col gap-2">
              {cart.map((item, index) => (
                <CartLine
                  key={item.cartId}
                  index={index + 1}
                  name={item.name}
                  optionSummary={item.optionSummary}
                  customerNote={item.customerNote}
                  quantity={item.quantity}
                  lineTotal={formatVND(item.price * item.quantity)}
                  locked={isCartLocked}
                  onDecrease={() => decreaseFromCart(item.cartId)}
                  onIncrease={() => {
                    if (isCartLocked) {
                      showMessage('Đang thanh toán, hãy hủy giao dịch trước khi sửa giỏ.')
                      return
                    }
                    setCart((previous) => previous.map((line) =>
                      line.cartId === item.cartId ? { ...line, quantity: line.quantity + 1 } : line
                    ))
                  }}
                  onEdit={() => editCartLine(item)}
                  onRemove={() => removeFromCart(item.cartId)}
                />
              ))}
            </div>
          )}
        </div>

        <div className="pos-cart-footer shrink-0 border-t border-border bg-white p-4 space-y-3">
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
            <div className="flex justify-between text-xl font-extrabold text-text-primary">
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
                  type="text"
                  inputMode="numeric"
                  value={formatCashInput(pendingCashInput)}
                  onChange={(event) => setPendingCashInput(event.target.value.replace(/\D/g, ''))}
                  className="min-w-0 flex-1 rounded-lg border border-border bg-white px-3 py-2 text-xs font-bold text-text-primary outline-none focus:border-brand-orange"
                  placeholder="0"
                />
                <span className="text-[11px] font-extrabold text-text-secondary">VNĐ</span>
              </div>
              {pendingCashValidation && (
                <p className="text-[11px] font-bold text-danger">{pendingCashValidation}</p>
              )}
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
                  disabled={isCheckingOut || !hasOpenShift || !isOnline || parsedPendingCash <= 0 || pendingCashValidation !== null}
                  className="w-full rounded-lg border border-brand-orange-border bg-brand-orange-light px-3 py-2 text-xs font-extrabold text-brand-orange hover:bg-brand-orange hover:text-white disabled:opacity-40 disabled:cursor-not-allowed transition-colors cursor-pointer"
                >
                  {parsedPendingCash >= totalAmount ? 'Xác nhận tiền mặt' : 'Ghi nhận tạm'}
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
              disabled={cart.length === 0 || isCheckingOut || !hasOpenShift || hasPendingPayment || hasStaleCart || cashConfirmation !== null}
              className="min-h-14 flex-1 py-3 rounded-xl bg-brand-orange text-white font-bold text-base shadow-[var(--shadow-button)] hover:bg-brand-orange-hover active:scale-[0.98] transition-all duration-150 cursor-pointer disabled:opacity-40 disabled:cursor-not-allowed"
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
              disabled={cart.length === 0 || isCheckingOut || !hasOpenShift || hasPendingPayment || hasStaleCart || !isOnline || cashConfirmation !== null}
              className="min-h-14 flex-1 py-3 rounded-xl bg-text-primary text-white font-bold text-base hover:opacity-90 active:scale-[0.98] transition-all duration-150 cursor-pointer disabled:opacity-40 disabled:cursor-not-allowed"
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

      <div className="pos-mobile-cart-bar" role="region" aria-label="Tóm tắt giỏ hàng">
        <div className="min-w-0">
          <p className="text-xs font-semibold text-text-secondary">{totalItems} món trong giỏ</p>
          <p className="truncate text-lg font-extrabold text-brand-orange tabular-nums">{formatVND(totalAmount)}</p>
        </div>
        <button
          type="button"
          onClick={() => setIsCartOpen(true)}
          className="pos-touch-target min-w-28 rounded-lg bg-brand-orange px-4 text-sm font-bold text-white shadow-[var(--shadow-button)]"
          aria-expanded={isCartOpen}
          aria-controls="pos-cart-panel"
        >
          Xem giỏ
        </button>
      </div>

      {checkoutMessage && (
        <div role="status" aria-live="polite" className="absolute bottom-24 md:bottom-6 left-1/2 -translate-x-1/2 bg-text-primary text-white font-bold text-sm py-3.5 px-6 rounded-xl shadow-lg z-[70]">
          {checkoutMessage}
        </div>
      )}

      {cashConfirmation && (
        <div className="pos-dialog-backdrop fixed inset-0 z-[60] flex items-center justify-center">
          <div
            role="dialog"
            aria-modal="true"
            aria-labelledby="cash-payment-title"
            className="pos-adaptive-dialog w-full max-w-md rounded-2xl bg-white shadow-2xl border border-border overflow-hidden flex flex-col"
          >
            <div className="px-5 py-4 border-b border-border">
              <h3 id="cash-payment-title" className="text-base font-extrabold text-text-primary">
                Xác nhận thanh toán tiền mặt
              </h3>
            </div>
            <div className="min-h-0 overflow-y-auto p-5 space-y-4">
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
                    type="text"
                    inputMode="numeric"
                    value={formatCashInput(cashConfirmation.receivedAmountInput)}
                    onChange={(event) => updateCashReceivedInput(event.target.value)}
                    className="min-h-14 min-w-0 flex-1 rounded-xl border border-border bg-white px-3 py-3 text-xl font-extrabold text-text-primary outline-none focus:border-brand-orange tabular-nums"
                    autoFocus
                  />
                  <span className="text-xs font-extrabold text-text-secondary">VNĐ</span>
                </div>
                {cashConfirmationReceivedAmount < cashConfirmation.totalAmount && (
                  <p className="text-xs font-bold text-danger">
                    Tiền khách đưa chưa đủ để thanh toán.
                  </p>
                )}
                {cashConfirmationReceivedAmount >= cashConfirmation.totalAmount
                  && validateCashVnd(cashConfirmationReceivedAmount) !== null && (
                  <p className="text-xs font-bold text-danger">
                    Số tiền mặt phải là bội số của 1.000đ.
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
                    className="pos-touch-target rounded-lg border border-border bg-surface px-3 py-2 text-sm font-extrabold text-text-secondary hover:border-brand-orange-border hover:text-brand-orange transition-colors cursor-pointer"
                  >
                    {formatVND(amount)}
                  </button>
                ))}
              </div>

              <div className="grid grid-cols-3 gap-2" aria-label="Bàn phím nhập tiền">
                {['1', '2', '3', '4', '5', '6', '7', '8', '9'].map((digit) => (
                  <button
                    key={digit}
                    type="button"
                    onClick={() => appendCashDigit(digit)}
                    className="pos-touch-target min-h-12 rounded-lg border border-border bg-white text-lg font-extrabold text-text-primary active:bg-brand-orange-light"
                  >
                    {digit}
                  </button>
                ))}
                <button
                  type="button"
                  onClick={() => updateCashReceivedInput('')}
                  className="pos-touch-target min-h-12 rounded-lg border border-danger/30 bg-[var(--pos-danger-soft)] text-sm font-bold text-danger"
                >
                  Xóa
                </button>
                <button
                  type="button"
                  onClick={() => appendCashDigit('0')}
                  className="pos-touch-target min-h-12 rounded-lg border border-border bg-white text-lg font-extrabold text-text-primary active:bg-brand-orange-light"
                >
                  0
                </button>
                <button
                  type="button"
                  onClick={() => updateCashReceivedInput(cashConfirmation.receivedAmountInput.slice(0, -1))}
                  className="pos-touch-target min-h-12 rounded-lg border border-border bg-surface text-lg font-bold text-text-secondary"
                  aria-label="Xóa chữ số cuối"
                >
                  ⌫
                </button>
              </div>
            </div>
            <div className="grid grid-cols-2 gap-2 px-5 py-4 bg-surface border-t border-border">
              <button
                type="button"
                onClick={cancelCashPaymentConfirmation}
                disabled={isCheckingOut}
                className="min-h-14 rounded-xl border border-border bg-white px-4 py-3 text-base font-extrabold text-text-secondary hover:bg-surface-hover disabled:opacity-40 disabled:cursor-not-allowed transition-colors cursor-pointer"
              >
                Hủy
              </button>
              <button
                type="button"
                onClick={() => void confirmCashPayment()}
                disabled={!canConfirmCashPayment}
                className="min-h-14 rounded-xl bg-brand-orange px-4 py-3 text-base font-extrabold text-white hover:bg-brand-orange-hover disabled:opacity-40 disabled:cursor-not-allowed transition-colors cursor-pointer"
              >
                {isCheckingOut ? 'Đang xử lý' : 'Xác nhận'}
              </button>
            </div>
          </div>
        </div>
      )}

      {cashReturnConfirmation && pendingPayment && (
        <div className="pos-dialog-backdrop fixed inset-0 z-[70] flex items-center justify-center">
          <div
            role="alertdialog"
            aria-modal="true"
            aria-labelledby="cash-return-title"
            className="pos-adaptive-dialog w-full max-w-md rounded-xl border border-border bg-white shadow-2xl overflow-hidden"
          >
            <div className="border-b border-border px-5 py-4">
              <h3 id="cash-return-title" className="text-base font-extrabold text-text-primary">
                Xác nhận hoàn tiền mặt
              </h3>
            </div>
            <div className="space-y-3 p-5">
              <p className="text-sm font-bold text-text-primary">
                Bạn đã nhận {formatVND(pendingPayment.pendingCashAmount)} từ khách.
              </p>
              <p className="text-sm font-semibold text-text-secondary">
                Hãy hoàn lại đủ tiền trước khi hủy. Giao dịch chỉ được hủy sau khi bạn xác nhận đã trả tiền cho khách.
              </p>
            </div>
            <div className="grid grid-cols-2 gap-2 border-t border-border bg-surface px-5 py-4">
              <button
                type="button"
                onClick={() => setCashReturnConfirmation(null)}
                disabled={isCancellingPayment}
                className="min-h-12 rounded-lg border border-border bg-white px-3 text-sm font-extrabold text-text-secondary disabled:opacity-40"
              >
                Quay lại thanh toán
              </button>
              <button
                type="button"
                onClick={() => void cancelPendingPayment(cashReturnConfirmation.reason, true)}
                disabled={isCancellingPayment}
                className="min-h-12 rounded-lg bg-danger px-3 text-sm font-extrabold text-white disabled:opacity-40"
              >
                {isCancellingPayment ? 'Đang xác nhận' : 'Đã hoàn tiền cho khách'}
              </button>
            </div>
          </div>
        </div>
      )}

      {pendingPayment?.status === 'awaiting-vietqr' && pendingPayment.checkoutUrl && pendingPayment.orderId && (
        <div className="pos-dialog-backdrop fixed inset-0 z-[60] flex items-center justify-center">
          <div
            role="dialog"
            aria-modal="true"
            aria-labelledby="vietqr-payment-title"
            className="pos-adaptive-dialog w-full max-w-[520px] rounded-xl bg-surface-white border border-border shadow-2xl overflow-hidden flex flex-col"
          >
            <div className="px-5 py-4 border-b border-border flex items-center justify-between gap-3">
              <div className="min-w-0">
                <p id="vietqr-payment-title" className="text-base font-extrabold text-text-primary">Thanh toán VietQR</p>
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
                  className="pos-touch-target rounded-lg border border-border text-text-secondary hover:border-danger/40 hover:bg-[var(--pos-danger-soft)] hover:text-danger transition-colors cursor-pointer"
                  aria-label="Đóng modal thanh toán"
                  title="Đóng modal"
                >
                  ×
                </button>
              </div>
            </div>

            <div className="min-h-0 overflow-y-auto p-5 space-y-4">
              <div className="flex items-center justify-between rounded-lg border border-brand-orange-border bg-brand-orange-light px-3 py-2">
                <span className="text-xs font-bold text-brand-orange">Thời gian giữ giao dịch</span>
                <span className="text-sm font-extrabold text-brand-orange tabular-nums">
                  {formatCountdown(paymentRemainingSeconds)}
                </span>
              </div>

              <div className="min-h-[300px] h-[min(48dvh,460px)] rounded-lg border border-border bg-surface overflow-hidden">
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
        key={activeModifier?.editingCartId ?? activeModifier?.item.id ?? 'closed'}
        isOpen={activeModifier !== null}
        onClose={closeModifierModal}
        menuItem={activeModifier?.item ?? null}
        initialSelection={activeModifier?.initialSelection}
        mode={activeModifier?.editingCartId ? 'edit' : 'add'}
        onConfirm={(selection) => {
          if (isCartLocked) {
            showMessage('Đang thanh toán, hãy hủy giao dịch trước khi sửa giỏ.')
            setActiveModifier(null)
            return
          }

          if (activeModifier) {
            applyModifierSelection(activeModifier.item, selection, activeModifier.editingCartId)
          }
          setActiveModifier(null)
        }}
      />
    </div>
  )
}
