import { useState } from 'react'

const formatVND = (amount: number): string =>
  new Intl.NumberFormat('vi-VN').format(amount) + 'đ'

export default function ShiftSummary() {
  const [startingCash] = useState(2000000)
  const [actualEndingCash, setActualEndingCash] = useState<number | ''>('')
  const [shiftStatus, setShiftStatus] = useState<'open' | 'closed'>('open')

  const expectedEndingCash = startingCash + 4500000 // mock additional cash sales

  const handleCloseShift = (e: React.FormEvent) => {
    e.preventDefault()
    if (actualEndingCash === '') return
    setShiftStatus('closed')
  }

  return (
    <div className="h-full w-full overflow-y-auto bg-surface p-6 font-sans select-none">
      <div className="max-w-4xl mx-auto space-y-6">
        {/* Header */}
        <div className="bg-surface-white p-5 rounded-2xl border border-border shadow-[var(--shadow-card)] flex justify-between items-center">
          <div>
            <h1 className="text-base font-bold text-text-primary">Quản lý phiên két tiền (WorkShift)</h1>
            <p className="text-[11px] text-text-secondary mt-1">
              Ghi nhận các giao dịch tiền mặt và đối soát số dư két khi kết thúc ca làm việc.
            </p>
          </div>
          <span className={`px-3 py-1 rounded-full text-xs font-bold border ${
            shiftStatus === 'open'
              ? 'bg-green-50 text-green-700 border-green-200'
              : 'bg-gray-50 text-gray-700 border-gray-200'
          }`}>
            {shiftStatus === 'open' ? '🟢 Đang mở' : '🔴 Đã đóng'}
          </span>
        </div>

        {/* Shift Details */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          <div className="bg-surface-white p-4 rounded-xl border border-border shadow-[var(--shadow-card)]">
            <p className="text-[10px] font-bold text-text-secondary uppercase">Tiền két đầu ca</p>
            <p className="text-lg font-bold text-text-primary mt-1">{formatVND(startingCash)}</p>
          </div>
          <div className="bg-surface-white p-4 rounded-xl border border-border shadow-[var(--shadow-card)]">
            <p className="text-[10px] font-bold text-text-secondary uppercase">Doanh thu dự kiến</p>
            <p className="text-lg font-bold text-brand-orange mt-1">{formatVND(4500000)}</p>
          </div>
          <div className="bg-surface-white p-4 rounded-xl border border-border shadow-[var(--shadow-card)]">
            <p className="text-[10px] font-bold text-text-secondary uppercase">Tiền két hệ thống tính</p>
            <p className="text-lg font-bold text-text-primary mt-1">{formatVND(expectedEndingCash)}</p>
          </div>
        </div>

        {/* Action Form */}
        {shiftStatus === 'open' ? (
          <form onSubmit={handleCloseShift} className="bg-surface-white p-5 rounded-xl border border-border shadow-[var(--shadow-card)] space-y-4">
            <h2 className="text-xs font-bold text-text-primary uppercase tracking-wider">Kết ca & Bàn giao két</h2>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div>
                <label className="block text-xs font-semibold text-text-secondary mb-1">
                  Nhập số tiền thực tế đếm được trong két:
                </label>
                <div className="relative">
                  <input
                    type="number"
                    value={actualEndingCash}
                    onChange={(e) => setActualEndingCash(e.target.value === '' ? '' : Number(e.target.value))}
                    placeholder="Ví dụ: 6500000"
                    required
                    className="w-full px-3 py-2 border border-border rounded-lg text-xs outline-none focus:border-brand-orange text-text-primary bg-surface font-semibold"
                  />
                  <span className="absolute right-3 top-2 text-xs text-text-secondary font-bold">VNĐ</span>
                </div>
              </div>
            </div>

            <button
              type="submit"
              className="px-4 py-2.5 bg-brand-orange text-white text-xs font-bold rounded-lg cursor-pointer hover:bg-brand-orange-hover active:scale-95 transition-all shadow-[var(--shadow-button)]"
            >
              🔒 Chốt số dư & Đóng ca
            </button>
          </form>
        ) : (
          <div className="bg-surface-white p-5 rounded-xl border border-border shadow-[var(--shadow-card)] space-y-3">
            <h2 className="text-xs font-bold text-text-primary uppercase tracking-wider">Kết quả đối soát két tiền</h2>
            <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 text-xs">
              <div>
                <p className="text-text-secondary">Thực tế bàn giao:</p>
                <p className="font-bold text-text-primary mt-1">{formatVND(Number(actualEndingCash))}</p>
              </div>
              <div>
                <p className="text-text-secondary">Hệ thống tính:</p>
                <p className="font-bold text-text-primary mt-1">{formatVND(expectedEndingCash)}</p>
              </div>
              <div>
                <p className="text-text-secondary">Chênh lệch két:</p>
                {Number(actualEndingCash) - expectedEndingCash >= 0 ? (
                  <p className="font-bold text-green-700 mt-1">
                    +{formatVND(Number(actualEndingCash) - expectedEndingCash)} (Khớp két)
                  </p>
                ) : (
                  <p className="font-bold text-danger mt-1">
                    {formatVND(Number(actualEndingCash) - expectedEndingCash)} (Hụt tiền)
                  </p>
                )}
              </div>
            </div>
            <div className="pt-2 border-t border-border">
              <button
                type="button"
                onClick={() => {
                  setShiftStatus('open')
                  setActualEndingCash('')
                }}
                className="px-4 py-2 border border-brand-orange text-brand-orange text-xs font-bold rounded-lg cursor-pointer hover:bg-brand-orange-light transition-all"
              >
                🔓 Mở lại ca mới
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}
