import { useEffect, useState } from 'react'
import QRCode from 'qrcode'

interface VietQrCodeProps {
  value?: string | null
  className?: string
  size?: number
  alt?: string
}

interface GeneratedQrCode {
  source: string
  dataUrl?: string
  error?: string
}

export default function VietQrCode({
  value,
  className = '',
  size = 640,
  alt = 'Mã VietQR thanh toán',
}: VietQrCodeProps) {
  const [generated, setGenerated] = useState<GeneratedQrCode | null>(null)

  useEffect(() => {
    if (!value) return
    let active = true

    QRCode.toDataURL(value, {
      width: size,
      margin: 2,
      errorCorrectionLevel: 'M',
      color: {
        dark: '#111827',
        light: '#ffffff',
      },
    }).then((dataUrl) => {
      if (active) setGenerated({ source: value, dataUrl })
    }).catch(() => {
      if (active) setGenerated({
        source: value,
        error: 'Không thể tạo mã VietQR. Vui lòng mở trang PayOS.',
      })
    })

    return () => {
      active = false
    }
  }, [size, value])

  if (!value) {
    return <p className="text-center text-sm font-bold text-text-muted">Đang chuẩn bị mã VietQR...</p>
  }

  if (generated?.source !== value) {
    return <p className="text-center text-sm font-bold text-text-muted">Đang tạo mã VietQR...</p>
  }

  if (!generated.dataUrl) {
    return <p className="max-w-sm text-center text-sm font-bold text-danger">{generated.error}</p>
  }

  return (
    <img
      src={generated.dataUrl}
      alt={alt}
      width={size}
      height={size}
      data-vietqr-ready="true"
      className={`block max-h-full max-w-full object-contain ${className}`}
    />
  )
}
