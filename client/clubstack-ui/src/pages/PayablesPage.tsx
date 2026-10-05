import { useEffect, useState, type FormEvent } from 'react';
import { describeError, json } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import type { Payable, PayableSearch } from '../types';

const PAYABLE_STATUS: Record<number, string> = { 0: 'Open', 1: 'PartiallyPaid', 2: 'Paid', 3: 'Cancelled' };

export function PayablesPage() {
  const { can } = useAuth();
  const [result, setResult] = useState<PayableSearch | null>(null);
  const [status, setStatus] = useState('');
  const [supplierId, setSupplierId] = useState('');
  const [asOf, setAsOf] = useState('');
  const [expanded, setExpanded] = useState<string | null>(null);
  const [cancelTarget, setCancelTarget] = useState<Payable | null>(null);
  const [reason, setReason] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  const canView = can('Finance.AP.View');
  const canManage = can('Finance.AP.Manage');

  const load = async () => {
    setError('');
    try {
      const parameters = new URLSearchParams({ pageSize: '100' });
      if (status) {
        parameters.set('status', status);
      }
      if (supplierId.trim()) {
        parameters.set('supplierId', supplierId.trim());
      }
      if (asOf) {
        parameters.set('asOf', new Date(asOf).toISOString());
      }
      setResult(await json.get<PayableSearch>(`/api/v1/finance/payables?${parameters.toString()}`));
    } catch (err) {
      setError(describeError(err));
    }
  };

  useEffect(() => {
    if (canView) {
      void load();
    }
  }, [canView]);

  const cancel = async (event: FormEvent) => {
    event.preventDefault();
    if (!cancelTarget) {
      return;
    }
    setBusy(true);
    try {
      await json.post<Payable>(`/api/v1/finance/payables/${cancelTarget.id}/cancel`, { reason: reason.trim() });
      setCancelTarget(null);
      setReason('');
      await load();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  if (!canView) {
    return <div className="empty-state">You do not have permission to view accounts payable.</div>;
  }

  const expandedPayable = result?.items.find((item) => item.id === expanded) ?? null;

  return (
    <>
      <h1 className="page-title">Accounts payable</h1>
      <p className="page-subtitle">
        A supplier invoice is raised by the D6 procurement operations, so there is no manual create here. A document is
        settled by allocating a payment made against it.
      </p>

      {error ? <div className="alert alert-error">{error}</div> : null}

      <div className="toolbar">
        <select aria-label="Status" value={status} onChange={(event) => setStatus(event.target.value)}>
          <option value="">All statuses</option>
          {Object.entries(PAYABLE_STATUS).map(([value, label]) => (
            <option key={value} value={value}>
              {label}
            </option>
          ))}
        </select>
        <input
          aria-label="Supplier id"
          placeholder="Supplier id"
          value={supplierId}
          onChange={(event) => setSupplierId(event.target.value)}
        />
        <input aria-label="As of" type="date" value={asOf} onChange={(event) => setAsOf(event.target.value)} />
        <button className="btn" onClick={() => void load()}>
          Filter
        </button>
        {result ? (
          <span className="muted">
            Total outstanding: {result.totalOutstanding.toFixed(2)} ({result.totalCount} documents)
          </span>
        ) : null}
      </div>

      {result === null ? (
        <div className="empty-state">Loading payables…</div>
      ) : result.items.length === 0 ? (
        <div className="empty-state">No payables match the current filters.</div>
      ) : (
        <table>
          <thead>
            <tr>
              <th>Reference</th>
              <th>Supplier</th>
              <th>Source</th>
              <th>Original</th>
              <th>Allocated</th>
              <th>Outstanding</th>
              <th>Date</th>
              <th>Due</th>
              <th>Status</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {result.items.map((payable) => (
              <tr key={payable.id}>
                <td>{payable.reference}</td>
                <td>
                  {payable.supplierName} ({payable.supplierCode})
                </td>
                <td className="muted">
                  {payable.sourceType}
                  {payable.sourceReference ? ` · ${payable.sourceReference}` : ''}
                </td>
                <td>{payable.originalAmount.toFixed(2)}</td>
                <td>{payable.allocatedAmount.toFixed(2)}</td>
                <td>{payable.outstandingAmount.toFixed(2)}</td>
                <td className="muted">{new Date(payable.transactionDate).toLocaleString()}</td>
                <td className="muted">{payable.dueDate ? new Date(payable.dueDate).toLocaleString() : '—'}</td>
                <td>
                  <span className={`status-pill ${payable.status === 2 ? 'good' : payable.status === 3 ? 'bad' : ''}`}>
                    {PAYABLE_STATUS[payable.status]}
                  </span>
                </td>
                <td>
                  <div className="row-actions">
                    <button
                      className="link-button"
                      onClick={() => setExpanded(expanded === payable.id ? null : payable.id)}
                    >
                      {expanded === payable.id ? 'Hide allocations' : 'Allocations'}
                    </button>
                    {payable.status === 0 && canManage ? (
                      <button
                        className="btn btn-danger"
                        onClick={() => {
                          setCancelTarget(payable);
                          setReason('');
                        }}
                      >
                        Cancel
                      </button>
                    ) : null}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {expandedPayable ? (
        <div className="card" style={{ marginTop: 18 }}>
          <h3>Allocations — {expandedPayable.reference}</h3>
          {expandedPayable.allocations.length === 0 ? (
            <div className="empty-state">No payments have been allocated to this document.</div>
          ) : (
            <table>
              <thead>
                <tr>
                  <th>Payment</th>
                  <th>Amount</th>
                  <th>Allocated</th>
                  <th>Allocated by</th>
                </tr>
              </thead>
              <tbody>
                {expandedPayable.allocations.map((allocation) => (
                  <tr key={allocation.id}>
                    <td>{allocation.paymentReference}</td>
                    <td>{allocation.amount.toFixed(2)}</td>
                    <td className="muted">{new Date(allocation.allocatedAtUtc).toLocaleString()}</td>
                    <td className="muted">{allocation.allocatedByUserId}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      ) : null}

      {cancelTarget ? (
        <div className="card" style={{ marginTop: 18 }}>
          <h3>Cancel payable {cancelTarget.reference}</h3>
          <form className="form-grid" onSubmit={(event) => void cancel(event)}>
            <div className="field">
              <label htmlFor="apCancelReason">Reason *</label>
              <input
                id="apCancelReason"
                required
                maxLength={512}
                value={reason}
                onChange={(event) => setReason(event.target.value)}
              />
            </div>
            <div className="input-row">
              <button type="submit" className="btn btn-danger" disabled={busy || reason.trim() === ''}>
                Cancel payable
              </button>
              <button type="button" className="btn" onClick={() => setCancelTarget(null)}>
                Close
              </button>
            </div>
          </form>
        </div>
      ) : null}
    </>
  );
}
