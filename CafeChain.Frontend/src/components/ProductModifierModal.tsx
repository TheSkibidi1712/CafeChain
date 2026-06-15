import { useState, useEffect } from 'react'

export interface MenuItem {
  id: number
  name: string
  price: number
  categoryId: number
}

export interface ToppingOption {
  id: number
  name: string
  price: number
}

export interface ModifierSelection {
  size: 'S' | 'M' | 'L'
  ice: '0%' | '50%' | '100%'
  sugar: '0%' | '50%' | '100%'
  selectedToppings: ToppingOption[]
  totalPrice: number
}

interface ProductModifierModalProps {
  isOpen: boolean
  onClose: () => void
  menuItem: MenuItem | null
  onConfirm: (selection: ModifierSelection) => void
}

const TOPPINGS: ToppingOption[] = [
  { id: 1, name: 'Trân châu trắng', price: 10000 },
  { id: 2, name: 'Thạch trái cây', price: 10000 },
  { id: 3, name: 'Kem Cheese', price: 15000 },
]

const SIZE_PRICES = {
  S: 0,
  M: 5000,
  L: 10000,
}

const formatVND = (amount: number): string =>
  new Intl.NumberFormat('vi-VN').format(amount) + 'đ'

export default function ProductModifierModal({
  isOpen,
  onClose,
  menuItem,
  onConfirm,
}: ProductModifierModalProps) {
  if (!isOpen || !menuItem) return null

  const [size, setSize] = useState<'S' | 'M' | 'L'>('S')
  const [ice, setIce] = useState<'0%' | '50%' | '100%'>('100%')
  const [sugar, setSugar] = useState<'0%' | '50%' | '100%'>('100%')
  const [selectedToppings, setSelectedToppings] = useState<ToppingOption[]>([])

  // Reset state when opening a new item
  useEffect(() => {
    setSize('S')
    setIce('100%')
    setSugar('100%')
    setSelectedToppings([])
  }, [menuItem])

  const handleToppingToggle = (topping: ToppingOption) => {
    setSelectedToppings((prev) => {
      const exists = prev.find((t) => t.id === topping.id)
      if (exists) {
        return prev.filter((t) => t.id !== topping.id)
      }
      return [...prev, topping]
    })
  }

  // Calculate live total price
  const basePrice = menuItem.price
  const sizePrice = SIZE_PRICES[size]
  const toppingsPrice = selectedToppings.reduce((sum, t) => sum + t.price, 0)
  const totalPrice = basePrice + sizePrice + toppingsPrice

  const handleConfirm = () => {
    onConfirm({
      size,
      ice,
      sugar,
      selectedToppings,
      totalPrice,
    })
    onClose()
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center select-none">
      {/* Blurred Backdrop */}
      <div
        className="absolute inset-0 bg-black/40 backdrop-blur-sm transition-opacity"
        onClick={onClose}
      />

      {/* Modal Container */}
      <div className="relative bg-surface-white w-full max-w-md rounded-2xl shadow-xl border border-border overflow-hidden animate-in fade-in zoom-in-95 duration-200 flex flex-col max-h-[90vh]">
        {/* Header */}
        <div className="px-6 py-4 border-b border-border flex justify-between items-center bg-surface-white">
          <div>
            <h2 className="text-sm font-bold text-text-primary">Tùy biến món nước</h2>
            <p className="text-[11px] font-semibold text-brand-orange mt-0.5">{menuItem.name}</p>
          </div>
          <button
            onClick={onClose}
            className="w-8 h-8 rounded-full border border-border text-text-secondary hover:bg-surface-hover flex items-center justify-center cursor-pointer transition-colors"
          >
            ✕
          </button>
        </div>

        {/* Content */}
        <div className="p-6 overflow-y-auto space-y-5 flex-1 bg-surface-white">
          {/* Size Choice */}
          <div className="space-y-2">
            <label className="block text-[10px] font-bold text-text-secondary uppercase tracking-wider">
              Kích cỡ (Size)
            </label>
            <div className="flex gap-2.5">
              {(['S', 'M', 'L'] as const).map((s) => (
                <button
                  key={s}
                  type="button"
                  onClick={() => setSize(s)}
                  className={`flex-1 py-2.5 rounded-xl text-xs font-bold border cursor-pointer transition-all ${
                    size === s
                      ? 'bg-brand-orange text-white border-brand-orange shadow-[var(--shadow-button)]'
                      : 'bg-surface border-border text-text-primary hover:bg-brand-orange-light hover:text-brand-orange hover:border-brand-orange-border'
                  }`}
                >
                  Size {s} {SIZE_PRICES[s] > 0 ? `(+${formatVND(SIZE_PRICES[s])})` : ''}
                </button>
              ))}
            </div>
          </div>

          {/* Ice Choice */}
          <div className="space-y-2">
            <label className="block text-[10px] font-bold text-text-secondary uppercase tracking-wider">
              Mức Đá (Ice)
            </label>
            <div className="flex gap-2.5">
              {(['0%', '50%', '100%'] as const).map((i) => (
                <button
                  key={i}
                  type="button"
                  onClick={() => setIce(i)}
                  className={`flex-1 py-2.5 rounded-xl text-xs font-bold border cursor-pointer transition-all ${
                    ice === i
                      ? 'bg-brand-orange text-white border-brand-orange shadow-[var(--shadow-button)]'
                      : 'bg-surface border-border text-text-primary hover:bg-brand-orange-light hover:text-brand-orange hover:border-brand-orange-border'
                  }`}
                >
                  Đá {i}
                </button>
              ))}
            </div>
          </div>

          {/* Sugar Choice */}
          <div className="space-y-2">
            <label className="block text-[10px] font-bold text-text-secondary uppercase tracking-wider">
              Mức Đường (Sugar)
            </label>
            <div className="flex gap-2.5">
              {(['0%', '50%', '100%'] as const).map((sg) => (
                <button
                  key={sg}
                  type="button"
                  onClick={() => setSugar(sg)}
                  className={`flex-1 py-2.5 rounded-xl text-xs font-bold border cursor-pointer transition-all ${
                    sugar === sg
                      ? 'bg-brand-orange text-white border-brand-orange shadow-[var(--shadow-button)]'
                      : 'bg-surface border-border text-text-primary hover:bg-brand-orange-light hover:text-brand-orange hover:border-brand-orange-border'
                  }`}
                >
                  Đường {sg}
                </button>
              ))}
            </div>
          </div>

          {/* Toppings list */}
          <div className="space-y-2">
            <label className="block text-[10px] font-bold text-text-secondary uppercase tracking-wider">
              Thêm Topping
            </label>
            <div className="divide-y divide-border border border-border rounded-xl overflow-hidden bg-surface">
              {TOPPINGS.map((topping) => {
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
        </div>

        {/* Footer */}
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
