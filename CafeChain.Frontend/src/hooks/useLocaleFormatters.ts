import { useMemo } from 'react'
import { usePreferences } from '../contexts/PreferencesContext'

export function useLocaleFormatters() {
  const { locale } = usePreferences()
  return useMemo(() => {
    const money = new Intl.NumberFormat(locale, { style: 'currency', currency: 'VND', maximumFractionDigits: 0 })
    const number = new Intl.NumberFormat(locale, { maximumFractionDigits: 3 })
    const dateTime = new Intl.DateTimeFormat(locale, { dateStyle: 'short', timeStyle: 'short' })
    return {
      formatMoney: (value: number) => money.format(Math.max(0, value)),
      formatNumber: (value: number) => number.format(value),
      formatDateTime: (value: string | number | Date) => {
        const date = new Date(value)
        return Number.isNaN(date.getTime()) ? String(value) : dateTime.format(date)
      },
    }
  }, [locale])
}
