import { useEffect, useState } from 'react'
import {
  fetchBranchInventory,
  type BranchInventoryItem,
  type BranchInventoryItemType,
} from '../services/branchInventoryService'
import { getPosSession } from '../services/posSession'

type FilterChip = '' | BranchInventoryItemType

const FILTERS: { key: FilterChip; label: string }[] = [
  { key: '', label: 'Tất cả' },
  { key: 'Ingredient', label: 'Nguyên liệu' },
  { key: 'Recipe', label: 'Bán thành phẩm' },
]

const PAGE_SIZE = 50

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
  if (itemType === 'Recipe') return 'Bán thành phẩm'
  return itemType
}

const quantityBadgeClass = (status: string): string => {
  if (status === 'Tồn âm') return 'bg-red-100 text-red-700 border-red-200'
  if (status === 'Hết hàng') return 'bg-amber-100 text-amber-800 border-amber-200'
  return 'bg-emerald-50 text-emerald-700 border-emerald-200'
}

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
  const [searchInput, setSearchInput] = useState('')
  const [debouncedSearch, setDebouncedSearch] = useState('')
  const [itemType, setItemType] = useState<FilterChip>('')
  const [page, setPage] = useState(1)
  const [items, setItems] = useState<BranchInventoryItem[]>([])
  const [total, setTotal] = useState(0)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [reloadToken, setReloadToken] = useState(0)

  // Debounce search (async timer callback — not sync setState in effect body)
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
  }, [debouncedSearch, itemType, page, reloadToken])

  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE))

  return (
    <div className="h-full w-full overflow-auto bg-surface p-4 md:p-6">
      <div className="max-w-7xl mx-auto flex flex-col gap-4">
        <header className="flex flex-col sm:flex-row sm:items-end sm:justify-between gap-3">
          <div>
            <h1 className="text-xl font-bold text-text-primary">Kho chi nhánh</h1>
            <p className="text-xs text-text-muted mt-0.5">
              Xem tồn kho hiện tại tại cửa hàng
              {session.storeId ? ` #${session.storeId}` : ''}
              {session.role ? ` · ${session.role}` : ''}
            </p>
            <p className="text-[11px] text-text-muted mt-1">
              Chỉ xem — không chỉnh sửa tồn kho. Ngưỡng tối thiểu sẽ cấu hình ở bước sau.
            </p>
          </div>
        </header>

        <div className="bg-surface-white border border-border rounded-xl p-4 shadow-[var(--shadow-card)] flex flex-col gap-3">
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
                  className={`px-3 py-1.5 text-xs font-semibold rounded-full border transition-colors ${
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
        </div>

        {loading && (
          <div className="bg-surface-white border border-border rounded-xl p-8 text-center text-sm text-text-secondary">
            Đang tải kho chi nhánh...
          </div>
        )}

        {!loading && error && (
          <div className="bg-red-50 border border-red-200 rounded-xl p-4 text-sm text-red-700">
            {error}
          </div>
        )}

        {!loading && !error && items.length === 0 && (
          <div className="bg-surface-white border border-border rounded-xl p-8 text-center text-sm text-text-secondary">
            Không có mặt hàng tồn kho phù hợp.
          </div>
        )}

        {!loading && !error && items.length > 0 && (
          <div className="bg-surface-white border border-border rounded-xl shadow-[var(--shadow-card)] overflow-hidden">
            <div className="overflow-x-auto">
              <table className="w-full text-left text-sm min-w-[880px]">
                <thead className="bg-surface border-b border-border text-xs text-text-muted uppercase tracking-wide">
                  <tr>
                    <th className="px-4 py-3 font-semibold">Tên mặt hàng</th>
                    <th className="px-4 py-3 font-semibold">Loại</th>
                    <th className="px-4 py-3 font-semibold text-right">Tồn</th>
                    <th className="px-4 py-3 font-semibold text-right">Giữ chỗ</th>
                    <th className="px-4 py-3 font-semibold">ĐVT</th>
                    <th className="px-4 py-3 font-semibold">Ngưỡng</th>
                    <th className="px-4 py-3 font-semibold">Trạng thái SL</th>
                    <th className="px-4 py-3 font-semibold">Cập nhật</th>
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
                      </td>
                      <td className="px-4 py-3 text-text-secondary whitespace-nowrap">
                        {itemTypeLabel(item.itemType)}
                      </td>
                      <td className="px-4 py-3 text-right font-semibold tabular-nums">
                        {formatQty(item.availableQty)}
                      </td>
                      <td className="px-4 py-3 text-right text-text-secondary tabular-nums">
                        {formatQty(item.reservedQty)}
                      </td>
                      <td className="px-4 py-3 text-text-secondary">{item.unitName || '—'}</td>
                      <td className="px-4 py-3 text-xs text-text-muted max-w-[160px]">
                        {item.thresholdStatus || 'Chưa cấu hình ngưỡng tối thiểu'}
                      </td>
                      <td className="px-4 py-3">
                        <span
                          className={`inline-flex px-2 py-0.5 rounded-full text-[11px] font-bold border ${quantityBadgeClass(
                            item.quantityStatus
                          )}`}
                        >
                          {item.quantityStatus}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-xs text-text-muted whitespace-nowrap">
                        {formatDateTime(item.lastUpdated)}
                      </td>
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
    </div>
  )
}
