import { useEffect, useState, type FormEvent } from 'react';
import { describeError, json } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import type {
  InventoryLocation,
  ProductSearch,
  StockAdjustment,
  StockAdjustmentSearch,
} from '../types';

export function WastagePage() {
  const { can } = useAuth();
  const [wastages, setWastages] = useState<StockAdjustment[]>([]);
  const [locations, setLocations] = useState<InventoryLocation[]>([]);
  const [products, setProducts] = useState<ProductSearch | null>(null);
  const [error, setError] = useState('');
  const [creating, setCreating] = useState(false);
  const [busy, setBusy] = useState(false);
  const [form, setForm] = useState({ locationId: '', productId: '', quantity: '', reason: '' });

  const canWastage = can('Inventory.Stock.Wastage');

  const load = async () => {
    setError('');
    try {
      const search = await json.get<StockAdjustmentSearch>('/api/v1/adjustments?adjustmentType=Wastage&pageSize=100');
      setWastages(search.items);
    } catch (err) {
      setError(describeError(err));
    }
  };

  useEffect(() => {
    if (!canWastage) {
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
  }, [canWastage]);

  const create = async (event: FormEvent) => {
    event.preventDefault();
    setBusy(true);
    try {
      await json.post<StockAdjustment>('/api/v1/adjustments', {
        adjustmentType: 'Wastage',
        locationId: form.locationId,
        reason: form.reason,
        lines: [{ productId: form.productId, quantity: Number(form.quantity), unitCost: null }],
      });
      setCreating(false);
      setForm({ ...form, productId: '', quantity: '', reason: '' });
      await load();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  if (!canWastage) {
    return <div className="empty-state">You do not have permission to record wastage.</div>;
  }

  return (
    <>
      <h1 className="page-title">Wastage</h1>
      <p className="page-subtitle">Record damaged, expired or discarded stock. Reduces on-hand balance immediately.</p>

      {error ? <div className="alert alert-error">{error}</div> : null}

      <div className="toolbar">
        <button className="btn btn-primary" onClick={() => setCreating((value) => !value)}>
          {creating ? 'Close' : 'Record wastage'}
        </button>
      </div>

      {creating ? (
        <div className="card">
          <h3>Record wastage</h3>
          <form className="form-grid" onSubmit={(event) => void create(event)}>
            <div className="field">
              <label htmlFor="loc">Location *</label>
              <select id="loc" required value={form.locationId} onChange={(event) => setForm({ ...form, locationId: event.target.value })}>
                <option value="">Select…</option>
                {locations.filter((loc) => loc.status === 'Active').map((loc) => (
                  <option key={loc.id} value={loc.id}>{loc.name}</option>
                ))}
              </select>
            </div>
            <div className="field">
              <label htmlFor="prod">Product *</label>
              <select id="prod" required value={form.productId} onChange={(event) => setForm({ ...form, productId: event.target.value })}>
                <option value="">Select…</option>
                {(products?.items ?? []).filter((p) => p.stockManaged && p.status === 'Active').map((p) => (
                  <option key={p.id} value={p.id}>{p.sku} — {p.name}</option>
                ))}
              </select>
            </div>
            <div className="field">
              <label htmlFor="qty">Quantity *</label>
              <input id="qty" type="number" min={0.01} step={0.01} required value={form.quantity} onChange={(event) => setForm({ ...form, quantity: event.target.value })} />
            </div>
            <div className="field">
              <label htmlFor="reason">Reason *</label>
              <textarea id="reason" required maxLength={512} value={form.reason} onChange={(event) => setForm({ ...form, reason: event.target.value })} />
            </div>
            <button type="submit" className="btn btn-primary" disabled={busy}>
              Post wastage
            </button>
          </form>
        </div>
      ) : null}

      {wastages.length === 0 ? (
        <div className="empty-state">No wastage recorded.</div>
      ) : (
        <table>
          <thead>
            <tr>
              <th>Number</th>
              <th>Location</th>
              <th>Reason</th>
              <th>Lines</th>
              <th>Posted</th>
            </tr>
          </thead>
          <tbody>
            {wastages.map((wastage) => (
              <tr key={wastage.id}>
                <td>{wastage.adjustmentNumber}</td>
                <td>{wastage.locationCode}</td>
                <td>{wastage.reason}</td>
                <td>
                  {wastage.lines.map((line) => (
                    <div key={line.productId}>
                      {line.productSku}: −{line.quantity}
                    </div>
                  ))}
                </td>
                <td>{new Date(wastage.postedAtUtc).toLocaleString()}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
}