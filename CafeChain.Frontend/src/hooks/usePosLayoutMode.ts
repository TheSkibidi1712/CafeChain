import { useCallback, useEffect, useState } from 'react'

export const POS_LAYOUT_PREFERENCE_KEY = 'cafechain.pos.layoutPreference'

export type PosLayoutPreference = 'auto' | 'desktop' | 'tablet'
export type PosResolvedLayout = Exclude<PosLayoutPreference, 'auto'>
export type PosOrientation = 'landscape' | 'portrait'

interface PosDeviceSignals {
  width: number
  orientation: PosOrientation
  hasCoarsePointer: boolean
  hasHover: boolean
}

interface PosLayoutState {
  preference: PosLayoutPreference
  resolvedLayout: PosResolvedLayout
  orientation: PosOrientation
}

const isLayoutPreference = (value: string | null): value is PosLayoutPreference =>
  value === 'auto' || value === 'desktop' || value === 'tablet'

export const readPosLayoutPreference = (): PosLayoutPreference => {
  if (typeof window === 'undefined') return 'auto'

  try {
    const storedPreference = window.localStorage.getItem(POS_LAYOUT_PREFERENCE_KEY)
    return isLayoutPreference(storedPreference) ? storedPreference : 'auto'
  } catch {
    return 'auto'
  }
}

const readDeviceSignals = (): PosDeviceSignals => {
  if (typeof window === 'undefined') {
    return {
      width: 1440,
      orientation: 'landscape',
      hasCoarsePointer: false,
      hasHover: true,
    }
  }

  return {
    width: window.innerWidth,
    orientation: window.matchMedia('(orientation: portrait)').matches ? 'portrait' : 'landscape',
    hasCoarsePointer: window.matchMedia('(pointer: coarse)').matches,
    hasHover: window.matchMedia('(hover: hover)').matches,
  }
}

export const resolveAutoPosLayout = (signals: PosDeviceSignals): PosResolvedLayout => {
  const touchFirstDevice = signals.hasCoarsePointer && !signals.hasHover
  const compactPortrait = signals.orientation === 'portrait' && signals.width <= 1280
  return signals.width <= 1180 || touchFirstDevice || compactPortrait ? 'tablet' : 'desktop'
}

const resolveLayoutState = (preference: PosLayoutPreference): PosLayoutState => {
  const signals = readDeviceSignals()
  return {
    preference,
    resolvedLayout: preference === 'auto' ? resolveAutoPosLayout(signals) : preference,
    orientation: signals.orientation,
  }
}

export function usePosLayoutMode() {
  const [layoutState, setLayoutState] = useState<PosLayoutState>(() =>
    resolveLayoutState(readPosLayoutPreference())
  )

  const setPreference = useCallback((preference: PosLayoutPreference) => {
    try {
      window.localStorage.setItem(POS_LAYOUT_PREFERENCE_KEY, preference)
    } catch {
      // The current session still switches layout when storage is unavailable.
    }

    setLayoutState(resolveLayoutState(preference))
  }, [])

  useEffect(() => {
    const updateLayout = () => {
      setLayoutState((current) => resolveLayoutState(current.preference))
    }
    const mediaQueries = [
      window.matchMedia('(orientation: portrait)'),
      window.matchMedia('(pointer: coarse)'),
      window.matchMedia('(hover: hover)'),
    ]

    window.addEventListener('resize', updateLayout)
    window.addEventListener('orientationchange', updateLayout)
    mediaQueries.forEach((query) => query.addEventListener('change', updateLayout))

    return () => {
      window.removeEventListener('resize', updateLayout)
      window.removeEventListener('orientationchange', updateLayout)
      mediaQueries.forEach((query) => query.removeEventListener('change', updateLayout))
    }
  }, [])

  return {
    ...layoutState,
    setPreference,
  }
}
