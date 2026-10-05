import { Navigate, Route, Routes } from 'react-router-dom';
import { AuthProvider, useAuth } from './auth/AuthContext';
import { Layout } from './layout/Layout';
import { AdjustmentsPage } from './pages/AdjustmentsPage';
import { AlertsPage } from './pages/AlertsPage';
import { ApplicationsPage } from './pages/ApplicationsPage';
import { CashierSessionsPage } from './pages/CashierSessionsPage';
import { CategoriesPage } from './pages/CategoriesPage';
import { ChartOfAccountsPage } from './pages/ChartOfAccountsPage';
import { CountsPage } from './pages/CountsPage';
import { DashboardPage } from './pages/DashboardPage';
import { FeeAssessmentsPage } from './pages/FeeAssessmentsPage';
import { FinanceOverviewPage } from './pages/FinanceOverviewPage';
import { FinanceReportsPage } from './pages/FinanceReportsPage';
import { FinancialPeriodsPage } from './pages/FinancialPeriodsPage';
import { FiscalisationPage } from './pages/FiscalisationPage';
import { GolfBookingDetailPage } from './pages/GolfBookingDetailPage';
import { GolfBookingsPage } from './pages/GolfBookingsPage';
import { GolfChargesPage } from './pages/GolfChargesPage';
import { GolfCoursesPage } from './pages/GolfCoursesPage';
import { GolfGuestsPage } from './pages/GolfGuestsPage';
import { GolfTeeSheetPage } from './pages/GolfTeeSheetPage';
import { HouseholdsPage } from './pages/HouseholdsPage';
import { JournalsPage } from './pages/JournalsPage';
import { LoginPage } from './pages/LoginPage';
import { MemberDetailPage } from './pages/MemberDetailPage';
import { MembersPage } from './pages/MembersPage';
import { PayablesPage } from './pages/PayablesPage';
import { PaymentsPage } from './pages/PaymentsPage';
import { PosPage } from './pages/PosPage';
import { ProductsPage } from './pages/ProductsPage';
import { ReceivablesPage } from './pages/ReceivablesPage';
import { ReconciliationPage } from './pages/ReconciliationPage';
import { SalesPage } from './pages/SalesPage';
import { StockPage } from './pages/StockPage';
import { SuppliersPage } from './pages/SuppliersPage';
import { TransfersPage } from './pages/TransfersPage';
import { WastagePage } from './pages/WastagePage';

function Router() {
  const { ready, user } = useAuth();

  if (!ready) {
    return <div className="login-shell muted">Loading…</div>;
  }

  if (!user) {
    return (
      <Routes>
        <Route path="*" element={<LoginPage />} />
      </Routes>
    );
  }

  return (
    <Routes>
      <Route element={<Layout />}>
        <Route path="/" element={<DashboardPage />} />
        <Route path="/members" element={<MembersPage />} />
        <Route path="/members/:memberId" element={<MemberDetailPage />} />
        <Route path="/applications" element={<ApplicationsPage />} />
        <Route path="/categories" element={<CategoriesPage />} />
        <Route path="/households" element={<HouseholdsPage />} />
        <Route path="/fees" element={<FeeAssessmentsPage />} />
        <Route path="/products" element={<ProductsPage />} />
        <Route path="/stock" element={<StockPage />} />
        <Route path="/transfers" element={<TransfersPage />} />
        <Route path="/counts" element={<CountsPage />} />
        <Route path="/adjustments" element={<AdjustmentsPage />} />
        <Route path="/wastage" element={<WastagePage />} />
        <Route path="/alerts" element={<AlertsPage />} />
        <Route path="/suppliers" element={<SuppliersPage />} />
        <Route path="/pos" element={<PosPage />} />
        <Route path="/sales" element={<SalesPage />} />
        <Route path="/golf/courses" element={<GolfCoursesPage />} />
        <Route path="/golf/tee-sheet" element={<GolfTeeSheetPage />} />
        <Route path="/golf/bookings" element={<GolfBookingsPage />} />
        <Route path="/golf/bookings/new" element={<GolfBookingsPage />} />
        <Route path="/golf/bookings/:bookingId" element={<GolfBookingDetailPage />} />
        <Route path="/golf/guests" element={<GolfGuestsPage />} />
        <Route path="/golf/charges" element={<GolfChargesPage />} />
        <Route path="/cashier-sessions" element={<CashierSessionsPage />} />
        <Route path="/fiscalisation" element={<FiscalisationPage />} />
        <Route path="/finance" element={<FinanceOverviewPage />} />
        <Route path="/finance/accounts" element={<ChartOfAccountsPage />} />
        <Route path="/finance/journals" element={<JournalsPage />} />
        <Route path="/finance/receivables" element={<ReceivablesPage />} />
        <Route path="/finance/payables" element={<PayablesPage />} />
        <Route path="/finance/payments" element={<PaymentsPage />} />
        <Route path="/finance/periods" element={<FinancialPeriodsPage />} />
        <Route path="/finance/reports" element={<FinanceReportsPage />} />
        <Route path="/finance/reconciliation" element={<ReconciliationPage />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}

export function App() {
  return (
    <AuthProvider>
      <Router />
    </AuthProvider>
  );
}