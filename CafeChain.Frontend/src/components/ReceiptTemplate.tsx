export interface ReceiptTemplateItem {
  key?: string
  name: string
  quantity: number
  unitPrice: number
  lineTotal: number
  modifiers?: string[]
}

export interface ReceiptTemplateData {
  storeName: string
  storeAddress: string
  hotline: string
  orderCode: string
  createdAt: string
  cashierName: string
  posMachine: string
  items: ReceiptTemplateItem[]
  subtotal: number
  discount?: number
  total: number
  paymentMethod: string
  wifiName?: string
  wifiPassword?: string
}

interface ReceiptTemplateProps {
  receipt: ReceiptTemplateData
}

export default function ReceiptTemplate({ receipt }: ReceiptTemplateProps) {
  const { t } = usePreferences()
  const { formatMoney, formatDateTime } = useLocaleFormatters()
  const hasDiscount = (receipt.discount ?? 0) > 0

  return (
    <section className="receipt-template w-[300px] bg-white text-black font-mono text-[11px] leading-tight">
      <header className="text-center border-b border-dashed border-black pb-2">
        <h1 className="text-[18px] font-black tracking-[0.08em]">{receipt.storeName}</h1>
        <p className="mt-1 break-words">{receipt.storeAddress}</p>
        <p>Hotline: {receipt.hotline}</p>
      </header>

      <section className="py-2 border-b border-dashed border-black space-y-1">
        <div className="flex justify-between gap-2">
          <span>{t('print.order')}</span>
          <span className="font-bold tabular-nums text-right">{receipt.orderCode}</span>
        </div>
        <div className="flex justify-between gap-2">
          <span>{t('print.dateTime')}</span>
          <span className="tabular-nums text-right">{formatDateTime(receipt.createdAt)}</span>
        </div>
        <div className="flex justify-between gap-2">
          <span>{t('print.cashier')}</span>
          <span className="text-right break-words">{receipt.cashierName}</span>
        </div>
        <div className="flex justify-between gap-2">
          <span>{t('print.posMachine')}</span>
          <span className="text-right">{receipt.posMachine}</span>
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
          {receipt.items.map((item, index) => (
            <div
              key={item.key ?? `${item.name}-${index}`}
              className="grid grid-cols-[minmax(0,1fr)_24px_58px_62px] gap-x-1 py-1"
            >
              <div className="min-w-0">
                <p className="font-bold break-words">{item.name}</p>
                {item.modifiers?.map((modifier) => (
                  <p key={modifier} className="text-[10px] break-words">
                    {modifier}
                  </p>
                ))}
              </div>
              <span className="text-right tabular-nums">{item.quantity}</span>
              <span className="text-right tabular-nums">{formatMoney(item.unitPrice)}</span>
              <span className="text-right tabular-nums">{formatMoney(item.lineTotal)}</span>
            </div>
          ))}
        </div>
      </section>

      <section className="py-2 border-b border-dashed border-black space-y-1">
        <div className="flex justify-between gap-2">
          <span>{t('print.subtotal')}</span>
          <span className="tabular-nums">{formatMoney(receipt.subtotal)}</span>
        </div>
        {hasDiscount && (
          <div className="flex justify-between gap-2">
            <span>{t('print.discount')}</span>
            <span className="tabular-nums">-{formatMoney(receipt.discount ?? 0)}</span>
          </div>
        )}
        <div className="flex justify-between gap-2 text-[15px] font-black pt-1">
          <span>{t('print.total')}</span>
          <span className="tabular-nums">{formatMoney(receipt.total)}</span>
        </div>
        <div className="flex justify-between gap-2">
          <span>{t('print.payment')}</span>
          <span className="text-right">{receipt.paymentMethod}</span>
        </div>
      </section>

      <footer className="text-center pt-2 space-y-1">
        <p className="font-bold">{t('print.thankYou')}</p>
        {receipt.wifiName && (
          <p>
            Wi-Fi: {receipt.wifiName}
            {receipt.wifiPassword ? ` / ${receipt.wifiPassword}` : ''}
          </p>
        )}
        <p>{t('print.seeYouAgain')}</p>
      </footer>
    </section>
  )
}
import { usePreferences } from '../contexts/PreferencesContext'
import { useLocaleFormatters } from '../hooks/useLocaleFormatters'
