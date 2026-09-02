import { useMemo } from 'react'
import { usePreferences } from '../contexts/PreferencesContext'

interface PaymentResultProps {
  status: 'success' | 'cancel'
}

export default function PaymentResult({ status }: PaymentResultProps) {
  const { t } = usePreferences()
  const content = useMemo(() => {
    if (status === 'success') {
      return {
        title: t('paymentResult.successTitle'),
        message: t('paymentResult.successMessage'),
        tone: 'text-green-700',
        bg: 'bg-green-50',
        border: 'border-green-200',
      }
    }

    return {
      title: t('paymentResult.cancelTitle'),
      message: t('paymentResult.cancelMessage'),
      tone: 'text-danger',
      bg: 'bg-red-50',
      border: 'border-red-100',
    }
  }, [status, t])

  return (
    <div className="min-h-screen w-full bg-surface flex items-center justify-center p-6 font-sans">
      <div className={`w-full max-w-sm rounded-xl border ${content.border} ${content.bg} p-6 text-center`}>
        <div className={`text-base font-extrabold ${content.tone}`}>{content.title}</div>
        <p className="mt-2 text-xs font-semibold text-text-secondary">{content.message}</p>
      </div>
    </div>
  )
}
