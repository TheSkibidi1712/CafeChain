import { useCallback, useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import VerificationCodeInput from '../components/VerificationCodeInput'
import { usePreferences } from '../contexts/PreferencesContext'
import { useLocaleFormatters } from '../hooks/useLocaleFormatters'
import {
  fetchNotifications,
  confirmTerminalFromNotification,
  markAllNotificationsRead,
  markNotificationRead,
  revealTerminalOtp,
  type OperationalOtpNotification,
  type StaffNotificationItem,
} from '../services/notificationService'

const PAGE_SIZE = 20

function OperationalOtpCard({
  notificationId,
  otp,
  onChanged,
}: {
  notificationId: number
  otp: OperationalOtpNotification
  onChanged: () => void
}) {
  const { t } = usePreferences()
  const { formatDateTime } = useLocaleFormatters()
  const [remainingSeconds, setRemainingSeconds] = useState(() => Math.max(0, otp.remainingSeconds))
  const [revealedCode, setRevealedCode] = useState('')
  const [enteredCode, setEnteredCode] = useState('')
  const [busyAction, setBusyAction] = useState<'reveal' | 'confirm' | 'copy' | null>(null)
  const [message, setMessage] = useState<string | null>(null)

  useEffect(() => {
    if (otp.status !== 'Waiting' || remainingSeconds <= 0) return
    const timer = window.setTimeout(
      () => setRemainingSeconds((value) => Math.max(0, value - 1)),
      1000
    )
    return () => window.clearTimeout(timer)
  }, [otp.status, remainingSeconds])

  const minutes = String(Math.floor(remainingSeconds / 60)).padStart(2, '0')
  const seconds = String(remainingSeconds % 60).padStart(2, '0')
  const effectiveStatus = otp.status === 'Waiting' && remainingSeconds <= 0 ? 'Expired' : otp.status
  const statusLabel = effectiveStatus === 'Waiting'
    ? t('notifications.status.waiting')
    : effectiveStatus === 'Expired'
      ? t('notifications.status.expired')
      : effectiveStatus === 'Confirmed'
        ? t('notifications.status.confirmed')
        : effectiveStatus

  return (
    <div className="mt-3 rounded-lg border border-amber-300 bg-amber-50 p-3">
      <div className="grid gap-1 text-[11px] text-amber-950 sm:grid-cols-2">
        <span><strong>{t('notifications.terminal')}:</strong> {otp.terminalName}</span>
        <span><strong>{t('notifications.branch')}:</strong> {otp.storeName}</span>
        <span><strong>{t('notifications.sender')}:</strong> {otp.requestedByName}</span>
        <span><strong>{t('notifications.approver')}:</strong> {otp.confirmedByName || otp.approverName}</span>
        <span><strong>{t('notifications.sentAt')}:</strong> {formatDateTime(otp.sentAtUtc)}</span>
        <span><strong>{t('notifications.expiresAt')}:</strong> {formatDateTime(otp.expiresAtUtc)}</span>
        <span><strong>{t('notifications.status')}:</strong> {statusLabel}</span>
      </div>
      {effectiveStatus === 'Waiting' && (
        <>
          <p className="mt-2 text-[11px] font-semibold text-amber-800">
            {t('notifications.otpRemaining', { minutes, seconds })}
          </p>
          <div className="mt-2 flex flex-wrap items-center gap-2">
            {revealedCode ? (
              <code className="text-xl font-extrabold tracking-widest text-amber-950">{revealedCode}</code>
            ) : (
              <button
                type="button"
                disabled={busyAction !== null || !otp.canRevealOtp}
                onClick={() => void (async () => {
                  setBusyAction('reveal')
                  setMessage(null)
                  try {
                    const result = await revealTerminalOtp(notificationId)
                    if (result.ok && result.data) setRevealedCode(result.data.code.trim())
                    else setMessage(result.error || t('notifications.revealError'))
                  } finally {
                    setBusyAction(null)
                  }
                })()}
                className="rounded-lg border border-amber-400 bg-white px-3 py-1.5 text-xs font-bold text-amber-900 disabled:opacity-50"
              >
                {busyAction === 'reveal' ? t('notifications.revealing') : t('notifications.revealOtp')}
              </button>
            )}
            {revealedCode && (
              <button type="button" disabled={busyAction !== null}
                onClick={() => void (async () => {
                  setBusyAction('copy')
                  try {
                    await navigator.clipboard.writeText(revealedCode.trim())
                    setMessage(t('notifications.copiedOtp'))
                  } catch {
                    setMessage(t('notifications.copyError'))
                  } finally {
                    setBusyAction(null)
                  }
                })()}
                className="rounded-lg border border-amber-400 bg-white px-3 py-1.5 text-xs font-bold text-amber-900 disabled:opacity-50">
                {t('notifications.copyOtp')}
              </button>
            )}
          </div>
          {otp.canContinueTerminalConfirmation && (
            <div className="mt-3 flex flex-wrap items-center gap-2">
              <div className="w-full sm:w-72">
                <VerificationCodeInput value={enteredCode} onChange={setEnteredCode}
                  mode="otp" label={t('notifications.confirmOtpLabel')} disabled={busyAction !== null} />
              </div>
              <button
                type="button"
                disabled={busyAction !== null || enteredCode.length !== 6}
                onClick={() => void (async () => {
                  setBusyAction('confirm')
                  setMessage(null)
                  try {
                    const requestKey = globalThis.crypto?.randomUUID?.() ?? `${Date.now()}`
                    const result = await confirmTerminalFromNotification(notificationId, enteredCode, requestKey)
                    if (result.ok) onChanged()
                    else setMessage(result.error || t('notifications.confirmError'))
                  } finally {
                    setBusyAction(null)
                  }
                })()}
                className="rounded-lg bg-amber-800 px-3 py-2 text-xs font-bold text-white disabled:opacity-50"
              >
                {busyAction === 'confirm' ? t('notifications.confirmingTerminal') : t('notifications.confirmTerminal')}
              </button>
            </div>
          )}
        </>
      )}
      {effectiveStatus === 'Expired' && (
        <p className="mt-2 text-xs font-semibold text-red-700">{t('notifications.otpExpired')}</p>
      )}
      {message && <p className="mt-2 text-xs text-red-700">{message}</p>}
    </div>
  )
}

export default function Notifications() {
  const { t } = usePreferences()
  const { formatDateTime } = useLocaleFormatters()
  const navigate = useNavigate()
  const [items, setItems] = useState<StaffNotificationItem[]>([])
  const [page, setPage] = useState(1)
  const [total, setTotal] = useState(0)
  const [unreadCount, setUnreadCount] = useState(0)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [busyId, setBusyId] = useState<number | null>(null)

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    const result = await fetchNotifications({ page, pageSize: PAGE_SIZE })
    if (!result.ok || !result.data) {
      setItems([])
      setTotal(0)
      setUnreadCount(0)
      setError(result.error || t('notifications.loadError'))
      setLoading(false)
      return
    }
    setItems(result.data.items ?? [])
    setTotal(result.data.total ?? 0)
    setUnreadCount(result.data.unreadCount ?? 0)
    setLoading(false)
    window.dispatchEvent(new CustomEvent('pos-notifications-changed'))
  }, [page, t])

  useEffect(() => {
    let cancelled = false
    const timer = window.setTimeout(() => {
      void (async () => {
        setLoading(true)
        setError(null)
        const result = await fetchNotifications({ page, pageSize: PAGE_SIZE })
        if (cancelled) return
        if (!result.ok || !result.data) {
          setItems([])
          setTotal(0)
          setUnreadCount(0)
          setError(result.error || t('notifications.loadError'))
          setLoading(false)
          return
        }
        setItems(result.data.items ?? [])
        setTotal(result.data.total ?? 0)
        setUnreadCount(result.data.unreadCount ?? 0)
        setLoading(false)
        window.dispatchEvent(new CustomEvent('pos-notifications-changed'))
      })()
    }, 0)
    return () => {
      cancelled = true
      window.clearTimeout(timer)
    }
  }, [page, t])

  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE))

  const onMarkOne = async (item: StaffNotificationItem) => {
    setBusyId(item.notificationId)
    if (!item.isRead) {
      await markNotificationRead(item.notificationId)
    }
    setBusyId(null)
    if (item.targetUrl) {
      navigate(item.targetUrl)
    } else {
      await load()
    }
    window.dispatchEvent(new CustomEvent('pos-notifications-changed'))
  }

  const onMarkAll = async () => {
    setBusyId(-1)
    await markAllNotificationsRead()
    setBusyId(null)
    await load()
  }

  return (
    <div className="h-full w-full overflow-auto bg-surface p-4 md:p-6">
      <div className="max-w-3xl mx-auto flex flex-col gap-4">
        <header className="flex flex-wrap items-end justify-between gap-3">
          <div>
            <h1 className="text-xl font-bold text-text-primary">{t('notifications.title')}</h1>
            <p className="text-xs text-text-muted mt-0.5">
              {t('notifications.description')}
              {unreadCount > 0 ? ` · ${t('notifications.unreadCount', { count: unreadCount })}` : ''}
            </p>
          </div>
          <button
            type="button"
            disabled={unreadCount === 0 || busyId === -1}
            onClick={() => void onMarkAll()}
            className="px-3 py-2 text-xs font-bold rounded-lg border border-border text-text-secondary hover:border-brand-orange hover:text-brand-orange disabled:opacity-40"
          >
            {t('notifications.markAllRead')}
          </button>
        </header>

        {loading && (
          <div className="bg-surface-white border border-border rounded-xl p-8 text-center text-sm text-text-secondary">
            {t('notifications.loading')}
          </div>
        )}

        {!loading && error && (
          <div className="bg-red-50 border border-red-200 rounded-xl p-4 text-sm text-red-700">
            {error}
          </div>
        )}

        {!loading && !error && items.length === 0 && (
          <div className="bg-surface-white border border-border rounded-xl p-8 text-center text-sm text-text-secondary">
            {t('notifications.empty')}
          </div>
        )}

        {!loading && !error && items.length > 0 && (
          <div className="flex flex-col gap-2">
            {items.map((item) => (
              <article
                key={item.notificationId}
                className={`text-left rounded-xl border p-4 shadow-[var(--shadow-card)] transition-colors ${
                  item.isRead
                    ? 'bg-surface-white border-border'
                    : 'bg-brand-orange-light border-brand-orange-border'
                }`}
              >
                <div className="flex items-start justify-between gap-2">
                  <div className="font-semibold text-sm text-text-primary">{item.title}</div>
                  {!item.isRead && (
                    <span className="shrink-0 text-[10px] font-bold px-2 py-0.5 rounded-full bg-danger text-white">
                      {t('notifications.unread')}
                    </span>
                  )}
                </div>
                <p className="mt-1 text-xs text-text-secondary whitespace-pre-wrap line-clamp-4">
                  {item.body}
                </p>
                {item.operationalOtp && (
                  <OperationalOtpCard
                    key={`${item.notificationId}:${item.operationalOtp.challengePublicId}:${item.operationalOtp.status}:${item.operationalOtp.remainingSeconds}`}
                    notificationId={item.notificationId}
                    otp={item.operationalOtp}
                    onChanged={load}
                  />
                )}
                <div className="mt-2 flex flex-wrap items-center gap-2 text-[11px] text-text-muted">
                  <span>{formatDateTime(item.createdAt)}</span>
                  {item.emailAttempted && !item.emailSent && (
                    <span className="text-amber-700">
                      {t('notifications.emailFailed')}
                    </span>
                  )}
                  {item.targetUrl && (
                    <button
                      type="button"
                      disabled={busyId === item.notificationId}
                      onClick={() => void onMarkOne(item)}
                      className="text-brand-orange font-semibold"
                    >
                      {t('notifications.openRelated')}
                    </button>
                  )}
                </div>
                {!item.isRead && (
                  <div className="mt-2">
                    <button
                      type="button"
                      disabled={busyId === item.notificationId}
                      onClick={() => void onMarkOne(item)}
                      className="text-[11px] font-bold text-brand-orange disabled:opacity-40"
                    >
                      {t('notifications.markRead')}
                    </button>
                  </div>
                )}
              </article>
            ))}

            <div className="flex items-center justify-between text-xs text-text-secondary pt-2">
              <span>
                {t('notifications.page', { page, pages: totalPages, total })}
              </span>
              <div className="flex gap-2">
                <button
                  type="button"
                  disabled={page <= 1}
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                  className="px-3 py-1.5 rounded-lg border border-border disabled:opacity-40"
                >
                  {t('notifications.previous')}
                </button>
                <button
                  type="button"
                  disabled={page >= totalPages}
                  onClick={() => setPage((p) => p + 1)}
                  className="px-3 py-1.5 rounded-lg border border-border disabled:opacity-40"
                >
                  {t('notifications.next')}
                </button>
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}
