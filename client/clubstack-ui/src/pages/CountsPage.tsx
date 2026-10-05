import { useEffect, useState, type FormEvent } from 'react';
import { describeError, json } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import type { InventoryLocation, StockCount, StockCountSearch } from '../types';

export function CountsPage() {
  const { can } = useAuth();
  const [counts, setCounts] = useState<StockCount[]>([]);
  const [locations, setLocations] = useState<InventoryLocation[]>([]);
  const [error, setError] = useState('');
  const [initiating, setInitiating] = useState(false);
  const [busy, setBusy] = useState(false);
  const [locationId, setLocationId] = useState('');

  const canCount = can('Inventory.Stock.Count');
  const canApprove = can('Inventory.Stock.ApproveAdjustment');

  const load = async () => {
    setError('');
    try {
      const search = await json.get<StockCountSearch>('/api/v1/counts?pageSize=100');
      setCounts(search.items);
    } catch (err) {
      setError(describeError(err));
    }
  };

  useEffect(() => {
    if (!canCount) {
      return;
    }
    void load();
    void (async () => {
      try {
        setLocations(await json.get<InventoryLocation[]>('/api/v1/inventory-locations'));
      } catch (err) {
        setError(describeError(err));
      }
    })();
  }, [canCount]);

  const initiate = async (event: FormEvent) => {
    event.preventDefault();
    setBusy(true);
    try {
      await json.post<StockCount>('/api/v1/counts', { locationId });
      setInitiating(false);
      setLocationId('');
      await load();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const record = async (count: StockCount) => {
    setBusy(true);
    setError('');
    try {
      const detail = await json.get<StockCount>(`/api/v1/counts/${count.id}`);
      const items = detail.lines.map((line) => ({ productId: line.productId, countedQuantity: line.countedQuantity ?? line.expectedQuantity }));
      await json.put<StockCount>(`/api/v1/counts/${count.id}/lines`, { items });
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
      await json.post<StockCount>(`/api/v1/counts/${id}/${action}`);
      await load();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  if (!canCount) {
    return <div className="empty-state">You do not have permission to view stock counts.</div>;
  }

  return (
    <>
      <h1 className="page-title">Stock counts</h1>
      <p className="page-subtitle">Initiate a count, record what was counted, complete and approve to post variances.</p>

      {error ? <div className="alert alert-error">{error}</div> : null}

      <div className="toolbar">
        <button className="btn btn-primary" onClick={() => setInitiating((value) => !value)}>
          {initiating ? 'Close' : 'Initiate count'}
        </button>
      </div>

      {initiating ? (
        <form className="card form-grid" onSubmit={(event) => void initiate(event)}>
          <div className="field">
            <label htmlFor="loc">Location *</label>
            <select id="loc" required value={locationId} onChange={(event) => setLocationId(event.target.value)}>
              <option value="">Select…</option>
              {locations.filter((loc) => loc.status === 'Active').map((loc) => (
                <option key={loc.id} value={loc.id}>{loc.name}</option>
              ))}
            </select>
          </div>
          <button type="submit" className="btn btn-primary" disabled={busy}>
            Start count
          </button>
        </form>
      ) : null}

      {counts.length === 0 ? (
        <div className="empty-state">No stock counts yet.</div>
      ) : (
        counts.map((count) => (
          <div className="card" key={count.id}>
            <div className="input-row">
              <strong>{count.countNumber}</strong>
              <span className="muted">{count.locationCode}</span>
              <span className={`status-pill ${count.status === 'Approved' ? 'good' : count.status === 'Cancelled' ? 'bad' : 'warn'}`}>{count.status}</span>
              {count.status === 'InProgress' || count.status === 'Completed' ? (
                <>
                  {count.status === 'InProgress' ? (
                    <button type="button" className="btn btn-secondary" disabled={busy} onClick={() => void record(count)}>
                      Count at expected
                    </button>
                  ) : null}
                  {count.status === 'InProgress' ? (
                    <button type="button" className="btn btn-secondary" disabled={busy} onClick={() => void run('complete', count.id)}>
                      Complete
                    </button>
                  ) : null}
                  {count.status === 'Completed' && canApprove ? (
                    <button type="button" className="btn btn-primary" disabled={busy} onClick={() => void run('approve', count.id)}>
                      Approve & post variance
                    </button>
                  ) : null}
                  <button type="button" className="link-button" disabled={busy} onClick={() => void run('cancel', count.id)}>
                    Cancel
                  </button>
                </>
              ) : null}
            </div>
            <table style={{ marginTop: 10 }}>
              <thead>
                <tr>
                  <th>Product</th>
                  <th>Expected</th>
                  <th>Counted</th>
                  <th>Variance</th>
                </tr>
              </thead>
              <tbody>
                {count.lines.map((line) => (
                  <tr key={line.productId}>
                    <td>{line.productSku} — {line.productName}</td>
                    <td>{line.expectedQuantity}</td>
                    <td>{line.countedQuantity ?? '—'}</td>
                    <td>{line.variance}</td>
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