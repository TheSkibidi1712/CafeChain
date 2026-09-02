import type { CartQueueItemSnapshot, CartSyncQueueItem } from '../db/CafeChainPOSDB'
import { usePreferences } from '../contexts/PreferencesContext'
import { useLocaleFormatters } from '../hooks/useLocaleFormatters'

interface TemporaryReceiptTemplateProps {
  order: CartSyncQueueItem
}

const getItemModifiers = (item: CartQueueItemSnapshot, labels: { size: string; topping: string; ice: (value: number) => string }): string[] => {
  const modifiers: string[] = []

  if (item.sizeName) modifiers.push(`${labels.size} ${item.sizeName}`)
  const iceLabel = item.iceLevelPercent === null || item.iceLevelPercent === undefined ? '' : labels.ice(item.iceLevelPercent)
  if (iceLabel) modifiers.push(iceLabel)
  if (item.toppings?.length) {
    modifiers.push(`${labels.topping}: ${item.toppings.map((topping) => topping.name ?? `#${topping.toppingId}`).join(', ')}`)
  }
  if (item.note) modifiers.push(item.note)
  if (!modifiers.length && item.detailText) modifiers.push(item.detailText)

  return modifiers
}

export default function TemporaryReceiptTemplate({ order }: TemporaryReceiptTemplateProps) {
  const { t } = usePreferences()
  const { formatMoney, formatDateTime } = useLocaleFormatters()
  const modifierLabels = {
    size: t('print.size'),
    topping: t('print.topping'),
    ice: (value: number) => value === 0 ? t('modifier.ice.none') : value === 50 ? t('modifier.ice.less') : value === 100 ? t('modifier.ice.normal') : '',
  }
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
        <h1 className="text-[15px] font-black">{t('temporary.pendingSync')}</h1>
        <p className="mt-1 text-[10px] font-black">{t('temporary.notOfficial')}</p>
      </header>

      <section className="py-2 border-b border-dashed border-black space-y-1">
        <div className="flex justify-between gap-2">
          <span>{t('temporary.clientOrderId')}</span>
          <span className="font-bold text-right break-all">{order.clientOrderId}</span>
        </div>
        <div className="flex justify-between gap-2">
          <span>{t('temporary.time')}</span>
          <span className="tabular-nums text-right">{formatDateTime(order.soldAt)}</span>
        </div>
        <div className="flex justify-between gap-2">
          <span>{t('temporary.shift')}</span>
          <span className="text-right">#{order.workShiftId}</span>
        </div>
        <div className="flex justify-between gap-2">
          <span>{t('temporary.store')}</span>
          <span className="text-right">#{order.storeId}</span>
        </div>
        <div className="flex justify-between gap-2">
          <span>{t('print.cashier')}</span>
          <span className="text-right">#{order.staffId}</span>
        </div>
      </section>

      <section className="py-2 border-b border-dashed border-black">
        <div className="grid grid-cols-[minmax(0,1fr)_24px_58px_62px] gap-x-1 font-bold border-b border-black pb-1">
          <span>{t('print.item')}</span>
          <span className="text-right">{t('print.quantity')}</span>
          <span className="text-right">{t('print.price')}</span>
          <span className="text-right">{t('print.amount')}</span>
        </div>

        <div className="divide-y divide-dashed divide-black/40">
          {items.map((item, index) => (
            <div
              key={`${item.menuItemId}-${item.sizeId ?? 'default'}-${index}`}
              className="grid grid-cols-[minmax(0,1fr)_24px_58px_62px] gap-x-1 py-1"
            >
              <div className="min-w-0">
                <p className="font-bold break-words">{item.name}</p>
                {getItemModifiers(item, modifierLabels).map((modifier) => (
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
          <span>{t('print.total')}</span>
          <span className="tabular-nums">{formatMoney(order.totalAmount)}</span>
        </div>
        {payment && (
          <>
            <div className="flex justify-between gap-2">
              <span>{t('print.payment')}</span>
              <span>{t('temporary.cash')}</span>
            </div>
            <div className="flex justify-between gap-2">
              <span>{t('temporary.received')}</span>
              <span className="tabular-nums">{formatMoney(payment.receivedAmount)}</span>
            </div>
            <div className="flex justify-between gap-2">
              <span>{t('temporary.change')}</span>
              <span className="tabular-nums">{formatMoney(payment.changeAmount)}</span>
            </div>
          </>
        )}
      </section>

      <footer className="text-center pt-2 space-y-1">
        <p className="font-black">{t('temporary.pendingSync')}</p>
        <p className="text-[10px]">{t('temporary.syncNotice')}</p>
      </footer>
    </section>
  )
}
