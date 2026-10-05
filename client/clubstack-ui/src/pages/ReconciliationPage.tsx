import { useEffect, useState, type FormEvent } from 'react';
import { describeError, json } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import type { FinancialAccount, Reconciliation, ReconciliationSearch } from '../types';

const RECONCILIATION_STATUS: Record<number, string> = { 0: 'InProgress', 1: 'Completed', 2: 'Cancelled' };
const CASH_KIND: Record<number, string> = { 0: 'None', 1: 'Cash on hand', 2: 'Bank' };

const EMPTY_FORM = {
  financialAccountId: '',
  statementDateUtc: new Date().toISOString().slice(0, 10),
  statementClosingBalance: '',
  note: '',
};

const EMPTY_MATCH = { journalId: '', ledgerAmount: '', note: '' };

export function ReconciliationPage() {
  const { can } = useAuth();
  const [result, setResult] = useState<ReconciliationSearch | null>(null);
  const [accounts, setAccounts] = useState<FinancialAccount[]>([]);
  const [accountId, setAccountId] = useState('');
  const [status, setStatus] = useState('');
  const [creating, setCreating] = useState(false);
  const [form, setForm] = useState(EMPTY_FORM);
  const [expanded, setExpanded] = useState<string | null>(null);
  const [match, setMatch] = useState(EMPTY_MATCH);
  const [completeNote, setCompleteNote] = useState('');
  const [cancelReason, setCancelReason] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  const canPerform = can('Finance.Reconciliation.Perform');

  const load = async () => {
    setError('');
    try {
      const parameters = new URLSearchParams({ pageSize: '100' });
      if (accountId) {
        parameters.set('financialAccountId', accountId);
      }
      if (status) {
        parameters.set('status', status);
      }
      setResult(await json.get<ReconciliationSearch>(`/api/v1/finance/reconciliations?${parameters.toString()}`));
    } catch (err) {
      setError(describeError(err));
    }
  };

  useEffect(() => {
    if (!canPerform) {
      return;
    }
    void load();
    void (async () => {
      try {
        const search = await json.get<{ items: FinancialAccount[] }>('/api/v1/finance/accounts?pageSize=200');
        setAccounts(search.items.filter((account) => account.cashKind !== 0));
      } catch (err) {
        setError(describeError(err));
      }
    })();
  }, [canPerform]);

  const create = async (event: FormEvent) => {
    event.preventDefault();
    setBusy(true);
    try {
      await json.post<Reconciliation>('/api/v1/finance/reconciliations', {
        financialAccountId: form.financialAccountId,
        statementDateUtc: new Date(form.statementDateUtc).toISOString(),
        statementClosingBalance: Number(form.statementClosingBalance),
        note: form.note || null,
      });
      setCreating(false);
      setForm({ ...EMPTY_FORM, financialAccountId: form.financialAccountId });
      await load();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const addMatch = async (reconciliationId: string) => {
    setBusy(true);
    try {
      await json.post<Reconciliation>(`/api/v1/finance/reconciliations/${reconciliationId}/matches`, {
        journalId: match.journalId,
        ledgerAmount: Number(match.ledgerAmount),
        note: match.note || null,
      });
      setMatch(EMPTY_MATCH);
      await load();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const removeMatch = async (reconciliationId: string, matchId: string) => {
    setBusy(true);
    try {
      await json.delete(`/api/v1/finance/reconciliations/${reconciliationId}/matches/${matchId}`);
      await load();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const complete = async (reconciliationId: string) => {
    setBusy(true);
    try {
      await json.post<Reconciliation>(`/api/v1/finance/reconciliations/${reconciliationId}/complete`, {
        note: completeNote || null,
      });
      setCompleteNote('');
      await load();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const cancel = async (reconciliationId: string) => {
    setBusy(true);
    try {
      await json.post<Reconciliation>(`/api/v1/finance/reconciliations/${reconciliationId}/cancel`, {
        reason: cancelReason.trim(),
      });
      setCancelReason('');
      await load();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  if (!canPerform) {
    return <div className="empty-state">You do not have permission to perform reconciliation.</div>;
  }

  const expandedReconciliation = result?.items.find((item) => item.id === expanded) ?? null;

  return (
    <>
      <h1 className="page-title">Reconciliation</h1>
      <p className="page-subtitle">
        Reconciliation is entirely manual: there is no bank feed and no statement import. Open a reconciliation for a
        cash or bank account, match ledger entries by hand, then complete it once the variance is explained.
      </p>

      {error ? <div className="alert alert-error">{error}</div> : null}

      <div className="toolbar">
        <select aria-label="Account" value={accountId} onChange={(event) => setAccountId(event.target.value)}>
          <option value="">All cash and bank accounts</option>
          {accounts.map((account) => (
            <option key={account.id} value={account.id}>
              {account.code} — {account.name}
            </option>
          ))}
        </select>
        <select aria-label="Status" value={status} onChange={(event) => setStatus(event.target.value)}>
          <option value="">All statuses</option>
          {Object.entries(RECONCILIATION_STATUS).map(([value, label]) => (
            <option key={value} value={value}>
              {label}
            </option>
          ))}
        </select>
        <button className="btn" onClick={() => void load()}>
          Filter
        </button>
        <button className="btn btn-primary" onClick={() => setCreating((value) => !value)}>
          {creating ? 'Close' : 'New reconciliation'}
        </button>
      </div>

      {creating ? (
        <div className="card">
          <h3>New reconciliation</h3>
          <form className="form-grid" onSubmit={(event) => void create(event)}>
            <div className="field">
              <label htmlFor="recAccount">Cash or bank account *</label>
              <select
                id="recAccount"
                required
                value={form.financialAccountId}
                onChange={(event) => setForm({ ...form, financialAccountId: event.target.value })}
              >
                <option value="">Select an account</option>
                {accounts.map((account) => (
                  <option key={account.id} value={account.id}>
                    {account.code} — {account.name} ({CASH_KIND[account.cashKind]})
                  </option>
                ))}
              </select>
            </div>
            <div className="field">
              <label htmlFor="recStatementDate">Statement date *</label>
              <input
                id="recStatementDate"
                type="date"
                required
                value={form.statementDateUtc}
                onChange={(event) => setForm({ ...form, statementDateUtc: event.target.value })}
              />
            </div>
            <div className="field">
              <label htmlFor="recClosingBalance">Statement closing balance *</label>
              <input
                id="recClosingBalance"
                type="number"
                step={0.01}
                required
                value={form.statementClosingBalance}
                onChange={(event) => setForm({ ...form, statementClosingBalance: event.target.value })}
              />
            </div>
            <div className="field">
              <label htmlFor="recNote">Note</label>
              <input
                id="recNote"
                maxLength={1024}
                value={form.note}
                onChange={(event) => setForm({ ...form, note: event.target.value })}
              />
            </div>
            <button type="submit" className="btn btn-primary" disabled={busy}>
              Open reconciliation
            </button>
          </form>
        </div>
      ) : null}

      {result === null ? (
        <div className="empty-state">Loading reconciliations…</div>
      ) : result.items.length === 0 ? (
        <div className="empty-state">No reconciliations match the current filters.</div>
      ) : (
        <table>
          <thead>
            <tr>
              <th>Reference</th>
              <th>Account</th>
              <th>Statement date</th>
              <th>Statement balance</th>
              <th>Ledger balance</th>
              <th>Variance</th>
              <th>Matched</th>
              <th>Status</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {result.items.map((reconciliation) => (
              <tr key={reconciliation.id}>
                <td>{reconciliation.reference}</td>
                <td>
                  {reconciliation.financialAccountCode} — {reconciliation.financialAccountName}
                </td>
                <td className="muted">{new Date(reconciliation.statementDateUtc).toLocaleString()}</td>
                <td>{reconciliation.statementClosingBalance.toFixed(2)}</td>
                <td>
                  {reconciliation.ledgerClosingBalance === null
                    ? '—'
                    : reconciliation.ledgerClosingBalance.toFixed(2)}
                </td>
                <td>
                  {reconciliation.variance === null || reconciliation.variance === 0 ? (
                    <strong>{reconciliation.variance === null ? '—' : reconciliation.variance.toFixed(2)}</strong>
                  ) : (
                    <strong className="status-pill bad">{reconciliation.variance.toFixed(2)}</strong>
                  )}
                </td>
                <td>{reconciliation.matchedLedgerTotal.toFixed(2)}</td>
                <td>
                  <span
                    className={`status-pill ${
                      reconciliation.status === 1 ? 'good' : reconciliation.status === 2 ? 'bad' : ''
                    }`}
                  >
                    {RECONCILIATION_STATUS[reconciliation.status]}
                  </span>
                </td>
                <td>
                  <div className="row-actions">
                    <button
                      className="link-button"
                      onClick={() => {
                        setExpanded(expanded === reconciliation.id ? null : reconciliation.id);
                        setMatch(EMPTY_MATCH);
                        setCompleteNote('');
                        setCancelReason('');
                      }}
                    >
                      {expanded === reconciliation.id ? 'Hide' : 'Open'}
                    </button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {expandedReconciliation ? (
        <div className="card" style={{ marginTop: 18 }}>
          <h3>
            {expandedReconciliation.reference} — {expandedReconciliation.financialAccountName}
          </h3>
          <div className="detail-grid">
            <div>
              <div className="field-label">Statement date</div>
              <div className="field-value">{new Date(expandedReconciliation.statementDateUtc).toLocaleString()}</div>
            </div>
            <div>
              <div className="field-label">Statement closing balance</div>
              <div className="field-value">{expandedReconciliation.statementClosingBalance.toFixed(2)}</div>
            </div>
            <div>
              <div className="field-label">Ledger closing balance</div>
              <div className="field-value">
                {expandedReconciliation.ledgerClosingBalance === null
                  ? '—'
                  : expandedReconciliation.ledgerClosingBalance.toFixed(2)}
              </div>
            </div>
            <div>
              <div className="field-label">Variance</div>
              <div className="field-value">
                {expandedReconciliation.variance === null || expandedReconciliation.variance === 0 ? (
                  expandedReconciliation.variance === null ? '—' : expandedReconciliation.variance.toFixed(2)
                ) : (
                  <span className="status-pill bad">{expandedReconciliation.variance.toFixed(2)}</span>
                )}
              </div>
            </div>
            <div>
              <div className="field-label">Matched ledger total</div>
              <div className="field-value">{expandedReconciliation.matchedLedgerTotal.toFixed(2)}</div>
            </div>
            <div>
              <div className="field-label">Note</div>
              <div className="field-value">{expandedReconciliation.note ?? '—'}</div>
            </div>
            <div>
              <div className="field-label">Created by</div>
              <div className="field-value">{expandedReconciliation.createdByUserId}</div>
            </div>
            <div>
              <div className="field-label">Completed</div>
              <div className="field-value">
                {expandedReconciliation.completedAtUtc
                  ? `${new Date(expandedReconciliation.completedAtUtc).toLocaleString()} · ${expandedReconciliation.completedByUserId}`
                  : '—'}
              </div>
            </div>
            <div>
              <div className="field-label">Cancelled reason</div>
              <div className="field-value">{expandedReconciliation.cancelledReason ?? '—'}</div>
            </div>
          </div>

          <h3 style={{ marginTop: 16 }}>Matches</h3>
          {expandedReconciliation.matches.length === 0 ? (
            <div className="empty-state">No ledger entries have been matched yet.</div>
          ) : (
            <table>
              <thead>
                <tr>
                  <th>Journal</th>
                  <th>Date</th>
                  <th>Ledger amount</th>
                  <th>Note</th>
                  <th>Matched</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {expandedReconciliation.matches.map((entry) => (
                  <tr key={entry.id}>
                    <td>{entry.journalReference}</td>
                    <td className="muted">{new Date(entry.transactionDate).toLocaleString()}</td>
                    <td>{entry.ledgerAmount.toFixed(2)}</td>
                    <td>{entry.note ?? '—'}</td>
                    <td className="muted">
                      {new Date(entry.matchedAtUtc).toLocaleString()} · {entry.matchedByUserId}
                    </td>
                    <td>
                      {expandedReconciliation.status === 0 ? (
                        <button
                          className="link-button"
                          disabled={busy}
                          onClick={() => void removeMatch(expandedReconciliation.id, entry.id)}
                        >
                          Remove
                        </button>
                      ) : null}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}

          {expandedReconciliation.status === 0 ? (
            <>
              <h3 style={{ marginTop: 16 }}>Match a journal</h3>
              <div className="toolbar">
                <div className="field">
                  <label htmlFor="matchJournalId">Journal id *</label>
                  <input
                    id="matchJournalId"
                    value={match.journalId}
                    onChange={(event) => setMatch({ ...match, journalId: event.target.value })}
                  />
                </div>
                <div className="field">
                  <label htmlFor="matchAmount">Ledger amount *</label>
                  <input
                    id="matchAmount"
                    type="number"
                    step={0.01}
                    value={match.ledgerAmount}
                    onChange={(event) => setMatch({ ...match, ledgerAmount: event.target.value })}
                  />
                </div>
                <div className="field">
                  <label htmlFor="matchNote">Note</label>
                  <input
                    id="matchNote"
                    maxLength={512}
                    value={match.note}
                    onChange={(event) => setMatch({ ...match, note: event.target.value })}
                  />
                </div>
                <button
                  className="btn"
                  disabled={busy || match.journalId.trim() === '' || match.ledgerAmount === ''}
                  onClick={() => void addMatch(expandedReconciliation.id)}
                >
                  Add match
                </button>
              </div>

              <h3 style={{ marginTop: 16 }}>Complete</h3>
              <div className="toolbar">
                <div className="field">
                  <label htmlFor="completeNote">Completion note</label>
                  <input
                    id="completeNote"
                    maxLength={1024}
                    value={completeNote}
                    onChange={(event) => setCompleteNote(event.target.value)}
                  />
                </div>
                <button className="btn btn-primary" disabled={busy} onClick={() => void complete(expandedReconciliation.id)}>
                  Complete reconciliation
                </button>
              </div>
            </>
          ) : null}

          {expandedReconciliation.status === 0 ? (
            <div className="toolbar" style={{ marginTop: 12 }}>
              <div className="field">
                <label htmlFor="recCancelReason">Cancellation reason</label>
                <input
                  id="recCancelReason"
                  value={cancelReason}
                  onChange={(event) => setCancelReason(event.target.value)}
                />
              </div>
              <button
                className="btn btn-danger"
                disabled={busy || cancelReason.trim() === ''}
                onClick={() => void cancel(expandedReconciliation.id)}
              >
                Cancel reconciliation
              </button>
            </div>
          ) : null}
        </div>
      ) : null}
    </>
  );
}
