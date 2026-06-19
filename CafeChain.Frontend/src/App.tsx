import { BrowserRouter, Routes, Route, Navigate, Outlet } from 'react-router-dom'
import TopNavbar from './components/TopNavbar'
import POSLayout from './POSLayout'
import OrderHistory from './pages/OrderHistory'
import ShiftSummary from './pages/ShiftSummary'
import PrinterStatusSimulator from './components/dev/PrinterStatusSimulator'

function RootLayout() {
  return (
    <div className="h-screen w-screen flex flex-col overflow-hidden bg-surface font-sans">
      <TopNavbar />
      <div className="flex-1 overflow-hidden">
        <Outlet />
      </div>
      <PrinterStatusSimulator />
    </div>
  )
}

function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<RootLayout />}>
          <Route index element={<Navigate to="/order" replace />} />
          <Route path="order" element={<POSLayout />} />
          <Route path="history" element={<OrderHistory />} />
          <Route path="shift" element={<ShiftSummary />} />
        </Route>
      </Routes>
    </BrowserRouter>
  )
}

export default App
