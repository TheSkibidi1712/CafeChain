import { usePreferences, type LocalePreference } from '../contexts/PreferencesContext'

export default function PreferenceControls() {
  const { locale, setLocale, t } = usePreferences()
  return <div className="pos-preference-controls flex items-center gap-1.5">
    <label className="sr-only" htmlFor="pos-locale-preference">{t('common.language')}</label>
    <select id="pos-locale-preference" value={locale} onChange={(event) => setLocale(event.target.value as LocalePreference)} className="pos-preference-select">
      <option value="vi-VN">VI</option><option value="en-US">EN</option>
    </select>
  </div>
}
