import { useEffect, useState, type FormEvent } from 'react';
import { describeError, json } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import type { Receivable, ReceivableSearch } from '../types';

const RECEIVABLE_STATUS: Record<number, string> = { 0: 'Open', 1: 'PartiallyPaid', 2: 'Paid', 3: 'Cancelled' };

export function ReceivablesPage() {
  const { can } = useAuth();
  const [result, setResult] = useState<ReceivableSearch | null>(null);
  const [status, setStatus] = useState('');
  const [memberId, setMemberId] = useState('');
  const [asOf, setAsOf] = useState('');
  const [expanded, setExpanded] = useState<string | null>(null);
  const [cancelTarget, setCancelTarget] = useState<Receivable | null>(null);
  const [reason, setReason] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  const canView = can('Finance.AR.View');
  const canManage = can('Finance.AR.Manage');

  const load = async () => {
    setError('');
    try {
      const parameters = new URLSearchParams({ pageSize: '100' });
      if (status) {
        parameters.set('status', status);
      }
      if (memberId.trim()) {
        parameters.set('memberId', memberId.trim());
      }
      if (asOf) {
        parameters.set('asOf', new Date(asOf).toISOString());
      }
      setResult(await json.get<ReceivableSearch>(`/api/v1/finance/receivables?${parameters.toString()}`));
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
      await json.post<Receivable>(`/api/v1/finance/receivables/${cancelTarget.id}/cancel`, { reason: reason.trim() });
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
    return <div className="empty-state">You do not have permission to view accounts receivable.</div>;
  }

  const expandedReceivable = result?.items.find((item) => item.id === expanded) ?? null;

  return (
    <>
      <h1 className="page-title">Accounts receivable</h1>
      <p className="page-subtitle">
        Receivables are raised by operational events such as fee assessments; there is no manual create here. A
        document is settled by allocating a payment against it.
      </p>

      {error ? <div className="alert alert-error">{error}</div> : null}

      <div className="toolbar">
        <select aria-label="Status" value={status} onChange={(event) => setStatus(event.target.value)}>
          <option value="">All statuses</option>
          {Object.entries(RECEIVABLE_STATUS).map(([value, label]) => (
            <option key={value} value={value}>
              {label}
            </option>
          ))}
        </select>
        <input
          aria-label="Member id"
          placeholder="Member id"
          value={memberId}
          onChange={(event) => setMemberId(event.target.value)}
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
        <div className="empty-state">Loading receivables…</div>
      ) : result.items.length === 0 ? (
        <div className="empty-state">No receivables match the current filters.</div>
      ) : (
        <table>
          <thead>
            <tr>
              <th>Reference</th>
              <th>Member</th>
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
            {result.items.map((receivable) => (
              <tr key={receivable.id}>
                <td>{receivable.reference}</td>
                <td>
                  {receivable.memberName} ({receivable.memberNumber})
                </td>
                <td className="muted">
                  {receivable.sourceType}
                  {receivable.sourceReference ? ` · ${receivable.sourceReference}` : ''}
                </td>
                <td>{receivable.originalAmount.toFixed(2)}</td>
                <td>{receivable.allocatedAmount.toFixed(2)}</td>
                <td>{receivable.outstandingAmount.toFixed(2)}</td>
                <td className="muted">{new Date(receivable.transactionDate).toLocaleString()}</td>
                <td className="muted">{receivable.dueDate ? new Date(receivable.dueDate).toLocaleString() : '—'}</td>
                <td>
                  <span className={`status-pill ${receivable.status === 2 ? 'good' : receivable.status === 3 ? 'bad' : ''}`}>
                    {RECEIVABLE_STATUS[receivable.status]}
                  </span>
                </td>
                <td>
                  <div className="row-actions">
                    <button
                      className="link-button"
                      onClick={() => setExpanded(expanded === receivable.id ? null : receivable.id)}
                    >
                      {expanded === receivable.id ? 'Hide allocations' : 'Allocations'}
                    </button>
                    {receivable.status === 0 && canManage ? (
                      <button
                        className="btn btn-danger"
                        onClick={() => {
                          setCancelTarget(receivable);
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

      {expandedReceivable ? (
        <div className="card" style={{ marginTop: 18 }}>
          <h3>Allocations — {expandedReceivable.reference}</h3>
          {expandedReceivable.allocations.length === 0 ? (
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
                {expandedReceivable.allocations.map((allocation) => (
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
          <h3>Cancel receivable {cancelTarget.reference}</h3>
          <form className="form-grid" onSubmit={(event) => void cancel(event)}>
            <div className="field">
              <label htmlFor="arCancelReason">Reason *</label>
              <input
                id="arCancelReason"
                required
                maxLength={512}
                value={reason}
                onChange={(event) => setReason(event.target.value)}
              />
            </div>
            <div className="input-row">
              <button type="submit" className="btn btn-danger" disabled={busy || reason.trim() === ''}>
                Cancel receivable
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
