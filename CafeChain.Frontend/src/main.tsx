import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'
import { seedLocalDB } from './db/seedLocalDB.ts'

// Seed IndexedDB với mock data nếu bảng rỗng (first launch)
seedLocalDB().catch(console.error)

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
