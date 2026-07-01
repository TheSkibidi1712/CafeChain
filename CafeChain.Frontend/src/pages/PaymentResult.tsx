import { useMemo } from 'react'

interface PaymentResultProps {
  status: 'success' | 'cancel'
}

export default function PaymentResult({ status }: PaymentResultProps) {
  const content = useMemo(() => {
    if (status === 'success') {
      return {
        title: 'Thanh toán đã ghi nhận',
        message: 'Bạn có thể quay lại màn hình POS.',
        tone: 'text-green-700',
        bg: 'bg-green-50',
        border: 'border-green-200',
      }
    }

    return {
      title: 'Thanh toán đã hủy',
      message: 'Giao dịch PayOS đã được đóng.',
      tone: 'text-danger',
      bg: 'bg-red-50',
      border: 'border-red-100',
    }
  }, [status])

  return (
    <div className="min-h-screen w-full bg-surface flex items-center justify-center p-6 font-sans">
      <div className={`w-full max-w-sm rounded-xl border ${content.border} ${content.bg} p-6 text-center`}>
        <div className={`text-base font-extrabold ${content.tone}`}>{content.title}</div>
        <p className="mt-2 text-xs font-semibold text-text-secondary">{content.message}</p>
      </div>
    </div>
  )
}
