import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { describeError, json } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import type { GolfChargeSearch } from '../types';

const BASE = '/api/v1/golf';

function money(amount: number, currency: string): string {
  return `${amount.toLocaleString(undefined, { minimumFractionDigits: 2 })} ${currency}`;
}

function chargePill(status: string): string {
  if (status === 'Charged') {
    return 'status-pill ok';
  }
  if (status === 'Waived' || status === 'Cancelled') {
    return 'status-pill bad';
  }
  return 'status-pill warn';
}

/**
 * GOLF-017: the charge register. A charge is only money once posted: a pending
 * charge shows the amount owed, a charged charge traces to its ledger rows.
 */
export function GolfChargesPage() {
  const { can } = useAuth();
  const canView = can('Golf.Booking.View');
  const canCharge = can('Golf.Charge.Create');

  const [rows, setRows] = useState<GolfChargeSearch['items']>([]);
  const [totalAmount, setTotalAmount] = useState(0);
  const [totalCount, setTotalCount] = useState(0);
  const [chargeType, setChargeType] = useState('');
  const [status, setStatus] = useState('');
  const [bookingId, setBookingId] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  const load = async () => {
    if (!canView) {
      return;
    }
    setError('');
    setBusy(true);
    try {
      const query = new URLSearchParams({ pageSize: '100' });
      if (chargeType) {
        query.set('chargeType', chargeType);
      }
      if (status) {
        query.set('status', status);
      }
      if (bookingId.trim()) {
        query.set('bookingId', bookingId.trim());
      }
      const result = await json.get<GolfChargeSearch>(`${BASE}/charges?${query.toString()}`);
      setRows(result.items);
      setTotalAmount(result.totalAmount);
      setTotalCount(result.totalCount);
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  useEffect(() => {
    void load();
  }, [canView, chargeType, status, bookingId]);

  if (!canView) {
    return <div className="empty-state">You do not have permission to view golf charges.</div>;
  }

  return (
    <>
      <h1 className="page-title">Golf Charges</h1>
      <p className="page-subtitle">Green fees and additional charges, and whether they have been posted.</p>

      {error ? <div className="alert alert-error">{error}</div> : null}

      <div className="toolbar">
        <div className="field">
          <label htmlFor="chargeTypeFilter">Type</label>
          <select id="chargeTypeFilter" value={chargeType} onChange={(event) => setChargeType(event.target.value)}>
            <option value="">All</option>
            <option value="GreenFee">Green fee</option>
            <option value="Additional">Additional</option>
          </select>
        </div>
        <div className="field">
          <label htmlFor="chargeStatusFilter">Status</label>
          <select id="chargeStatusFilter" value={status} onChange={(event) => setStatus(event.target.value)}>
            <option value="">All</option>
            <option value="Pending">Pending</option>
            <option value="Charged">Charged</option>
            <option value="Waived">Waived</option>
            <option value="Cancelled">Cancelled</option>
          </select>
        </div>
        <div className="field">
          <label htmlFor="chargeBookingFilter">Booking id</label>
          <input
            id="chargeBookingFilter"
            value={bookingId}
            onChange={(event) => setBookingId(event.target.value)}
            placeholder="Optional booking id"
          />
        </div>
        <button type="button" className="btn" onClick={() => void load()} disabled={busy}>
          Refresh
        </button>
      </div>

      <p className="muted">
        {totalCount} charge(s) totalling {money(totalAmount, rows[0]?.currencyCode ?? '')}
      </p>

      {rows.length === 0 ? (
        <div className="empty-state">No charges match the current filter.</div>
      ) : (
        <table>
          <thead>
            <tr>
              <th>Description</th>
              <th>Type</th>
              <th>Amount</th>
              <th>Status</th>
              <th>Booking</th>
              <th>Account transaction</th>
              <th>Journal</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((charge) => (
              <tr key={charge.id}>
                <td>{charge.description}</td>
                <td>{charge.chargeType}</td>
                <td>{money(charge.amount, charge.currencyCode)}</td>
                <td>
                  <span className={chargePill(charge.status)}>{charge.status}</span>
                </td>
                <td>{canCharge ? <Link to={`/golf/bookings/${charge.bookingId}`}>Open</Link> : charge.bookingId.slice(0, 8)}</td>
                <td>{charge.accountTransactionId ? charge.accountTransactionId.slice(0, 8) : '—'}</td>
                <td>{charge.journalId ? charge.journalId.slice(0, 8) : '—'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
}
