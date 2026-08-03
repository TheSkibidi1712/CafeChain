import { useCallback, useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  fetchNotifications,
  markAllNotificationsRead,
  markNotificationRead,
  type StaffNotificationItem,
} from '../services/notificationService'

const PAGE_SIZE = 20

const formatDateTime = (value: string): string => {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value || '—'
  const parts = new Intl.DateTimeFormat('vi-VN', {
    timeZone: 'Asia/Ho_Chi_Minh',
    hourCycle: 'h23',
    hour: '2-digit',
    minute: '2-digit',
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  }).formatToParts(date)
  const valueOf = (type: Intl.DateTimeFormatPartTypes) =>
    parts.find((part) => part.type === type)?.value ?? ''
  return `${valueOf('hour')}:${valueOf('minute')} ${valueOf('day')}/${valueOf('month')}/${valueOf('year')}`
}

function ActiveOtpCard({
  code,
  expiresAtUtc,
  onExpired,
}: {
  code: string
  expiresAtUtc: string
  onExpired: () => void
}) {
  const [now, setNow] = useState(() => Date.now())
  const [copied, setCopied] = useState(false)
  const expiresAt = Date.parse(expiresAtUtc)
  const remainingSeconds = Number.isFinite(expiresAt)
    ? Math.max(0, Math.ceil((expiresAt - now) / 1000))
    : 0

  useEffect(() => {
    if (remainingSeconds <= 0) {
      onExpired()
      return
    }
    const timer = window.setTimeout(() => setNow(Date.now()), 1000)
    return () => window.clearTimeout(timer)
  }, [onExpired, remainingSeconds])

  if (remainingSeconds <= 0) return null
  const minutes = String(Math.floor(remainingSeconds / 60)).padStart(2, '0')
  const seconds = String(remainingSeconds % 60).padStart(2, '0')

  return (
    <div className="mt-3 rounded-lg border border-amber-300 bg-amber-50 p-3">
      <p className="text-[11px] font-bold text-amber-900">Mã OTP còn hiệu lực</p>
      <div className="mt-1 flex flex-wrap items-center gap-3">
        <code className="text-2xl font-extrabold tracking-widest text-amber-950">{code}</code>
        <button
          type="button"
          onClick={async () => {
            try {
              await navigator.clipboard.writeText(code)
              setCopied(true)
            } catch {
              setCopied(false)
            }
          }}
          className="rounded-lg border border-amber-400 bg-white px-3 py-1.5 text-xs font-bold text-amber-900"
        >
          {copied ? 'Đã sao chép' : 'Sao chép mã'}
        </button>
      </div>
      <p className="mt-2 text-[11px] font-semibold text-amber-800">
        Hết hạn sau {minutes}:{seconds} · {formatDateTime(expiresAtUtc)}
      </p>
    </div>
  )
}

export default function Notifications() {
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
      setError(result.error || 'Không tải được thông báo.')
      setLoading(false)
      return
    }
    setItems(result.data.items ?? [])
    setTotal(result.data.total ?? 0)
    setUnreadCount(result.data.unreadCount ?? 0)
    setLoading(false)
    window.dispatchEvent(new CustomEvent('pos-notifications-changed'))
  }, [page])

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
          setError(result.error || 'Không tải được thông báo.')
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
  }, [page])

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
            <h1 className="text-xl font-bold text-text-primary">Thông báo</h1>
            <p className="text-xs text-text-muted mt-0.5">
              Thông báo vận hành, POS, OTP và kho thuộc phạm vi của bạn
              {unreadCount > 0 ? ` · ${unreadCount} chưa đọc` : ''}
            </p>
          </div>
          <button
            type="button"
            disabled={unreadCount === 0 || busyId === -1}
            onClick={() => void onMarkAll()}
            className="px-3 py-2 text-xs font-bold rounded-lg border border-border text-text-secondary hover:border-brand-orange hover:text-brand-orange disabled:opacity-40"
          >
            Đánh dấu tất cả đã đọc
          </button>
        </header>

        {loading && (
          <div className="bg-surface-white border border-border rounded-xl p-8 text-center text-sm text-text-secondary">
            Đang tải thông báo...
          </div>
        )}

        {!loading && error && (
          <div className="bg-red-50 border border-red-200 rounded-xl p-4 text-sm text-red-700">
            {error}
          </div>
        )}

        {!loading && !error && items.length === 0 && (
          <div className="bg-surface-white border border-border rounded-xl p-8 text-center text-sm text-text-secondary">
            Chưa có thông báo
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
                      Chưa đọc
                    </span>
                  )}
                </div>
                <p className="mt-1 text-xs text-text-secondary whitespace-pre-wrap line-clamp-4">
                  {item.body}
                </p>
                {item.activeOtp && (
                  <ActiveOtpCard
                    code={item.activeOtp.code}
                    expiresAtUtc={item.activeOtp.expiresAtUtc}
                    onExpired={load}
                  />
                )}
                <div className="mt-2 flex flex-wrap items-center gap-2 text-[11px] text-text-muted">
                  <span>{formatDateTime(item.createdAt)}</span>
                  {item.emailAttempted && !item.emailSent && (
                    <span className="text-amber-700">
                      Email chưa gửi được, nhưng thông báo đã được ghi nhận trong hệ thống.
                    </span>
                  )}
                  {item.targetUrl && (
                    <button
                      type="button"
                      disabled={busyId === item.notificationId}
                      onClick={() => void onMarkOne(item)}
                      className="text-brand-orange font-semibold"
                    >
                      Mở liên quan →
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
                      Đánh dấu đã đọc
                    </button>
                  </div>
                )}
              </article>
            ))}

            <div className="flex items-center justify-between text-xs text-text-secondary pt-2">
              <span>
                Trang {page}/{totalPages} · Tổng {total}
              </span>
              <div className="flex gap-2">
                <button
                  type="button"
                  disabled={page <= 1}
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                  className="px-3 py-1.5 rounded-lg border border-border disabled:opacity-40"
                >
                  Trước
                </button>
                <button
                  type="button"
                  disabled={page >= totalPages}
                  onClick={() => setPage((p) => p + 1)}
                  className="px-3 py-1.5 rounded-lg border border-border disabled:opacity-40"
                >
                  Sau
                </button>
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}
