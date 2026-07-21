import { useEffect, useRef, useState } from 'react'
import type { MenuItem, MenuItemSize, ToppingOption } from '../db/CafeChainPOSDB'

export type { MenuItem, ToppingOption }

export interface ModifierSelection {
  size: MenuItemSize | null
  ice: '0%' | '50%' | '100%'
  sugar: '0%' | '50%' | '100%'
  selectedToppings: ToppingOption[]
  totalPrice: number
  note: string
}

interface ProductModifierModalProps {
  isOpen: boolean
  onClose: () => void
  menuItem: MenuItem | null
  onConfirm: (selection: ModifierSelection) => void
}

const formatVND = (amount: number): string =>
  new Intl.NumberFormat('vi-VN').format(amount) + 'đ'

export default function ProductModifierModal({
  isOpen,
  onClose,
  menuItem,
  onConfirm,
}: ProductModifierModalProps) {
  const dialogRef = useRef<HTMLDivElement>(null)
  const buildDefaultToppings = (selectedSize: MenuItemSize | null) => {
    const policies = selectedSize?.toppingPolicies ?? []
    return (menuItem?.availableToppings ?? [])
      .filter((topping) => policies.some((policy) =>
        policy.toppingId === topping.id && (policy.isDefaultSelected || policy.isRequired)))
      .map((topping) => {
        const policy = policies.find((item) => item.toppingId === topping.id)
        const acceptedPrice = policy?.priceTreatment === 'INCLUDED_IN_BASE_PRICE'
          ? 0
          : topping.price * (policy?.quantityPerDrink ?? 1)
        return {
          ...topping,
          acceptedPrice,
          isRequired: policy?.isRequired ?? false,
          isDefaultSelected: policy?.isDefaultSelected ?? false,
          priceTreatment: policy?.priceTreatment,
          quantityPerDrink: policy?.quantityPerDrink ?? 1,
        }
      })
  }
  const initialSize = menuItem?.sizes?.find((item) => item.isAvailable) ?? menuItem?.sizes?.[0] ?? null
  const [size, setSize] = useState<MenuItemSize | null>(initialSize)
  const [ice, setIce] = useState<'0%' | '50%' | '100%'>('100%')
  const [sugar, setSugar] = useState<'0%' | '50%' | '100%'>('100%')
  const [selectedToppings, setSelectedToppings] = useState<ToppingOption[]>(
    () => buildDefaultToppings(initialSize)
  )

  useEffect(() => {
    if (!isOpen) return

    const previousFocus = document.activeElement as HTMLElement | null
    const focusableSelector = 'button:not([disabled]), input:not([disabled]), [tabindex]:not([tabindex="-1"])'
    const dialog = dialogRef.current
    dialog?.querySelector<HTMLElement>(focusableSelector)?.focus()

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.preventDefault()
        onClose()
        return
      }
      if (event.key !== 'Tab' || !dialog) return

      const items = Array.from(dialog.querySelectorAll<HTMLElement>(focusableSelector))
      if (items.length === 0) return
      const first = items[0]
      const last = items[items.length - 1]
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault()
        last.focus()
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault()
        first.focus()
      }
    }

    document.addEventListener('keydown', handleKeyDown)
    return () => {
      document.removeEventListener('keydown', handleKeyDown)
      previousFocus?.focus()
    }
  }, [isOpen, onClose])

  if (!isOpen || !menuItem) return null

  const sizes = menuItem.sizes ?? []
  const toppings = menuItem.availableToppings ?? []

  const handleToppingToggle = (topping: ToppingOption) => {
    setSelectedToppings((prev) => {
      const exists = prev.find((t) => t.id === topping.id)
      if (exists?.isRequired) return prev
      if (exists) return prev.filter((t) => t.id !== topping.id)

      const policy = size?.toppingPolicies?.find((item) => item.toppingId === topping.id)
      return [...prev, {
        ...topping,
        acceptedPrice: policy?.priceTreatment === 'INCLUDED_IN_BASE_PRICE'
          ? 0
          : topping.price * (policy?.quantityPerDrink ?? 1),
        isRequired: policy?.isRequired ?? false,
        isDefaultSelected: policy?.isDefaultSelected ?? false,
        priceTreatment: policy?.priceTreatment,
        quantityPerDrink: policy?.quantityPerDrink ?? 1,
      }]
    })
  }

  const handleSizeChange = (option: MenuItemSize) => {
    if (!option.isAvailable) return
    setSize(option)
    setSelectedToppings(buildDefaultToppings(option))
  }

  const basePrice = size?.price ?? menuItem.price
  const toppingsPrice = selectedToppings.reduce(
    (sum, topping) => sum + (topping.acceptedPrice ?? topping.price),
    0
  )
  const totalPrice = basePrice + toppingsPrice
  const note = `Đá ${ice}, Đường ${sugar}`

  const handleConfirm = () => {
    onConfirm({
      size,
      ice,
      sugar,
      selectedToppings,
      totalPrice,
      note,
    })
    onClose()
  }

  return (
    <div className="pos-dialog-backdrop fixed inset-0 z-50 flex items-center justify-center select-none">
      <div
        className="absolute inset-0"
        onClick={onClose}
        aria-hidden="true"
      />

      <div
        ref={dialogRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby="modifier-dialog-title"
        className="pos-adaptive-dialog relative bg-surface-white w-full max-w-xl rounded-2xl shadow-xl border border-border overflow-hidden flex flex-col"
      >
        <div className="px-5 py-4 border-b border-border flex justify-between items-center bg-surface-white">
          <div>
            <h2 id="modifier-dialog-title" className="text-lg font-extrabold text-text-primary">Tùy chỉnh món</h2>
            <p className="text-sm font-semibold text-brand-orange mt-0.5">{menuItem.name}</p>
          </div>
          <button
            onClick={onClose}
            className="pos-touch-target rounded-lg border border-border text-xl text-text-secondary hover:bg-surface-hover flex items-center justify-center cursor-pointer transition-colors"
            aria-label="Đóng"
          >
            ×
          </button>
        </div>

        <div className="p-5 overflow-y-auto space-y-5 flex-1 min-h-0 bg-surface-white">
          {sizes.length > 0 && (
            <div className="space-y-2">
              <label className="block text-sm font-bold text-text-secondary">
                Kích cỡ
              </label>
              <div className="grid grid-cols-3 gap-2.5">
                {sizes.map((option) => (
                  <button
                    key={option.sizeId}
                    type="button"
                    onClick={() => handleSizeChange(option)}
                    disabled={!option.isAvailable}
                    title={option.isAvailable ? option.sizeName : (option.availabilityReason ?? 'Tạm hết hàng')}
                    className={`min-h-14 py-2.5 rounded-xl text-sm font-bold border cursor-pointer transition-all ${
                      !option.isAvailable
                        ? 'bg-surface-muted border-border text-text-muted opacity-60 cursor-not-allowed'
                        :
                      size?.sizeId === option.sizeId
                        ? 'bg-brand-orange text-white border-brand-orange shadow-[var(--shadow-button)]'
                        : 'bg-surface border-border text-text-primary hover:bg-brand-orange-light hover:text-brand-orange hover:border-brand-orange-border'
                    }`}
                  >
                    {option.sizeName}
                    <span className="block text-xs font-semibold mt-0.5 tabular-nums">
                      {formatVND(option.price)}
                    </span>
                    {!option.isAvailable && (
                      <span className="block text-xs font-semibold mt-1">
                        {option.availabilityReason ?? 'Tạm hết hàng'}
                      </span>
                    )}
                  </button>
                ))}
              </div>
            </div>
          )}

          <div className="space-y-2">
            <label className="block text-sm font-bold text-text-secondary">
              Mức đá
            </label>
            <div className="grid grid-cols-3 gap-2.5">
              {(['0%', '50%', '100%'] as const).map((option) => (
                <button
                  key={option}
                  type="button"
                  onClick={() => setIce(option)}
                  className={`min-h-12 py-2.5 rounded-xl text-sm font-bold border cursor-pointer transition-all ${
                    ice === option
                      ? 'bg-brand-orange text-white border-brand-orange shadow-[var(--shadow-button)]'
                      : 'bg-surface border-border text-text-primary hover:bg-brand-orange-light hover:text-brand-orange hover:border-brand-orange-border'
                  }`}
                >
                  {option}
                </button>
              ))}
            </div>
          </div>

          <div className="space-y-2">
            <label className="block text-sm font-bold text-text-secondary">
              Mức đường
            </label>
            <div className="grid grid-cols-3 gap-2.5">
              {(['0%', '50%', '100%'] as const).map((option) => (
                <button
                  key={option}
                  type="button"
                  onClick={() => setSugar(option)}
                  className={`min-h-12 py-2.5 rounded-xl text-sm font-bold border cursor-pointer transition-all ${
                    sugar === option
                      ? 'bg-brand-orange text-white border-brand-orange shadow-[var(--shadow-button)]'
                      : 'bg-surface border-border text-text-primary hover:bg-brand-orange-light hover:text-brand-orange hover:border-brand-orange-border'
                  }`}
                >
                  {option}
                </button>
              ))}
            </div>
          </div>

          {toppings.length > 0 && (
            <div className="space-y-2">
              <label className="block text-sm font-bold text-text-secondary">
                Topping
              </label>
              <div className="divide-y divide-border border border-border rounded-xl overflow-hidden bg-surface">
                {toppings.map((topping) => {
                  const isSelected = !!selectedToppings.find((t) => t.id === topping.id)
                  const selected = selectedToppings.find((t) => t.id === topping.id)
                  const policy = size?.toppingPolicies?.find((item) => item.toppingId === topping.id)
                  const acceptedPrice = policy?.priceTreatment === 'INCLUDED_IN_BASE_PRICE'
                    ? 0
                    : topping.price * (policy?.quantityPerDrink ?? 1)
                  const isRequired = selected?.isRequired ?? policy?.isRequired ?? false
                  return (
                    <label
                      key={topping.id}
                      className={`min-h-14 flex items-center justify-between p-3 cursor-pointer transition-colors ${
                        isSelected ? 'bg-brand-orange-light' : 'hover:bg-surface-hover'
                      }`}
                    >
                      <div className="flex items-center gap-3">
                        <input
                          type="checkbox"
                          checked={isSelected}
                          onChange={() => handleToppingToggle(topping)}
                          disabled={isRequired}
                          className="w-5 h-5 rounded text-brand-orange accent-brand-orange focus:ring-brand-orange cursor-pointer"
                        />
                        <span className="text-sm font-semibold text-text-primary">
                          {topping.name}
                        </span>
                        {isRequired && (
                          <span className="text-xs font-bold text-brand-orange">Bắt buộc</span>
                        )}
                      </div>
                      <span className="text-sm font-bold text-brand-orange tabular-nums">
                        {acceptedPrice === 0 ? 'Đã gồm' : `+${formatVND(acceptedPrice)}`}
                      </span>
                    </label>
                  )
                })}
              </div>
            </div>
          )}
        </div>

        <div className="shrink-0 px-5 py-4 border-t border-border bg-surface flex items-center justify-between gap-4">
          <div className="flex flex-col">
            <span className="text-xs font-bold text-text-secondary">
              Tổng tạm tính
            </span>
            <span className="text-xl font-extrabold text-brand-orange tabular-nums">
              {formatVND(totalPrice)}
            </span>
          </div>

          <div className="flex gap-2">
            <button
              onClick={onClose}
              className="pos-touch-target px-4 py-2 rounded-lg border border-brand-orange text-brand-orange bg-surface-white text-sm font-bold cursor-pointer hover:bg-brand-orange-light active:scale-95 transition-all"
            >
              Hủy
            </button>
            <button
              onClick={handleConfirm}
              className="min-h-12 px-5 py-2 rounded-lg bg-brand-orange text-white text-sm font-bold cursor-pointer hover:bg-brand-orange-hover shadow-[var(--shadow-button)] active:scale-95 transition-all"
            >
              Xác nhận
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}
