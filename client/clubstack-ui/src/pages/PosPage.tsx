import { useEffect, useState } from 'react';
import { describeError, json } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import { FiscalStatusForSale } from '../components/FiscalStatusForSale';
import type { PosConfiguration, PosMemberSearch, PosProduct, PosProductSearch, Receipt, Sale } from '../types';

interface CartLine {
  product: PosProduct;
  quantity: number;
}

interface Tender {
  paymentMethodCode: string;
  amount: string;
  externalReference: string;
}

const TENDER_CODES = ['CASH', 'CARD', 'ACCOUNT'] as const;

function money(amount: number, currency: string): string {
  return `${amount.toLocaleString(undefined, { minimumFractionDigits: 2 })} ${currency}`;
}

function newIdempotencyKey(): string {
  return `pos-${crypto.randomUUID()}`;
}

export function PosPage() {
  const { can } = useAuth();
  const [config, setConfig] = useState<PosConfiguration>({ outlets: [], registers: [] });
  const [registerId, setRegisterId] = useState('');
  const [query, setQuery] = useState('');
  const [products, setProducts] = useState<PosProduct[]>([]);
  const [cart, setCart] = useState<CartLine[]>([]);
  const [credentialValue, setCredentialValue] = useState('');
  const [member, setMember] = useState<{ id: string; label: string } | null>(null);
  const [sale, setSale] = useState<Sale | null>(null);
  const [completionKey, setCompletionKey] = useState('');
  const [tenders, setTenders] = useState<Tender[]>([
    { paymentMethodCode: 'CASH', amount: '', externalReference: '' },
  ]);
  const [receipt, setReceipt] = useState<Receipt | null>(null);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [busy, setBusy] = useState(false);

  const canUse = can('POS.Sale.Create');
  const canComplete = can('POS.Sale.Complete');
  const canMemberCharge = can('POS.Sale.MemberCharge');

  const register = config.registers.find((r) => r.id === registerId) ?? null;
  const locationId = register?.inventoryLocationId ?? '';

  useEffect(() => {
    if (!can('POS.Register.View')) {
      return;
    }
    void (async () => {
      try {
        const loaded = await json.get<PosConfiguration>('/api/v1/pos/configuration');
        setConfig(loaded);
        const firstActive = loaded.registers.find((r) => r.status === 'Active');
        if (firstActive) {
          setRegisterId(firstActive.id);
        }
      } catch (err) {
        setError(describeError(err));
      }
    })();
  }, []);

  useEffect(() => {
    if (!canUse || !locationId) {
      return;
    }
    void (async () => {
      try {
        const search = await json.get<PosProductSearch>(
          `/api/v1/pos/products?locationId=${locationId}&query=${encodeURIComponent(query)}&pageSize=50`,
        );
        setProducts(search.items);
      } catch (err) {
        setError(describeError(err));
      }
    })();
  }, [canUse, locationId, query]);

  const addToCart = (product: PosProduct) => {
    if (product.availableQuantity !== null && product.availableQuantity <= 0) {
      setError(`${product.name} has no stock available at this register.`);
      return;
    }
    setCart((lines) => {
      const existing = lines.find((line) => line.product.id === product.id);
      if (existing) {
        return lines.map((line) =>
          line.product.id === product.id ? { ...line, quantity: line.quantity + 1 } : line,
        );
      }
      return [...lines, { product, quantity: 1 }];
    });
    setError('');
  };

  const setQuantity = (productId: string, quantity: number) => {
    setCart((lines) =>
      quantity <= 0
        ? lines.filter((line) => line.product.id !== productId)
        : lines.map((line) => (line.product.id === productId ? { ...line, quantity } : line)),
    );
  };

  const resetSale = () => {
    setSale(null);
    setCart([]);
    setReceipt(null);
    setCompletionKey('');
    setTenders([{ paymentMethodCode: 'CASH', amount: '', externalReference: '' }]);
    setNotice('');
  };

  const lookUpMember = async () => {
    if (!credentialValue.trim()) {
      return;
    }
    setBusy(true);
    setError('');
    try {
      const result = await json.get<PosMemberSearch>(
        `/api/v1/pos/products/members?query=${encodeURIComponent(credentialValue.trim())}`,
      );
      const found = result.items[0];
      if (!found) {
        setMember(null);
        setError('No active member matches that card or number.');
        return;
      }
      setMember({ id: found.memberId, label: `${found.memberNumber} — ${found.memberName}` });
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const createSale = async () => {
    if (cart.length === 0) {
      setError('Scan or select at least one product first.');
      return;
    }
    setBusy(true);
    setError('');
    try {
      const created = await json.post<Sale>('/api/v1/sales', {
        registerId,
        memberId: member?.id ?? null,
        credentialValue: member || !credentialValue.trim() ? null : credentialValue.trim(),
        lines: cart.map((line) => ({ productId: line.product.id, quantity: line.quantity })),
      });
      setSale(created);
      if (created.memberId) {
        setMember({ id: created.memberId, label: `${created.memberNumber} — ${created.memberName}` });
      }
      setCompletionKey(newIdempotencyKey());
      setTenders([
        { paymentMethodCode: 'CASH', amount: created.grandTotal.toFixed(2), externalReference: '' },
      ]);
      setNotice(`Sale ${created.saleNumber} created. Take payment to complete it.`);
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const payInFull = (paymentMethodCode: string) => {
    if (!sale) {
      return;
    }
    if (paymentMethodCode === 'ACCOUNT' && !member) {
      setError('Select a member before charging the sale to an account.');
      return;
    }
    setTenders([{ paymentMethodCode, amount: sale.grandTotal.toFixed(2), externalReference: '' }]);
    setError('');
  };

  const addSplitTender = () => {
    setTenders((current) => [
      ...current,
      { paymentMethodCode: 'CASH', amount: '', externalReference: '' },
    ]);
  };

  const updateTender = (index: number, patch: Partial<Tender>) => {
    setTenders((current) => current.map((tender, i) => (i === index ? { ...tender, ...patch } : tender)));
  };

  const tenderedTotal = tenders.reduce((sum, tender) => sum + (Number(tender.amount) || 0), 0);
  const outstanding = sale ? Math.round((sale.grandTotal - tenderedTotal) * 100) / 100 : 0;

  const completeSale = async () => {
    if (!sale) {
      return;
    }
    if (outstanding !== 0) {
      setError('Tendered amounts must equal the sale total before completing.');
      return;
    }
    setBusy(true);
    setError('');
    try {
      // The key is created once per sale and reused on every retry, so a retried
      // completion replays the stored result instead of posting the sale twice.
      const completed = await json.post<Sale>(`/api/v1/sales/${sale.id}/complete`, {
        payments: tenders.map((tender) => ({
          paymentMethodCode: tender.paymentMethodCode,
          amount: Number(tender.amount),
          externalReference: tender.externalReference || null,
        })),
        idempotencyKey: completionKey,
      });
      setSale(completed);
      setReceipt(completed.receipt);
      setNotice(`Sale ${completed.saleNumber} completed.`);
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const voidSale = async () => {
    if (!sale) {
      return;
    }
    setBusy(true);
    setError('');
    try {
      const voided = await json.post<Sale>(`/api/v1/sales/${sale.id}/void`, {
        reason: 'Voided at the till',
      });
      setSale(voided);
      setNotice(`Sale ${voided.saleNumber} voided.`);
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  if (!canUse) {
    return <div className="empty-state">You do not have permission to operate the POS.</div>;
  }

  return (
    <>
      <h1 className="page-title">Point of Sale</h1>
      <p className="page-subtitle">Search products, build the sale and take payment.</p>

      {error ? <div className="alert alert-error">{error}</div> : null}
      {notice ? <div className="alert alert-info">{notice}</div> : null}

      <div className="toolbar">
        <div className="field">
          <label htmlFor="register">Register</label>
          <select
            id="register"
            value={registerId}
            disabled={sale !== null}
            onChange={(event) => {
              setRegisterId(event.target.value);
              resetSale();
            }}
          >
            <option value="">Select a register…</option>
            {config.registers.map((item) => (
              <option key={item.id} value={item.id}>
                {item.posOutletCode} / {item.code} — {item.name}
                {item.status === 'Active' ? '' : ` (${item.status})`}
              </option>
            ))}
          </select>
        </div>

        <div className="field">
          <label htmlFor="credential">Member card</label>
          <div className="input-row">
            <input
              id="credential"
              value={credentialValue}
              onChange={(event) => setCredentialValue(event.target.value)}
              placeholder="Scan or type credential"
            />
            <button type="button" className="btn" onClick={() => void lookUpMember()} disabled={busy}>
              Find
            </button>
          </div>
        </div>

        <div className="field">
          <span className="field-label">Member</span>
          <span className="field-value">{member ? member.label : 'Walk-in customer'}</span>
        </div>
      </div>

      <div className="detail-grid">
        <div className="card">
          <h3>Product search</h3>
          <div className="toolbar">
            <input
              value={query}
              onChange={(event) => setQuery(event.target.value)}
              placeholder="Search by name, SKU or barcode"
              style={{ flex: 1 }}
            />
          </div>
          {register ? null : <div className="empty-state">Select a register to search products.</div>}
          {register && products.length === 0 ? <div className="empty-state">No products found.</div> : null}
          {register && products.length > 0 ? (
            <table>
              <thead>
                <tr>
                  <th>SKU</th>
                  <th>Product</th>
                  <th>Price</th>
                  <th>Available</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {products.map((product) => (
                  <tr key={product.id}>
                    <td>{product.sku}</td>
                    <td>{product.name}</td>
                    <td>{money(product.unitPrice, product.currencyCode)}</td>
                    <td>{product.availableQuantity ?? '—'}</td>
                    <td>
                      <button
                        type="button"
                        className="btn"
                        onClick={() => addToCart(product)}
                        disabled={sale !== null}
                      >
                        Add
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          ) : null}
        </div>

        <div className="card">
          <h3>Current sale</h3>
          {cart.length === 0 ? (
            <div className="empty-state">No items on this sale yet.</div>
          ) : (
            <table>
              <thead>
                <tr>
                  <th>Item</th>
                  <th>Qty</th>
                  <th>Line total</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {cart.map((line) => (
                  <tr key={line.product.id}>
                    <td>{line.product.name}</td>
                    <td>
                      <input
                        type="number"
                        min={0}
                        step={1}
                        value={line.quantity}
                        style={{ width: 70 }}
                        disabled={sale !== null}
                        onChange={(event) =>
                          setQuantity(line.product.id, Number(event.target.value) || 0)
                        }
                      />
                    </td>
                    <td>{money(line.product.unitPrice * line.quantity, line.product.currencyCode)}</td>
                    <td>
                      <button
                        type="button"
                        className="btn"
                        disabled={sale !== null}
                        onClick={() => setQuantity(line.product.id, 0)}
                      >
                        Remove
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}

          {sale ? (
            <div className="detail-grid" style={{ marginTop: 12 }}>
              <div className="field">
                <span className="field-label">Sale</span>
                <span className="field-value">{sale.saleNumber}</span>
              </div>
              <div className="field">
                <span className="field-label">Status</span>
                <span className="field-value">{sale.status}</span>
              </div>
              <div className="field">
                <span className="field-label">Subtotal</span>
                <span className="field-value">{money(sale.subtotal, sale.currencyCode)}</span>
              </div>
              <div className="field">
                <span className="field-label">Tax</span>
                <span className="field-value">{money(sale.taxTotal, sale.currencyCode)}</span>
              </div>
              <div className="field">
                <span className="field-label">Total</span>
                <span className="field-value">
                  <strong>{money(sale.grandTotal, sale.currencyCode)}</strong>
                </span>
              </div>
            </div>
          ) : null}

          <div className="row-actions" style={{ marginTop: 12 }}>
            {sale === null ? (
              <button
                type="button"
                className="btn btn-primary"
                onClick={() => void createSale()}
                disabled={busy || cart.length === 0 || !registerId}
              >
                Create sale
              </button>
            ) : null}
            {sale && sale.status === 'Open' ? (
              <>
                <button type="button" className="btn" onClick={() => payInFull('CASH')} disabled={busy}>
                  Cash
                </button>
                <button type="button" className="btn" onClick={() => payInFull('CARD')} disabled={busy}>
                  Card
                </button>
                <button
                  type="button"
                  className="btn"
                  onClick={() => payInFull('ACCOUNT')}
                  disabled={busy || !canMemberCharge || !member}
                  title={!member ? 'Select a member first' : undefined}
                >
                  Account
                </button>
                <button type="button" className="btn" onClick={addSplitTender} disabled={busy}>
                  Split
                </button>
              </>
            ) : null}
            {sale && sale.status === 'Open' && canComplete ? (
              <button
                type="button"
                className="btn btn-primary"
                onClick={() => void completeSale()}
                disabled={busy || outstanding !== 0}
              >
                Complete sale
              </button>
            ) : null}
            {sale && sale.status === 'Open' && can('POS.Sale.Void') ? (
              <button type="button" className="btn btn-danger" onClick={() => void voidSale()} disabled={busy}>
                Void sale
              </button>
            ) : null}
            <button type="button" className="link-button" onClick={resetSale} disabled={busy}>
              Start a new sale
            </button>
          </div>
        </div>
      </div>

      {sale && sale.status === 'Open' ? (
        <div className="card" style={{ marginTop: 16 }}>
          <h3>Payment</h3>
          <table>
            <thead>
              <tr>
                <th>Method</th>
                <th>Amount</th>
                <th>Reference</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {tenders.map((tender, index) => (
                <tr key={index}>
                  <td>
                    <select
                      value={tender.paymentMethodCode}
                      onChange={(event) => updateTender(index, { paymentMethodCode: event.target.value })}
                    >
                      {TENDER_CODES.map((code) => (
                        <option key={code} value={code}>
                          {code}
                        </option>
                      ))}
                    </select>
                  </td>
                  <td>
                    <input
                      type="number"
                      min={0}
                      step={0.01}
                      value={tender.amount}
                      style={{ width: 110 }}
                      onChange={(event) => updateTender(index, { amount: event.target.value })}
                    />
                  </td>
                  <td>
                    <input
                      value={tender.externalReference}
                      onChange={(event) => updateTender(index, { externalReference: event.target.value })}
                    />
                  </td>
                  <td>
                    <button
                      type="button"
                      className="link-button"
                      onClick={() => setTenders((current) => current.filter((_, i) => i !== index))}
                      disabled={tenders.length === 1}
                    >
                      Remove
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          <div className="detail-grid" style={{ marginTop: 12 }}>
            <div className="field">
              <span className="field-label">Tendered</span>
              <span className="field-value">{money(tenderedTotal, sale.currencyCode)}</span>
            </div>
            <div className="field">
              <span className="field-label">Outstanding</span>
              <span className="field-value">{money(outstanding, sale.currencyCode)}</span>
            </div>
          </div>
        </div>
      ) : null}

      {receipt ? (
        <div className="card" style={{ marginTop: 16 }}>
          <h3>Receipt {receipt.receiptNumber}</h3>
          <div className="detail-grid">
            <div className="field">
              <span className="field-label">Register</span>
              <span className="field-value">{receipt.registerCode}</span>
            </div>
            <div className="field">
              <span className="field-label">Member</span>
              <span className="field-value">{receipt.memberName ?? 'Walk-in customer'}</span>
            </div>
            <div className="field">
              <span className="field-label">Total</span>
              <span className="field-value">{money(receipt.grandTotal, receipt.currencyCode)}</span>
            </div>
          </div>
          <FiscalStatusForSale saleId={sale?.id ?? null} />
        </div>
      ) : null}
    </>
  );
}
