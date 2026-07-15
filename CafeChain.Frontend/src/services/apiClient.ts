import { POS_TOKEN_KEY } from './posSession'

const configuredApiBase = import.meta.env.VITE_API_BASE_URL?.trim()
const API_BASE = (configuredApiBase || 'http://localhost:5111').replace(/\/$/, '')
export const API_BASE_URL = API_BASE

export interface ApiResponse<T> {
  data: T | null
  ok: boolean
  status: number
  error?: string
}

async function request<T>(
  path: string,
  options?: RequestInit
): Promise<ApiResponse<T>> {
  const url = `${API_BASE}${path}`
  // Issue #69: Đọc JWT token từ localStorage — gắn vào mọi request
  const token = localStorage.getItem(POS_TOKEN_KEY)
  try {
    const response = await fetch(url, {
      ...options,
      headers: {
        'Accept': 'application/json',
        ...(token ? { 'Authorization': `Bearer ${token}` } : {}),
        ...options?.headers,
      },
    })

    if (!response.ok) {
      const errorText = await response.text().catch(() => '')
      let errorData: T | null = null
      let errorMessage = errorText || response.statusText
      if (errorText) {
        try {
          errorData = JSON.parse(errorText) as T
          const message = (errorData as { message?: unknown } | null)?.message
          if (typeof message === 'string' && message.trim()) errorMessage = message
        } catch {
          // Non-JSON error bodies remain available as plain text.
        }
      }
      return {
        data: errorData,
        ok: false,
        status: response.status,
        error: errorMessage,
      }
    }

    const data = await response.json().catch(() => null) as T
    return {
      data,
      ok: true,
      status: response.status,
    }
  } catch (error) {
    // Catch-all try-catch prevents unhandled network exceptions and red console logs when offline
    const errorMessage = error instanceof Error ? error.message : String(error)
    console.warn(`[apiClient] ⚠️ Network request to ${path} failed: ${errorMessage}`)
    return {
      data: null,
      ok: false,
      status: 0,
      error: 'Network connection error',
    }
  }
}

export const apiClient = {
  get: <T>(path: string, options?: RequestInit) =>
    request<T>(path, { ...options, method: 'GET' }),

  post: <T>(path: string, body: unknown, options?: RequestInit) =>
    request<T>(path, {
      ...options,
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        ...options?.headers,
      },
      body: JSON.stringify(body),
    }),
}
