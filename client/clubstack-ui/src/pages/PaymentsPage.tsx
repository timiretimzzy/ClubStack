import { useEffect, useState, type FormEvent } from 'react';
import { describeError, json } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import type { FinancePayment, FinancePaymentSearch } from '../types';

const PAYMENT_DIRECTION: Record<number, string> = { 0: 'Received', 1: 'Made' };

const EMPTY_FORM = {
  direction: '0',
  memberId: '',
  supplierId: '',
  memberAccountId: '',
  accountTransactionId: '',
  sourceType: '',
  sourceId: '',
  sourceReference: '',
  amount: '',
  currencyCode: '',
  transactionDate: new Date().toISOString().slice(0, 10),
  valueDate: '',
  paymentMethodCode: '',
  externalReference: '',
  description: '',
};

const EMPTY_ALLOCATION = { receivableId: '', payableId: '', amount: '' };

export function PaymentsPage() {
  const { can } = useAuth();
  const [result, setResult] = useState<FinancePaymentSearch | null>(null);
  const [direction, setDirection] = useState('');
  const [memberId, setMemberId] = useState('');
  const [supplierId, setSupplierId] = useState('');
  const [fromDate, setFromDate] = useState('');
  const [toDate, setToDate] = useState('');
  const [expanded, setExpanded] = useState<string | null>(null);
  const [recording, setRecording] = useState(false);
  const [form, setForm] = useState(EMPTY_FORM);
  const [allocations, setAllocations] = useState([EMPTY_ALLOCATION]);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  const canManage = can('Finance.Payment.Manage');

  const load = async () => {
    setError('');
    try {
      const parameters = new URLSearchParams({ pageSize: '100' });
      if (direction) {
        parameters.set('direction', direction);
      }
      if (memberId.trim()) {
        parameters.set('memberId', memberId.trim());
      }
      if (supplierId.trim()) {
        parameters.set('supplierId', supplierId.trim());
      }
      if (fromDate) {
        parameters.set('fromDate', new Date(fromDate).toISOString());
      }
      if (toDate) {
        parameters.set('toDate', new Date(toDate).toISOString());
      }
      setResult(await json.get<FinancePaymentSearch>(`/api/v1/finance/payments?${parameters.toString()}`));
    } catch (err) {
      setError(describeError(err));
    }
  };

  useEffect(() => {
    if (canManage) {
      void load();
    }
  }, [canManage]);

  const record = async (event?: FormEvent) => {
    event?.preventDefault();
    setBusy(true);
    setError('');
    try {
      await json.post<FinancePayment>('/api/v1/finance/payments', {
        direction: Number(form.direction),
        memberId: form.memberId.trim() || null,
        supplierId: form.supplierId.trim() || null,
        memberAccountId: form.memberAccountId.trim() || null,
        accountTransactionId: form.accountTransactionId.trim() || null,
        sourceType: form.sourceType || null,
        sourceId: form.sourceId.trim() || null,
        sourceReference: form.sourceReference || null,
        amount: Number(form.amount),
        currencyCode: form.currencyCode.toUpperCase(),
        transactionDate: new Date(form.transactionDate).toISOString(),
        valueDate: form.valueDate ? new Date(form.valueDate).toISOString() : null,
        paymentMethodCode: form.paymentMethodCode || null,
        externalReference: form.externalReference || null,
        description: form.description || null,
        idempotencyKey: `finance-payment-${crypto.randomUUID()}`,
        allocations: allocations
          .filter((allocation) => allocation.receivableId.trim() !== '' || allocation.payableId.trim() !== '')
          .map((allocation) => ({
            receivableId: allocation.receivableId.trim() || null,
            payableId: allocation.payableId.trim() || null,
            amount: Number(allocation.amount),
          })),
      });
      setRecording(false);
      setForm(EMPTY_FORM);
      setAllocations([EMPTY_ALLOCATION]);
      await load();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  if (!canManage) {
    return <div className="empty-state">You do not have permission to view finance payments.</div>;
  }

  const expandedPayment = result?.items.find((item) => item.id === expanded) ?? null;

  return (
    <>
      <h1 className="page-title">Payments</h1>
      <p className="page-subtitle">
        Record money received from members and money paid to suppliers, and allocate it to receivables and payables.
        Money that is not allocated stays as an unapplied amount.
      </p>

      {error ? <div className="alert alert-error">{error}</div> : null}

      <div className="toolbar">
        <select aria-label="Direction" value={direction} onChange={(event) => setDirection(event.target.value)}>
          <option value="">All directions</option>
          {Object.entries(PAYMENT_DIRECTION).map(([value, label]) => (
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
        <input
          aria-label="Supplier id"
          placeholder="Supplier id"
          value={supplierId}
          onChange={(event) => setSupplierId(event.target.value)}
        />
        <input aria-label="From" type="date" value={fromDate} onChange={(event) => setFromDate(event.target.value)} />
        <input aria-label="To" type="date" value={toDate} onChange={(event) => setToDate(event.target.value)} />
        <button className="btn" onClick={() => void load()}>
          Filter
        </button>
        <button className="btn btn-primary" onClick={() => setRecording((value) => !value)}>
          {recording ? 'Close' : 'Record payment'}
        </button>
      </div>

      {recording ? (
        <div className="card">
          <h3>Record payment</h3>
          <form className="form-grid" onSubmit={(event) => void record(event)}>
            <div className="field">
              <label htmlFor="paymentDirection">Direction *</label>
              <select
                id="paymentDirection"
                value={form.direction}
                onChange={(event) => setForm({ ...form, direction: event.target.value })}
              >
                {Object.entries(PAYMENT_DIRECTION).map(([value, label]) => (
                  <option key={value} value={value}>
                    {label}
                  </option>
                ))}
              </select>
            </div>
            <div className="field">
              <label htmlFor="paymentAmount">Amount *</label>
              <input
                id="paymentAmount"
                type="number"
                min={0.0001}
                step={0.01}
                required
                value={form.amount}
                onChange={(event) => setForm({ ...form, amount: event.target.value })}
              />
            </div>
            <div className="field">
              <label htmlFor="paymentCurrency">Currency *</label>
              <input
                id="paymentCurrency"
                required
                maxLength={8}
                value={form.currencyCode}
                onChange={(event) => setForm({ ...form, currencyCode: event.target.value })}
              />
            </div>
            <div className="field">
              <label htmlFor="paymentTransactionDate">Transaction date *</label>
              <input
                id="paymentTransactionDate"
                type="date"
                required
                value={form.transactionDate}
                onChange={(event) => setForm({ ...form, transactionDate: event.target.value })}
              />
            </div>
            <div className="field">
              <label htmlFor="paymentValueDate">Value date</label>
              <input
                id="paymentValueDate"
                type="date"
                value={form.valueDate}
                onChange={(event) => setForm({ ...form, valueDate: event.target.value })}
              />
            </div>
            <div className="field">
              <label htmlFor="paymentMemberId">Member id</label>
              <input
                id="paymentMemberId"
                value={form.memberId}
                onChange={(event) => setForm({ ...form, memberId: event.target.value })}
              />
            </div>
            <div className="field">
              <label htmlFor="paymentSupplierId">Supplier id</label>
              <input
                id="paymentSupplierId"
                value={form.supplierId}
                onChange={(event) => setForm({ ...form, supplierId: event.target.value })}
              />
            </div>
            <div className="field">
              <label htmlFor="paymentMemberAccountId">Member account id</label>
              <input
                id="paymentMemberAccountId"
                value={form.memberAccountId}
                onChange={(event) => setForm({ ...form, memberAccountId: event.target.value })}
              />
            </div>
            <div className="field">
              <label htmlFor="paymentAccountTransactionId">Account transaction id</label>
              <input
                id="paymentAccountTransactionId"
                value={form.accountTransactionId}
                onChange={(event) => setForm({ ...form, accountTransactionId: event.target.value })}
              />
            </div>
            <div className="field">
              <label htmlFor="paymentSourceType">Source type</label>
              <input
                id="paymentSourceType"
                maxLength={64}
                value={form.sourceType}
                onChange={(event) => setForm({ ...form, sourceType: event.target.value })}
              />
            </div>
            <div className="field">
              <label htmlFor="paymentSourceId">Source id</label>
              <input
                id="paymentSourceId"
                value={form.sourceId}
                onChange={(event) => setForm({ ...form, sourceId: event.target.value })}
              />
            </div>
            <div className="field">
              <label htmlFor="paymentSourceReference">Source reference</label>
              <input
                id="paymentSourceReference"
                maxLength={128}
                value={form.sourceReference}
                onChange={(event) => setForm({ ...form, sourceReference: event.target.value })}
              />
            </div>
            <div className="field">
              <label htmlFor="paymentMethod">Payment method</label>
              <input
                id="paymentMethod"
                maxLength={32}
                value={form.paymentMethodCode}
                onChange={(event) => setForm({ ...form, paymentMethodCode: event.target.value })}
              />
            </div>
            <div className="field">
              <label htmlFor="paymentExternalReference">External reference</label>
              <input
                id="paymentExternalReference"
                maxLength={128}
                value={form.externalReference}
                onChange={(event) => setForm({ ...form, externalReference: event.target.value })}
              />
            </div>
            <div className="field">
              <label htmlFor="paymentDescription">Description</label>
              <input
                id="paymentDescription"
                maxLength={512}
                value={form.description}
                onChange={(event) => setForm({ ...form, description: event.target.value })}
              />
            </div>
          </form>

          <h3 style={{ marginTop: 16 }}>Allocations</h3>
          <p className="muted">
            Leave every allocation empty to hold the money as an unapplied amount. A received payment settles a
            receivable and a payment made settles a payable.
          </p>
          <table>
            <thead>
              <tr>
                <th>Receivable id</th>
                <th>Payable id</th>
                <th>Amount</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {allocations.map((allocation, index) => (
                <tr key={index}>
                  <td>
                    <input
                      aria-label="Receivable id"
                      value={allocation.receivableId}
                      onChange={(event) =>
                        setAllocations(
                          allocations.map((current, position) =>
                            position === index ? { ...current, receivableId: event.target.value } : current,
                          ),
                        )
                      }
                    />
                  </td>
                  <td>
                    <input
                      aria-label="Payable id"
                      value={allocation.payableId}
                      onChange={(event) =>
                        setAllocations(
                          allocations.map((current, position) =>
                            position === index ? { ...current, payableId: event.target.value } : current,
                          ),
                        )
                      }
                    />
                  </td>
                  <td>
                    <input
                      aria-label="Amount"
                      type="number"
                      min={0.0001}
                      step={0.01}
                      value={allocation.amount}
                      onChange={(event) =>
                        setAllocations(
                          allocations.map((current, position) =>
                            position === index ? { ...current, amount: event.target.value } : current,
                          ),
                        )
                      }
                    />
                  </td>
                  <td>
                    <button
                      type="button"
                      className="link-button"
                      disabled={allocations.length <= 1}
                      onClick={() => setAllocations(allocations.filter((_current, position) => position !== index))}
                    >
                      Remove
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          <div className="input-row" style={{ marginTop: 12 }}>
            <button type="button" className="btn" onClick={() => setAllocations([...allocations, EMPTY_ALLOCATION])}>
              Add allocation
            </button>
            <button type="button" className="btn btn-primary" disabled={busy} onClick={() => void record()}>
              Record payment
            </button>
          </div>
        </div>
      ) : null}

      {result === null ? (
        <div className="empty-state">Loading payments…</div>
      ) : result.items.length === 0 ? (
        <div className="empty-state">No payments match the current filters.</div>
      ) : (
        <table>
          <thead>
            <tr>
              <th>Reference</th>
              <th>Direction</th>
              <th>Counterparty</th>
              <th>Amount</th>
              <th>Unapplied</th>
              <th>Date</th>
              <th>Method</th>
              <th>Journal</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {result.items.map((payment) => (
              <tr key={payment.id}>
                <td>{payment.reference}</td>
                <td>
                  <span className={`status-pill ${payment.direction === 0 ? 'good' : 'bad'}`}>
                    {PAYMENT_DIRECTION[payment.direction]}
                  </span>
                </td>
                <td>
                  {payment.memberNumber
                    ? `${payment.memberNumber} · ${payment.memberId}`
                    : payment.supplierCode
                      ? `${payment.supplierCode} · ${payment.supplierId}`
                      : '—'}
                </td>
                <td>{payment.amount.toFixed(2)}</td>
                <td>
                  <strong>{payment.unappliedAmount.toFixed(2)}</strong>
                </td>
                <td className="muted">{new Date(payment.transactionDate).toLocaleString()}</td>
                <td className="muted">{payment.paymentMethodCode ?? '—'}</td>
                <td className="muted">{payment.journalReference ?? '—'}</td>
                <td>
                  <div className="row-actions">
                    <button
                      className="link-button"
                      onClick={() => setExpanded(expanded === payment.id ? null : payment.id)}
                    >
                      {expanded === payment.id ? 'Hide allocations' : 'Allocations'}
                    </button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {expandedPayment ? (
        <div className="card" style={{ marginTop: 18 }}>
          <h3>Allocations — {expandedPayment.reference}</h3>
          {expandedPayment.allocations.length === 0 ? (
            <div className="empty-state">This payment is fully unapplied.</div>
          ) : (
            <table>
              <thead>
                <tr>
                  <th>Document</th>
                  <th>Amount</th>
                  <th>Created</th>
                </tr>
              </thead>
              <tbody>
                {expandedPayment.allocations.map((allocation) => (
                  <tr key={allocation.id}>
                    <td>{allocation.receivableReference ?? allocation.payableReference ?? '—'}</td>
                    <td>{allocation.amount.toFixed(2)}</td>
                    <td className="muted">{new Date(allocation.createdAtUtc).toLocaleString()}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      ) : null}
    </>
  );
}
