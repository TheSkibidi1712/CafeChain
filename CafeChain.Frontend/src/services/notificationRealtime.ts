import {
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
  type HubConnection,
} from '@microsoft/signalr'
import Swal from 'sweetalert2'
import { API_BASE_URL } from './apiClient'
import { getPosSession } from './posSession'

export interface InventoryNotificationChanged {
  eventId: string
  storeId: number
  type: string
  severity: string
  changeKind: 'Created' | 'Updated' | 'Escalated' | 'Resolved' | string
  entityType: string
  entityId: number
  shouldToast: boolean
  occurredAt: string
}

const EVENT_NAME = 'InventoryNotificationChanged'
const seenEventIds = new Set<string>()
let connection: HubConnection | null = null
let startPromise: Promise<void> | null = null

function rememberEvent(eventId: string): boolean {
  if (!eventId || seenEventIds.has(eventId)) return false
  seenEventIds.add(eventId)
  if (seenEventIds.size > 200) {
    const oldest = seenEventIds.values().next().value
    if (oldest) seenEventIds.delete(oldest)
  }
  return true
}

function buildConnection(): HubConnection {
  const hub = new HubConnectionBuilder()
    .withUrl(`${API_BASE_URL}/hubs/inventory-notifications`, {
      accessTokenFactory: () => getPosSession().token ?? '',
    })
    .withAutomaticReconnect([0, 2_000, 5_000, 10_000, 30_000])
    .configureLogging(LogLevel.Warning)
    .build()

  hub.on(EVENT_NAME, (message: InventoryNotificationChanged) => {
    if (!rememberEvent(message.eventId)) return
    window.dispatchEvent(new CustomEvent('pos-notifications-changed', { detail: message }))
    if (!message.shouldToast || message.changeKind === 'Resolved') return

    void Swal.fire({
      toast: true,
      position: 'top-end',
      timer: 5_000,
      timerProgressBar: true,
      showConfirmButton: false,
      icon: message.severity === 'URGENT' || message.severity === 'CRITICAL'
        ? 'error'
        : 'warning',
      title: message.changeKind === 'Escalated'
        ? 'Cảnh báo kho đã tăng mức độ'
        : 'Có thông báo kho mới',
      text: 'Mở mục Thông báo để xem chi tiết.',
    })
  })

  hub.onreconnected(() => {
    window.dispatchEvent(new CustomEvent('pos-notifications-changed'))
  })
  return hub
}

export async function startNotificationRealtime(): Promise<void> {
  if (!getPosSession().token) return
  if (!connection) connection = buildConnection()
  if (connection.state === HubConnectionState.Connected
    || connection.state === HubConnectionState.Connecting
    || connection.state === HubConnectionState.Reconnecting) return
  if (startPromise) return startPromise

  startPromise = connection.start()
    .catch((error: unknown) => {
      console.warn('[notifications] SignalR connection failed; polling remains active.', error)
    })
    .finally(() => {
      startPromise = null
    })
  return startPromise
}

export async function stopNotificationRealtime(): Promise<void> {
  if (!connection || connection.state === HubConnectionState.Disconnected) return
  await connection.stop()
}
