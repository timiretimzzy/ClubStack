import { useCallback, useEffect, useState } from 'react';
import { describeError, json } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import type {
  FiscalDayAction,
  FiscalDayStatus,
  FiscalDeviceAction,
  FiscalDeviceConfiguration,
  FiscalDeviceStatus,
  Fiscalisation,
  FiscalisationRetry,
  FiscalisationSearch,
  FiscalisationSubmitPending,
} from '../types';

const STATUSES = [
  'NotRequired',
  'Pending',
  'Submitted',
  'Fiscalised',
  'PendingRetry',
  'Failed',
] as const;

function money(amount: number): string {
  return amount.toLocaleString(undefined, { minimumFractionDigits: 2 });
}

function stamp(value: string | null): string {
  return value ? new Date(value).toLocaleString() : '—';
}

function documentPill(kind: string): string {
  return kind === 'CreditNote' ? 'status-pill warn' : 'status-pill ok';
}

function statusPill(status: string): string {
  switch (status) {
    case 'Fiscalised':
      return 'status-pill ok';
    case 'Submitted':
    case 'Pending':
      return 'status-pill';
    case 'PendingRetry':
    case 'Failed':
      return 'status-pill warn';
    default:
      return 'status-pill';
  }
}

/** Only documents that have not reached the device are worth retrying. */
function isRetryable(status: string): boolean {
  return status === 'PendingRetry' || status === 'Failed' || status === 'Pending';
}

export function FiscalisationPage() {
  const { can } = useAuth();
  const [status, setStatus] = useState<FiscalDeviceStatus | null>(null);
  const [configuration, setConfiguration] = useState<FiscalDeviceConfiguration | null>(null);
  const [day, setDay] = useState<FiscalDayStatus | null>(null);
  const [rows, setRows] = useState<Fiscalisation[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [statusFilter, setStatusFilter] = useState('');
  const [page, setPage] = useState(1);
  const pageSize = 25;
  const [expanded, setExpanded] = useState<string | null>(null);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [busy, setBusy] = useState(false);

  const [licenseKey, setLicenseKey] = useState('');
  const [deviceId, setDeviceId] = useState('');
  const [serialNumber, setSerialNumber] = useState('');
  const [companyName, setCompanyName] = useState('');
  const [branchName, setBranchName] = useState('');
  const [city, setCity] = useState('');
  const [activationKey, setActivationKey] = useState('');
  const [certificateFolder, setCertificateFolder] = useState('');

  const canView = can('Fiscalization.Device.View');
  const canManage = can('Fiscalization.Device.Manage');
  const canOpen = can('Fiscalization.Day.Open');
  const canClose = can('Fiscalization.Day.Close');
  const canViewTransactions = can('Fiscalization.Transaction.View');
  const canRetry = can('Fiscalization.Transaction.Retry');

  const loadDevice = useCallback(async () => {
    try {
      setStatus(await json.get<FiscalDeviceStatus>('/api/v1/fiscalisation/device/status'));
      setConfiguration(
        await json.get<FiscalDeviceConfiguration>('/api/v1/fiscalisation/device/configuration'),
      );
    } catch (err) {
      setError(describeError(err));
    }
  }, []);

  const loadDay = useCallback(async () => {
    try {
      setDay(await json.get<FiscalDayStatus>('/api/v1/fiscalisation/day'));
    } catch (err) {
      setError(describeError(err));
    }
  }, []);

  const loadDocuments = useCallback(async () => {
    if (!canViewTransactions) {
      return;
    }
    try {
      const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
      if (statusFilter) {
        params.set('status', statusFilter);
      }
      const result = await json.get<FiscalisationSearch>(
        `/api/v1/fiscalisation/documents?${params.toString()}`,
      );
      setRows(result.items);
      setTotalCount(result.totalCount);
    } catch (err) {
      setError(describeError(err));
    }
  }, [canViewTransactions, page, statusFilter]);

  useEffect(() => {
    if (canView) {
      void loadDevice();
      void loadDay();
    }
  }, [canView, loadDevice, loadDay]);

  useEffect(() => {
    void loadDocuments();
  }, [loadDocuments]);

  // Pre-fill the administration form from the device's own identity.
  useEffect(() => {
    if (configuration === null) {
      return;
    }
    setDeviceId(configuration.deviceId ?? '');
    setSerialNumber(configuration.serialNumber ?? '');
    setCompanyName(configuration.companyName ?? '');
    setBranchName(configuration.branchName ?? '');
    setCity(configuration.city ?? '');
    setCertificateFolder(configuration.certificateFolder ?? '');
  }, [configuration]);

  const runDayAction = async (action: 'open' | 'close') => {
    setBusy(true);
    setError('');
    setNotice('');
    try {
      const result = await json.post<FiscalDayAction>(`/api/v1/fiscalisation/day/${action}`);
      setNotice(`${result.message} (day ${result.fiscalDayNo ?? '—'})`);
      await loadDay();
      await loadDevice();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const installLicense = async () => {
    setBusy(true);
    setError('');
    setNotice('');
    try {
      const result = await json.post<FiscalDeviceAction>('/api/v1/fiscalisation/device/license', {
        licenseKey,
      });
      setNotice(result.message);
      // The key is never read back, so clear the field immediately.
      setLicenseKey('');
      await loadDevice();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const applyConfiguration = async () => {
    setBusy(true);
    setError('');
    setNotice('');
    try {
      const result = await json.post<FiscalDeviceAction>('/api/v1/fiscalisation/device/configuration', {
        deviceId,
        serialNumber,
        companyName,
        branchName,
        city,
        activationKey,
        certificateFolder,
      });
      setNotice(result.message);
      setActivationKey('');
      await loadDevice();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const submitPending = async () => {
    setBusy(true);
    setError('');
    setNotice('');
    try {
      const result = await json.post<FiscalisationSubmitPending>(
        '/api/v1/fiscalisation/documents/submit-pending',
      );
      setNotice(
        result.stillPending === 0
          ? 'The engine flushed its offline queue; nothing is still pending.'
          : `The engine still holds ${result.stillPending} document(s) offline.`,
      );
      await loadDevice();
      await loadDocuments();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const retry = async (id: string) => {
    setBusy(true);
    setError('');
    setNotice('');
    try {
      const result = await json.post<FiscalisationRetry>(`/api/v1/fiscalisation/documents/${id}/retry`);
      setNotice(result.message);
      await loadDocuments();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  if (!canView && !canViewTransactions) {
    return <div className="empty-state">You do not have permission to view fiscalisation.</div>;
  }

  return (
    <>
      <h1 className="page-title">Fiscalisation</h1>
      <p className="page-subtitle">
        ZIMRA fiscal device status, fiscal day control and fiscalised documents.
      </p>

      {error ? <div className="alert alert-error">{error}</div> : null}
      {notice ? <div className="alert alert-info">{notice}</div> : null}

      {canView ? (
        <>
          <div className="detail-grid">
            <div className="card">
              <h3>Device</h3>
              {status === null ? (
                <div className="empty-state">Reading the device…</div>
              ) : (
                <div className="detail-grid">
                  <div className="field">
                    <span className="field-label">Available</span>
                    <span className="field-value">
                      <span className={status.available ? 'status-pill ok' : 'status-pill warn'}>
                        {status.available ? 'Online' : 'Unreachable'}
                      </span>
                    </span>
                  </div>
                  <div className="field">
                    <span className="field-label">Device id</span>
                    <span className="field-value">{status.deviceId ?? '—'}</span>
                  </div>
                  <div className="field">
                    <span className="field-label">Serial number</span>
                    <span className="field-value">{status.serialNumber ?? '—'}</span>
                  </div>
                  <div className="field">
                    <span className="field-label">Taxpayer</span>
                    <span className="field-value">
                      {status.taxpayerName ?? '—'}
                      {status.taxpayerTaxNumber ? ` (${status.taxpayerTaxNumber})` : ''}
                    </span>
                  </div>
                  <div className="field">
                    <span className="field-label">Licence</span>
                    <span className="field-value">
                      <span className={status.licenseActive ? 'status-pill ok' : 'status-pill warn'}>
                        {status.licenseActive ? 'Active' : 'Not active'}
                      </span>
                    </span>
                  </div>
                  <div className="field">
                    <span className="field-label">Licence status</span>
                    <span className="field-value">{status.licenseStatus ?? '—'}</span>
                  </div>
                  <div className="field">
                    <span className="field-label">Licence expires</span>
                    <span className="field-value">{stamp(status.licenseExpiry)}</span>
                  </div>
                  <div className="field">
                    <span className="field-label">Certificates</span>
                    <span className="field-value">{status.certificateCount}</span>
                  </div>
                  <div className="field">
                    <span className="field-label">ZIMRA link</span>
                    <span className="field-value">
                      <span className={status.zimraReachable ? 'status-pill ok' : 'status-pill warn'}>
                        {status.zimraReachable ? 'Reachable' : 'Offline'}
                      </span>
                    </span>
                  </div>
                  <div className="field">
                    <span className="field-label">Last receipt</span>
                    <span className="field-value">{status.lastReceiptGlobalNo ?? '—'}</span>
                  </div>
                  {status.lastError ? (
                    <div className="field">
                      <span className="field-label">Last error</span>
                      <span className="field-value">{status.lastError}</span>
                    </div>
                  ) : null}
                </div>
              )}
            </div>

            <div className="card">
              <h3>Fiscal day</h3>
              {day === null ? (
                <div className="empty-state">Reading the fiscal day…</div>
              ) : (
                <>
                  <div className="detail-grid">
                    <div className="field">
                      <span className="field-label">State</span>
                      <span className="field-value">
                        <span className={day.state === 'Opened' ? 'status-pill ok' : 'status-pill warn'}>
                          {day.state}
                        </span>
                      </span>
                    </div>
                    <div className="field">
                      <span className="field-label">Device reports</span>
                      <span className="field-value">{day.fiscalDayStatus ?? '—'}</span>
                    </div>
                    <div className="field">
                      <span className="field-label">Last fiscal day</span>
                      <span className="field-value">{day.lastFiscalDayNo ?? '—'}</span>
                    </div>
                    <div className="field">
                      <span className="field-label">Source</span>
                      <span className="field-value">{day.zimraSource ?? '—'}</span>
                    </div>
                  </div>

                  <div className="toolbar" style={{ marginTop: 12 }}>
                    {canOpen ? (
                      <button
                        type="button"
                        className="btn btn-primary"
                        onClick={() => void runDayAction('open')}
                        disabled={busy}
                      >
                        Open fiscal day
                      </button>
                    ) : null}
                    {canClose ? (
                      <button
                        type="button"
                        className="btn"
                        onClick={() => void runDayAction('close')}
                        disabled={busy}
                      >
                        Close fiscal day
                      </button>
                    ) : null}
                    {canRetry ? (
                      <button
                        type="button"
                        className="btn"
                        onClick={() => void submitPending()}
                        disabled={busy}
                      >
                        Submit engine queue
                      </button>
                    ) : null}
                  </div>
                  {!canOpen && !canClose ? (
                    <p className="muted">You do not have permission to control the fiscal day.</p>
                  ) : null}
                </>
              )}
            </div>
          </div>

          {configuration !== null ? (
            <div className="card" style={{ marginTop: 16 }}>
              <h3>Tax identifiers</h3>
              <p className="muted">
                Read from the device configuration. Tax id 0 means the engine selects the bracket
                itself.
              </p>
              <div className="detail-grid">
                <div className="field">
                  <span className="field-label">Standard rated</span>
                  <span className="field-value">{configuration.standardRatedTaxId}</span>
                </div>
                <div className="field">
                  <span className="field-label">Zero rated</span>
                  <span className="field-value">{configuration.zeroRatedTaxId}</span>
                </div>
                <div className="field">
                  <span className="field-label">Exempt</span>
                  <span className="field-value">{configuration.exemptTaxId}</span>
                </div>
                <div className="field">
                  <span className="field-label">Environment</span>
                  <span className="field-value">{configuration.environment ?? '—'}</span>
                </div>
              </div>
            </div>
          ) : null}

          {canManage ? (
            <div className="card" style={{ marginTop: 16 }}>
              <h3>Device administration</h3>
              <p className="muted">
                Keys are forwarded to the fiscal device and are never stored or displayed by
                ClubStack.
              </p>

              <div className="toolbar">
                <div className="field">
                  <label htmlFor="licenseKey">Licence key</label>
                  <input
                    id="licenseKey"
                    type="password"
                    autoComplete="off"
                    value={licenseKey}
                    onChange={(event) => setLicenseKey(event.target.value)}
                  />
                </div>
                <button
                  type="button"
                  className="btn btn-primary"
                  onClick={() => void installLicense()}
                  disabled={busy || licenseKey.trim().length === 0}
                >
                  Install licence
                </button>
              </div>

              <h3 style={{ marginTop: 16 }}>Device settings</h3>
              <div className="toolbar">
                <div className="field">
                  <label htmlFor="deviceId">Device id</label>
                  <input
                    id="deviceId"
                    value={deviceId}
                    onChange={(event) => setDeviceId(event.target.value)}
                  />
                </div>
                <div className="field">
                  <label htmlFor="serialNumber">Serial number</label>
                  <input
                    id="serialNumber"
                    value={serialNumber}
                    onChange={(event) => setSerialNumber(event.target.value)}
                  />
                </div>
              </div>
              <div className="toolbar">
                <div className="field">
                  <label htmlFor="companyName">Company name</label>
                  <input
                    id="companyName"
                    value={companyName}
                    onChange={(event) => setCompanyName(event.target.value)}
                  />
                </div>
                <div className="field">
                  <label htmlFor="branchName">Branch name</label>
                  <input
                    id="branchName"
                    value={branchName}
                    onChange={(event) => setBranchName(event.target.value)}
                  />
                </div>
                <div className="field">
                  <label htmlFor="city">City</label>
                  <input id="city" value={city} onChange={(event) => setCity(event.target.value)} />
                </div>
              </div>
              <div className="toolbar">
                <div className="field">
                  <label htmlFor="activationKey">Activation key</label>
                  <input
                    id="activationKey"
                    type="password"
                    autoComplete="off"
                    value={activationKey}
                    onChange={(event) => setActivationKey(event.target.value)}
                  />
                </div>
                <div className="field">
                  <label htmlFor="certificateFolder">Certificate folder</label>
                  <input
                    id="certificateFolder"
                    value={certificateFolder}
                    onChange={(event) => setCertificateFolder(event.target.value)}
                  />
                </div>
                <button
                  type="button"
                  className="btn btn-primary"
                  onClick={() => void applyConfiguration()}
                  disabled={busy || activationKey.trim().length === 0}
                >
                  Apply settings
                </button>
              </div>
            </div>
          ) : null}
        </>
      ) : null}

      {canViewTransactions ? (
        <>
          <h2 className="page-title" style={{ marginTop: 24 }}>Fiscalised documents</h2>
          <div className="toolbar">
            <div className="field">
              <label htmlFor="statusFilter">Status</label>
              <select
                id="statusFilter"
                value={statusFilter}
                onChange={(event) => {
                  setStatusFilter(event.target.value);
                  setPage(1);
                }}
              >
                <option value="">All</option>
                {STATUSES.map((value) => (
                  <option key={value} value={value}>
                    {value}
                  </option>
                ))}
              </select>
            </div>
            <button
              type="button"
              className="btn"
              onClick={() => void loadDocuments()}
              disabled={busy}
            >
              Refresh
            </button>
          </div>

          {rows.length === 0 ? (
            <div className="empty-state">No fiscalised documents yet.</div>
          ) : (
            <table>
              <thead>
                <tr>
                  <th>Invoice</th>
                  <th>Kind</th>
                  <th>Sale</th>
                  <th>Status</th>
                  <th>Receipt</th>
                  <th>Attempts</th>
                  <th>Fiscalised</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {rows.map((row) => (
                  <tr key={row.id}>
                    <td>{row.invoiceNumber}</td>
                    <td>
                      <span className={documentPill(row.documentKind)}>{row.documentKind}</span>
                    </td>
                    <td>{row.refundNumber ?? row.saleNumber}</td>
                    <td>
                      <span className={statusPill(row.status)}>{row.status}</span>
                      {row.pendingSync ? ' (offline)' : ''}
                    </td>
                    <td>{row.receiptGlobalNo ?? '—'}</td>
                    <td>{row.attemptCount}</td>
                    <td>{stamp(row.fiscalisedAtUtc)}</td>
                    <td>
                      <div className="row-actions">
                        <button
                          type="button"
                          className="btn"
                          onClick={() => setExpanded(expanded === row.id ? null : row.id)}
                        >
                          {expanded === row.id ? 'Hide' : 'Details'}
                        </button>
                        {canRetry && isRetryable(row.status) ? (
                          <button
                            type="button"
                            className="btn btn-primary"
                            onClick={() => void retry(row.id)}
                            disabled={busy}
                          >
                            Retry
                          </button>
                        ) : null}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}

          {totalCount > pageSize ? (
            <div className="toolbar" style={{ marginTop: 12 }}>
              <button
                type="button"
                className="btn"
                onClick={() => setPage(Math.max(1, page - 1))}
                disabled={busy || page === 1}
              >
                Previous
              </button>
              <span className="muted">
                Page {page} of {Math.max(1, Math.ceil(totalCount / pageSize))} ({totalCount} documents)
              </span>
              <button
                type="button"
                className="btn"
                onClick={() => setPage(page + 1)}
                disabled={busy || page * pageSize >= totalCount}
              >
                Next
              </button>
            </div>
          ) : null}

          {expanded !== null ? (
            <div className="card" style={{ marginTop: 16 }}>
              <h3>Document detail</h3>
              {rows
                .filter((row) => row.id === expanded)
                .map((row) => (
                  <div key={row.id}>
                    <div className="detail-grid">
                      <div className="field">
                        <span className="field-label">Invoice number</span>
                        <span className="field-value">{row.invoiceNumber}</span>
                      </div>
                      <div className="field">
                        <span className="field-label">Original invoice</span>
                        <span className="field-value">{row.originalInvoiceNumber ?? '—'}</span>
                      </div>
                      <div className="field">
                        <span className="field-label">Fiscal day</span>
                        <span className="field-value">{row.fiscalDayNo ?? '—'}</span>
                      </div>
                      <div className="field">
                        <span className="field-label">Receipt id</span>
                        <span className="field-value">{row.receiptId ?? '—'}</span>
                      </div>
                      <div className="field">
                        <span className="field-label">Payment mapping</span>
                        <span className="field-value">{row.paymentMapping ?? '—'}</span>
                      </div>
                      <div className="field">
                        <span className="field-label">Last attempt</span>
                        <span className="field-value">{stamp(row.lastAttemptAtUtc)}</span>
                      </div>
                    </div>

                    {row.qrUrl ? (
                      <p className="muted" style={{ marginTop: 12 }}>
                        Verification code:{' '}
                        <a href={row.qrUrl} target="_blank" rel="noreferrer">
                          {row.qrUrl}
                        </a>
                      </p>
                    ) : null}

                    {row.message ? (
                      <p className={row.status === 'Failed' ? 'alert alert-error' : 'muted'}>
                        {row.message}
                      </p>
                    ) : null}

                    {row.validationWarnings.length > 0 ? (
                      <>
                        <h4 style={{ marginTop: 12 }}>Device warnings</h4>
                        <ul>
                          {row.validationWarnings.map((warning) => (
                            <li key={warning}>{warning}</li>
                          ))}
                        </ul>
                      </>
                    ) : null}

                    <h4 style={{ marginTop: 12 }}>Tenders as recorded by ClubStack</h4>
                    {row.tenders.length === 0 ? (
                      <div className="empty-state">No tender breakdown recorded.</div>
                    ) : (
                      <table>
                        <thead>
                          <tr>
                            <th>Method</th>
                            <th>Name</th>
                            <th>Kind</th>
                            <th>Amount</th>
                            <th>Reference</th>
                          </tr>
                        </thead>
                        <tbody>
                          {row.tenders.map((tender) => (
                            <tr key={`${tender.paymentMethodCode}-${tender.amount}`}>
                              <td>{tender.paymentMethodCode}</td>
                              <td>{tender.paymentMethodName}</td>
                              <td>{tender.kind}</td>
                              <td>{money(tender.amount)}</td>
                              <td>{tender.externalReference ?? '—'}</td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    )}
                  </div>
                ))}
            </div>
          ) : null}
        </>
      ) : null}
    </>
  );
}
