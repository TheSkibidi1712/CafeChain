import { useEffect, useRef, useState } from 'react'
import type { MenuItem, MenuItemSize, ToppingOption } from '../db/CafeChainPOSDB'

export type { MenuItem, ToppingOption }

export interface ModifierSelection {
  size: MenuItemSize | null
  ice: '0%' | '50%' | '100%'
  sugar: '0%' | '50%' | '100%'
  selectedToppings: ToppingOption[]
  quantity: number
  customerNote: string
  totalPrice: number
  note: string
}

interface ProductModifierModalProps {
  isOpen: boolean
  onClose: () => void
  menuItem: MenuItem | null
  onConfirm: (selection: ModifierSelection) => void
  initialSelection?: ModifierSelection | null
  mode?: 'add' | 'edit'
}

const formatVND = (amount: number): string =>
  new Intl.NumberFormat('vi-VN').format(amount) + 'đ'

export default function ProductModifierModal({
  isOpen,
  onClose,
  menuItem,
  onConfirm,
  initialSelection = null,
  mode = 'add',
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

  const initialSize = initialSelection?.size
    ?? menuItem?.sizes?.find((item) => item.isAvailable)
    ?? menuItem?.sizes?.[0]
    ?? null
  const [size, setSize] = useState<MenuItemSize | null>(initialSize)
  const [ice, setIce] = useState<'0%' | '50%' | '100%'>(initialSelection?.ice ?? '100%')
  const [sugar, setSugar] = useState<'0%' | '50%' | '100%'>(initialSelection?.sugar ?? '100%')
  const [selectedToppings, setSelectedToppings] = useState<ToppingOption[]>(
    () => initialSelection?.selectedToppings ?? buildDefaultToppings(initialSize)
  )
  const [quantity, setQuantity] = useState(initialSelection?.quantity ?? 1)
  const [customerNote, setCustomerNote] = useState(initialSelection?.customerNote ?? '')

  useEffect(() => {
    if (!isOpen) return

    const previousFocus = document.activeElement as HTMLElement | null
    const focusableSelector = 'button:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
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
    setSelectedToppings((previous) => {
      const existing = previous.find((item) => item.id === topping.id)
      if (existing?.isRequired) return previous
      if (existing) return previous.filter((item) => item.id !== topping.id)

      const policy = size?.toppingPolicies?.find((item) => item.toppingId === topping.id)
      return [...previous, {
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
  const normalizedCustomerNote = customerNote.trim()
  const note = [
    `Đá ${ice}, Đường ${sugar}`,
    normalizedCustomerNote,
  ].filter(Boolean).join('. ')

  const handleConfirm = () => {
    if (!size?.isAvailable) return
    onConfirm({
      size,
      ice,
      sugar,
      selectedToppings,
      quantity,
      customerNote: normalizedCustomerNote,
      totalPrice,
      note,
    })
    onClose()
  }

  return (
    <div className="pos-option-sheet-backdrop fixed inset-0 z-50 flex select-none">
      <button
        type="button"
        className="absolute inset-0 cursor-default"
        onClick={onClose}
        aria-label="Đóng tùy chọn món"
      />

      <div
        ref={dialogRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby="modifier-dialog-title"
        className="pos-option-sheet relative ml-auto flex h-full w-full flex-col overflow-hidden border-l border-border bg-surface-white shadow-xl"
      >
        <div className="flex items-center justify-between border-b border-border bg-surface-white px-5 py-4">
          <div className="min-w-0">
            <p className="text-xs font-bold uppercase text-text-secondary">
              {mode === 'edit' ? 'Chỉnh sửa món' : 'Tùy chọn món'}
            </p>
            <h2 id="modifier-dialog-title" className="truncate text-lg font-extrabold text-text-primary">
              {menuItem.name}
            </h2>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="pos-touch-target flex items-center justify-center rounded-lg border border-border text-xl text-text-secondary hover:bg-surface-hover"
            aria-label="Đóng"
          >
            ×
          </button>
        </div>

        <div className="pos-option-sheet-body flex-1 min-h-0 overflow-y-auto bg-surface-white p-5">
          <div className="space-y-5">
            {sizes.length > 0 && (
              <section className="space-y-2" aria-labelledby="modifier-size-label">
                <h3 id="modifier-size-label" className="text-sm font-bold text-text-secondary">Kích cỡ</h3>
                <div className="grid grid-cols-3 gap-2.5">
                  {sizes.map((option) => (
                    <button
                      key={option.sizeId}
                      type="button"
                      onClick={() => handleSizeChange(option)}
                      disabled={!option.isAvailable}
                      aria-pressed={size?.sizeId === option.sizeId}
                      title={option.isAvailable ? option.sizeName : (option.availabilityReason ?? 'Tạm hết hàng')}
                      className={`min-h-14 rounded-lg border px-2 py-2.5 text-sm font-bold transition-colors ${
                        !option.isAvailable
                          ? 'cursor-not-allowed border-border bg-surface-muted text-text-muted opacity-60'
                          : size?.sizeId === option.sizeId
                            ? 'border-brand-orange bg-brand-orange text-white'
                            : 'cursor-pointer border-border bg-surface text-text-primary hover:border-brand-orange-border hover:bg-brand-orange-light'
                      }`}
                    >
                      {option.sizeName}
                      <span className="mt-0.5 block text-xs font-semibold tabular-nums">{formatVND(option.price)}</span>
                    </button>
                  ))}
                </div>
              </section>
            )}

            <div className="grid grid-cols-1 gap-5 sm:grid-cols-2">
              <OptionGroup label="Mức đá" value={ice} onChange={setIce} />
              <OptionGroup label="Mức đường" value={sugar} onChange={setSugar} />
            </div>

            {toppings.length > 0 && (
              <section className="space-y-2" aria-labelledby="modifier-topping-label">
                <h3 id="modifier-topping-label" className="text-sm font-bold text-text-secondary">Topping</h3>
                <div className="grid gap-2">
                  {toppings.map((topping) => {
                    const selected = selectedToppings.find((item) => item.id === topping.id)
                    const policy = size?.toppingPolicies?.find((item) => item.toppingId === topping.id)
                    const acceptedPrice = policy?.priceTreatment === 'INCLUDED_IN_BASE_PRICE'
                      ? 0
                      : topping.price * (policy?.quantityPerDrink ?? 1)
                    const isRequired = selected?.isRequired ?? policy?.isRequired ?? false
                    const isSelected = Boolean(selected)
                    return (
                      <button
                        key={topping.id}
                        type="button"
                        onClick={() => handleToppingToggle(topping)}
                        disabled={isRequired}
                        aria-pressed={isSelected}
                        className={`flex min-h-14 items-center justify-between gap-3 rounded-lg border px-3 py-2 text-left transition-colors ${
                          isSelected
                            ? 'border-brand-orange-border bg-brand-orange-light'
                            : 'cursor-pointer border-border bg-white hover:bg-surface-hover'
                        } ${isRequired ? 'cursor-default' : ''}`}
                      >
                        <span className="min-w-0">
                          <span className="block text-sm font-bold text-text-primary">{topping.name}</span>
                          <span className="block text-xs font-semibold text-text-secondary">
                            {isRequired ? 'Bắt buộc' : isSelected ? 'Đã chọn' : 'Chạm để chọn'}
                          </span>
                        </span>
                        <span className="shrink-0 text-sm font-extrabold text-brand-orange tabular-nums">
                          {acceptedPrice === 0 ? 'Đã gồm' : `+${formatVND(acceptedPrice)}`}
                        </span>
                      </button>
                    )
                  })}
                </div>
              </section>
            )}

            <section className="space-y-2" aria-labelledby="modifier-note-label">
              <div className="flex items-center justify-between gap-3">
                <h3 id="modifier-note-label" className="text-sm font-bold text-text-secondary">Ghi chú cho quầy pha chế</h3>
                <span className="text-xs font-semibold text-text-muted">{customerNote.length}/160</span>
              </div>
              <textarea
                value={customerNote}
                onChange={(event) => setCustomerNote(event.target.value)}
                maxLength={160}
                rows={3}
                placeholder="Ví dụ: ít ngọt, mang riêng topping"
                className="w-full resize-none rounded-lg border border-border bg-white px-3 py-3 text-base text-text-primary outline-none focus:border-brand-orange focus:ring-2 focus:ring-brand-orange/20"
              />
            </section>
          </div>
        </div>

        <div className="shrink-0 space-y-3 border-t border-border bg-surface p-4 pb-[max(16px,env(safe-area-inset-bottom))]">
          <div className="flex items-center justify-between gap-4">
            <div>
              <span className="block text-xs font-bold text-text-secondary">Số lượng</span>
              <div className="mt-1 flex items-center gap-2">
                <button
                  type="button"
                  onClick={() => setQuantity((current) => Math.max(1, current - 1))}
                  disabled={quantity <= 1}
                  className="pos-touch-target flex items-center justify-center rounded-lg border border-border bg-white text-lg font-bold text-text-secondary disabled:opacity-40"
                  aria-label="Giảm số lượng"
                >
                  −
                </button>
                <span className="w-10 text-center text-base font-extrabold text-text-primary tabular-nums" aria-label={`Số lượng ${quantity}`}>
                  {quantity}
                </span>
                <button
                  type="button"
                  onClick={() => setQuantity((current) => Math.min(99, current + 1))}
                  disabled={quantity >= 99}
                  className="pos-touch-target flex items-center justify-center rounded-lg bg-brand-orange text-lg font-bold text-white hover:bg-brand-orange-hover disabled:opacity-40"
                  aria-label="Tăng số lượng"
                >
                  +
                </button>
              </div>
            </div>
            <div className="text-right">
              <span className="block text-xs font-bold text-text-secondary">Tạm tính</span>
              <span className="text-xl font-extrabold text-brand-orange tabular-nums">{formatVND(totalPrice * quantity)}</span>
            </div>
          </div>

          <div className="grid grid-cols-[auto_minmax(0,1fr)] gap-2">
            <button
              type="button"
              onClick={onClose}
              className="min-h-14 rounded-lg border border-brand-orange px-4 text-sm font-bold text-brand-orange hover:bg-brand-orange-light"
            >
              Hủy
            </button>
            <button
              type="button"
              onClick={handleConfirm}
              disabled={!size?.isAvailable}
              className="min-h-14 rounded-lg bg-brand-orange px-5 text-sm font-bold text-white shadow-[var(--shadow-button)] hover:bg-brand-orange-hover disabled:cursor-not-allowed disabled:opacity-50"
            >
              {mode === 'edit' ? 'Cập nhật món' : `Thêm ${quantity} món`}
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}

interface OptionGroupProps {
  label: string
  value: '0%' | '50%' | '100%'
  onChange: (value: '0%' | '50%' | '100%') => void
}

function OptionGroup({ label, value, onChange }: OptionGroupProps) {
  return (
    <section className="space-y-2">
      <h3 className="text-sm font-bold text-text-secondary">{label}</h3>
      <div className="grid grid-cols-3 gap-2">
        {(['0%', '50%', '100%'] as const).map((option) => (
          <button
            key={option}
            type="button"
            onClick={() => onChange(option)}
            aria-pressed={value === option}
            className={`min-h-12 rounded-lg border px-2 py-2 text-sm font-bold transition-colors ${
              value === option
                ? 'border-brand-orange bg-brand-orange text-white'
                : 'cursor-pointer border-border bg-white text-text-primary hover:bg-brand-orange-light'
            }`}
          >
            {option}
          </button>
        ))}
      </div>
    </section>
  )
}
