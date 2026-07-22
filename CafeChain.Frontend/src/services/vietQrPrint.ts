const VIET_QR_PRINT_HOST_SELECTOR = '.pos-vietqr-print-host'

export function printVietQrSlip(): void {
  if (!document.querySelector(VIET_QR_PRINT_HOST_SELECTOR)) {
    throw new Error('Không tìm thấy phiếu QR để in.')
  }

  window.print()
}
