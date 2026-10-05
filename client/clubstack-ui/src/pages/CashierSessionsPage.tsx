import { useEffect, useState } from 'react';
import { describeError, json } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import type { CashierSession, CashierSessionSearch, PosConfiguration } from '../types';

const UNFINISHED = ['Open', 'Active', 'Closing'];

function money(amount: number): string {
  return amount.toLocaleString(undefined, { minimumFractionDigits: 2 });
}

function stamp(value: string | null): string {
  return value ? new Date(value).toLocaleString() : '—';
}

function statusPill(status: string): string {
  if (status === 'Active') {
    return 'status-pill ok';
  }
  if (status === 'Reconciled') {
    return 'status-pill ok';
  }
  if (status === 'Closing') {
    return 'status-pill warn';
  }
  if (status === 'Closed') {
    return 'status-pill warn';
  }
  return 'status-pill warn';
}

export function CashierSessionsPage() {
  const { can } = useAuth();
  const [config, setConfig] = useState<PosConfiguration>({ outlets: [], registers: [] });
  const [current, setCurrent] = useState<CashierSession | null>(null);
  const [rows, setRows] = useState<CashierSession[]>([]);
  const [registerId, setRegisterId] = useState('');
  const [openingFloat, setOpeningFloat] = useState('0');
  const [movementType, setMovementType] = useState('PayIn');
  const [movementAmount, setMovementAmount] = useState('');
  const [movementReason, setMovementReason] = useState('');
  const [declaredCash, setDeclaredCash] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [busy, setBusy] = useState(false);

  const canView = can('Cashiering.Session.View');
  const canOpen = can('Cashiering.Session.Open');
  const canMove = can('Cashiering.CashMovement.Create');
  const canClose = can('Cashiering.Session.Close');
  const canReconcile = can('Cashiering.Session.Reconcile');

  const loadHistory = async () => {
    try {
      const params = new URLSearchParams({ pageSize: '25' });
      if (statusFilter) {
        params.set('status', statusFilter);
      }
      const result = await json.get<CashierSessionSearch>(`/api/v1/cashier-sessions?${params.toString()}`);
      setRows(result.items);
    } catch (err) {
      setError(describeError(err));
    }
  };

  const loadCurrent = async (forRegister: string) => {
    if (!forRegister) {
      setCurrent(null);
      return;
    }
    try {
      const result = await json.get<CashierSessionSearch>(
        `/api/v1/cashier-sessions?registerId=${forRegister}&pageSize=25`,
      );
      const unfinished = result.items
        .filter((session) => UNFINISHED.includes(session.status))
        .sort((a, b) => b.openedAtUtc.localeCompare(a.openedAtUtc));
      setCurrent(unfinished[0] ?? null);
    } catch (err) {
      setError(describeError(err));
    }
  };

  useEffect(() => {
    if (!canView) {
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
    void loadHistory();
  }, [canView, statusFilter]);

  useEffect(() => {
    if (canView) {
      void loadCurrent(registerId);
    }
  }, [canView, registerId]);

  const refresh = async () => {
    await Promise.all([loadCurrent(registerId), loadHistory()]);
  };

  const openSession = async () => {
    setBusy(true);
    setError('');
    setNotice('');
    try {
      const opened = await json.post<CashierSession>('/api/v1/cashier-sessions', {
        registerId,
        openingFloat: Number(openingFloat) || 0,
      });
      setCurrent(opened);
      setDeclaredCash(String(opened.expectedCash));
      setNotice(`Session ${opened.sessionNumber} opened.`);
      await refresh();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const addMovement = async () => {
    if (!current) {
      return;
    }
    setBusy(true);
    setError('');
    setNotice('');
    try {
      const updated = await json.post<CashierSession>(`/api/v1/cashier-sessions/${current.id}/movements`, {
        movementType,
        amount: Number(movementAmount) || 0,
        reason: movementReason,
      });
      setCurrent(updated);
      setMovementAmount('');
      setMovementReason('');
      setNotice(`${movementType} recorded. Expected cash is now ${money(updated.expectedCash)}.`);
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const closeSession = async () => {
    if (!current) {
      return;
    }
    setBusy(true);
    setError('');
    setNotice('');
    try {
      const closed = await json.post<CashierSession>(`/api/v1/cashier-sessions/${current.id}/close`, {
        declaredCash: Number(declaredCash) || 0,
      });
      setCurrent(closed);
      const variance = closed.variance ?? 0;
      setNotice(
        variance === 0
          ? `Session ${closed.sessionNumber} closed with no variance.`
          : `Session ${closed.sessionNumber} closed with a variance of ${money(variance)}.`,
      );
      await refresh();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const reconcileSession = async () => {
    if (!current) {
      return;
    }
    setBusy(true);
    setError('');
    setNotice('');
    try {
      const reconciled = await json.post<CashierSession>(
        `/api/v1/cashier-sessions/${current.id}/reconcile`,
        {},
      );
      setCurrent(reconciled);
      setNotice(`Session ${reconciled.sessionNumber} reconciled.`);
      await refresh();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  if (!canView) {
    return <div className="empty-state">You do not have permission to view cashier sessions.</div>;
  }

  return (
    <>
      <h1 className="page-title">Cashier Sessions</h1>
      <p className="page-subtitle">Opening float, cash movements, reconciliation and session history.</p>

      {error ? <div className="alert alert-error">{error}</div> : null}
      {notice ? <div className="alert alert-info">{notice}</div> : null}

      <div className="toolbar">
        <div className="field">
          <label htmlFor="register">Register</label>
          <select
            id="register"
            value={registerId}
            disabled={current !== null}
            onChange={(event) => setRegisterId(event.target.value)}
          >
            <option value="">Select a register…</option>
            {config.registers.map((register) => (
              <option key={register.id} value={register.id}>
                {register.posOutletCode} / {register.code} — {register.name}
              </option>
            ))}
          </select>
        </div>
      </div>

      <div className="detail-grid">
        <div className="card">
          <h3>Current session</h3>
          {current === null ? (
            <div className="empty-state">No unfinished session on this register.</div>
          ) : (
            <>
              <div className="detail-grid">
                <div className="field">
                  <span className="field-label">Session</span>
                  <span className="field-value">{current.sessionNumber}</span>
                </div>
                <div className="field">
                  <span className="field-label">Status</span>
                  <span className="field-value">
                    <span className={statusPill(current.status)}>{current.status}</span>
                  </span>
                </div>
                <div className="field">
                  <span className="field-label">Opening float</span>
                  <span className="field-value">{money(current.openingFloat)}</span>
                </div>
                <div className="field">
                  <span className="field-label">Expected cash</span>
                  <span className="field-value">
                    <strong>{money(current.expectedCash)}</strong>
                  </span>
                </div>
                <div className="field">
                  <span className="field-label">Declared cash</span>
                  <span className="field-value">
                    {current.declaredCash === null ? '—' : money(current.declaredCash)}
                  </span>
                </div>
                <div className="field">
                  <span className="field-label">Variance</span>
                  <span className="field-value">
                    {current.variance === null ? '—' : money(current.variance)}
                    {current.varianceApproved ? ' (approved)' : ''}
                  </span>
                </div>
                <div className="field">
                  <span className="field-label">Opened</span>
                  <span className="field-value">{stamp(current.openedAtUtc)}</span>
                </div>
                <div className="field">
                  <span className="field-label">Closed</span>
                  <span className="field-value">{stamp(current.closedAtUtc)}</span>
                </div>
              </div>

              <h3 style={{ marginTop: 16 }}>Cash movements</h3>
              {current.movements.length === 0 ? (
                <div className="empty-state">No movements recorded.</div>
              ) : (
                <table>
                  <thead>
                    <tr>
                      <th>Type</th>
                      <th>Amount</th>
                      <th>Drawer effect</th>
                      <th>Reason</th>
                      <th>Recorded</th>
                    </tr>
                  </thead>
                  <tbody>
                    {current.movements.map((movement) => (
                      <tr key={movement.id}>
                        <td>{movement.movementType}</td>
                        <td>{money(movement.amount)}</td>
                        <td>{money(movement.signedAmount)}</td>
                        <td>{movement.reason ?? '—'}</td>
                        <td>{stamp(movement.createdAtUtc)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}

              {canMove && current.status === 'Active' ? (
                <div className="toolbar" style={{ marginTop: 12 }}>
                  <div className="field">
                    <label htmlFor="movementType">Movement</label>
                    <select
                      id="movementType"
                      value={movementType}
                      onChange={(event) => setMovementType(event.target.value)}
                    >
                      <option value="PayIn">Pay in</option>
                      <option value="PayOut">Pay out</option>
                    </select>
                  </div>
                  <div className="field">
                    <label htmlFor="movementAmount">Amount</label>
                    <input
                      id="movementAmount"
                      type="number"
                      min={0.01}
                      step={0.01}
                      value={movementAmount}
                      onChange={(event) => setMovementAmount(event.target.value)}
                    />
                  </div>
                  <div className="field">
                    <label htmlFor="movementReason">Reason</label>
                    <input
                      id="movementReason"
                      value={movementReason}
                      onChange={(event) => setMovementReason(event.target.value)}
                    />
                  </div>
                  <button
                    type="button"
                    className="btn"
                    onClick={() => void addMovement()}
                    disabled={busy || !Number(movementAmount)}
                  >
                    Record movement
                  </button>
                </div>
              ) : null}

              {canClose && current.status === 'Active' ? (
                <div className="toolbar" style={{ marginTop: 12 }}>
                  <div className="field">
                    <label htmlFor="declaredCash">Declared cash in drawer</label>
                    <input
                      id="declaredCash"
                      type="number"
                      min={0}
                      step={0.01}
                      value={declaredCash}
                      onChange={(event) => setDeclaredCash(event.target.value)}
                    />
                  </div>
                  <button
                    type="button"
                    className="btn btn-primary"
                    onClick={() => void closeSession()}
                    disabled={busy}
                  >
                    Close session
                  </button>
                </div>
              ) : null}

              {canReconcile && current.status === 'Closed' ? (
                <div className="row-actions" style={{ marginTop: 12 }}>
                  <button
                    type="button"
                    className="btn btn-primary"
                    onClick={() => void reconcileSession()}
                    disabled={busy}
                  >
                    Reconcile session
                  </button>
                </div>
              ) : null}
            </>
          )}
        </div>

        <div className="card">
          <h3>Open a session</h3>
          <p className="muted">A register may only have one unfinished session at a time.</p>
          <div className="toolbar">
            <div className="field">
              <label htmlFor="openingFloat">Opening float</label>
              <input
                id="openingFloat"
                type="number"
                min={0}
                step={0.01}
                value={openingFloat}
                onChange={(event) => setOpeningFloat(event.target.value)}
              />
            </div>
            <button
              type="button"
              className="btn btn-primary"
              onClick={() => void openSession()}
              disabled={busy || !canOpen || !registerId || current !== null}
            >
              Open session
            </button>
          </div>
          {!canOpen ? <p className="muted">You do not have permission to open a session.</p> : null}
        </div>
      </div>

      <h2 className="page-title" style={{ marginTop: 24 }}>History</h2>
      <div className="toolbar">
        <div className="field">
          <label htmlFor="statusFilter">Status</label>
          <select
            id="statusFilter"
            value={statusFilter}
            onChange={(event) => setStatusFilter(event.target.value)}
          >
            <option value="">All</option>
            <option value="Open">Open</option>
            <option value="Active">Active</option>
            <option value="Closing">Closing</option>
            <option value="Closed">Closed</option>
            <option value="Reconciled">Reconciled</option>
          </select>
        </div>
        <button type="button" className="btn" onClick={() => void loadHistory()} disabled={busy}>
          Refresh
        </button>
      </div>

      {rows.length === 0 ? (
        <div className="empty-state">No sessions recorded yet.</div>
      ) : (
        <table>
          <thead>
            <tr>
              <th>Session</th>
              <th>Register</th>
              <th>Status</th>
              <th>Float</th>
              <th>Expected</th>
              <th>Variance</th>
              <th>Opened</th>
              <th>Reconciled</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((session) => (
              <tr key={session.id}>
                <td>{session.sessionNumber}</td>
                <td>{session.registerCode}</td>
                <td>
                  <span className={statusPill(session.status)}>{session.status}</span>
                </td>
                <td>{money(session.openingFloat)}</td>
                <td>{money(session.expectedCash)}</td>
                <td>{session.variance === null ? '—' : money(session.variance)}</td>
                <td>{stamp(session.openedAtUtc)}</td>
                <td>{stamp(session.reconciledAtUtc)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
}
