import { useState } from 'react';
import { NavLink, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';

interface NavItem {
  to: string;
  label: string;
  permission: string;
}

interface NavGroup {
  label: string;
  items: NavItem[];
}

const NAV_GROUPS: NavGroup[] = [
  {
    label: 'Membership',
    items: [
      { to: '/members', label: 'Members', permission: 'Membership.Member.View' },
      { to: '/applications', label: 'Applications', permission: 'Membership.Application.View' },
      { to: '/categories', label: 'Categories', permission: 'Membership.Category.View' },
      { to: '/households', label: 'Households', permission: 'Membership.Member.View' },
      { to: '/fees', label: 'Fee Assessments', permission: 'Membership.Fee.View' },
    ],
  },
  {
    label: 'Products & Inventory',
    items: [
      { to: '/products', label: 'Products', permission: 'Inventory.Product.View' },
      { to: '/stock', label: 'Stock', permission: 'Inventory.Stock.View' },
      { to: '/transfers', label: 'Transfers', permission: 'Inventory.Stock.Transfer' },
      { to: '/counts', label: 'Counts', permission: 'Inventory.Stock.Count' },
      { to: '/adjustments', label: 'Adjustments', permission: 'Inventory.Stock.Adjust' },
      { to: '/wastage', label: 'Wastage', permission: 'Inventory.Stock.Wastage' },
      { to: '/alerts', label: 'Reorder Alerts', permission: 'Inventory.Stock.View' },
      { to: '/suppliers', label: 'Suppliers', permission: 'Procurement.Supplier.View' },
    ],
  },
  {
    label: 'Point of Sale',
    items: [
      { to: '/pos', label: 'Point of Sale', permission: 'POS.Sale.Create' },
      { to: '/sales', label: 'Sales', permission: 'POS.Sale.Create' },
      { to: '/cashier-sessions', label: 'Cashier Sessions', permission: 'Cashiering.Session.View' },
    ],
  },
  {
    label: 'Finance',
    items: [
      { to: '/finance', label: 'Finance Overview', permission: 'Finance.Report.View' },
      { to: '/finance/accounts', label: 'Chart of Accounts', permission: 'Finance.Account.View' },
      { to: '/finance/journals', label: 'Journals', permission: 'Finance.Journal.View' },
      { to: '/finance/receivables', label: 'Accounts Receivable', permission: 'Finance.AR.View' },
      { to: '/finance/payables', label: 'Accounts Payable', permission: 'Finance.AP.View' },
      { to: '/finance/payments', label: 'Payments', permission: 'Finance.Payment.Manage' },
      { to: '/finance/periods', label: 'Financial Periods', permission: 'Finance.Period.View' },
      { to: '/finance/reconciliation', label: 'Reconciliation', permission: 'Finance.Reconciliation.Perform' },
    ],
  },
  {
    label: 'Golf',
    items: [
      { to: '/golf/courses', label: 'Courses', permission: 'Golf.Course.View' },
      { to: '/golf/tee-sheet', label: 'Tee Sheet', permission: 'Golf.TeeSheet.View' },
      { to: '/golf/bookings', label: 'Bookings', permission: 'Golf.Booking.View' },
      { to: '/golf/guests', label: 'Guests', permission: 'Golf.TeeSheet.View' },
      { to: '/golf/charges', label: 'Charges', permission: 'Golf.Booking.View' },
    ],
  },
  {
    label: 'Administration',
    items: [
      { to: '/fiscalisation', label: 'Fiscalisation', permission: 'Fiscalization.Device.View' },
    ],
  },
  {
    label: 'Reports',
    items: [
      { to: '/finance/reports', label: 'Financial Reports', permission: 'Finance.Report.View' },
    ],
  },
];

function isGroupActive(pathname: string, group: NavGroup): boolean {
  return group.items.some((item) => pathname.startsWith(item.to));
}

export function Layout() {
  const { user, logout, can } = useAuth();
  const [expandedGroups, setExpandedGroups] = useState<Record<string, boolean>>({});
  const { pathname } = useLocation();

  const toggleGroup = (label: string) => {
    setExpandedGroups((prev) => ({ ...prev, [label]: !prev[label] }));
  };

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="brand">ClubStack</div>
        <nav className="nav">
          <NavLink
            to="/"
            end
            className={({ isActive }) => (isActive ? 'nav-link active' : 'nav-link')}
          >
            Dashboard
          </NavLink>

          {NAV_GROUPS.map((group) => {
            const visibleItems = group.items.filter((item) => can(item.permission));
            if (visibleItems.length === 0) return null;

            const isExpanded = expandedGroups[group.label] ?? isGroupActive(pathname, group);
            const isActive = isGroupActive(pathname, group);

            return (
              <div key={group.label} className="nav-group">
                <button
                  className={`nav-group-toggle ${isActive ? 'active' : ''}`}
                  onClick={() => toggleGroup(group.label)}
                >
                  <span>{group.label}</span>
                  <span className="nav-group-arrow">{isExpanded ? '▾' : '▸'}</span>
                </button>
                {isExpanded && (
                  <div className="nav-group-items">
                    {visibleItems.map((item) => (
                      <NavLink
                        key={item.to}
                        to={item.to}
                        className={({ isActive }) => (isActive ? 'nav-link nav-sublink active' : 'nav-link nav-sublink')}
                      >
                        {item.label}
                      </NavLink>
                    ))}
                  </div>
                )}
              </div>
            );
          })}
        </nav>
        <div className="sidebar-footer">
          <span className="user-name">{user?.displayName}</span>
          <button type="button" className="link-button" onClick={() => void logout()}>
            Sign out
          </button>
        </div>
      </aside>
      <main className="content">
        <Outlet />
      </main>
    </div>
  );
}
