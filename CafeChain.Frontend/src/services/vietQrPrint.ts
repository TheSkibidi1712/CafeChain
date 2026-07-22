const VIET_QR_PRINT_HOST_SELECTOR = '.pos-vietqr-print-host'

export function printVietQrSlip(): void {
  const printHost = document.querySelector(VIET_QR_PRINT_HOST_SELECTOR)
  if (!printHost) {
    throw new Error('Không tìm thấy phiếu QR để in.')
  }
  if (!printHost.querySelector('[data-vietqr-ready="true"]')) {
    throw new Error('Mã VietQR chưa sẵn sàng để in.')
  }

  window.print()
}
