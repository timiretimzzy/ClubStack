import { useEffect, useState, type FormEvent } from 'react';
import { describeError, json } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import type { InventoryLocation, ProductSearch, StockTransfer, StockTransferSearch } from '../types';

export function TransfersPage() {
  const { can } = useAuth();
  const [transfers, setTransfers] = useState<StockTransfer[]>([]);
  const [locations, setLocations] = useState<InventoryLocation[]>([]);
  const [products, setProducts] = useState<ProductSearch | null>(null);
  const [error, setError] = useState('');
  const [creating, setCreating] = useState(false);
  const [busy, setBusy] = useState(false);
  const [form, setForm] = useState({
    fromLocationId: '',
    toLocationId: '',
    productId: '',
    quantity: '',
    unitCost: '',
  });

  const canTransfer = can('Inventory.Stock.Transfer');

  const load = async () => {
    setError('');
    try {
      const search = await json.get<StockTransferSearch>('/api/v1/transfers?pageSize=100');
      setTransfers(search.items);
    } catch (err) {
      setError(describeError(err));
    }
  };

  useEffect(() => {
    if (!canTransfer) {
      return;
    }
    void load();
    void (async () => {
      try {
        setLocations(await json.get<InventoryLocation[]>('/api/v1/inventory-locations'));
        setProducts(await json.get<ProductSearch>('/api/v1/products?pageSize=500'));
      } catch (err) {
        setError(describeError(err));
      }
    })();
  }, [canTransfer]);

  const activeLocations = locations.filter((loc) => loc.status === 'Active');
  const stockManagedProducts = (products?.items ?? []).filter((p) => p.stockManaged && p.status === 'Active');

  const createTransfer = async (event: FormEvent) => {
    event.preventDefault();
    setBusy(true);
    try {
      await json.post<StockTransfer>('/api/v1/transfers', {
        fromLocationId: form.fromLocationId,
        toLocationId: form.toLocationId,
        lines: [
          {
            productId: form.productId,
            quantity: Number(form.quantity),
            unitCost: form.unitCost ? Number(form.unitCost) : null,
          },
        ],
      });
      setCreating(false);
      setForm({ ...form, productId: '', quantity: '', unitCost: '' });
      await load();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const run = async (action: string, id: string) => {
    setBusy(true);
    setError('');
    try {
      await json.post<StockTransfer>(`/api/v1/transfers/${id}/${action}`);
      await load();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  if (!canTransfer) {
    return <div className="empty-state">You do not have permission to manage transfers.</div>;
  }

  return (
    <>
      <h1 className="page-title">Stock transfers</h1>
      <p className="page-subtitle">Move stock between locations. Drafts can be completed or cancelled.</p>

      {error ? <div className="alert alert-error">{error}</div> : null}

      <div className="toolbar">
        <button className="btn btn-primary" onClick={() => setCreating((value) => !value)}>
          {creating ? 'Close' : 'New transfer'}
        </button>
      </div>

      {creating ? (
        <div className="card">
          <h3>New transfer</h3>
          <form className="form-grid" onSubmit={(event) => void createTransfer(event)}>
            <div className="field">
              <label htmlFor="from">From location *</label>
              <select id="from" required value={form.fromLocationId} onChange={(event) => setForm({ ...form, fromLocationId: event.target.value })}>
                <option value="">Select…</option>
                {activeLocations.map((loc) => (
                  <option key={loc.id} value={loc.id}>{loc.name}</option>
                ))}
              </select>
            </div>
            <div className="field">
              <label htmlFor="to">To location *</label>
              <select id="to" required value={form.toLocationId} onChange={(event) => setForm({ ...form, toLocationId: event.target.value })}>
                <option value="">Select…</option>
                {activeLocations.map((loc) => (
                  <option key={loc.id} value={loc.id}>{loc.name}</option>
                ))}
              </select>
            </div>
            <div className="field">
              <label htmlFor="prod">Product *</label>
              <select id="prod" required value={form.productId} onChange={(event) => setForm({ ...form, productId: event.target.value })}>
                <option value="">Select…</option>
                {stockManagedProducts.map((p) => (
                  <option key={p.id} value={p.id}>{p.sku} — {p.name}</option>
                ))}
              </select>
            </div>
            <div className="field">
              <label htmlFor="qty">Quantity *</label>
              <input id="qty" type="number" min={0.01} step={0.01} required value={form.quantity} onChange={(event) => setForm({ ...form, quantity: event.target.value })} />
            </div>
            <div className="field">
              <label htmlFor="cost">Unit cost</label>
              <input id="cost" type="number" min={0} step={0.01} value={form.unitCost} onChange={(event) => setForm({ ...form, unitCost: event.target.value })} />
            </div>
            <button type="submit" className="btn btn-primary" disabled={busy}>
              Save draft
            </button>
          </form>
        </div>
      ) : null}

      {transfers.length === 0 ? (
        <div className="empty-state">No transfers yet.</div>
      ) : (
        transfers.map((transfer) => (
          <div className="card" key={transfer.id}>
            <div className="input-row">
              <strong>{transfer.transferNumber}</strong>
              <span className="muted">{transfer.fromLocationCode} → {transfer.toLocationCode}</span>
              <span className={`status-pill ${transfer.status === 'Completed' ? 'good' : transfer.status === 'Cancelled' ? 'bad' : 'warn'}`}>{transfer.status}</span>
              {transfer.status === 'Draft' ? (
                <>
                  <button type="button" className="btn btn-secondary" disabled={busy} onClick={() => void run('complete', transfer.id)}>
                    Complete
                  </button>
                  <button type="button" className="link-button" disabled={busy} onClick={() => void run('cancel', transfer.id)}>
                    Cancel
                  </button>
                </>
              ) : null}
            </div>
            <table style={{ marginTop: 10 }}>
              <thead>
                <tr>
                  <th>Product</th>
                  <th>Qty</th>
                  <th>Unit cost</th>
                </tr>
              </thead>
              <tbody>
                {transfer.lines.map((line) => (
                  <tr key={line.productId}>
                    <td>{line.productSku} — {line.productName}</td>
                    <td>{line.quantity}</td>
                    <td>{line.unitCost}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ))
      )}
    </>
  );
}