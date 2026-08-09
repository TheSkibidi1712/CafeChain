import { BrowserRouter, Routes, Route, Navigate, Outlet, useLocation } from 'react-router-dom'
import TopNavbar from './components/TopNavbar'
import POSLayout from './POSLayout'
import OrderHistory from './pages/OrderHistory'
import ShiftSummary from './pages/ShiftSummary'
import BranchInventory from './pages/BranchInventory'
import Notifications from './pages/Notifications'
import PaymentResult from './pages/PaymentResult'
import CustomerDisplay from './pages/CustomerDisplay'
import PrinterStatusSimulator from './components/dev/PrinterStatusSimulator'
import PosAccessGate from './components/PosAccessGate'

function RootLayout() {
  const location = useLocation()
  const isSellingRoute = location.pathname === '/' || location.pathname === '/order'

  return (
    <PosAccessGate>
      <div className="pos-app-frame w-full flex flex-col overflow-hidden bg-surface font-sans">
      <a
        href="#pos-main-content"
        className="sr-only focus:not-sr-only focus:absolute focus:left-3 focus:top-3 focus:z-[80] focus:rounded-lg focus:bg-white focus:px-4 focus:py-3 focus:text-sm focus:font-bold focus:text-brand-orange"
      >
        Bỏ qua điều hướng
      </a>
      {!isSellingRoute && <TopNavbar />}
      <div className="flex-1 overflow-hidden">
        <Outlet />
      </div>
      <PrinterStatusSimulator />
      </div>
    </PosAccessGate>
  )
}

function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/payment-success" element={<PaymentResult status="success" />} />
        <Route path="/payment-cancel" element={<PaymentResult status="cancel" />} />
        <Route path="/pos/customer-display" element={<CustomerDisplay />} />
        <Route path="/" element={<RootLayout />}>
          <Route index element={<Navigate to="/order" replace />} />
          <Route path="order" element={<POSLayout />} />
          <Route path="history" element={<OrderHistory />} />
          <Route path="inventory" element={<BranchInventory />} />
          <Route path="notifications" element={<Notifications />} />
          <Route path="shift" element={<ShiftSummary />} />
        </Route>
      </Routes>
    </BrowserRouter>
  )
}

export default App
