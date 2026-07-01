import { useState } from 'react'
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
  const [size, setSize] = useState<MenuItemSize | null>(menuItem?.sizes?.[0] ?? null)
  const [ice, setIce] = useState<'0%' | '50%' | '100%'>('100%')
  const [sugar, setSugar] = useState<'0%' | '50%' | '100%'>('100%')
  const [selectedToppings, setSelectedToppings] = useState<ToppingOption[]>([])

  if (!isOpen || !menuItem) return null

  const sizes = menuItem.sizes ?? []
  const toppings = menuItem.availableToppings ?? []

  const handleToppingToggle = (topping: ToppingOption) => {
    setSelectedToppings((prev) => {
      const exists = prev.find((t) => t.id === topping.id)
      if (exists) return prev.filter((t) => t.id !== topping.id)
      return [...prev, topping]
    })
  }

  const basePrice = size?.price ?? menuItem.price
  const toppingsPrice = selectedToppings.reduce((sum, topping) => sum + topping.price, 0)
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
    <div className="fixed inset-0 z-50 flex items-center justify-center select-none">
      <div
        className="absolute inset-0 bg-black/40 backdrop-blur-sm transition-opacity"
        onClick={onClose}
      />

      <div className="relative bg-surface-white w-full max-w-md rounded-2xl shadow-xl border border-border overflow-hidden animate-in fade-in zoom-in-95 duration-200 flex flex-col max-h-[90vh]">
        <div className="px-6 py-4 border-b border-border flex justify-between items-center bg-surface-white">
          <div>
            <h2 className="text-sm font-bold text-text-primary">Tùy biến món nước</h2>
            <p className="text-[11px] font-semibold text-brand-orange mt-0.5">{menuItem.name}</p>
          </div>
          <button
            onClick={onClose}
            className="w-8 h-8 rounded-full border border-border text-text-secondary hover:bg-surface-hover flex items-center justify-center cursor-pointer transition-colors"
            aria-label="Đóng"
          >
            x
          </button>
        </div>

        <div className="p-6 overflow-y-auto space-y-5 flex-1 bg-surface-white">
          {sizes.length > 0 && (
            <div className="space-y-2">
              <label className="block text-[10px] font-bold text-text-secondary uppercase tracking-wider">
                Kích cỡ
              </label>
              <div className="grid grid-cols-3 gap-2.5">
                {sizes.map((option) => (
                  <button
                    key={option.sizeId}
                    type="button"
                    onClick={() => setSize(option)}
                    className={`py-2.5 rounded-xl text-xs font-bold border cursor-pointer transition-all ${
                      size?.sizeId === option.sizeId
                        ? 'bg-brand-orange text-white border-brand-orange shadow-[var(--shadow-button)]'
                        : 'bg-surface border-border text-text-primary hover:bg-brand-orange-light hover:text-brand-orange hover:border-brand-orange-border'
                    }`}
                  >
                    {option.sizeName}
                    <span className="block text-[10px] font-semibold mt-0.5">
                      {formatVND(option.price)}
                    </span>
                  </button>
                ))}
              </div>
            </div>
          )}

          <div className="space-y-2">
            <label className="block text-[10px] font-bold text-text-secondary uppercase tracking-wider">
              Mức đá
            </label>
            <div className="grid grid-cols-3 gap-2.5">
              {(['0%', '50%', '100%'] as const).map((option) => (
                <button
                  key={option}
                  type="button"
                  onClick={() => setIce(option)}
                  className={`py-2.5 rounded-xl text-xs font-bold border cursor-pointer transition-all ${
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
            <label className="block text-[10px] font-bold text-text-secondary uppercase tracking-wider">
              Mức đường
            </label>
            <div className="grid grid-cols-3 gap-2.5">
              {(['0%', '50%', '100%'] as const).map((option) => (
                <button
                  key={option}
                  type="button"
                  onClick={() => setSugar(option)}
                  className={`py-2.5 rounded-xl text-xs font-bold border cursor-pointer transition-all ${
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
              <label className="block text-[10px] font-bold text-text-secondary uppercase tracking-wider">
                Topping
              </label>
              <div className="divide-y divide-border border border-border rounded-xl overflow-hidden bg-surface">
                {toppings.map((topping) => {
                  const isSelected = !!selectedToppings.find((t) => t.id === topping.id)
                  return (
                    <label
                      key={topping.id}
                      className={`flex items-center justify-between p-3 cursor-pointer transition-colors ${
                        isSelected ? 'bg-brand-orange-light' : 'hover:bg-surface-hover'
                      }`}
                    >
                      <div className="flex items-center gap-3">
                        <input
                          type="checkbox"
                          checked={isSelected}
                          onChange={() => handleToppingToggle(topping)}
                          className="w-4 h-4 rounded text-brand-orange accent-brand-orange focus:ring-brand-orange cursor-pointer"
                        />
                        <span className="text-xs font-semibold text-text-primary">
                          {topping.name}
                        </span>
                      </div>
                      <span className="text-xs font-bold text-brand-orange">
                        +{formatVND(topping.price)}
                      </span>
                    </label>
                  )
                })}
              </div>
            </div>
          )}
        </div>

        <div className="px-6 py-4 border-t border-border bg-surface flex items-center justify-between">
          <div className="flex flex-col">
            <span className="text-[10px] font-bold text-text-secondary uppercase tracking-wider">
              Tổng tạm tính
            </span>
            <span className="text-sm font-extrabold text-brand-orange">
              {formatVND(totalPrice)}
            </span>
          </div>

          <div className="flex gap-2">
            <button
              onClick={onClose}
              className="px-4 py-2 rounded-lg border border-brand-orange text-brand-orange bg-surface-white text-xs font-bold cursor-pointer hover:bg-brand-orange-light active:scale-95 transition-all"
            >
              Hủy
            </button>
            <button
              onClick={handleConfirm}
              className="px-4 py-2 rounded-lg bg-brand-orange text-white text-xs font-bold cursor-pointer hover:bg-brand-orange-hover shadow-[var(--shadow-button)] active:scale-95 transition-all"
            >
              Xác nhận
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}
