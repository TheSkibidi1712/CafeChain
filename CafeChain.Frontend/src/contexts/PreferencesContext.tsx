/* eslint-disable react-refresh/only-export-components -- provider and its typed hook intentionally share one module */
import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { translate, type MessageValues } from '../i18n/catalog'
import type { MessageKey } from '../i18n/locales/vi'

export type LocalePreference = 'vi-VN' | 'en-US'

const LOCALE_KEY = 'cafechain.locale'
type PreferencesValue = { locale: LocalePreference; setLocale: (value: LocalePreference) => void; t: (key: MessageKey, values?: MessageValues) => string }
const PreferencesContext = createContext<PreferencesValue | null>(null)

function readPreference(key: string): string | null {
  try { return localStorage.getItem(key) } catch { return null }
}
function storePreference(key: string, value: string): void {
  try { localStorage.setItem(key, value) } catch { /* The UI still works for this session. */ }
}
function initialLocale(): LocalePreference { return readPreference(LOCALE_KEY) === 'en-US' ? 'en-US' : 'vi-VN' }

export function PreferencesProvider({ children }: { children: ReactNode }) {
  const [locale, setLocale] = useState<LocalePreference>(initialLocale)
  useEffect(() => {
    document.documentElement.dataset.themePreference = 'light'
    document.documentElement.dataset.theme = 'light'
    document.documentElement.style.colorScheme = 'light'
    try { localStorage.removeItem('cafechain.theme') } catch { /* Storage can be unavailable. */ }
  }, [])
  useEffect(() => {
    document.documentElement.lang = locale
    document.documentElement.dataset.culture = locale
    storePreference(LOCALE_KEY, locale)
  }, [locale])
  useEffect(() => {
    const syncPreferences = (event: StorageEvent) => {
      if (event.key === LOCALE_KEY) setLocale(initialLocale())
    }
    window.addEventListener('storage', syncPreferences)
    return () => window.removeEventListener('storage', syncPreferences)
  }, [])
  const value = useMemo<PreferencesValue>(() => ({ locale, setLocale, t: (key, values) => translate(locale, key, values) }), [locale])
  return <PreferencesContext.Provider value={value}>{children}</PreferencesContext.Provider>
}
export function usePreferences() { const value = useContext(PreferencesContext); if (!value) throw new Error('PreferencesProvider is missing'); return value }
