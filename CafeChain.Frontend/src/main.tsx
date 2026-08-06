import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'
import { API_BASE_URL } from './services/apiClient.ts'
import {
  bootstrapPosTokenFromUrl,
  clearPosAuthentication,
  getPosTerminalId,
  PosSessionBootstrapError,
} from './services/posSession.ts'

const renderApp = () => {
    createRoot(document.getElementById('root')!).render(
      <StrictMode>
        <App />
      </StrictMode>,
    )
}

void bootstrapPosTokenFromUrl()
  .then(renderApp)
  .catch((error: unknown) => {
    const terminalId = getPosTerminalId()
    clearPosAuthentication()
    const target = new URL('/StaffHub', API_BASE_URL)
    target.searchParams.set('openPos', '1')
    if (terminalId) target.searchParams.set('terminalId', terminalId)
    target.searchParams.set(
      'posErrorCode',
      error instanceof PosSessionBootstrapError
        ? error.errorCode
        : 'POS_EXCHANGE_UNAVAILABLE',
    )
    window.location.replace(target.toString())
  })
