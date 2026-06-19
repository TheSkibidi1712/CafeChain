import { useState, useEffect, useRef } from 'react'
import * as signalR from '@microsoft/signalr'
import Swal from 'sweetalert2'

export type PrinterStatus = 'ready' | 'error' | 'offline'

export function usePrinterStatus(storeId: number = 1) {
  const [status, setStatus] = useState<PrinterStatus>('offline')
  const [overrideStatus, setOverrideStatus] = useState<PrinterStatus | null>(null)
  
  const prevStatusRef = useRef<PrinterStatus>('offline')

  // 1. Listen to simulator override events for development visual testing
  useEffect(() => {
    const handleOverride = (e: Event) => {
      const customEvent = e as CustomEvent<{ status: PrinterStatus }>
      if (customEvent.detail && customEvent.detail.status) {
        setOverrideStatus(customEvent.detail.status)
      }
    }

    const handleResetOverride = () => {
      setOverrideStatus(null)
    }

    window.addEventListener('mock-printer-status', handleOverride)
    window.addEventListener('mock-printer-status-reset', handleResetOverride)

    return () => {
      window.removeEventListener('mock-printer-status', handleOverride)
      window.removeEventListener('mock-printer-status-reset', handleResetOverride)
    }
  }, [])

  // 2. Connect to Backend SignalR Hub (PrintBridgeHub)
  useEffect(() => {
    const apiBaseUrl = import.meta.env.VITE_API_BASE_URL || 'https://localhost:7231'
    const hubUrl = `${apiBaseUrl}/hubs/print-bridge`

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl)
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (retryContext) => {
          // Reconnect backoff
          if (retryContext.elapsedMilliseconds < 10000) {
            return 2000
          }
          if (retryContext.elapsedMilliseconds < 30000) {
            return 5000
          }
          return 10000;
        }
      })
      .build()

    let active = true

    const startConnection = async () => {
      try {
        await connection.start()
        console.log('[SignalR POS] Connected to PrintBridgeHub successfully.')
        if (active) {
          await connection.invoke('JoinPosGroup', storeId)
        }
      } catch (err) {
        console.error('[SignalR POS] Connection failed: ', err)
        if (active) {
          // Retry connection in 5 seconds
          setTimeout(startConnection, 5000)
        }
      }
    }

    connection.onreconnecting((error) => {
      console.warn('[SignalR POS] Reconnecting due to error: ', error)
      if (active) {
        setStatus('offline')
      }
    })

    connection.onreconnected((connectionId) => {
      console.log('[SignalR POS] Reconnected successfully. ConnectionId:', connectionId)
      connection.invoke('JoinPosGroup', storeId).catch((err) => {
        console.error('[SignalR POS] Failed to join POS group after reconnect:', err)
      })
    })

    connection.onclose((error) => {
      console.warn('[SignalR POS] Connection closed:', error)
      if (active) {
        setStatus('offline')
      }
    })

    // Listen to PrinterStatusChanged
    connection.on('PrinterStatusChanged', (data: { storeId: number; isOnline: boolean }) => {
      console.log('[SignalR POS] Received PrinterStatusChanged:', data)
      if (data.storeId === storeId) {
        if (active) {
          setStatus(data.isOnline ? 'ready' : 'offline')
        }
      }
    })

    startConnection()

    return () => {
      active = false
      connection.stop().catch((err) => console.error('[SignalR POS] Error stopping connection:', err))
    }
  }, [storeId])

  // Get current active status (considering overrides)
  const currentStatus = overrideStatus !== null ? overrideStatus : status

  // 3. Trigger SweetAlert alert on status transition from online (ready) to offline
  useEffect(() => {
    const prevStatus = prevStatusRef.current
    if (prevStatus === 'ready' && currentStatus === 'offline') {
      Swal.fire({
        title: 'Mất kết nối máy in!',
        text: 'Ứng dụng không thể kết nối tới máy in hóa đơn. Vui lòng kiểm tra lại thiết bị hoặc kết nối LAN.',
        icon: 'error',
        confirmButtonColor: '#EA580C', // Brand Orange Color
        confirmButtonText: 'Đóng'
      })
    }
    prevStatusRef.current = currentStatus
  }, [currentStatus])

  return currentStatus
}
