import { useEffect, useRef } from 'react'
import { usePreferences } from '../../../contexts/PreferencesContext'
import { useLocaleFormatters } from '../../../hooks/useLocaleFormatters'
import { printVietQrSlip } from '../../../services/vietQrPrint'
import VietQrCode from '../VietQrCode'

export type PaymentWorkspaceMode = 'cash' | 'vietqr' | 'split'

export interface PaymentWorkspacePendingPayment {
  status: 'collecting' | 'awaiting-vietqr'
  orderId?: number
  checkoutUrl?: string
  qrCode?: string | null
  totalAmount: number
  pendingCashAmount: number
  vietQrAmount: number
}

interface PaymentWorkspaceProps {
  isOpen: boolean
  mode: PaymentWorkspaceMode
  totalAmount: number
  isOnline: boolean
  isCheckingOut: boolean
  isCancellingPayment: boolean
  cashReceivedInput: string
  cashReceivedAmount: number
  cashChangeAmount: number
  cashQuickAmounts: number[]
  cashValidationMessage: string | null
  canConfirmCash: boolean
  splitCashInput: string
  splitCashAmount: number
  splitRemainingAmount: number
  splitValidationMessage: string | null
  canBeginSplit: boolean
  pendingPayment: PaymentWorkspacePendingPayment | null
  remainingSeconds: number
  cashReturnAmount: number | null
  onSelectMode: (mode: PaymentWorkspaceMode) => void
  onClose: () => void
  onCashInputChange: (value: string) => void
  onConfirmCash: () => void
  onSplitCashInputChange: (value: string) => void
  onBeginSplit: () => void
  onCreateSplitVietQr: () => void
  onSettleSplitCash: () => void
  onSwitchVietQrToCash: () => void
  onCancelPending: () => void
  onDismissCashReturn: () => void
  onConfirmCashReturned: () => void
}

const formatCountdown = (seconds: number): string => {
  const minutes = Math.floor(seconds / 60)
  const remainingSeconds = seconds % 60
  return `${minutes.toString().padStart(2, '0')}:${remainingSeconds.toString().padStart(2, '0')}`
}

export default function PaymentWorkspace({
  isOpen,
  mode,
  totalAmount,
  isOnline,
  isCheckingOut,
  isCancellingPayment,
  cashReceivedInput,
  cashReceivedAmount,
  cashChangeAmount,
  cashQuickAmounts,
  cashValidationMessage,
  canConfirmCash,
  splitCashInput,
  splitCashAmount,
  splitRemainingAmount,
  splitValidationMessage,
  canBeginSplit,
  pendingPayment,
  remainingSeconds,
  cashReturnAmount,
  onSelectMode,
  onClose,
  onCashInputChange,
  onConfirmCash,
  onSplitCashInputChange,
  onBeginSplit,
  onCreateSplitVietQr,
  onSettleSplitCash,
  onSwitchVietQrToCash,
  onCancelPending,
  onDismissCashReturn,
  onConfirmCashReturned,
}: PaymentWorkspaceProps) {
  const { t } = usePreferences()
  const dialogRef = useRef<HTMLDivElement>(null)
  const modeLabels: Record<PaymentWorkspaceMode, string> = {
    cash: t('payment.cash'),
    vietqr: t('payment.vietqr'),
    split: t('payment.split'),
  }

  useEffect(() => {
    if (!isOpen) return

    const previousFocus = document.activeElement as HTMLElement | null
    const focusableSelector = 'button:not([disabled]), input:not([disabled]), a[href], [tabindex]:not([tabindex="-1"])'
    const dialog = dialogRef.current
    dialog?.querySelector<HTMLElement>(focusableSelector)?.focus()

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.preventDefault()
        onClose()
        return
      }
      if (event.key !== 'Tab' || !dialog) return

      const focusableItems = Array.from(dialog.querySelectorAll<HTMLElement>(focusableSelector))
      if (focusableItems.length === 0) return
      const first = focusableItems[0]
      const last = focusableItems[focusableItems.length - 1]
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

  if (!isOpen) return null

  const isInteractionLocked = Boolean(pendingPayment) || isCheckingOut || isCancellingPayment
  const isQrWaiting = pendingPayment?.status === 'awaiting-vietqr'

  return (
    <div className="pos-payment-workspace-backdrop fixed inset-0 z-[60] flex select-none">
      <div
        ref={dialogRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby="payment-workspace-title"
        className="pos-payment-workspace relative ml-auto flex h-full max-h-[100dvh] min-h-0 w-full flex-col overflow-hidden border-l border-border bg-white shadow-xl"
      >
        <header className="shrink-0 border-b border-border bg-white px-5 py-4">
          <div className="flex items-start justify-between gap-4">
            <div>
              <p className="text-xs font-bold uppercase text-text-secondary">{t('payment.title')}</p>
              <h2 id="payment-workspace-title" className="mt-0.5 text-xl font-extrabold text-text-primary">
                {cashReturnAmount !== null ? t('payment.refundConfirm') : modeLabels[mode]}
              </h2>
            </div>
            <button
              type="button"
              onClick={onClose}
              disabled={isCheckingOut || isCancellingPayment || Boolean(pendingPayment)}
              className="pos-touch-target flex items-center justify-center rounded-lg border border-border bg-white text-xl text-text-secondary hover:bg-surface-hover disabled:opacity-40"
              aria-label={pendingPayment
                ? t('payment.cannotClose')
                : t('payment.close')}
              title={pendingPayment
                ? t('payment.resolvePending')
                : t('payment.close')}
            >
              ×
            </button>
          </div>

          {cashReturnAmount === null && !pendingPayment && (
            <div className="mt-4 grid grid-cols-3 gap-2" role="tablist" aria-label={t('payment.methods')}>
              {(Object.keys(modeLabels) as PaymentWorkspaceMode[]).map((option) => (
                <button
                  key={option}
                  type="button"
                  role="tab"
                  aria-selected={mode === option}
                  onClick={() => onSelectMode(option)}
                  disabled={isInteractionLocked || (option !== 'cash' && !isOnline)}
                  className={`min-h-12 rounded-lg border px-2 text-sm font-bold transition-colors ${
                    mode === option
                      ? 'border-brand-orange bg-brand-orange text-white'
                      : 'border-border bg-white text-text-primary hover:bg-brand-orange-light'
                  } disabled:cursor-not-allowed disabled:opacity-40`}
                >
                  {modeLabels[option]}
                </button>
              ))}
            </div>
          )}
        </header>

        <div className={`pos-payment-workspace-body min-h-0 flex-1 bg-surface ${isQrWaiting ? 'overflow-hidden p-4' : 'overflow-y-auto p-5'}`}>
          {cashReturnAmount !== null ? (
            <CashReturnPanel
              amount={cashReturnAmount}
              isCancelling={isCancellingPayment}
              onDismiss={onDismissCashReturn}
              onConfirm={onConfirmCashReturned}
            />
          ) : isQrWaiting ? (
            <VietQrPanel
              pendingPayment={pendingPayment}
              remainingSeconds={remainingSeconds}
              isCancelling={isCancellingPayment}
              onSwitchToCash={onSwitchVietQrToCash}
              onCancel={onCancelPending}
            />
          ) : mode === 'cash' ? (
            <CashPanel
              totalAmount={totalAmount}
              receivedInput={cashReceivedInput}
              receivedAmount={cashReceivedAmount}
              changeAmount={cashChangeAmount}
              quickAmounts={cashQuickAmounts}
              validationMessage={cashValidationMessage}
              canConfirm={canConfirmCash}
              isCheckingOut={isCheckingOut}
              onInputChange={onCashInputChange}
              onConfirm={onConfirmCash}
              onCancel={onClose}
            />
          ) : mode === 'split' ? (
            <SplitPanel
              totalAmount={totalAmount}
              splitCashInput={splitCashInput}
              splitCashAmount={splitCashAmount}
              splitRemainingAmount={splitRemainingAmount}
              validationMessage={splitValidationMessage}
              canBegin={canBeginSplit}
              isCheckingOut={isCheckingOut}
              isCancelling={isCancellingPayment}
              pendingPayment={pendingPayment}
              onInputChange={onSplitCashInputChange}
              onBegin={onBeginSplit}
              onCreateVietQr={onCreateSplitVietQr}
              onSettleCash={onSettleSplitCash}
              onCancelPending={onCancelPending}
            />
          ) : (
            <VietQrPreparingPanel
              totalAmount={totalAmount}
              isCheckingOut={isCheckingOut}
              isOnline={isOnline}
              onRetry={() => onSelectMode('vietqr')}
              onCancel={onClose}
            />
          )}
        </div>
      </div>
    </div>
  )
}

interface CashPanelProps {
  totalAmount: number
  receivedInput: string
  receivedAmount: number
  changeAmount: number
  quickAmounts: number[]
  validationMessage: string | null
  canConfirm: boolean
  isCheckingOut: boolean
  onInputChange: (value: string) => void
  onConfirm: () => void
  onCancel: () => void
}

function CashPanel({
  totalAmount,
  receivedInput,
  receivedAmount,
  changeAmount,
  quickAmounts,
  validationMessage,
  canConfirm,
  isCheckingOut,
  onInputChange,
  onConfirm,
  onCancel,
}: CashPanelProps) {
  const { t } = usePreferences()
  const { formatMoney, formatNumber } = useLocaleFormatters()
  const appendValue = (value: string) => {
    const nextValue = `${receivedInput}${value}`.replace(/^0+(?=\d)/, '')
    onInputChange(nextValue)
  }
  const insufficientCash = receivedAmount < totalAmount

  return (
    <div className="mx-auto flex w-full max-w-xl flex-col gap-4">
      <div className="grid grid-cols-2 gap-3">
        <PaymentMetric label={t('payment.totalDue')} value={formatMoney(totalAmount)} />
        <PaymentMetric label={t('payment.change')} value={formatMoney(changeAmount)} accent={changeAmount > 0} />
      </div>

      <label className="block rounded-lg border border-border bg-white p-3" htmlFor="cash-received-input">
        <span className="block text-xs font-bold text-text-secondary">{t('payment.received')}</span>
        <div className="mt-2 flex items-center gap-2">
          <input
            id="cash-received-input"
            type="text"
            inputMode="numeric"
            autoComplete="off"
            value={receivedInput ? formatNumber(Number(receivedInput)) : ''}
            onChange={(event) => onInputChange(event.target.value.replace(/\D/g, ''))}
            className="min-w-0 flex-1 bg-transparent text-2xl font-extrabold text-text-primary outline-none tabular-nums"
            aria-describedby="cash-validation-message"
          />
          <span className="text-sm font-bold text-text-secondary">VNĐ</span>
        </div>
      </label>

      <div className="grid grid-cols-3 gap-2 sm:grid-cols-5" aria-label={t('payment.quickAmounts')}>
        {quickAmounts.map((amount) => (
          <button
            key={amount}
            type="button"
            onClick={() => onInputChange(String(amount))}
            className="min-h-12 rounded-lg border border-brand-orange-border bg-white px-2 text-sm font-bold text-brand-orange hover:bg-brand-orange-light"
          >
            {amount === totalAmount ? t('payment.exact') : formatMoney(amount)}
          </button>
        ))}
      </div>

      <div className="grid grid-cols-3 gap-2" aria-label={t('payment.keypad')}>
        {['1', '2', '3', '4', '5', '6', '7', '8', '9'].map((digit) => (
          <button
            key={digit}
            type="button"
            onClick={() => appendValue(digit)}
            className="pos-payment-key min-h-16 rounded-lg border border-border bg-white text-xl font-extrabold text-text-primary hover:bg-brand-orange-light"
          >
            {digit}
          </button>
        ))}
        <button type="button" onClick={() => appendValue('000')} className="pos-payment-key min-h-16 rounded-lg border border-border bg-white text-lg font-extrabold text-text-primary hover:bg-brand-orange-light">000</button>
        <button type="button" onClick={() => appendValue('0')} className="pos-payment-key min-h-16 rounded-lg border border-border bg-white text-xl font-extrabold text-text-primary hover:bg-brand-orange-light">0</button>
        <button
          type="button"
          onClick={() => onInputChange(receivedInput.slice(0, -1))}
          className="pos-payment-key min-h-16 rounded-lg border border-border bg-white text-sm font-extrabold text-text-secondary hover:bg-surface-hover"
        >
          {t('payment.eraseDigit')}
        </button>
      </div>

      <div id="cash-validation-message" aria-live="polite">
        {insufficientCash && <p className="text-sm font-bold text-danger">{t('payment.insufficient')}</p>}
        {!insufficientCash && validationMessage && <p className="text-sm font-bold text-danger">{validationMessage}</p>}
      </div>

      <div className="grid grid-cols-[auto_minmax(0,1fr)] gap-2">
        <button type="button" onClick={onCancel} disabled={isCheckingOut} className="min-h-14 rounded-lg border border-brand-orange px-4 text-sm font-bold text-brand-orange hover:bg-brand-orange-light disabled:opacity-40">{t('payment.backToCart')}</button>
        <button type="button" onClick={onConfirm} disabled={!canConfirm} className="min-h-14 rounded-lg bg-brand-orange px-5 text-base font-extrabold text-white hover:bg-brand-orange-hover disabled:cursor-not-allowed disabled:opacity-40">
          {isCheckingOut ? t('payment.processing') : t('payment.confirmAmount', { amount: formatMoney(totalAmount) })}
        </button>
      </div>
    </div>
  )
}

interface SplitPanelProps {
  totalAmount: number
  splitCashInput: string
  splitCashAmount: number
  splitRemainingAmount: number
  validationMessage: string | null
  canBegin: boolean
  isCheckingOut: boolean
  isCancelling: boolean
  pendingPayment: PaymentWorkspacePendingPayment | null
  onInputChange: (value: string) => void
  onBegin: () => void
  onCreateVietQr: () => void
  onSettleCash: () => void
  onCancelPending: () => void
}

function SplitPanel({
  totalAmount,
  splitCashInput,
  splitCashAmount,
  splitRemainingAmount,
  validationMessage,
  canBegin,
  isCheckingOut,
  isCancelling,
  pendingPayment,
  onInputChange,
  onBegin,
  onCreateVietQr,
  onSettleCash,
  onCancelPending,
}: SplitPanelProps) {
  const { t } = usePreferences()
  const { formatMoney, formatNumber } = useLocaleFormatters()
  if (pendingPayment?.status === 'collecting') {
    return (
      <div className="mx-auto w-full max-w-xl space-y-4">
        <div className="rounded-lg border border-warning/30 bg-white p-4">
          <p className="text-sm font-extrabold text-warning">{t('payment.cashHeld')}</p>
          <p className="mt-1 text-sm text-text-secondary">{t('payment.cashHeldDetail')}</p>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <PaymentMetric label={t('payment.cashReceived')} value={formatMoney(pendingPayment.pendingCashAmount)} />
          <PaymentMetric label={t('payment.remainingDue')} value={formatMoney(pendingPayment.vietQrAmount)} accent />
        </div>
        <div className="grid gap-2 sm:grid-cols-2">
          <button type="button" onClick={onCreateVietQr} disabled={isCheckingOut} className="min-h-14 rounded-lg bg-text-primary px-4 text-sm font-extrabold text-white hover:bg-gray-700 disabled:opacity-40">{t('payment.collectRemainingQr')}</button>
          <button type="button" onClick={onSettleCash} disabled={isCheckingOut} className="min-h-14 rounded-lg bg-brand-orange px-4 text-sm font-extrabold text-white hover:bg-brand-orange-hover disabled:opacity-40">{t('payment.collectRemainingCash')}</button>
        </div>
        <button type="button" onClick={onCancelPending} disabled={isCheckingOut || isCancelling} className="min-h-12 w-full rounded-lg border border-danger/40 bg-white px-4 text-sm font-bold text-danger hover:bg-[var(--pos-danger-soft)] disabled:opacity-40">
          {isCancelling ? t('payment.cancelling') : t('payment.cancelAndRefund')}
        </button>
      </div>
    )
  }

  const amountTooLarge = splitCashAmount >= totalAmount && splitCashAmount > 0

  return (
    <div className="mx-auto w-full max-w-xl space-y-4">
      <div className="grid grid-cols-2 gap-3">
        <PaymentMetric label={t('payment.totalDue')} value={formatMoney(totalAmount)} />
        <PaymentMetric label={t('payment.remaining')} value={formatMoney(splitRemainingAmount)} accent={splitCashAmount > 0} />
      </div>
      <label className="block rounded-lg border border-border bg-white p-3" htmlFor="split-cash-input">
        <span className="block text-xs font-bold text-text-secondary">{t('payment.cashFirst')}</span>
        <div className="mt-2 flex items-center gap-2">
          <input
            id="split-cash-input"
            type="text"
            inputMode="numeric"
            autoComplete="off"
            value={splitCashInput ? formatNumber(Number(splitCashInput)) : ''}
            onChange={(event) => onInputChange(event.target.value.replace(/\D/g, ''))}
            className="min-w-0 flex-1 bg-transparent text-2xl font-extrabold text-text-primary outline-none tabular-nums"
          />
          <span className="text-sm font-bold text-text-secondary">VNĐ</span>
        </div>
      </label>
      <p className="text-sm text-text-secondary">{t('payment.holdNotice')}</p>
      <div aria-live="polite">
        {validationMessage && <p className="text-sm font-bold text-danger">{validationMessage}</p>}
        {amountTooLarge && <p className="text-sm font-bold text-danger">{t('payment.splitTooLarge')}</p>}
      </div>
      <button type="button" onClick={onBegin} disabled={!canBegin} className="min-h-14 w-full rounded-lg bg-brand-orange px-5 text-base font-extrabold text-white hover:bg-brand-orange-hover disabled:cursor-not-allowed disabled:opacity-40">
        {t('payment.recordHeldCash')}
      </button>
    </div>
  )
}

function VietQrPreparingPanel({
  totalAmount,
  isCheckingOut,
  isOnline,
  onRetry,
  onCancel,
}: {
  totalAmount: number
  isCheckingOut: boolean
  isOnline: boolean
  onRetry: () => void
  onCancel: () => void
}) {
  const { t } = usePreferences()
  const { formatMoney } = useLocaleFormatters()
  return (
    <div className="mx-auto flex min-h-full w-full max-w-xl flex-col items-center justify-center gap-4 text-center">
      <PaymentMetric label={t('payment.qrAmount')} value={formatMoney(totalAmount)} accent />
      <div>
        <p className="text-base font-extrabold text-text-primary">{isCheckingOut ? t('payment.creatingQr') : t('payment.noQr')}</p>
        <p className="mt-1 text-sm text-text-secondary">{t('payment.autoConfirm')}</p>
      </div>
      {!isCheckingOut && (
        <div className="flex gap-2">
          <button type="button" onClick={onCancel} className="min-h-12 rounded-lg border border-border bg-white px-4 text-sm font-bold text-text-primary">{t('payment.backToCart')}</button>
          <button type="button" onClick={onRetry} disabled={!isOnline} className="min-h-12 rounded-lg bg-brand-orange px-4 text-sm font-bold text-white disabled:opacity-40">{t('payment.retryQr')}</button>
        </div>
      )}
    </div>
  )
}

function VietQrPanel({
  pendingPayment,
  remainingSeconds,
  isCancelling,
  onSwitchToCash,
  onCancel,
}: {
  pendingPayment: PaymentWorkspacePendingPayment
  remainingSeconds: number
  isCancelling: boolean
  onSwitchToCash: () => void
  onCancel: () => void
}) {
  const { t } = usePreferences()
  const { formatMoney } = useLocaleFormatters()
  return (
    <div className="mx-auto flex h-full w-full max-w-xl flex-col gap-3">
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
        <PaymentMetric label={t('payment.qrAmount')} value={formatMoney(pendingPayment.vietQrAmount)} accent />
        <PaymentMetric label={t('payment.orderCode')} value={pendingPayment.orderId ? `#${pendingPayment.orderId}` : t('payment.creating')} />
        <PaymentMetric label={t('payment.timeRemaining')} value={formatCountdown(remainingSeconds)} />
      </div>
      {pendingPayment.pendingCashAmount > 0 && (
        <p className="rounded-lg border border-warning/30 bg-white px-3 py-2 text-sm font-bold text-warning">
          {t('payment.holdingCash', { amount: formatMoney(pendingPayment.pendingCashAmount) })}
        </p>
      )}
      <div className="pos-vietqr-print-host min-h-0 flex-1 overflow-hidden rounded-lg border border-border bg-white">
        <div className="flex h-full items-center justify-center p-3">
          <VietQrCode
            value={pendingPayment.qrCode}
            size={720}
            alt={t('payment.qrAlt', { id: pendingPayment.orderId ?? '' })}
          />
        </div>
        <div className="pos-vietqr-print-details">
          <strong>CAFECHAIN</strong>
          <span>{t('customerDisplay.vietQrPayment')}</span>
          <b>{formatMoney(pendingPayment.vietQrAmount)}</b>
          <span>{t('customerDisplay.orderCode', { id: pendingPayment.orderId ?? '-' })}</span>
        </div>
      </div>
      <div className="shrink-0 space-y-2">
        <p className="text-center text-sm font-bold text-text-secondary">{t('payment.waitingPayOs')}</p>
        <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
          {pendingPayment.pendingCashAmount === 0 && (
            <button type="button" onClick={onSwitchToCash} disabled={isCancelling} className="min-h-12 rounded-lg border border-brand-orange-border bg-brand-orange-light px-3 text-sm font-bold text-brand-orange hover:bg-brand-orange hover:text-white disabled:opacity-40">
              {t('payment.switchCash')}
            </button>
          )}
          <button type="button" onClick={onCancel} disabled={isCancelling} className="min-h-12 rounded-lg border border-danger/40 bg-white px-3 text-sm font-bold text-danger hover:bg-[var(--pos-danger-soft)] disabled:opacity-40">
            {isCancelling ? t('payment.cancelling') : t('payment.cancelTransaction')}
          </button>
          {pendingPayment.checkoutUrl && (
            <a href={pendingPayment.checkoutUrl} target="_blank" rel="noreferrer" className="flex min-h-12 items-center justify-center rounded-lg border border-border bg-white px-3 text-center text-sm font-bold text-text-primary hover:bg-surface-hover">
              {t('payment.openPayOs')}
            </a>
          )}
          {pendingPayment.qrCode && (
            <button type="button" onClick={printVietQrSlip} className="min-h-12 rounded-lg border border-brand-orange-border bg-white px-3 text-sm font-bold text-brand-orange hover:bg-brand-orange-light">
              {t('payment.printQr')}
            </button>
          )}
        </div>
      </div>
    </div>
  )
}

function CashReturnPanel({
  amount,
  isCancelling,
  onDismiss,
  onConfirm,
}: {
  amount: number
  isCancelling: boolean
  onDismiss: () => void
  onConfirm: () => void
}) {
  const { t } = usePreferences()
  const { formatMoney } = useLocaleFormatters()
  return (
    <div className="mx-auto flex min-h-full w-full max-w-lg flex-col justify-center gap-4">
      <div className="rounded-lg border border-danger/30 bg-white p-5">
        <p className="text-base font-extrabold text-text-primary">{t('payment.refundReceived', { amount: formatMoney(amount) })}</p>
        <p className="mt-2 text-sm leading-6 text-text-secondary">{t('payment.refundInstruction')}</p>
      </div>
      <div className="grid grid-cols-2 gap-2">
        <button type="button" onClick={onDismiss} disabled={isCancelling} className="min-h-14 rounded-lg border border-border bg-white px-4 text-sm font-bold text-text-primary disabled:opacity-40">{t('payment.backToPayment')}</button>
        <button type="button" onClick={onConfirm} disabled={isCancelling} className="min-h-14 rounded-lg bg-danger px-4 text-sm font-extrabold text-white disabled:opacity-40">
          {isCancelling ? t('payment.confirming') : t('payment.refunded')}
        </button>
      </div>
    </div>
  )
}

function PaymentMetric({ label, value, accent = false }: { label: string; value: string; accent?: boolean }) {
  return (
    <div className="min-w-0 rounded-lg border border-border bg-white p-3">
      <span className="block text-xs font-bold text-text-secondary">{label}</span>
      <strong className={`mt-1 block truncate text-xl font-extrabold tabular-nums ${accent ? 'text-brand-orange' : 'text-text-primary'}`} title={value}>{value}</strong>
    </div>
  )
}
