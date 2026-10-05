import { useEffect, useState, type FormEvent } from 'react';
import { describeError, json } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import type {
  InventoryLocation,
  ProductSearch,
  StockAdjustment,
  StockAdjustmentSearch,
} from '../types';

export function AdjustmentsPage() {
  const { can } = useAuth();
  const [adjustments, setAdjustments] = useState<StockAdjustment[]>([]);
  const [locations, setLocations] = useState<InventoryLocation[]>([]);
  const [products, setProducts] = useState<ProductSearch | null>(null);
  const [error, setError] = useState('');
  const [creating, setCreating] = useState(false);
  const [busy, setBusy] = useState(false);
  const [form, setForm] = useState({
    locationId: '',
    productId: '',
    quantity: '',
    unitCost: '',
    reason: '',
  });

  const canAdjust = can('Inventory.Stock.Adjust');

  const load = async () => {
    setError('');
    try {
      const search = await json.get<StockAdjustmentSearch>('/api/v1/adjustments?adjustmentType=Adjustment&pageSize=100');
      setAdjustments(search.items);
    } catch (err) {
      setError(describeError(err));
    }
  };

  useEffect(() => {
    if (!canAdjust) {
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
  }, [canAdjust]);

  const create = async (event: FormEvent) => {
    event.preventDefault();
    setBusy(true);
    try {
      await json.post<StockAdjustment>('/api/v1/adjustments', {
        adjustmentType: 'Adjustment',
        locationId: form.locationId,
        reason: form.reason,
        lines: [
          {
            productId: form.productId,
            quantity: Number(form.quantity),
            unitCost: form.unitCost ? Number(form.unitCost) : null,
          },
        ],
      });
      setCreating(false);
      setForm({ ...form, productId: '', quantity: '', unitCost: '', reason: '' });
      await load();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  if (!canAdjust) {
    return <div className="empty-state">You do not have permission to manage stock adjustments.</div>;
  }

  return (
    <>
      <h1 className="page-title">Stock adjustments</h1>
      <p className="page-subtitle">Post manual corrections to on-hand quantities. A positive quantity increases stock, negative decreases it.</p>

      {error ? <div className="alert alert-error">{error}</div> : null}

      <div className="toolbar">
        <button className="btn btn-primary" onClick={() => setCreating((value) => !value)}>
          {creating ? 'Close' : 'New adjustment'}
        </button>
      </div>

      {creating ? (
        <div className="card">
          <h3>New adjustment</h3>
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
              <label htmlFor="qty">Quantity * (use − for decreases)</label>
              <input id="qty" type="number" step={0.01} required value={form.quantity} onChange={(event) => setForm({ ...form, quantity: event.target.value })} />
            </div>
            <div className="field">
              <label htmlFor="cost">Unit cost</label>
              <input id="cost" type="number" min={0} step={0.01} value={form.unitCost} onChange={(event) => setForm({ ...form, unitCost: event.target.value })} />
            </div>
            <div className="field">
              <label htmlFor="reason">Reason *</label>
              <textarea id="reason" required maxLength={512} value={form.reason} onChange={(event) => setForm({ ...form, reason: event.target.value })} />
            </div>
            <button type="submit" className="btn btn-primary" disabled={busy}>
              Post adjustment
            </button>
          </form>
        </div>
      ) : null}

      {adjustments.length === 0 ? (
        <div className="empty-state">No adjustments recorded.</div>
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
            {adjustments.map((adjustment) => (
              <tr key={adjustment.id}>
                <td>{adjustment.adjustmentNumber}</td>
                <td>{adjustment.locationCode}</td>
                <td>{adjustment.reason}</td>
                <td>
                  {adjustment.lines.map((line) => (
                    <div key={line.productId}>
                      {line.productSku}: {line.quantity > 0 ? '+' : ''}{line.quantity}
                    </div>
                  ))}
                </td>
                <td>{new Date(adjustment.postedAtUtc).toLocaleString()}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
}