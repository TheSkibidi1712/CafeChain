import type { CartQueueItemSnapshot, CartSyncQueueItem } from '../db/CafeChainPOSDB'
import { usePreferences } from '../contexts/PreferencesContext'
import { useLocaleFormatters } from '../hooks/useLocaleFormatters'

interface TemporaryDrinkLabelTemplateProps {
  order: CartSyncQueueItem
}

interface DrinkLabel {
  item: CartQueueItemSnapshot
  cupNo: number
  cupCount: number
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

const getLabels = (order: CartSyncQueueItem): DrinkLabel[] =>
  order.cartSnapshot.flatMap((item) =>
    Array.from({ length: Math.max(1, item.quantity) }, (_, index) => ({
      item,
      cupNo: index + 1,
      cupCount: item.quantity,
    }))
  )

export default function TemporaryDrinkLabelTemplate({ order }: TemporaryDrinkLabelTemplateProps) {
  const { t } = usePreferences()
  const { formatDateTime } = useLocaleFormatters()
  const modifierLabels = {
    size: t('print.size'),
    topping: t('print.topping'),
    ice: (value: number) => value === 0 ? t('modifier.ice.none') : value === 50 ? t('modifier.ice.less') : value === 100 ? t('modifier.ice.normal') : '',
  }
  const labels = getLabels(order)

  return (
    <section className="temporary-drink-label-template bg-white text-black font-mono text-[11px] leading-tight">
      {labels.map(({ item, cupNo, cupCount }, index) => (
        <article
          key={`${item.menuItemId}-${item.sizeId ?? 'default'}-${index}`}
          className="temporary-drink-label border border-black p-2"
        >
          <header className="border-b border-dashed border-black pb-1">
            <div className="flex items-center justify-between gap-2">
              <span className="text-[10px] font-black">{t('temporary.pendingSync')}</span>
              <span className="text-[10px] font-bold tabular-nums">
                {cupNo}/{cupCount}
              </span>
            </div>
            <p className="mt-1 break-all text-[9px]">{t('temporary.clientOrderId')}: {order.clientOrderId}</p>
          </header>

          <section className="py-2 space-y-1">
            <p className="text-[15px] font-black break-words">{item.name}</p>
            {getItemModifiers(item, modifierLabels).map((modifier) => (
              <p key={modifier} className="break-words">
                {modifier}
              </p>
            ))}
          </section>

          <footer className="border-t border-dashed border-black pt-1 flex items-center justify-between gap-2 text-[9px]">
            <span>{t('temporary.shift')} #{order.workShiftId}</span>
            <span>{formatDateTime(order.soldAt)}</span>
          </footer>
        </article>
      ))}
    </section>
  )
}
