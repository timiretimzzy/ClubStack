import { useEffect, useState, type FormEvent } from 'react';
import { describeError, json } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import type { FinancialPeriod, FinancialPeriodSearch } from '../types';

const PERIOD_STATUS: Record<number, string> = { 0: 'Open', 1: 'Closed' };

export function FinancialPeriodsPage() {
  const { can } = useAuth();
  const [result, setResult] = useState<FinancialPeriodSearch | null>(null);
  const [status, setStatus] = useState('');
  const [year, setYear] = useState('');
  const [opening, setOpening] = useState(false);
  const [form, setForm] = useState({ year: String(new Date().getFullYear()), month: '1', name: '' });
  const [reopenTarget, setReopenTarget] = useState<FinancialPeriod | null>(null);
  const [reason, setReason] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  const canView = can('Finance.Period.View');
  const canOpen = can('Finance.Period.Open');
  const canClose = can('Finance.Period.Close');
  const canReopen = can('Finance.Period.Reopen');

  const load = async () => {
    setError('');
    try {
      const parameters = new URLSearchParams({ pageSize: '100' });
      if (status) {
        parameters.set('status', status);
      }
      if (year.trim()) {
        parameters.set('year', year.trim());
      }
      setResult(await json.get<FinancialPeriodSearch>(`/api/v1/finance/periods?${parameters.toString()}`));
    } catch (err) {
      setError(describeError(err));
    }
  };

  useEffect(() => {
    if (canView) {
      void load();
    }
  }, [canView]);

  const open = async (event: FormEvent) => {
    event.preventDefault();
    setBusy(true);
    try {
      await json.post<FinancialPeriod>('/api/v1/finance/periods', {
        year: Number(form.year),
        month: Number(form.month),
        name: form.name || null,
      });
      setOpening(false);
      await load();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const close = async (period: FinancialPeriod) => {
    setBusy(true);
    try {
      await json.post<FinancialPeriod>(`/api/v1/finance/periods/${period.id}/close`);
      await load();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const reopen = async (event: FormEvent) => {
    event.preventDefault();
    if (!reopenTarget) {
      return;
    }
    setBusy(true);
    try {
      await json.post<FinancialPeriod>(`/api/v1/finance/periods/${reopenTarget.id}/reopen`, { reason: reason.trim() });
      setReopenTarget(null);
      setReason('');
      await load();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  if (!canView) {
    return <div className="empty-state">You do not have permission to view financial periods.</div>;
  }

  return (
    <>
      <h1 className="page-title">Financial periods</h1>
      <p className="page-subtitle">
        A journal can only be posted into an open period. Only the most recently closed period can be reopened, and
        only with a recorded reason.
      </p>

      {error ? <div className="alert alert-error">{error}</div> : null}

      <div className="toolbar">
        <select aria-label="Status" value={status} onChange={(event) => setStatus(event.target.value)}>
          <option value="">All statuses</option>
          {Object.entries(PERIOD_STATUS).map(([value, label]) => (
            <option key={value} value={value}>
              {label}
            </option>
          ))}
        </select>
        <input
          aria-label="Year"
          type="number"
          placeholder="Year"
          value={year}
          onChange={(event) => setYear(event.target.value)}
        />
        <button className="btn" onClick={() => void load()}>
          Filter
        </button>
        {canOpen ? (
          <button className="btn btn-primary" onClick={() => setOpening((value) => !value)}>
            {opening ? 'Close' : 'Open a period'}
          </button>
        ) : null}
      </div>

      {opening ? (
        <div className="card">
          <h3>Open a period</h3>
          <form className="form-grid" onSubmit={(event) => void open(event)}>
            <div className="field">
              <label htmlFor="periodYear">Year *</label>
              <input
                id="periodYear"
                type="number"
                min={2000}
                max={9999}
                step={1}
                required
                value={form.year}
                onChange={(event) => setForm({ ...form, year: event.target.value })}
              />
            </div>
            <div className="field">
              <label htmlFor="periodMonth">Month *</label>
              <select
                id="periodMonth"
                value={form.month}
                onChange={(event) => setForm({ ...form, month: event.target.value })}
              >
                {Array.from({ length: 12 }, (_value, index) => String(index + 1)).map((value) => (
                  <option key={value} value={value}>
                    {value}
                  </option>
                ))}
              </select>
            </div>
            <div className="field">
              <label htmlFor="periodName">Name</label>
              <input
                id="periodName"
                maxLength={128}
                value={form.name}
                onChange={(event) => setForm({ ...form, name: event.target.value })}
              />
            </div>
            <button type="submit" className="btn btn-primary" disabled={busy}>
              Open period
            </button>
          </form>
        </div>
      ) : null}

      {result === null ? (
        <div className="empty-state">Loading periods…</div>
      ) : result.items.length === 0 ? (
        <div className="empty-state">No periods match the current filters.</div>
      ) : (
        <table>
          <thead>
            <tr>
              <th>Reference</th>
              <th>Year / month</th>
              <th>Name</th>
              <th>Window</th>
              <th>Status</th>
              <th>Posted journals</th>
              <th>Posted debit</th>
              <th>Posted credit</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {result.items.map((period) => (
              <tr key={period.id}>
                <td>{period.reference}</td>
                <td>
                  {period.year} / {String(period.month).padStart(2, '0')}
                </td>
                <td>
                  {period.name}
                  {period.reopenReason ? <div className="muted">Reopened: {period.reopenReason}</div> : null}
                </td>
                <td className="muted">
                  {new Date(period.startUtc).toLocaleString()} → {new Date(period.endUtc).toLocaleString()}
                </td>
                <td>
                  <span className={`status-pill ${period.status === 0 ? 'good' : 'bad'}`}>
                    {PERIOD_STATUS[period.status]}
                  </span>
                </td>
                <td>{period.postedJournalCount}</td>
                <td>{period.postedDebitTotal.toFixed(2)}</td>
                <td>{period.postedCreditTotal.toFixed(2)}</td>
                <td>
                  <div className="row-actions">
                    {period.status === 0 && canClose ? (
                      <button className="btn" disabled={busy} onClick={() => void close(period)}>
                        Close
                      </button>
                    ) : null}
                    {period.status === 1 && canReopen ? (
                      <button
                        className="btn"
                        onClick={() => {
                          setReopenTarget(period);
                          setReason('');
                        }}
                      >
                        Reopen
                      </button>
                    ) : null}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {reopenTarget ? (
        <div className="card" style={{ marginTop: 18 }}>
          <h3>Reopen period {reopenTarget.reference}</h3>
          <form className="form-grid" onSubmit={(event) => void reopen(event)}>
            <div className="field">
              <label htmlFor="reopenReason">Reason *</label>
              <input
                id="reopenReason"
                required
                maxLength={512}
                value={reason}
                onChange={(event) => setReason(event.target.value)}
              />
            </div>
            <div className="input-row">
              <button type="submit" className="btn btn-primary" disabled={busy || reason.trim() === ''}>
                Reopen period
              </button>
              <button type="button" className="btn" onClick={() => setReopenTarget(null)}>
                Close
              </button>
            </div>
          </form>
        </div>
      ) : null}
    </>
  );
}
