import { useEffect, useState } from 'react';
import { describeError, json } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import type { StockSearch } from '../types';

export function AlertsPage() {
  const { can } = useAuth();
  const [rows, setRows] = useState<StockSearch | null>(null);
  const [error, setError] = useState('');

  const canView = can('Inventory.Stock.View');

  useEffect(() => {
    if (!canView) {
      return;
    }
    setError('');
    json
      .get<StockSearch>('/api/v1/stock?reorderOnly=true&pageSize=200')
      .then(setRows)
      .catch((err) => setError(describeError(err)));
  }, [canView]);

  if (!canView) {
    return <div className="empty-state">You do not have permission to view stock alerts.</div>;
  }

  return (
    <>
      <h1 className="page-title">Reorder alerts</h1>
      <p className="page-subtitle">Products whose total on-hand stock across all locations is at or below the reorder level.</p>

      {error ? <div className="alert alert-error">{error}</div> : null}

      {rows === null ? (
        <div className="empty-state">Loading…</div>
      ) : rows.items.length === 0 ? (
        <div className="empty-state">No products below their reorder level.</div>
      ) : (
        <table>
          <thead>
            <tr>
              <th>Product</th>
              <th>Location</th>
              <th>On hand</th>
              <th>Reorder level</th>
              <th>Status</th>
            </tr>
          </thead>
          <tbody>
            {rows.items.map((row) => (
              <tr key={`${row.productId}-${row.locationId}`}>
                <td>{row.productSku} — {row.productName}</td>
                <td>{row.locationName}</td>
                <td>{row.onHand}</td>
                <td>{row.reorderLevel ?? '—'}</td>
                <td><span className="status-pill bad">{row.status}</span></td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
}