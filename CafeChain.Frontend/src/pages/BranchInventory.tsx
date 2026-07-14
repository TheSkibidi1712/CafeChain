import { useEffect, useMemo, useState } from 'react'
import {
  fetchBranchInventory,
  reportShortage,
  type BranchInventoryItem,
  type BranchInventoryItemType,
  type BranchInventoryStockFilter,
} from '../services/branchInventoryService'
import { getPosSession } from '../services/posSession'

type FilterChip = '' | BranchInventoryItemType

const FILTERS: { key: FilterChip; label: string }[] = [
  { key: '', label: 'Tất cả' },
  { key: 'Ingredient', label: 'Nguyên liệu' },
  { key: 'Recipe', label: 'Bán thành phẩm' },
]

const STOCK_FILTERS: { key: BranchInventoryStockFilter; label: string }[] = [
  { key: '', label: 'Tất cả trạng thái' },
  { key: 'OUT', label: 'Hết khả dụng' },
  { key: 'LOW', label: 'Sắp hết' },
  { key: 'NORMAL', label: 'Bình thường' },
  { key: 'UNCONFIGURED', label: 'Chưa đặt ngưỡng' },
]

const PAGE_SIZE = 50

/** Roles that may report shortage (must match backend allow-list). */
const REPORT_ROLES = new Set([
  'Nhân viên bán hàng',
  'Ca trưởng',
  'Quản lý chi nhánh',
])

const formatQty = (value: number): string =>
  new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 3 }).format(value)

const formatDateTime = (value: string): string => {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value || '—'
  return date.toLocaleString('vi-VN', {
    hour: '2-digit',
    minute: '2-digit',
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  })
}

const itemTypeLabel = (itemType: string): string => {
  if (itemType === 'Ingredient') return 'Nguyên liệu'
  if (itemType === 'Recipe' || itemType === 'PreparedItem') return 'Bán thành phẩm'
  return itemType
}

const quantityBadgeClass = (status: string): string => {
  if (status === 'Tồn âm') return 'bg-red-100 text-red-700 border-red-200'
  if (status === 'Hết hàng') return 'bg-amber-100 text-amber-800 border-amber-200'
  return 'bg-emerald-50 text-emerald-700 border-emerald-200'
}

const onHandQty = (item: BranchInventoryItem): number =>
  item.onHandQty ?? item.availableQty

const usableQty = (item: BranchInventoryItem): number =>
  item.usableQty ?? onHandQty(item) - item.reservedQty

const toDisplayError = (message?: string): string => {
  const raw = message?.trim()
  if (!raw) return 'Không tải được kho chi nhánh.'
  if (raw.includes('<!DOCTYPE') || raw.includes('<html')) {
    return 'Backend đang lỗi khi tải kho chi nhánh. Vui lòng thử lại.'
  }
  if (raw.includes('403') || raw.toLowerCase().includes('forbidden')) {
    return 'Tài khoản không có quyền xem kho chi nhánh.'
  }
  return raw.length > 280 ? `${raw.slice(0, 280)}...` : raw
}

export default function BranchInventory() {
  const session = getPosSession()
  const canReport = useMemo(() => REPORT_ROLES.has(session.role), [session.role])

  const [searchInput, setSearchInput] = useState('')
  const [debouncedSearch, setDebouncedSearch] = useState('')
  const [itemType, setItemType] = useState<FilterChip>('')
  const [stockStatus, setStockStatus] = useState<BranchInventoryStockFilter>('')
  const [page, setPage] = useState(1)
  const [items, setItems] = useState<BranchInventoryItem[]>([])
  const [total, setTotal] = useState(0)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [reloadToken, setReloadToken] = useState(0)
  const [banner, setBanner] = useState<{ kind: 'ok' | 'warn'; text: string } | null>(null)

  // Report modal
  const [reportItem, setReportItem] = useState<BranchInventoryItem | null>(null)
  const [reportNote, setReportNote] = useState('')
  const [reportSubmitting, setReportSubmitting] = useState(false)
  const [reportError, setReportError] = useState<string | null>(null)

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setDebouncedSearch(searchInput.trim())
      setPage(1)
    }, 350)
    return () => window.clearTimeout(timer)
  }, [searchInput])

  useEffect(() => {
    let cancelled = false

    void (async () => {
      setLoading(true)
      setError(null)
      const result = await fetchBranchInventory({
        search: debouncedSearch || undefined,
        itemType: itemType || undefined,
        stockStatus: stockStatus || undefined,
        page,
        pageSize: PAGE_SIZE,
      })
      if (cancelled) return

      if (!result.ok || !result.data) {
        setItems([])
        setTotal(0)
        setError(toDisplayError(result.error))
        setLoading(false)
        return
      }

      setItems(result.data.items ?? [])
      setTotal(result.data.total ?? 0)
      setLoading(false)
    })()

    return () => {
      cancelled = true
    }
  }, [debouncedSearch, itemType, stockStatus, page, reloadToken])

  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE))
  const pageOutCount = items.filter((item) => usableQty(item) <= 0).length
  const pageLowCount = items.filter(
    (item) =>
      usableQty(item) > 0 &&
      item.minStockLevel != null &&
      usableQty(item) <= item.minStockLevel
  ).length
  const pageNormalCount = items.filter(
    (item) => item.minStockLevel != null && usableQty(item) > item.minStockLevel
  ).length

  const openReport = (item: BranchInventoryItem) => {
    setReportItem(item)
    setReportNote('')
    setReportError(null)
  }

  const closeReport = () => {
    if (reportSubmitting) return
    setReportItem(null)
    setReportNote('')
    setReportError(null)
  }

  const submitReport = async () => {
    if (!reportItem) return
    const note = reportNote.trim()
    if (note.length < 5) {
      setReportError('Ghi chú phải có ít nhất 5 ký tự.')
      return
    }
    if (note.length > 500) {
      setReportError('Ghi chú không được vượt quá 500 ký tự.')
      return
    }

    setReportSubmitting(true)
    setReportError(null)
    const result = await reportShortage({
      storeInventoryId: reportItem.storeInventoryId,
      note,
    })
    setReportSubmitting(false)

    if (!result.ok) {
      setReportError(result.error || 'Không gửi được báo thiếu hàng.')
      return
    }

    const emailFailed = (result.data?.emailFailedCount ?? 0) > 0
    setBanner({
      kind: emailFailed ? 'warn' : 'ok',
      text: emailFailed
        ? 'Đã ghi nhận yêu cầu. Email có thể chưa gửi được.'
        : result.message ||
          'Đã gửi yêu cầu kiểm tra tồn kho cho Quản lý chi nhánh và Kế toán/kho.',
    })
    setReportItem(null)
    setReportNote('')
  }

  return (
    <div className="h-full w-full overflow-auto bg-surface p-4 md:p-6">
      <div className="max-w-7xl mx-auto flex flex-col gap-4">
        <header className="flex flex-col sm:flex-row sm:items-end sm:justify-between gap-3">
          <div>
            <p className="text-[10px] font-extrabold uppercase text-brand-orange mb-1">Vận hành POS</p>
            <h1 className="text-xl font-bold text-text-primary">Kho chi nhánh</h1>
            <p className="text-xs text-text-muted mt-0.5">
              Xem tồn kho hiện tại tại cửa hàng
              {session.storeId ? ` #${session.storeId}` : ''}
              {session.role ? ` · ${session.role}` : ''}
            </p>
            <p className="text-[11px] text-text-muted mt-1">
              Có thể báo thiếu hàng để Quản lý chi nhánh / Kế toán-kho kiểm tra. Không chỉnh sửa tồn kho tại đây.
            </p>
          </div>
        </header>

        <section className="grid grid-cols-2 lg:grid-cols-4 gap-2" aria-label="Tóm tắt tồn kho trên trang">
          <div className="rounded-lg border border-border bg-surface-white px-4 py-3">
            <p className="text-[10px] font-bold text-text-muted">KẾT QUẢ THEO BỘ LỌC</p>
            <p className="mt-1 text-xl font-extrabold text-text-primary tabular-nums">{total}</p>
          </div>
          <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3">
            <p className="text-[10px] font-bold text-red-600">HẾT KHẢ DỤNG TRÊN TRANG</p>
            <p className="mt-1 text-xl font-extrabold text-red-700 tabular-nums">{pageOutCount}</p>
          </div>
          <div className="rounded-lg border border-amber-200 bg-amber-50 px-4 py-3">
            <p className="text-[10px] font-bold text-amber-700">SẮP HẾT TRÊN TRANG</p>
            <p className="mt-1 text-xl font-extrabold text-amber-800 tabular-nums">{pageLowCount}</p>
          </div>
          <div className="rounded-lg border border-emerald-200 bg-emerald-50 px-4 py-3">
            <p className="text-[10px] font-bold text-emerald-700">BÌNH THƯỜNG TRÊN TRANG</p>
            <p className="mt-1 text-xl font-extrabold text-emerald-800 tabular-nums">{pageNormalCount}</p>
          </div>
        </section>

        {banner && (
          <div
            className={`rounded-lg border px-4 py-3 text-sm ${
              banner.kind === 'ok'
                ? 'bg-emerald-50 border-emerald-200 text-emerald-800'
                : 'bg-amber-50 border-amber-200 text-amber-900'
            }`}
          >
            {banner.text}
            <button
              type="button"
              className="ml-3 text-xs underline"
              onClick={() => setBanner(null)}
            >
              Đóng
            </button>
          </div>
        )}

        <div className="bg-surface-white border border-border rounded-lg p-4 shadow-[var(--shadow-card)] flex flex-col gap-3">
          <div className="flex flex-col md:flex-row gap-3 md:items-center">
            <input
              type="search"
              value={searchInput}
              onChange={(e) => setSearchInput(e.target.value)}
              placeholder="Tìm theo tên nguyên liệu / bán thành phẩm..."
              className="flex-1 rounded-lg border border-border px-3 py-2.5 text-sm outline-none focus:border-brand-orange focus:ring-1 focus:ring-brand-orange"
            />
            <button
              type="button"
              onClick={() => setReloadToken((n) => n + 1)}
              className="px-4 py-2.5 text-xs font-bold rounded-lg bg-brand-orange text-white hover:bg-brand-orange-hover shadow-[var(--shadow-button)]"
            >
              Làm mới
            </button>
          </div>

          <div className="flex flex-wrap gap-2">
            {FILTERS.map((f) => {
              const active = itemType === f.key
              return (
                <button
                  key={f.label}
                  type="button"
                  onClick={() => {
                    setItemType(f.key)
                    setPage(1)
                  }}
                  className={`px-3 py-1.5 text-xs font-semibold rounded-md border transition-colors ${
                    active
                      ? 'bg-brand-orange text-white border-brand-orange'
                      : 'bg-surface text-text-secondary border-border hover:border-brand-orange-border hover:text-brand-orange'
                  }`}
                >
                  {f.label}
                </button>
              )
            })}
          </div>
          <div className="flex flex-wrap gap-2 pt-3 border-t border-border-light">
            {STOCK_FILTERS.map((filter) => {
              const active = stockStatus === filter.key
              return (
                <button
                  key={filter.label}
                  type="button"
                  onClick={() => {
                    setStockStatus(filter.key)
                    setPage(1)
                  }}
                  className={`px-3 py-2 text-xs font-semibold rounded-lg border transition-colors ${
                    active
                      ? 'bg-text-primary text-white border-text-primary'
                      : 'bg-surface-white text-text-secondary border-border hover:border-brand-orange hover:text-brand-orange'
                  }`}
                >
                  {filter.label}
                </button>
              )
            })}
          </div>
        </div>

        {loading && (
          <div className="bg-surface-white border border-border rounded-lg p-8 text-center text-sm text-text-secondary">
            Đang tải kho chi nhánh...
          </div>
        )}

        {!loading && error && (
          <div className="bg-red-50 border border-red-200 rounded-lg p-4 text-sm text-red-700 flex items-center justify-between gap-3">
            <span>{error}</span>
            <button type="button" onClick={() => setReloadToken((n) => n + 1)} className="shrink-0 rounded-lg border border-red-300 px-3 py-2 text-xs font-bold hover:bg-red-100">Thử lại</button>
          </div>
        )}

        {!loading && !error && items.length === 0 && (
          <div className="bg-surface-white border border-border rounded-lg p-8 text-center text-sm text-text-secondary">
            Không có mặt hàng tồn kho phù hợp.
          </div>
        )}

        {!loading && !error && items.length > 0 && (
          <div className="bg-surface-white border border-border rounded-lg shadow-[var(--shadow-card)] overflow-hidden">
            <div className="overflow-x-auto">
              <table className="w-full text-left text-sm min-w-[960px]">
                <thead className="bg-surface border-b border-border text-xs text-text-muted uppercase tracking-wide">
                  <tr>
                    <th className="px-4 py-3 font-semibold">Tên mặt hàng</th>
                    <th className="px-4 py-3 font-semibold">Loại</th>
                    <th className="px-4 py-3 font-semibold text-right">Tồn vật lý</th>
                    <th className="px-4 py-3 font-semibold text-right">Giữ chỗ</th>
                    <th className="px-4 py-3 font-semibold text-right">Khả dụng</th>
                    <th className="px-4 py-3 font-semibold">ĐVT</th>
                    <th className="px-4 py-3 font-semibold">Ngưỡng</th>
                    <th className="px-4 py-3 font-semibold">Trạng thái SL</th>
                    <th className="px-4 py-3 font-semibold">Cập nhật</th>
                    {canReport ? (
                      <th className="px-4 py-3 font-semibold text-right">Thao tác</th>
                    ) : null}
                  </tr>
                </thead>
                <tbody>
                  {items.map((item) => (
                    <tr
                      key={item.storeInventoryId}
                      className="border-b border-border-light last:border-0 hover:bg-surface-hover"
                    >
                      <td className="px-4 py-3">
                        <div className="font-semibold text-text-primary">{item.itemName}</div>
                        {item.itemCode ? (
                          <div className="text-[11px] text-text-muted">{item.itemCode}</div>
                        ) : null}
                        {item.isLegacyUnmapped ? (
                          <div className="mt-1 inline-flex rounded border border-amber-200 bg-amber-50 px-1.5 py-0.5 text-[10px] font-semibold text-amber-800">
                            BTP legacy · Chưa liên kết BTP
                          </div>
                        ) : null}
                        {item.legacyRecipeId && item.preparedItemId ? (
                          <>
                            <div className="mt-1 text-[10px] text-text-muted">Công thức legacy #{item.legacyRecipeId}</div>
                            {item.quantitySemanticsStatus === 'QUANTITY_SEMANTICS_UNKNOWN' ? (
                              <div className="mt-1 inline-flex rounded border border-amber-200 bg-amber-50 px-1.5 py-0.5 text-[10px] font-semibold text-amber-800">
                                Chưa xác nhận đơn vị tồn
                              </div>
                            ) : null}
                          </>
                        ) : null}
                      </td>
                      <td className="px-4 py-3 text-text-secondary whitespace-nowrap">
                        {itemTypeLabel(item.itemType)}
                      </td>
                      <td className="px-4 py-3 text-right font-semibold tabular-nums">
                        {formatQty(onHandQty(item))}
                      </td>
                      <td className="px-4 py-3 text-right text-text-secondary tabular-nums">
                        {formatQty(item.reservedQty)}
                      </td>
                      <td className={`px-4 py-3 text-right font-bold tabular-nums ${usableQty(item) <= 0 ? 'text-red-700' : 'text-text-primary'}`}>
                        {formatQty(usableQty(item))}
                      </td>
                      <td className="px-4 py-3 text-text-secondary">{item.unitName || '—'}</td>
                      <td className="px-4 py-3 text-xs text-text-muted max-w-[160px]">
                        {item.thresholdStatus || 'Chưa cấu hình ngưỡng tối thiểu'}
                      </td>
                      <td className="px-4 py-3">
                        <span
                          className={`inline-flex px-2 py-0.5 rounded-md text-[11px] font-bold border ${quantityBadgeClass(
                            item.quantityStatus
                          )}`}
                        >
                          {item.quantityStatus}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-xs text-text-muted whitespace-nowrap">
                        {formatDateTime(item.lastUpdated)}
                      </td>
                      {canReport ? (
                        <td className="px-4 py-3 text-right">
                          <button
                            type="button"
                            onClick={() => openReport(item)}
                            className="px-2.5 py-1.5 text-[11px] font-bold rounded-lg border border-brand-orange text-brand-orange hover:bg-brand-orange-light"
                          >
                            Báo thiếu hàng
                          </button>
                        </td>
                      ) : null}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <div className="flex items-center justify-between px-4 py-3 border-t border-border text-xs text-text-secondary">
              <span>
                Tổng {total} mặt hàng · Trang {page}/{totalPages} · {PAGE_SIZE}/trang
              </span>
              <div className="flex gap-2">
                <button
                  type="button"
                  disabled={page <= 1}
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                  className="px-3 py-1.5 rounded-lg border border-border disabled:opacity-40 hover:border-brand-orange"
                >
                  Trước
                </button>
                <button
                  type="button"
                  disabled={page >= totalPages}
                  onClick={() => setPage((p) => p + 1)}
                  className="px-3 py-1.5 rounded-lg border border-border disabled:opacity-40 hover:border-brand-orange"
                >
                  Sau
                </button>
              </div>
            </div>
          </div>
        )}
      </div>

      {reportItem && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <div className="w-full max-w-md rounded-lg bg-surface-white border border-border shadow-xl p-5" role="dialog" aria-modal="true" aria-labelledby="shortage-report-title">
            <h2 id="shortage-report-title" className="text-base font-bold text-text-primary">Báo thiếu hàng</h2>
            <p className="text-xs text-text-muted mt-1">
              {reportItem.itemName} · {itemTypeLabel(reportItem.itemType)} · Tồn{' '}
              {formatQty(usableQty(reportItem))} {reportItem.unitName || ''} khả dụng
            </p>

            <label className="block mt-4 text-xs font-semibold text-text-secondary">
              Ghi chú (bắt buộc)
              <textarea
                value={reportNote}
                onChange={(e) => setReportNote(e.target.value)}
                rows={4}
                maxLength={500}
                placeholder="Mô tả tình trạng thiếu hàng / yêu cầu kiểm tra tồn kho..."
                className="mt-1.5 w-full rounded-lg border border-border px-3 py-2 text-sm outline-none focus:border-brand-orange focus:ring-1 focus:ring-brand-orange resize-y"
              />
            </label>
            <p className="text-[10px] text-text-muted mt-1">{reportNote.trim().length}/500</p>

            {reportError && (
              <p className="mt-2 text-xs text-red-600">{reportError}</p>
            )}

            <div className="mt-4 flex justify-end gap-2">
              <button
                type="button"
                disabled={reportSubmitting}
                onClick={closeReport}
                className="px-4 py-2 text-xs font-semibold rounded-lg border border-border text-text-secondary"
              >
                Hủy
              </button>
              <button
                type="button"
                disabled={reportSubmitting}
                onClick={() => void submitReport()}
                className="px-4 py-2 text-xs font-bold rounded-lg bg-brand-orange text-white hover:bg-brand-orange-hover disabled:opacity-50"
              >
                {reportSubmitting ? 'Đang gửi...' : 'Gửi báo cáo'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
