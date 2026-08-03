import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'
import { bootstrapPosTokenFromUrl } from './services/posSession.ts'

void bootstrapPosTokenFromUrl()
  .catch((error: unknown) => {
    console.error('Không thể khởi tạo phiên POS.', error)
  })
  .finally(() => {
    createRoot(document.getElementById('root')!).render(
      <StrictMode>
        <App />
      </StrictMode>,
    )
  })
