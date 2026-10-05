import { useEffect, useState, type FormEvent } from 'react';
import { describeError, json } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import type {
  InventoryLocation,
  MovementSearch,
  ProductSearch,
  StockMovement,
  StockSearch,
} from '../types';

export function StockPage() {
  const { can } = useAuth();
  const [balances, setBalances] = useState<StockSearch | null>(null);
  const [movements, setMovements] = useState<MovementSearch | null>(null);
  const [locations, setLocations] = useState<InventoryLocation[]>([]);
  const [products, setProducts] = useState<ProductSearch | null>(null);
  const [error, setError] = useState('');
  const [audit, setAudit] = useState('');
  const [receiving, setReceiving] = useState(false);
  const [busy, setBusy] = useState(false);
  const [receiveForm, setReceiveForm] = useState({
    locationId: '',
    productId: '',
    quantity: '',
    unitCost: '',
    reference: '',
  });

  const canView = can('Inventory.Stock.View');
  const canReceive = can('Inventory.Stock.Receive');

  const load = async () => {
    setError('');
    try {
      setBalances(await json.get<StockSearch>('/api/v1/stock?pageSize=200'));
      setMovements(await json.get<MovementSearch>('/api/v1/stock/movements?pageSize=50'));
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
        const [locs, prods] = await Promise.all([
          json.get<InventoryLocation[]>('/api/v1/inventory-locations'),
          json.get<ProductSearch>('/api/v1/products?pageSize=500'),
        ]);
        setLocations(locs);
        setProducts(prods);
      } catch (err) {
        setError(describeError(err));
      }
    })();
  }, [canView]);

  const activeLocations = locations.filter((loc) => loc.status === 'Active');
  const stockManagedProducts = (products?.items ?? []).filter((p) => p.stockManaged && p.status === 'Active');

  const receive = async (event: FormEvent) => {
    event.preventDefault();
    setBusy(true);
    setAudit('');
    try {
      const movements = await json.post<StockMovement[]>('/api/v1/stock/receive', {
        locationId: receiveForm.locationId,
        reference: receiveForm.reference || null,
        lines: [
          {
            productId: receiveForm.productId,
            quantity: Number(receiveForm.quantity),
            unitCost: receiveForm.unitCost ? Number(receiveForm.unitCost) : null,
          },
        ],
      });
      const refs = movements.map((m) => m.reference).join(', ');
      setAudit(await `${refs || 'Stock'} received at ${activeLocations.find((l) => l.id === receiveForm.locationId)?.name ?? ''}`);
      setReceiveForm({ ...receiveForm, productId: '', quantity: '', unitCost: '', reference: '' });
      await load();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  if (!canView) {
    return <div className="empty-state">You do not have permission to view stock.</div>;
  }

  return (
    <>
      <h1 className="page-title">Stock positions</h1>
      <p className="page-subtitle">On-hand balances by location and recent movement history.</p>

      {error ? <div className="alert alert-error">{error}</div> : null}
      {audit ? <div className="alert">{audit}</div> : null}

      <div className="toolbar">
        {canReceive ? (
          <button className="btn btn-primary" onClick={() => setReceiving((value) => !value)}>
            {receiving ? 'Close' : 'Receive stock'}
          </button>
        ) : null}
      </div>

      {receiving ? (
        <div className="card">
          <h3>Receive stock</h3>
          <form className="form-grid" onSubmit={(event) => void receive(event)}>
            <div className="field">
              <label htmlFor="loc">Location *</label>
              <select id="loc" required value={receiveForm.locationId} onChange={(event) => setReceiveForm({ ...receiveForm, locationId: event.target.value })}>
                <option value="">Select…</option>
                {activeLocations.map((loc) => (
                  <option key={loc.id} value={loc.id}>{loc.name}</option>
                ))}
              </select>
            </div>
            <div className="field">
              <label htmlFor="prod">Product *</label>
              <select id="prod" required value={receiveForm.productId} onChange={(event) => setReceiveForm({ ...receiveForm, productId: event.target.value })}>
                <option value="">Select…</option>
                {stockManagedProducts.map((p) => (
                  <option key={p.id} value={p.id}>{p.sku} — {p.name}</option>
                ))}
              </select>
            </div>
            <div className="field">
              <label htmlFor="qty">Quantity *</label>
              <input id="qty" type="number" min={0.01} step={0.01} required value={receiveForm.quantity} onChange={(event) => setReceiveForm({ ...receiveForm, quantity: event.target.value })} />
            </div>
            <div className="field">
              <label htmlFor="cost">Unit cost</label>
              <input id="cost" type="number" min={0} step={0.01} value={receiveForm.unitCost} onChange={(event) => setReceiveForm({ ...receiveForm, unitCost: event.target.value })} />
            </div>
            <div className="field">
              <label htmlFor="ref">Supplier reference</label>
              <input id="ref" maxLength={128} value={receiveForm.reference} onChange={(event) => setReceiveForm({ ...receiveForm, reference: event.target.value })} />
            </div>
            <button type="submit" className="btn btn-primary" disabled={busy}>
              Post receipt
            </button>
          </form>
        </div>
      ) : null}

      <h2 className="section-title">Balances</h2>
      {balances && balances.items.length === 0 ? (
        <div className="empty-state">No stock positions yet.</div>
      ) : (
        <table>
          <thead>
            <tr>
              <th>Product</th>
              <th>Location</th>
              <th>On hand</th>
              <th>Reserved</th>
              <th>Available</th>
              <th>Status</th>
            </tr>
          </thead>
          <tbody>
            {(balances?.items ?? []).map((row) => (
              <tr key={`${row.productId}-${row.locationId}`}>
                <td>
                  {row.productSku} — {row.productName}
                </td>
                <td>{row.locationName}</td>
                <td>{row.onHand}</td>
                <td>{row.reserved}</td>
                <td>{row.available}</td>
                <td>
                  <span className={`status-pill ${row.status === 'InStock' ? 'good' : 'bad'}`}>{row.status}</span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      <h2 className="section-title">Movement history</h2>
      {movements && movements.items.length === 0 ? (
        <div className="empty-state">No movements recorded.</div>
      ) : (
        <table>
          <thead>
            <tr>
              <th>Reference</th>
              <th>Product</th>
              <th>Location</th>
              <th>Type</th>
              <th>Qty</th>
              <th>Date</th>
            </tr>
          </thead>
          <tbody>
            {(movements?.items ?? []).map((movement) => (
              <tr key={movement.id}>
                <td>{movement.reference}</td>
                <td>{movement.productSku} — {movement.productName}</td>
                <td>{movement.locationCode}</td>
                <td>{movement.movementType}</td>
                <td>{movement.quantity}</td>
                <td>{new Date(movement.businessDateUtc).toLocaleString()}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
}