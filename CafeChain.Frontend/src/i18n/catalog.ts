import { enMessages } from './locales/en'
import { viMessages, type MessageKey } from './locales/vi'

export type MessageValues = Record<string, string | number>

export const messageCatalog = {
  'vi-VN': viMessages,
  'en-US': enMessages,
} as const

const placeholderPattern = /\{([A-Za-z][A-Za-z0-9]*)\}/g

function placeholders(message: string): string[] {
  return [...message.matchAll(placeholderPattern)]
    .map((match) => match[1])
    .sort()
}

export function validateCatalog(): void {
  for (const key of Object.keys(viMessages) as MessageKey[]) {
    const viPlaceholders = placeholders(viMessages[key])
    const enPlaceholders = placeholders(enMessages[key])
    if (viPlaceholders.join('|') !== enPlaceholders.join('|')) {
      throw new Error(`Localization placeholder mismatch for "${key}".`)
    }
  }
}

export function translate(
  locale: keyof typeof messageCatalog,
  key: MessageKey,
  values: MessageValues = {},
): string {
  const template: string = messageCatalog[locale][key]
  return template.replace(placeholderPattern, (_placeholder, name: string) => {
    if (!(name in values)) {
      throw new Error(`Missing localization value "${name}" for "${key}".`)
    }
    return String(values[name])
  })
}

validateCatalog()
