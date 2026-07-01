import { useState, useEffect } from 'react'
import { apiClient } from '../services/apiClient'
import { syncPendingOrders } from '../services/OfflineSyncService'

// ============================================================
// Issue #69: OrderHistory — Fetch từ API GET /api/v1/pos/orders
// Phân trang server-side + hiển thị chi tiết OrderDetails + Toppings
// ============================================================

const formatVND = (amount: number): string =>
  new Intl.NumberFormat('vi-VN').format(amount) + 'đ'

/** Response shape từ Backend */
interface OrderHistoryItem {
  orderId: number
  clientOrderId?: string
  orderType: string
  createdAt: string
  total: number
  paymentMethod: string
  staffName: string
  orderDetails: {
    drinkName: string
    sizeName?: string
    quantity: number
    price: number
    toppings: string[]
  }[]
}

interface PaginationInfo {
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

interface OrderHistoryApiResponse {
  success: boolean
  message?: string
  data?: {
    items?: OrderHistoryItem[]
    pagination?: PaginationInfo
  }
}

export default function OrderHistory() {
  const [orders, setOrders] = useState<OrderHistoryItem[]>([])
  const [pagination, setPagination] = useState<PaginationInfo>({ page: 1, pageSize: 20, totalCount: 0, totalPages: 1 })
  const [isLoading, setIsLoading] = useState(true)
  const [isSyncing, setIsSyncing] = useState(false)
  const [syncResult, setSyncResult] = useState<string | null>(null)

  // ── Fetch order history từ Backend API ──
  const fetchOrders = async (page: number) => {
    setIsLoading(true)
    try {
      const res = await apiClient.get<OrderHistoryApiResponse>(`/api/v1/pos/orders?page=${page}&pageSize=20`)
      if (res.ok && res.data?.success && res.data?.data) {
        setOrders(res.data.data.items ?? [])
        setPagination(res.data.data.pagination ?? { page: 1, pageSize: 20, totalCount: 0, totalPages: 1 })
      } else {
        console.warn('[OrderHistory] API failed:', res.data?.message || res.error)
        setOrders([])
      }
    } catch (err) {
      console.error('[OrderHistory] Fetch error:', err)
      setOrders([])
    } finally {
      setIsLoading(false)
    }
  }

  useEffect(() => {
    queueMicrotask(() => {
      void fetchOrders(1)
    })
  }, [])

  const handlePageChange = (newPage: number) => {
    if (newPage < 1 || newPage > pagination.totalPages) return
    setPagination(prev => ({ ...prev, page: newPage }))
    fetchOrders(newPage)
  }

  const handleManualSync = async () => {
    setIsSyncing(true)
    setSyncResult(null)
    try {
      const count = await syncPendingOrders()
      setSyncResult(`Đồng bộ thành công ${count} đơn hàng!`)
      // Refresh data sau khi sync
      await fetchOrders(pagination.page)
      setTimeout(() => setSyncResult(null), 4000)
    } catch (error) {
      setSyncResult(`Đồng bộ thất bại: ${error instanceof Error ? error.message : String(error)}`)
    } finally {
      setIsSyncing(false)
    }
  }

  return (
    <div className="h-full w-full overflow-y-auto bg-surface p-6 font-sans select-none">
      <div className="max-w-6xl mx-auto space-y-6">
        {/* Header */}
        <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4 bg-surface-white p-5 rounded-2xl border border-border shadow-[var(--shadow-card)]">
          <div>
            <h1 className="text-base font-bold text-text-primary">Lịch sử đơn hàng</h1>
            <p className="text-[11px] text-text-secondary mt-1">
              {pagination.totalCount > 0
                ? `Tổng ${pagination.totalCount} đơn hàng — Trang ${pagination.page}/${pagination.totalPages}`
                : 'Xem và quản lý tất cả đơn hàng đã bán.'}
            </p>
          </div>
          <button
            onClick={handleManualSync}
            disabled={isSyncing}
            className="px-4 py-2.5 bg-brand-orange text-white text-xs font-bold rounded-lg cursor-pointer hover:bg-brand-orange-hover active:scale-95 disabled:opacity-50 disabled:cursor-not-allowed transition-all shadow-[var(--shadow-button)] flex items-center gap-2"
          >
            {isSyncing ? (
              <>
                <svg className="animate-spin h-3.5 w-3.5 text-white" fill="none" viewBox="0 0 24 24">
                  <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                  <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
                </svg>
                Đang đồng bộ...
              </>
            ) : (
              '🔄 Đồng bộ thủ công'
            )}
          </button>
        </div>

        {syncResult && (
          <div className={`p-4 rounded-xl border text-xs font-bold ${
            syncResult.includes('thành công')
              ? 'bg-green-50 border-green-200 text-green-700'
              : 'bg-red-50 border-red-200 text-red-700'
          }`}>
            {syncResult}
          </div>
        )}

        {/* Orders Table */}
        <div className="bg-surface-white rounded-2xl border border-border shadow-[var(--shadow-card)] overflow-hidden">
          {isLoading ? (
            <div className="p-16 flex flex-col items-center justify-center text-text-muted">
              <svg className="animate-spin h-8 w-8 text-brand-orange mb-3" fill="none" viewBox="0 0 24 24">
                <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
              </svg>
              <p className="text-xs font-semibold">Đang tải lịch sử đơn hàng...</p>
            </div>
          ) : orders.length === 0 ? (
            <div className="p-16 flex flex-col items-center justify-center text-text-muted">
              <span className="text-4xl mb-3 opacity-30">📜</span>
              <p className="text-xs font-semibold">Chưa có đơn hàng nào</p>
              <p className="text-[10px] opacity-60 mt-1">Các đơn hàng bán từ POS sẽ xuất hiện tại đây</p>
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-left border-collapse">
                <thead>
                  <tr className="bg-surface border-b border-border text-[10px] font-bold text-text-secondary uppercase tracking-wider">
                    <th className="px-6 py-4">Mã đơn</th>
                    <th className="px-6 py-4">Thời gian</th>
                    <th className="px-6 py-4">Loại</th>
                    <th className="px-6 py-4">Sản phẩm</th>
                    <th className="px-6 py-4">Thanh toán</th>
                    <th className="px-6 py-4">Nhân viên</th>
                    <th className="px-6 py-4">Tổng tiền</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border text-xs text-text-primary">
                  {orders.map((order) => (
                    <tr key={order.orderId} className="hover:bg-brand-orange-light/20 transition-colors">
                      <td className="px-6 py-4 font-mono font-medium text-text-secondary">
                        #{order.orderId}
                        {order.clientOrderId && (
                          <p className="text-[9px] opacity-50 truncate max-w-[100px]" title={order.clientOrderId}>
                            {order.clientOrderId.substring(0, 8)}...
                          </p>
                        )}
                      </td>
                      <td className="px-6 py-4 text-text-secondary">
                        {new Date(order.createdAt).toLocaleString('vi-VN')}
                      </td>
                      <td className="px-6 py-4">
                        <span className="px-2 py-0.5 rounded-full text-[10px] font-bold bg-blue-50 text-blue-700 border border-blue-200">
                          {order.orderType}
                        </span>
                      </td>
                      <td className="px-6 py-4 max-w-[250px]">
                        <div className="space-y-0.5">
                          {order.orderDetails.map((detail, idx) => (
                            <p key={idx} className="truncate text-text-secondary">
                              <span className="font-bold text-brand-orange">{detail.quantity}x</span>{' '}
                              {detail.drinkName}
                              {detail.sizeName && <span className="text-[10px] opacity-60"> ({detail.sizeName})</span>}
                              {detail.toppings.length > 0 && (
                                <span className="text-[9px] opacity-50"> +{detail.toppings.join(', ')}</span>
                              )}
                            </p>
                          ))}
                        </div>
                      </td>
                      <td className="px-6 py-4">
                        <span className="capitalize font-semibold text-text-secondary">
                          {order.paymentMethod}
                        </span>
                      </td>
                      <td className="px-6 py-4 text-text-secondary text-[11px]">
                        {order.staffName}
                      </td>
                      <td className="px-6 py-4 font-bold text-text-primary">
                        {formatVND(order.total)}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>

        {/* Pagination Controls */}
        {pagination.totalPages > 1 && (
          <div className="flex items-center justify-center gap-2">
            <button
              onClick={() => handlePageChange(pagination.page - 1)}
              disabled={pagination.page <= 1}
              className="px-3 py-1.5 text-xs font-bold rounded-lg border border-border bg-surface-white hover:bg-surface disabled:opacity-30 disabled:cursor-not-allowed transition-all"
            >
              ← Trước
            </button>
            {Array.from({ length: Math.min(pagination.totalPages, 7) }, (_, i) => {
              const pageNum = i + 1
              return (
                <button
                  key={pageNum}
                  onClick={() => handlePageChange(pageNum)}
                  className={`px-3 py-1.5 text-xs font-bold rounded-lg border transition-all ${
                    pageNum === pagination.page
                      ? 'bg-brand-orange text-white border-brand-orange'
                      : 'border-border bg-surface-white hover:bg-surface'
                  }`}
                >
                  {pageNum}
                </button>
              )
            })}
            {pagination.totalPages > 7 && (
              <span className="text-xs text-text-muted">...</span>
            )}
            <button
              onClick={() => handlePageChange(pagination.page + 1)}
              disabled={pagination.page >= pagination.totalPages}
              className="px-3 py-1.5 text-xs font-bold rounded-lg border border-border bg-surface-white hover:bg-surface disabled:opacity-30 disabled:cursor-not-allowed transition-all"
            >
              Sau →
            </button>
          </div>
        )}
      </div>
    </div>
  )
}
