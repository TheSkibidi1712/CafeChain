interface OpenCustomerDisplayResult {
  ok: boolean
  reused: boolean
  manualUrl: string
  message: string
}

interface ScreenLike {
  left: number
  top: number
  width: number
  height: number
  isPrimary?: boolean
}

interface ScreenDetailsLike {
  screens: ScreenLike[]
  currentScreen?: ScreenLike
}

type WindowWithScreenDetails = Window & {
  getScreenDetails?: () => Promise<ScreenDetailsLike>
}

const DISPLAY_PATH = '/pos/customer-display'
const displayWindows = new Map<number, Window>()

const buildDisplayUrl = (workShiftId: number): string => {
  const url = new URL(DISPLAY_PATH, window.location.origin)
  url.searchParams.set('workShiftId', String(workShiftId))
  return url.toString()
}

export async function openCustomerDisplayWindow(workShiftId: number): Promise<OpenCustomerDisplayResult> {
  const manualUrl = buildDisplayUrl(workShiftId)
  const existingWindow = displayWindows.get(workShiftId)
  if (existingWindow && !existingWindow.closed) {
    existingWindow.focus()
    return {
      ok: true,
      reused: true,
      manualUrl,
      message: 'Đã chuyển tới màn hình khách hàng đang mở.',
    }
  }
  if (existingWindow?.closed) displayWindows.delete(workShiftId)

  const windowName = `cafechain-customer-display-${workShiftId}`
  const displayWindow = window.open(
    manualUrl,
    windowName,
    'popup=yes,width=1024,height=768,resizable=yes,scrollbars=no',
  )

  if (!displayWindow) {
    return {
      ok: false,
      reused: false,
      manualUrl,
      message: `Trình duyệt đã chặn cửa sổ. Hãy mở thủ công ${manualUrl} trên màn hình khách.`,
    }
  }

  displayWindows.set(workShiftId, displayWindow)
  displayWindow.focus()

  const screenApi = window as WindowWithScreenDetails
  if (screenApi.getScreenDetails) {
    try {
      const details = await screenApi.getScreenDetails()
      const target = details.screens.find((screen) => screen !== details.currentScreen && !screen.isPrimary)
        ?? details.screens.find((screen) => screen !== details.currentScreen)
      if (target && !displayWindow.closed) {
        displayWindow.moveTo(target.left, target.top)
        displayWindow.resizeTo(target.width, target.height)
      }
    } catch {
      // Permission denied: the named popup remains usable for manual move/fullscreen.
    }
  }

  return {
    ok: true,
    reused: false,
    manualUrl,
    message: 'Đã mở màn hình khách hàng. Có thể kéo sang màn hình thứ hai nếu trình duyệt chưa tự chuyển.',
  }
}
