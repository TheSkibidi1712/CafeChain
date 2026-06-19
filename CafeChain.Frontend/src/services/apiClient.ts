const API_BASE = import.meta.env.VITE_API_BASE_URL ?? 'https://localhost:7231'

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
  try {
    const response = await fetch(url, {
      ...options,
      headers: {
        'Accept': 'application/json',
        ...options?.headers,
      },
    })

    if (!response.ok) {
      const errorText = await response.text().catch(() => '')
      return {
        data: null,
        ok: false,
        status: response.status,
        error: errorText || response.statusText,
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

  post: <T>(path: string, body: any, options?: RequestInit) =>
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
