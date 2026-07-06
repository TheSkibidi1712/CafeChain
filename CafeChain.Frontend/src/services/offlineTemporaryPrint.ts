import { createElement, type ReactElement } from 'react'
import { createRoot } from 'react-dom/client'
import TemporaryDrinkLabelTemplate from '../components/TemporaryDrinkLabelTemplate'
import TemporaryReceiptTemplate from '../components/TemporaryReceiptTemplate'
import type { CartSyncQueueItem } from '../db/CafeChainPOSDB'

const temporaryLabelEnv = import.meta.env.VITE_ALLOW_OFFLINE_TEMPORARY_DRINK_LABEL

// Temporary frontend flag for issue #80; later this can come from backend Store config.
export const allowOfflineTemporaryDrinkLabel =
  temporaryLabelEnv === undefined
    ? true
    : ['1', 'true', 'yes', 'on'].includes(String(temporaryLabelEnv).trim().toLowerCase())

const nextFrame = () =>
  new Promise<void>((resolve) => {
    window.requestAnimationFrame(() => resolve())
  })

async function renderAndPrint(element: ReactElement, hostClassName: string): Promise<void> {
  const host = document.createElement('div')
  host.className = hostClassName
  document.body.appendChild(host)

  const root = createRoot(host)
  root.render(element)

  await nextFrame()
  await nextFrame()

  let cleanupTimer = 0
  let cleaned = false

  const cleanup = () => {
    if (cleaned) return
    cleaned = true
    window.removeEventListener('afterprint', cleanup)
    if (cleanupTimer) window.clearTimeout(cleanupTimer)
    root.unmount()
    host.remove()
  }

  window.addEventListener('afterprint', cleanup, { once: true })
  window.print()
  cleanupTimer = window.setTimeout(cleanup, 15000)
}

export async function printTemporaryReceipt(order: CartSyncQueueItem): Promise<void> {
  await renderAndPrint(
    createElement(TemporaryReceiptTemplate, { order }),
    'temporary-print-host temporary-receipt-print-host'
  )
}

export async function printTemporaryDrinkLabels(order: CartSyncQueueItem): Promise<void> {
  if (!allowOfflineTemporaryDrinkLabel) {
    throw new Error('In tem tạm offline đang bị tắt.')
  }

  await renderAndPrint(
    createElement(TemporaryDrinkLabelTemplate, { order }),
    'temporary-print-host temporary-drink-label-print-host'
  )
}
