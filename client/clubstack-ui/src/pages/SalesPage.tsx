import { useEffect, useState } from 'react';
import { describeError, json } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import type { Receipt, Sale, SaleRefund, SaleSearch } from '../types';

function money(amount: number, currency: string): string {
  return `${amount.toLocaleString(undefined, { minimumFractionDigits: 2 })} ${currency}`;
}

function stamp(value: string | null): string {
  return value ? new Date(value).toLocaleString() : '—';
}

function statusPill(status: string): string {
  if (status === 'Completed') {
    return 'status-pill ok';
  }
  if (status === 'Voided' || status === 'Cancelled') {
    return 'status-pill bad';
  }
  return 'status-pill warn';
}

export function SalesPage() {
  const { can } = useAuth();
  const [rows, setRows] = useState<Sale[]>([]);
  const [status, setStatus] = useState('');
  const [query, setQuery] = useState('');
  const [selected, setSelected] = useState<Sale | null>(null);
  const [receipt, setReceipt] = useState<Receipt | null>(null);
  const [refunds, setRefunds] = useState<SaleRefund[]>([]);
  const [refundReason, setRefundReason] = useState('');
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [busy, setBusy] = useState(false);

  const canView = can('POS.Sale.Create') || can('POS.Sale.Reprint') || can('POS.Sale.Void') || can('POS.Sale.Refund');

  const load = async () => {
    setError('');
    try {
      const params = new URLSearchParams({ pageSize: '50' });
      if (status) {
        params.set('status', status);
      }
      if (query.trim()) {
        params.set('query', query.trim());
      }
      const result = await json.get<SaleSearch>(`/api/v1/sales?${params.toString()}`);
      setRows(result.items);
    } catch (err) {
      setError(describeError(err));
    }
  };

  useEffect(() => {
    if (canView) {
      void load();
    }
  }, [canView, status, query]);

  const open = async (id: string) => {
    setBusy(true);
    setError('');
    setNotice('');
    setRefunds([]);
    setReceipt(null);
    try {
      const sale = await json.get<Sale>(`/api/v1/sales/${id}`);
      setSelected(sale);
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const reprint = async (id: string) => {
    setBusy(true);
    setError('');
    try {
      setReceipt(await json.get<Receipt>(`/api/v1/sales/${id}/receipt`));
      setNotice('Receipt loaded.');
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const voidSale = async (id: string) => {
    setBusy(true);
    setError('');
    try {
      const voided = await json.post<Sale>(`/api/v1/sales/${id}/void`, {
        reason: 'Voided from sales history',
      });
      setSelected(voided);
      setNotice(`Sale ${voided.saleNumber} voided.`);
      await load();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const refundSale = async (id: string) => {
    if (!refundReason.trim()) {
      setError('A reason is required before refunding.');
      return;
    }
    setBusy(true);
    setError('');
    try {
      // A fresh key per refund keeps the request safely retryable without ever
      // replaying an unrelated refund.
      const refund = await json.post<SaleRefund>(`/api/v1/sales/${id}/refund`, {
        reason: refundReason.trim(),
        idempotencyKey: `sales-refund-${crypto.randomUUID()}`,
      });
      setRefunds((current) => [refund, ...current]);
      setRefundReason('');
      setNotice(`Refund ${refund.refundNumber} recorded.`);
      setSelected(await json.get<Sale>(`/api/v1/sales/${id}`));
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  if (!canView) {
    return <div className="empty-state">You do not have permission to view sales.</div>;
  }

  return (
    <>
      <h1 className="page-title">Sales</h1>
      <p className="page-subtitle">Completed and in-progress sales, receipts, voids and refunds.</p>

      {error ? <div className="alert alert-error">{error}</div> : null}
      {notice ? <div className="alert alert-info">{notice}</div> : null}

      <div className="toolbar">
        <div className="field">
          <label htmlFor="query">Search</label>
          <input
            id="query"
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            placeholder="Sale number, member or cashier"
          />
        </div>
        <div className="field">
          <label htmlFor="status">Status</label>
          <select id="status" value={status} onChange={(event) => setStatus(event.target.value)}>
            <option value="">All</option>
            <option value="Open">Open</option>
            <option value="Completed">Completed</option>
            <option value="Voided">Voided</option>
            <option value="Cancelled">Cancelled</option>
          </select>
        </div>
        <button type="button" className="btn" onClick={() => void load()} disabled={busy}>
          Refresh
        </button>
      </div>

      {rows.length === 0 ? (
        <div className="empty-state">No sales match the current filter.</div>
      ) : (
        <table>
          <thead>
            <tr>
              <th>Sale</th>
              <th>Status</th>
              <th>Register</th>
              <th>Member</th>
              <th>Total</th>
              <th>Completed</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {rows.map((sale) => (
              <tr key={sale.id}>
                <td>{sale.saleNumber}</td>
                <td>
                  <span className={statusPill(sale.status)}>{sale.status}</span>
                </td>
                <td>{sale.registerCode}</td>
                <td>{sale.memberName ?? 'Walk-in customer'}</td>
                <td>{money(sale.grandTotal, sale.currencyCode)}</td>
                <td>{stamp(sale.completedAtUtc)}</td>
                <td className="row-actions">
                  <button type="button" className="link-button" onClick={() => void open(sale.id)}>
                    View
                  </button>
                  {sale.status === 'Completed' && can('POS.Sale.Reprint') ? (
                    <button type="button" className="link-button" onClick={() => void reprint(sale.id)}>
                      Receipt
                    </button>
                  ) : null}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {selected ? (
        <div className="card" style={{ marginTop: 16 }}>
          <h3>
            Sale {selected.saleNumber}{' '}
            <span className={statusPill(selected.status)}>{selected.status}</span>
          </h3>
          <div className="detail-grid">
            <div className="field">
              <span className="field-label">Outlet / register</span>
              <span className="field-value">
                {selected.posOutletCode} / {selected.registerCode}
              </span>
            </div>
            <div className="field">
              <span className="field-label">Cashier</span>
              <span className="field-value">{selected.cashierUserId}</span>
            </div>
            <div className="field">
              <span className="field-label">Member</span>
              <span className="field-value">{selected.memberName ?? 'Walk-in customer'}</span>
            </div>
            <div className="field">
              <span className="field-label">Subtotal</span>
              <span className="field-value">{money(selected.subtotal, selected.currencyCode)}</span>
            </div>
            <div className="field">
              <span className="field-label">Discount</span>
              <span className="field-value">{money(selected.discountTotal, selected.currencyCode)}</span>
            </div>
            <div className="field">
              <span className="field-label">Tax</span>
              <span className="field-value">{money(selected.taxTotal, selected.currencyCode)}</span>
            </div>
            <div className="field">
              <span className="field-label">Total</span>
              <span className="field-value">
                <strong>{money(selected.grandTotal, selected.currencyCode)}</strong>
              </span>
            </div>
            <div className="field">
              <span className="field-label">Completed</span>
              <span className="field-value">{stamp(selected.completedAtUtc)}</span>
            </div>
            {selected.voidReason ? (
              <div className="field">
                <span className="field-label">Void reason</span>
                <span className="field-value">{selected.voidReason}</span>
              </div>
            ) : null}
          </div>

          <div className="row-actions" style={{ marginTop: 12 }}>
            {selected.status === 'Open' && can('POS.Sale.Void') ? (
              <button type="button" className="btn btn-danger" onClick={() => void voidSale(selected.id)} disabled={busy}>
                Void sale
              </button>
            ) : null}
            {selected.status === 'Completed' && can('POS.Sale.Reprint') ? (
              <button type="button" className="btn" onClick={() => void reprint(selected.id)} disabled={busy}>
                Reprint receipt
              </button>
            ) : null}
            <button type="button" className="link-button" onClick={() => setSelected(null)}>
              Close
            </button>
          </div>

          {selected.status === 'Completed' && can('POS.Sale.Refund') ? (
            <div className="toolbar" style={{ marginTop: 12 }}>
              <div className="field">
                <label htmlFor="refundReason">Refund reason</label>
                <input
                  id="refundReason"
                  value={refundReason}
                  onChange={(event) => setRefundReason(event.target.value)}
                  placeholder="Why is this sale being returned?"
                />
              </div>
              <button
                type="button"
                className="btn btn-danger"
                onClick={() => void refundSale(selected.id)}
                disabled={busy}
              >
                Refund sale
              </button>
            </div>
          ) : null}

          <h3 style={{ marginTop: 16 }}>Lines</h3>
          <table>
            <thead>
              <tr>
                <th>SKU</th>
                <th>Product</th>
                <th>Qty</th>
                <th>Unit price</th>
                <th>Discount</th>
                <th>Tax</th>
                <th>Line total</th>
              </tr>
            </thead>
            <tbody>
              {selected.lines.map((line) => (
                <tr key={line.id}>
                  <td>{line.productSku}</td>
                  <td>{line.productName}</td>
                  <td>{line.quantity}</td>
                  <td>{money(line.unitPrice, selected.currencyCode)}</td>
                  <td>{money(line.discountAmount, selected.currencyCode)}</td>
                  <td>{money(line.taxAmount, selected.currencyCode)}</td>
                  <td>{money(line.lineTotal, selected.currencyCode)}</td>
                </tr>
              ))}
            </tbody>
          </table>

          {selected.payments.length > 0 ? (
            <>
              <h3 style={{ marginTop: 16 }}>Tenders</h3>
              <table>
                <thead>
                  <tr>
                    <th>Method</th>
                    <th>Name</th>
                    <th>Amount</th>
                    <th>Account reference</th>
                  </tr>
                </thead>
                <tbody>
                  {selected.payments.map((payment) => (
                    <tr key={payment.id}>
                      <td>{payment.paymentMethodCode}</td>
                      <td>{payment.paymentMethodName}</td>
                      <td>{money(payment.amount, selected.currencyCode)}</td>
                      <td>{payment.accountTransactionReference ?? '—'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </>
          ) : null}

          {refunds.length > 0 ? (
            <>
              <h3 style={{ marginTop: 16 }}>Refunds issued</h3>
              <table>
                <thead>
                  <tr>
                    <th>Refund</th>
                    <th>Amount</th>
                    <th>Reason</th>
                    <th>Refunded</th>
                  </tr>
                </thead>
                <tbody>
                  {refunds.map((refund) => (
                    <tr key={refund.id}>
                      <td>{refund.refundNumber}</td>
                      <td>{money(refund.amount, refund.currencyCode)}</td>
                      <td>{refund.reason}</td>
                      <td>{stamp(refund.refundedAtUtc)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </>
          ) : null}

          {receipt ? (
            <>
              <h3 style={{ marginTop: 16 }}>Receipt {receipt.receiptNumber}</h3>
              <div className="detail-grid">
                <div className="field">
                  <span className="field-label">Register</span>
                  <span className="field-value">{receipt.registerCode}</span>
                </div>
                <div className="field">
                  <span className="field-label">Total</span>
                  <span className="field-value">{money(receipt.grandTotal, receipt.currencyCode)}</span>
                </div>
                <div className="field">
                  <span className="field-label">Generated</span>
                  <span className="field-value">{stamp(receipt.generatedAtUtc)}</span>
                </div>
              </div>
            </>
          ) : null}
        </div>
      ) : null}
    </>
  );
}
