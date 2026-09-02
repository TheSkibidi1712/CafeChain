import { useEffect, useMemo, useRef, useState } from 'react'
import {
  readCustomerDisplaySnapshot,
  subscribeCustomerDisplay,
  type CustomerDisplaySnapshot,
} from '../services/customerDisplay'
import VietQrCode from '../components/pos/VietQrCode'
import { usePreferences } from '../contexts/PreferencesContext'
import { useLocaleFormatters } from '../hooks/useLocaleFormatters'

const readExpectedWorkShiftId = (): number | null => {
  const value = Number(new URLSearchParams(window.location.search).get('workShiftId'))
  return Number.isSafeInteger(value) && value > 0 ? value : null
}

export default function CustomerDisplay() {
  const { t } = usePreferences()
  const { formatMoney } = useLocaleFormatters()
  const expectedWorkShiftId = useMemo(() => readExpectedWorkShiftId(), [])
  const [snapshot, setSnapshot] = useState<CustomerDisplaySnapshot | null>(() =>
    readCustomerDisplaySnapshot(expectedWorkShiftId)
  )
  const initialSnapshotRef = useRef(snapshot)
  const [clock, setClock] = useState(Date.now)

  useEffect(() => subscribeCustomerDisplay(setSnapshot, {
    expectedWorkShiftId,
    initialSnapshot: initialSnapshotRef.current,
  }), [expectedWorkShiftId])

  useEffect(() => {
    if (snapshot?.state !== 'success') return
    const successMessageId = snapshot.messageId
    const timer = window.setTimeout(() => {
      setSnapshot((current) => current?.messageId === successMessageId ? null : current)
    }, 4000)
    return () => window.clearTimeout(timer)
  }, [snapshot?.messageId, snapshot?.state])

  useEffect(() => {
    if (snapshot?.state !== 'vietqr' || !snapshot.expiresAt) return
    const timer = window.setInterval(() => setClock(Date.now()), 1000)
    return () => window.clearInterval(timer)
  }, [snapshot?.expiresAt, snapshot?.state])

  const remainingSeconds = snapshot?.state === 'vietqr' && snapshot.expiresAt
    ? Math.max(0, Math.ceil((snapshot.expiresAt - clock) / 1000))
    : 0
  const isQrExpired = snapshot?.state === 'vietqr'
    && !!snapshot.expiresAt
    && remainingSeconds === 0

  const countdown = useMemo(() => {
    const minutes = Math.floor(remainingSeconds / 60)
    const seconds = remainingSeconds % 60
    return `${minutes.toString().padStart(2, '0')}:${seconds.toString().padStart(2, '0')}`
  }, [remainingSeconds])

  const requestFullscreen = async () => {
    try {
      await document.documentElement.requestFullscreen()
    } catch {
      // Browser fallback remains the manually positioned window.
    }
  }

  return (
    <main className="customer-display" aria-live="polite">
      <header className="customer-display-header">
        <div className="flex items-center gap-3">
          <span className="customer-display-mark" aria-hidden="true">CC</span>
          <div>
            <h1 className="text-xl font-extrabold text-text-primary">CafeChain</h1>
            <p className="text-sm font-semibold text-text-secondary">
              {t('common.branch', { id: snapshot?.storeId ?? '-' })}
            </p>
          </div>
        </div>
        <button
          type="button"
          onClick={() => void requestFullscreen()}
          className="pos-touch-target rounded-lg border border-border bg-white px-4 text-sm font-bold text-text-primary hover:bg-surface-hover"
        >
          {t('customerDisplay.fullscreen')}
        </button>
      </header>

      <section className="customer-display-content">
        {!snapshot && (
          <DisplayMessage title={t('customerDisplay.welcome')} detail={t('customerDisplay.ready')} />
        )}

        {snapshot?.state === 'offline' && (
          <DisplayMessage
            title={t('customerDisplay.offlineTitle')}
            detail={t('customerDisplay.offlineDetail')}
            tone="warning"
          />
        )}

        {snapshot?.state === 'success' && (
          <DisplayMessage
            title={t('customerDisplay.successTitle')}
            detail={snapshot.message || t('customerDisplay.successDetail')}
            tone="success"
          />
        )}

        {snapshot?.state === 'cancelled' && (
          <DisplayMessage
            title={t('customerDisplay.cancelledTitle')}
            detail={t('customerDisplay.cancelledDetail')}
            tone="warning"
          />
        )}

        {(snapshot?.state === 'expired' || isQrExpired) && (
          <DisplayMessage
            title={t('customerDisplay.expiredTitle')}
            detail={t('customerDisplay.expiredDetail')}
            tone="warning"
          />
        )}

        {snapshot?.state === 'cart' && (
          <CartPreview snapshot={snapshot} />
        )}

        {snapshot?.state === 'vietqr' && !isQrExpired && (
          <div className="customer-display-qr-layout">
            <div className="customer-display-qr-copy">
              <p className="text-sm font-bold uppercase text-text-secondary">{t('customerDisplay.vietQrPayment')}</p>
              <strong className="mt-2 block text-4xl font-extrabold text-brand-orange tabular-nums">
                {formatMoney(snapshot.totalAmount)}
              </strong>
              {snapshot.orderId && (
                <p className="mt-2 text-base font-bold text-text-secondary">{t('customerDisplay.orderCode', { id: snapshot.orderId })}</p>
              )}
              <p className="mt-6 text-lg font-bold text-text-primary">{t('customerDisplay.scanToPay')}</p>
              <p className="mt-1 text-sm text-text-secondary">{t('customerDisplay.timeRemaining', { time: countdown })}</p>
            </div>
            <div className="customer-display-qr-frame">
              <VietQrCode
                value={snapshot.qrCode}
                size={900}
                alt={t('customerDisplay.qrAlt', { id: snapshot.orderId ?? '' })}
              />
            </div>
          </div>
        )}
      </section>
    </main>
  )
}

function CartPreview({ snapshot }: { snapshot: CustomerDisplaySnapshot }) {
  const { t } = usePreferences()
  const { formatMoney } = useLocaleFormatters()
  return (
    <div className="customer-display-cart-layout">
      <div className="min-w-0">
        <p className="text-sm font-bold uppercase text-text-secondary">
          {snapshot.orderType === 'take-away' ? t('customerDisplay.takeAwayOrder') : t('customerDisplay.dineInOrder')}
        </p>
        <h2 className="mt-1 text-3xl font-extrabold text-text-primary">{t('customerDisplay.yourOrder')}</h2>
        <div className="customer-display-item-list mt-5">
          {snapshot.items.map((item, index) => (
            <div key={`${item.name}-${index}`} className="customer-display-item-row">
              <div className="min-w-0">
                <p className="truncate text-lg font-extrabold text-text-primary">
                  {item.quantity} × {item.name}
                </p>
                {item.optionSummary && (
                  <p className="mt-1 line-clamp-2 text-sm text-text-secondary">{item.optionSummary}</p>
                )}
              </div>
              <strong className="shrink-0 text-lg font-extrabold text-text-primary tabular-nums">
                {formatMoney(item.lineTotal)}
              </strong>
            </div>
          ))}
        </div>
      </div>
      <div className="customer-display-total">
        <span>{t('customerDisplay.total')}</span>
        <strong>{formatMoney(snapshot.totalAmount)}</strong>
      </div>
    </div>
  )
}

function DisplayMessage({
  title,
  detail,
  tone = 'neutral',
}: {
  title: string
  detail: string
  tone?: 'neutral' | 'success' | 'warning'
}) {
  return (
    <div className={`customer-display-message customer-display-message-${tone}`}>
      <span className="customer-display-message-icon" aria-hidden="true">
        {tone === 'success' ? '✓' : tone === 'warning' ? '!' : 'CC'}
      </span>
      <h2>{title}</h2>
      <p>{detail}</p>
    </div>
  )
}
