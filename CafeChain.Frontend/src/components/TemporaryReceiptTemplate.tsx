import type { CartQueueItemSnapshot, CartSyncQueueItem } from '../db/CafeChainPOSDB'
import { formatIceLevel } from '../utils/iceLevel'

interface TemporaryReceiptTemplateProps {
  order: CartSyncQueueItem
}

const moneyFormatter = new Intl.NumberFormat('vi-VN', {
  maximumFractionDigits: 0,
})

const formatMoney = (amount: number): string =>
  `${moneyFormatter.format(Math.max(0, amount))}đ`

const formatDateTime = (value: string): string => {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value

  const day = date.getDate().toString().padStart(2, '0')
  const month = (date.getMonth() + 1).toString().padStart(2, '0')
  const year = date.getFullYear()
  const hour = date.getHours().toString().padStart(2, '0')
  const minute = date.getMinutes().toString().padStart(2, '0')
  return `${day}/${month}/${year} ${hour}:${minute}`
}

const getItemModifiers = (item: CartQueueItemSnapshot): string[] => {
  const modifiers: string[] = []

  if (item.sizeName) modifiers.push(`Size ${item.sizeName}`)
  const iceLabel = formatIceLevel(item.iceLevelPercent)
  if (iceLabel) modifiers.push(iceLabel)
  if (item.toppings?.length) {
    modifiers.push(`Topping: ${item.toppings.map((topping) => topping.name ?? `#${topping.toppingId}`).join(', ')}`)
  }
  if (item.note) modifiers.push(item.note)
  if (!modifiers.length && item.detailText) modifiers.push(item.detailText)

  return modifiers
}

export default function TemporaryReceiptTemplate({ order }: TemporaryReceiptTemplateProps) {
  const items = order.cartSnapshot?.length
    ? order.cartSnapshot
    : order.items.map((item) => ({
      menuItemId: item.menuItemId,
      storeMenuItemId: item.storeMenuItemId,
      drinkSizeId: item.drinkSizeId,
      recipeIdSnapshot: item.recipeIdSnapshot,
      name: item.name,
      sizeId: item.sizeId,
      quantity: item.quantity,
      unitPrice: item.unitPrice,
      effectivePrice: item.effectivePrice,
      priceSource: item.priceSource,
      catalogVersion: item.catalogVersion,
      iceLevelPercent: item.iceLevelPercent,
      note: item.note,
      toppings: item.toppings,
    }))
  const payment = order.paymentSnapshot

  return (
    <section className="temporary-receipt-template bg-white text-black font-mono text-[11px] leading-tight">
      <header className="text-center border-b border-dashed border-black pb-2">
        <h1 className="text-[15px] font-black">PHIẾU TẠM - CHƯA ĐỒNG BỘ</h1>
        <p className="mt-1 text-[10px] font-black">KHÔNG PHẢI HÓA ĐƠN CHÍNH THỨC</p>
      </header>

      <section className="py-2 border-b border-dashed border-black space-y-1">
        <div className="flex justify-between gap-2">
          <span>ClientOrderId</span>
          <span className="font-bold text-right break-all">{order.clientOrderId}</span>
        </div>
        <div className="flex justify-between gap-2">
          <span>Thời gian</span>
          <span className="tabular-nums text-right">{formatDateTime(order.soldAt)}</span>
        </div>
        <div className="flex justify-between gap-2">
          <span>Ca</span>
          <span className="text-right">#{order.workShiftId}</span>
        </div>
        <div className="flex justify-between gap-2">
          <span>Cửa hàng</span>
          <span className="text-right">#{order.storeId}</span>
        </div>
        <div className="flex justify-between gap-2">
          <span>Thu ngân</span>
          <span className="text-right">#{order.staffId}</span>
        </div>
      </section>

      <section className="py-2 border-b border-dashed border-black">
        <div className="grid grid-cols-[minmax(0,1fr)_24px_58px_62px] gap-x-1 font-bold border-b border-black pb-1">
          <span>Món</span>
          <span className="text-right">SL</span>
          <span className="text-right">Giá</span>
          <span className="text-right">Tiền</span>
        </div>

        <div className="divide-y divide-dashed divide-black/40">
          {items.map((item, index) => (
            <div
              key={`${item.menuItemId}-${item.sizeId ?? 'default'}-${index}`}
              className="grid grid-cols-[minmax(0,1fr)_24px_58px_62px] gap-x-1 py-1"
            >
              <div className="min-w-0">
                <p className="font-bold break-words">{item.name}</p>
                {getItemModifiers(item).map((modifier) => (
                  <p key={modifier} className="text-[10px] break-words">
                    {modifier}
                  </p>
                ))}
              </div>
              <span className="text-right tabular-nums">{item.quantity}</span>
              <span className="text-right tabular-nums">{formatMoney(item.unitPrice)}</span>
              <span className="text-right tabular-nums">{formatMoney(item.unitPrice * item.quantity)}</span>
            </div>
          ))}
        </div>
      </section>

      <section className="py-2 border-b border-dashed border-black space-y-1">
        <div className="flex justify-between gap-2 text-[15px] font-black">
          <span>TỔNG CỘNG</span>
          <span className="tabular-nums">{formatMoney(order.totalAmount)}</span>
        </div>
        {payment && (
          <>
            <div className="flex justify-between gap-2">
              <span>Thanh toán</span>
              <span>Tiền mặt</span>
            </div>
            <div className="flex justify-between gap-2">
              <span>Đã thu</span>
              <span className="tabular-nums">{formatMoney(payment.receivedAmount)}</span>
            </div>
            <div className="flex justify-between gap-2">
              <span>Tiền thừa</span>
              <span className="tabular-nums">{formatMoney(payment.changeAmount)}</span>
            </div>
          </>
        )}
      </section>

      <footer className="text-center pt-2 space-y-1">
        <p className="font-black">PHIẾU TẠM - CHƯA ĐỒNG BỘ</p>
        <p className="text-[10px]">Đợi đồng bộ thành công mới có hóa đơn chính thức.</p>
      </footer>
    </section>
  )
}
