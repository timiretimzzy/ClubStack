import { useEffect, useState, type FormEvent } from 'react';
import { describeError, json } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import type { FinancialAccount, Journal, JournalSearch, JournalValidation } from '../types';

const JOURNAL_STATUS: Record<number, string> = { 0: 'Draft', 1: 'Posted', 2: 'Cancelled' };
const DIRECTION: Record<number, string> = { 0: 'Debit', 1: 'Credit' };

const EMPTY_FORM = {
  description: '',
  transactionDate: new Date().toISOString().slice(0, 10),
  businessDate: '',
  currencyCode: '',
  exchangeRate: '',
  rateDateUtc: '',
  financialPeriodId: '',
  sourceReference: '',
};

const EMPTY_LINE = { financialAccountId: '', direction: '0', amount: '', description: '' };

export function JournalsPage() {
  const { can } = useAuth();
  const [result, setResult] = useState<JournalSearch | null>(null);
  const [accounts, setAccounts] = useState<FinancialAccount[]>([]);
  const [status, setStatus] = useState('');
  const [sourceType, setSourceType] = useState('');
  const [fromDate, setFromDate] = useState('');
  const [toDate, setToDate] = useState('');
  const [selected, setSelected] = useState<Journal | null>(null);
  const [validation, setValidation] = useState<JournalValidation | null>(null);
  const [reason, setReason] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [creating, setCreating] = useState(false);
  const [form, setForm] = useState(EMPTY_FORM);
  const [lines, setLines] = useState([EMPTY_LINE, EMPTY_LINE]);

  const canView = can('Finance.Journal.View');
  const canCreate = can('Finance.Journal.Create');
  const canPost = can('Finance.Journal.Post');
  const canReverse = can('Finance.Journal.Reverse');

  const load = async () => {
    setError('');
    try {
      const parameters = new URLSearchParams({ pageSize: '100' });
      if (status) {
        parameters.set('status', status);
      }
      if (sourceType.trim()) {
        parameters.set('sourceType', sourceType.trim());
      }
      if (fromDate) {
        parameters.set('fromDate', new Date(fromDate).toISOString());
      }
      if (toDate) {
        parameters.set('toDate', new Date(toDate).toISOString());
      }
      setResult(await json.get<JournalSearch>(`/api/v1/finance/journals?${parameters.toString()}`));
    } catch (err) {
      setError(describeError(err));
    }
  };

  useEffect(() => {
    if (!canView) {
      return;
    }
    void load();
    void (async () => {
      try {
        const [search, baseCurrency] = await Promise.all([
          json.get<{ items: FinancialAccount[] }>('/api/v1/finance/accounts?pageSize=200'),
          json.get<string>('/api/v1/finance/accounts/base-currency'),
        ]);
        setAccounts(search.items);
        setForm((current) => (current.currencyCode === '' ? { ...current, currencyCode: baseCurrency } : current));
      } catch (err) {
        setError(describeError(err));
      }
    })();
  }, [canView]);

  const open = async (id: string) => {
    setBusy(true);
    setError('');
    setValidation(null);
    setReason('');
    try {
      setSelected(await json.get<Journal>(`/api/v1/finance/journals/${id}`));
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const validate = async () => {
    if (!selected) {
      return;
    }
    setBusy(true);
    setError('');
    try {
      setValidation(await json.get<JournalValidation>(`/api/v1/finance/journals/${selected.id}/validation`));
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const runAction = async (action: 'post' | 'cancel' | 'reverse') => {
    if (!selected) {
      return;
    }
    setBusy(true);
    setError('');
    try {
      const updated =
        action === 'post'
          ? await json.post<Journal>(`/api/v1/finance/journals/${selected.id}/post`)
          : await json.post<Journal>(`/api/v1/finance/journals/${selected.id}/${action}`, { reason: reason.trim() });
      setSelected(updated);
      setValidation(null);
      setReason('');
      await load();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const create = async (event?: FormEvent) => {
    event?.preventDefault();
    setBusy(true);
    setError('');
    try {
      const created = await json.post<Journal>('/api/v1/finance/journals', {
        description: form.description,
        transactionDate: new Date(form.transactionDate).toISOString(),
        businessDate: form.businessDate ? new Date(form.businessDate).toISOString() : null,
        currencyCode: form.currencyCode.toUpperCase(),
        exchangeRate: form.exchangeRate ? Number(form.exchangeRate) : null,
        rateDateUtc: form.rateDateUtc ? new Date(form.rateDateUtc).toISOString() : null,
        financialPeriodId: form.financialPeriodId.trim() || null,
        sourceReference: form.sourceReference || null,
        lines: lines.map((line) => ({
          financialAccountId: line.financialAccountId,
          direction: Number(line.direction),
          amount: Number(line.amount),
          description: line.description || null,
        })),
      });
      setCreating(false);
      setForm({ ...EMPTY_FORM, currencyCode: form.currencyCode });
      setLines([EMPTY_LINE, EMPTY_LINE]);
      setSelected(created);
      setValidation(null);
      await load();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  if (!canView) {
    return <div className="empty-state">You do not have permission to view journals.</div>;
  }

  return (
    <>
      <h1 className="page-title">Journals</h1>
      <p className="page-subtitle">
        Draft, post, cancel and reverse journals. Posting is irreversible: a posted journal is only corrected by a
        compensating reversal.
      </p>

      {error ? <div className="alert alert-error">{error}</div> : null}

      <div className="toolbar">
        <select aria-label="Status" value={status} onChange={(event) => setStatus(event.target.value)}>
          <option value="">All statuses</option>
          {Object.entries(JOURNAL_STATUS).map(([value, label]) => (
            <option key={value} value={value}>
              {label}
            </option>
          ))}
        </select>
        <input
          aria-label="Source type"
          placeholder="Source type"
          value={sourceType}
          onChange={(event) => setSourceType(event.target.value)}
        />
        <input aria-label="From" type="date" value={fromDate} onChange={(event) => setFromDate(event.target.value)} />
        <input aria-label="To" type="date" value={toDate} onChange={(event) => setToDate(event.target.value)} />
        <button className="btn" onClick={() => void load()}>
          Filter
        </button>
        {canCreate ? (
          <button className="btn btn-primary" onClick={() => setCreating((value) => !value)}>
            {creating ? 'Close' : 'New draft journal'}
          </button>
        ) : null}
      </div>

      {creating ? (
        <div className="card">
          <h3>New draft journal</h3>
          <form className="form-grid" onSubmit={(event) => void create(event)}>
            <div className="field">
              <label htmlFor="journalDescription">Description *</label>
              <input
                id="journalDescription"
                required
                maxLength={512}
                value={form.description}
                onChange={(event) => setForm({ ...form, description: event.target.value })}
              />
            </div>
            <div className="field">
              <label htmlFor="transactionDate">Transaction date *</label>
              <input
                id="transactionDate"
                type="date"
                required
                value={form.transactionDate}
                onChange={(event) => setForm({ ...form, transactionDate: event.target.value })}
              />
            </div>
            <div className="field">
              <label htmlFor="businessDate">Business date</label>
              <input
                id="businessDate"
                type="date"
                value={form.businessDate}
                onChange={(event) => setForm({ ...form, businessDate: event.target.value })}
              />
            </div>
            <div className="field">
              <label htmlFor="journalCurrency">Currency *</label>
              <input
                id="journalCurrency"
                required
                maxLength={8}
                value={form.currencyCode}
                onChange={(event) => setForm({ ...form, currencyCode: event.target.value })}
              />
            </div>
            <div className="field">
              <label htmlFor="exchangeRate">Exchange rate</label>
              <input
                id="exchangeRate"
                type="number"
                min={0.000001}
                step={0.000001}
                value={form.exchangeRate}
                onChange={(event) => setForm({ ...form, exchangeRate: event.target.value })}
              />
            </div>
            <div className="field">
              <label htmlFor="rateDate">Rate date</label>
              <input
                id="rateDate"
                type="date"
                value={form.rateDateUtc}
                onChange={(event) => setForm({ ...form, rateDateUtc: event.target.value })}
              />
            </div>
            <div className="field">
              <label htmlFor="periodId">Financial period id</label>
              <input
                id="periodId"
                placeholder="Resolved from the transaction date when empty"
                value={form.financialPeriodId}
                onChange={(event) => setForm({ ...form, financialPeriodId: event.target.value })}
              />
            </div>
            <div className="field">
              <label htmlFor="sourceReference">Source reference</label>
              <input
                id="sourceReference"
                maxLength={128}
                value={form.sourceReference}
                onChange={(event) => setForm({ ...form, sourceReference: event.target.value })}
              />
            </div>
          </form>

          <h3 style={{ marginTop: 16 }}>Lines</h3>
          <table>
            <thead>
              <tr>
                <th>Account</th>
                <th>Direction</th>
                <th>Amount</th>
                <th>Description</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {lines.map((line, index) => (
                <tr key={index}>
                  <td>
                    <select
                      aria-label="Account"
                      value={line.financialAccountId}
                      onChange={(event) =>
                        setLines(lines.map((current, position) => (position === index ? { ...current, financialAccountId: event.target.value } : current)))
                      }
                    >
                      <option value="">Select an account</option>
                      {accounts.map((account) => (
                        <option key={account.id} value={account.id}>
                          {account.code} — {account.name}
                        </option>
                      ))}
                    </select>
                  </td>
                  <td>
                    <select
                      aria-label="Direction"
                      value={line.direction}
                      onChange={(event) =>
                        setLines(lines.map((current, position) => (position === index ? { ...current, direction: event.target.value } : current)))
                      }
                    >
                      {Object.entries(DIRECTION).map(([value, label]) => (
                        <option key={value} value={value}>
                          {label}
                        </option>
                      ))}
                    </select>
                  </td>
                  <td>
                    <input
                      aria-label="Amount"
                      type="number"
                      min={0.0001}
                      step={0.01}
                      value={line.amount}
                      onChange={(event) =>
                        setLines(lines.map((current, position) => (position === index ? { ...current, amount: event.target.value } : current)))
                      }
                    />
                  </td>
                  <td>
                    <input
                      aria-label="Line description"
                      value={line.description}
                      onChange={(event) =>
                        setLines(lines.map((current, position) => (position === index ? { ...current, description: event.target.value } : current)))
                      }
                    />
                  </td>
                  <td>
                    <button
                      type="button"
                      className="link-button"
                      disabled={lines.length <= 2}
                      onClick={() => setLines(lines.filter((_current, position) => position !== index))}
                    >
                      Remove
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          <div className="input-row" style={{ marginTop: 12 }}>
            <button type="button" className="btn" onClick={() => setLines([...lines, EMPTY_LINE])}>
              Add line
            </button>
            <button type="button" className="btn btn-primary" disabled={busy} onClick={() => void create()}>
              Create draft
            </button>
          </div>
        </div>
      ) : null}

      {result === null ? (
        <div className="empty-state">Loading journals…</div>
      ) : result.items.length === 0 ? (
        <div className="empty-state">No journals match the current filters.</div>
      ) : (
        <table>
          <thead>
            <tr>
              <th>Reference</th>
              <th>Date</th>
              <th>Description</th>
              <th>Status</th>
              <th>Period</th>
              <th>Source</th>
              <th>Debit</th>
              <th>Credit</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {result.items.map((journal) => (
              <tr key={journal.id}>
                <td>{journal.reference}</td>
                <td className="muted">{new Date(journal.transactionDate).toLocaleString()}</td>
                <td>{journal.description}</td>
                <td>
                  <span className={`status-pill ${journal.status === 1 ? 'good' : journal.status === 2 ? 'bad' : ''}`}>
                    {JOURNAL_STATUS[journal.status]}
                  </span>
                </td>
                <td className="muted">{journal.financialPeriodReference}</td>
                <td className="muted">
                  {journal.sourceType}
                  {journal.sourceReference ? ` · ${journal.sourceReference}` : ''}
                </td>
                <td>{journal.totalDebit.toFixed(2)}</td>
                <td>{journal.totalCredit.toFixed(2)}</td>
                <td>
                  <div className="row-actions">
                    <button className="link-button" onClick={() => void open(journal.id)}>
                      View
                    </button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {selected ? (
        <div className="card" style={{ marginTop: 18 }}>
          <h3>
            {selected.reference}{' '}
            <span className={`status-pill ${selected.status === 1 ? 'good' : selected.status === 2 ? 'bad' : ''}`}>
              {JOURNAL_STATUS[selected.status]}
            </span>
          </h3>
          <div className="detail-grid">
            <div>
              <div className="field-label">Description</div>
              <div className="field-value">{selected.description}</div>
            </div>
            <div>
              <div className="field-label">Period</div>
              <div className="field-value">{selected.financialPeriodReference}</div>
            </div>
            <div>
              <div className="field-label">Transaction currency</div>
              <div className="field-value">
                {selected.transactionCurrencyCode} @ {selected.exchangeRate.toFixed(4)}
              </div>
            </div>
            <div>
              <div className="field-label">Source</div>
              <div className="field-value">
                {selected.sourceType}
                {selected.sourceReference ? ` · ${selected.sourceReference}` : ''}
              </div>
            </div>
            <div>
              <div className="field-label">Reversal of</div>
              <div className="field-value">{selected.reversalOfJournalReference ?? '—'}</div>
            </div>
            <div>
              <div className="field-label">Posted</div>
              <div className="field-value">
                {selected.postedAtUtc ? `${new Date(selected.postedAtUtc).toLocaleString()} · ${selected.postedByUserId}` : '—'}
              </div>
            </div>
            <div>
              <div className="field-label">Cancelled reason</div>
              <div className="field-value">{selected.cancelledReason || '—'}</div>
            </div>
            <div>
              <div className="field-label">Created by</div>
              <div className="field-value">{selected.createdByUserId}</div>
            </div>
          </div>

          <h3 style={{ marginTop: 16 }}>Journal entry</h3>
          <table>
            <thead>
              <tr>
                <th>Date</th>
                <th>Reference</th>
                <th>Description</th>
                <th>Account</th>
                <th>Debit</th>
                <th>Credit</th>
              </tr>
            </thead>
            <tbody>
              {selected.lines.map((line) => (
                <tr key={line.id}>
                  <td className="muted">{new Date(selected.transactionDate).toLocaleString()}</td>
                  <td>{selected.reference}</td>
                  <td>{line.description || selected.description}</td>
                  <td>
                    {line.financialAccountCode} — {line.financialAccountName}
                  </td>
                  <td>{line.direction === 0 ? line.amount.toFixed(2) : ''}</td>
                  <td>{line.direction === 1 ? line.amount.toFixed(2) : ''}</td>
                </tr>
              ))}
              <tr>
                <td />
                <td />
                <td>Total</td>
                <td />
                <td>{selected.totalDebit.toFixed(2)}</td>
                <td>{selected.totalCredit.toFixed(2)}</td>
              </tr>
            </tbody>
          </table>

          <div className="input-row" style={{ marginTop: 12 }}>
            <button className="btn" disabled={busy} onClick={() => void validate()}>
              Validate
            </button>
            {selected.status === 0 && canPost ? (
              <button className="btn btn-primary" disabled={busy} onClick={() => void runAction('post')}>
                Post journal
              </button>
            ) : null}
            <button className="link-button" onClick={() => setSelected(null)}>
              Close
            </button>
          </div>

          {validation ? (
            <div style={{ marginTop: 12 }}>
              <div className="input-row">
                <span className={`status-pill ${validation.isValid ? 'good' : 'bad'}`}>
                  {validation.isValid ? 'Valid' : 'Invalid'}
                </span>
                <span className="muted">
                  {validation.lineCount} lines · debit {validation.totalDebit.toFixed(2)} · credit{' '}
                  {validation.totalCredit.toFixed(2)}
                </span>
              </div>
              {validation.errors.length > 0 ? (
                <ul>
                  {validation.errors.map((message) => (
                    <li key={message}>{message}</li>
                  ))}
                </ul>
              ) : null}
            </div>
          ) : null}

          {selected.status === 0 && canPost ? (
            <div className="toolbar" style={{ marginTop: 12 }}>
              <div className="field">
                <label htmlFor="cancelReason">Cancellation reason</label>
                <input
                  id="cancelReason"
                  value={reason}
                  onChange={(event) => setReason(event.target.value)}
                  placeholder="Required to cancel a draft"
                />
              </div>
              <button
                className="btn btn-danger"
                disabled={busy || reason.trim() === ''}
                onClick={() => void runAction('cancel')}
              >
                Cancel journal
              </button>
            </div>
          ) : null}

          {selected.status === 1 && canReverse ? (
            <div className="toolbar" style={{ marginTop: 12 }}>
              <div className="field">
                <label htmlFor="reverseReason">Reversal reason</label>
                <input
                  id="reverseReason"
                  value={reason}
                  onChange={(event) => setReason(event.target.value)}
                  placeholder="Required to reverse a posted journal"
                />
              </div>
              <button
                className="btn btn-danger"
                disabled={busy || reason.trim() === ''}
                onClick={() => void runAction('reverse')}
              >
                Reverse journal
              </button>
            </div>
          ) : null}
        </div>
      ) : null}
    </>
  );
}
