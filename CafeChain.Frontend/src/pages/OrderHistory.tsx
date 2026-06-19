import { useState } from 'react'
import { useLiveQuery } from 'dexie-react-hooks'
import { db } from '../db/CafeChainPOSDB'
import { syncPendingOrders } from '../services/OfflineSyncService'

const formatVND = (amount: number): string =>
  new Intl.NumberFormat('vi-VN').format(amount) + 'đ'

export default function OrderHistory() {
  const [isSyncing, setIsSyncing] = useState(false)
  const [syncResult, setSyncResult] = useState<string | null>(null)

  const orders = useLiveQuery(
    () => db.cartSyncQueue.orderBy('createdAt').reverse().toArray(),
    []
  )

  const handleManualSync = async () => {
    setIsSyncing(true)
    setSyncResult(null)
    try {
      const count = await syncPendingOrders()
      setSyncResult(`Đồng bộ thành công ${count} đơn hàng!`)
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
              Xem và quản lý tất cả đơn hàng đã bán trên thiết bị này (Offline & Online).
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

        {/* Orders Table/List */}
        <div className="bg-surface-white rounded-2xl border border-border shadow-[var(--shadow-card)] overflow-hidden">
          {!orders || orders.length === 0 ? (
            <div className="p-16 flex flex-col items-center justify-center text-text-muted">
              <span className="text-4xl mb-3 opacity-30">📜</span>
              <p className="text-xs font-semibold">Chưa có đơn hàng nào được tạo</p>
              <p className="text-[10px] opacity-60 mt-1">Các đơn hàng bán từ POS sẽ xuất hiện tại đây</p>
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-left border-collapse">
                <thead>
                  <tr className="bg-surface border-b border-border text-[10px] font-bold text-text-secondary uppercase tracking-wider">
                    <th className="px-6 py-4">Mã đơn hàng</th>
                    <th className="px-6 py-4">Thời gian</th>
                    <th className="px-6 py-4">Sản phẩm</th>
                    <th className="px-6 py-4">Thanh toán</th>
                    <th className="px-6 py-4">Tổng tiền</th>
                    <th className="px-6 py-4">Đồng bộ</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border text-xs text-text-primary">
                  {orders.map((order) => {
                    const statusClass = (status: typeof order.syncStatus) => {
                      switch (status) {
                        case 'Synced':
                          return 'bg-green-50 text-green-700 border-green-200'
                        case 'Pending':
                          return 'bg-orange-50 text-brand-orange border-brand-orange-border'
                        case 'Syncing':
                          return 'bg-blue-50 text-blue-700 border-blue-200'
                        case 'Failed':
                          return 'bg-red-50 text-danger border-danger/30'
                        default:
                          return 'bg-gray-50 text-gray-700'
                      }
                    }

                    return (
                      <tr key={order.queueId} className="hover:bg-brand-orange-light/20 transition-colors">
                        <td className="px-6 py-4 font-mono font-medium text-text-secondary max-w-[120px] truncate" title={order.clientOrderId}>
                          {order.clientOrderId.substring(0, 8)}...
                        </td>
                        <td className="px-6 py-4 text-text-secondary">
                          {new Date(order.createdAt).toLocaleString('vi-VN')}
                        </td>
                        <td className="px-6 py-4 max-w-[250px]">
                          <div className="space-y-0.5">
                            {order.items.map((it, idx) => (
                              <p key={idx} className="truncate text-text-secondary">
                                <span className="font-bold text-brand-orange">{it.quantity}x</span> {it.name}
                              </p>
                            ))}
                          </div>
                        </td>
                        <td className="px-6 py-4">
                          <span className="capitalize font-semibold text-text-secondary">
                            {order.paymentMethod === 'cash' ? '💵 Tiền mặt' : '📱 Chuyển khoản'}
                          </span>
                        </td>
                        <td className="px-6 py-4 font-bold text-text-primary">
                          {formatVND(order.totalAmount)}
                        </td>
                        <td className="px-6 py-4">
                          <span className={`px-2.5 py-1 rounded-full text-[10px] font-bold border ${statusClass(order.syncStatus)}`}>
                            {order.syncStatus}
                          </span>
                          {order.lastError && order.syncStatus === 'Failed' && (
                            <p className="text-[9px] text-danger mt-1 max-w-[150px] truncate" title={order.lastError}>
                              Lỗi: {order.lastError}
                            </p>
                          )}
                        </td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}
