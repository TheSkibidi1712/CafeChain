import { apiClient } from './apiClient'

export type BranchInventoryItemType = 'Ingredient' | 'Recipe'

export type QuantityStatus = 'Tồn âm' | 'Hết hàng' | 'Còn hàng'

export interface BranchInventoryItem {
  storeInventoryId: number
  storeId: number
  itemType: BranchInventoryItemType | string
  itemId: number
  itemName: string
  itemCode?: string | null
  availableQty: number
  reservedQty: number
  unitName: string
  minStockLevel: number | null
  thresholdConfigured: boolean
  thresholdStatus: string
  quantityStatus: QuantityStatus | string
  lastUpdated: string
}

export interface BranchInventoryListData {
  storeId: number
  page: number
  pageSize: number
  total: number
  items: BranchInventoryItem[]
}

interface BranchInventoryApiResponse {
  success?: boolean
  data?: BranchInventoryListData
  message?: string
}

export interface FetchBranchInventoryParams {
  search?: string
  itemType?: '' | BranchInventoryItemType
  page?: number
  pageSize?: number
}

function buildQuery(params: FetchBranchInventoryParams): string {
  const query = new URLSearchParams()
  if (params.search?.trim()) query.set('search', params.search.trim())
  if (params.itemType) query.set('itemType', params.itemType)
  if (params.page && params.page > 0) query.set('page', String(params.page))
  if (params.pageSize && params.pageSize > 0) query.set('pageSize', String(params.pageSize))
  const qs = query.toString()
  return qs ? `?${qs}` : ''
}

/**
 * Issue #96 — GET /api/v1/pos/branch-inventory (read-only, JWT store scope).
 */
export async function fetchBranchInventory(
  params: FetchBranchInventoryParams = {}
): Promise<{ ok: boolean; data: BranchInventoryListData | null; error?: string; status: number }> {
  const path = `/api/v1/pos/branch-inventory${buildQuery(params)}`
  const res = await apiClient.get<BranchInventoryApiResponse | BranchInventoryListData>(path)

  if (!res.ok || res.data == null) {
    return {
      ok: false,
      data: null,
      error: res.error || 'Không tải được kho chi nhánh.',
      status: res.status,
    }
  }

  // Support both { success, data } envelope and raw list DTO.
  const body = res.data as BranchInventoryApiResponse & BranchInventoryListData
  if (body.items && Array.isArray(body.items) && body.storeId != null) {
    return { ok: true, data: body as BranchInventoryListData, status: res.status }
  }

  if (body.data?.items) {
    return { ok: true, data: body.data, status: res.status }
  }

  return {
    ok: false,
    data: null,
    error: body.message || 'Phản hồi kho chi nhánh không hợp lệ.',
    status: res.status,
  }
}
