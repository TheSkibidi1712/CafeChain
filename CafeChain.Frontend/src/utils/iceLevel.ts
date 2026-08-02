export type IceLevelPercent = 0 | 50 | 100

export const formatIceLevel = (value: IceLevelPercent | null | undefined): string => {
  if (value === 0) return 'Không đá'
  if (value === 50) return 'Ít đá'
  if (value === 100) return 'Đá bình thường'
  return ''
}
