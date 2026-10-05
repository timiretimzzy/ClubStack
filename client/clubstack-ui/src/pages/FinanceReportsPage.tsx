import { useEffect, useState } from 'react';
import { describeError, json } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import type {
  Aging,
  CashPosition,
  FinancialAccount,
  FinancialPeriodsReport,
  GeneralLedger,
  TrialBalance,
} from '../types';

const ACCOUNT_TYPE: Record<number, string> = { 0: 'Asset', 1: 'Liability', 2: 'Equity', 3: 'Revenue', 4: 'Expense' };
const NORMAL_BALANCE: Record<number, string> = { 0: 'Debit', 1: 'Credit' };
const PERIOD_STATUS: Record<number, string> = { 0: 'Open', 1: 'Closed' };
const CASH_KIND: Record<number, string> = { 0: 'None', 1: 'Cash on hand', 2: 'Bank' };

const TABS = [
  { key: 'trial-balance', label: 'Trial balance' },
  { key: 'general-ledger', label: 'General ledger' },
  { key: 'receivable-aging', label: 'Receivable aging' },
  { key: 'payable-aging', label: 'Payable aging' },
  { key: 'cash-position', label: 'Cash position' },
  { key: 'periods', label: 'Periods' },
];

export function FinanceReportsPage() {
  const { can } = useAuth();
  const [tab, setTab] = useState('trial-balance');
  const [accounts, setAccounts] = useState<FinancialAccount[]>([]);
  const [trialBalance, setTrialBalance] = useState<TrialBalance | null>(null);
  const [trialBalanceAsOf, setTrialBalanceAsOf] = useState('');
  const [ledgerAccountId, setLedgerAccountId] = useState('');
  const [ledgerFrom, setLedgerFrom] = useState('');
  const [ledgerTo, setLedgerTo] = useState('');
  const [ledger, setLedger] = useState<GeneralLedger | null>(null);
  const [receivableAsOf, setReceivableAsOf] = useState('');
  const [receivableAging, setReceivableAging] = useState<Aging | null>(null);
  const [payableAsOf, setPayableAsOf] = useState('');
  const [payableAging, setPayableAging] = useState<Aging | null>(null);
  const [cashAsOf, setCashAsOf] = useState('');
  const [cashPosition, setCashPosition] = useState<CashPosition | null>(null);
  const [periods, setPeriods] = useState<FinancialPeriodsReport | null>(null);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  const canView = can('Finance.Report.View');

  const loadTrialBalance = async () => {
    setError('');
    try {
      const parameters = new URLSearchParams();
      if (trialBalanceAsOf) {
        parameters.set('asOf', new Date(trialBalanceAsOf).toISOString());
      }
      setTrialBalance(
        await json.get<TrialBalance>(`/api/v1/finance/reports/trial-balance?${parameters.toString()}`),
      );
    } catch (err) {
      setError(describeError(err));
    }
  };

  const loadLedger = async () => {
    setError('');
    if (!ledgerAccountId) {
      setError('Select an account first.');
      return;
    }
    try {
      const parameters = new URLSearchParams();
      if (ledgerFrom) {
        parameters.set('from', new Date(ledgerFrom).toISOString());
      }
      if (ledgerTo) {
        parameters.set('to', new Date(ledgerTo).toISOString());
      }
      setLedger(
        await json.get<GeneralLedger>(
          `/api/v1/finance/reports/general-ledger/${ledgerAccountId}?${parameters.toString()}`,
        ),
      );
    } catch (err) {
      setError(describeError(err));
    }
  };

  const loadReceivableAging = async () => {
    setError('');
    try {
      const parameters = new URLSearchParams();
      if (receivableAsOf) {
        parameters.set('asOf', new Date(receivableAsOf).toISOString());
      }
      setReceivableAging(await json.get<Aging>(`/api/v1/finance/reports/receivable-aging?${parameters.toString()}`));
    } catch (err) {
      setError(describeError(err));
    }
  };

  const loadPayableAging = async () => {
    setError('');
    try {
      const parameters = new URLSearchParams();
      if (payableAsOf) {
        parameters.set('asOf', new Date(payableAsOf).toISOString());
      }
      setPayableAging(await json.get<Aging>(`/api/v1/finance/reports/payable-aging?${parameters.toString()}`));
    } catch (err) {
      setError(describeError(err));
    }
  };

  const loadCashPosition = async () => {
    setError('');
    try {
      const parameters = new URLSearchParams();
      if (cashAsOf) {
        parameters.set('asOf', new Date(cashAsOf).toISOString());
      }
      setCashPosition(await json.get<CashPosition>(`/api/v1/finance/reports/cash-position?${parameters.toString()}`));
    } catch (err) {
      setError(describeError(err));
    }
  };

  const loadPeriods = async () => {
    setError('');
    try {
      setPeriods(await json.get<FinancialPeriodsReport>('/api/v1/finance/reports/periods'));
    } catch (err) {
      setError(describeError(err));
    }
  };

  const loadReport = async (report: string) => {
    setBusy(true);
    try {
      if (report === 'trial-balance') {
        await loadTrialBalance();
      } else if (report === 'general-ledger') {
        await loadLedger();
      } else if (report === 'receivable-aging') {
        await loadReceivableAging();
      } else if (report === 'payable-aging') {
        await loadPayableAging();
      } else if (report === 'cash-position') {
        await loadCashPosition();
      } else {
        await loadPeriods();
      }
    } finally {
      setBusy(false);
    }
  };

  useEffect(() => {
    if (!canView) {
      return;
    }
    void (async () => {
      try {
        const search = await json.get<{ items: FinancialAccount[] }>('/api/v1/finance/accounts?pageSize=200');
        setAccounts(search.items);
      } catch (err) {
        setError(describeError(err));
      }
    })();
    void loadReport('trial-balance');
  }, [canView]);

  if (!canView) {
    return <div className="empty-state">You do not have permission to view finance reports.</div>;
  }

  const aging = tab === 'receivable-aging' ? receivableAging : tab === 'payable-aging' ? payableAging : null;

  return (
    <>
      <h1 className="page-title">Finance reports</h1>
      <p className="page-subtitle">The six mandated reports. Every figure is derived from posted journals only.</p>
      <p className="muted">An empty as-of date means now.</p>

      {error ? <div className="alert alert-error">{error}</div> : null}

      <div className="toolbar">
        {TABS.map((entry) => (
          <button
            key={entry.key}
            className={tab === entry.key ? 'btn btn-primary' : 'btn'}
            onClick={() => {
              setTab(entry.key);
              void loadReport(entry.key);
            }}
          >
            {entry.label}
          </button>
        ))}
      </div>

      {tab === 'trial-balance' ? (
        <div className="card">
          <h3>Trial balance</h3>
          <div className="toolbar">
            <div className="field">
              <label htmlFor="tbAsOf">As of</label>
              <input
                id="tbAsOf"
                type="date"
                value={trialBalanceAsOf}
                onChange={(event) => setTrialBalanceAsOf(event.target.value)}
              />
            </div>
            <button className="btn" disabled={busy} onClick={() => void loadTrialBalance()}>
              Refresh
            </button>
          </div>
          {trialBalance === null ? (
            <div className="empty-state">No trial balance loaded.</div>
          ) : (
            <>
              {trialBalance.difference !== 0 ? (
                <div className="alert alert-error">
                  The trial balance does not balance: difference {trialBalance.difference.toFixed(2)}{' '}
                  {trialBalance.currencyCode}.
                </div>
              ) : null}
              {trialBalance.rows.length === 0 ? (
                <div className="empty-state">No posted movements in scope.</div>
              ) : (
                <table>
                  <thead>
                    <tr>
                      <th>Code</th>
                      <th>Account</th>
                      <th>Type</th>
                      <th>Normal balance</th>
                      <th>Debit</th>
                      <th>Credit</th>
                      <th>Balance</th>
                    </tr>
                  </thead>
                  <tbody>
                    {trialBalance.rows.map((row) => (
                      <tr key={row.accountCode}>
                        <td>{row.accountCode}</td>
                        <td>{row.accountName}</td>
                        <td>{ACCOUNT_TYPE[row.accountType]}</td>
                        <td>{NORMAL_BALANCE[row.normalBalance]}</td>
                        <td>{row.debit.toFixed(2)}</td>
                        <td>{row.credit.toFixed(2)}</td>
                        <td>{row.balance.toFixed(2)}</td>
                      </tr>
                    ))}
                    <tr>
                      <td />
                      <td>Total</td>
                      <td />
                      <td />
                      <td>
                        <strong>{trialBalance.totalDebit.toFixed(2)}</strong>
                      </td>
                      <td>
                        <strong>{trialBalance.totalCredit.toFixed(2)}</strong>
                      </td>
                      <td>{trialBalance.difference.toFixed(2)}</td>
                    </tr>
                  </tbody>
                </table>
              )}
              <p className="muted">
                {trialBalance.currencyCode}
                {trialBalance.asOf ? ` · as of ${new Date(trialBalance.asOf).toLocaleString()}` : ''}
              </p>
            </>
          )}
        </div>
      ) : null}

      {tab === 'general-ledger' ? (
        <div className="card">
          <h3>General ledger</h3>
          <div className="toolbar">
            <div className="field">
              <label htmlFor="glAccount">Account *</label>
              <select
                id="glAccount"
                value={ledgerAccountId}
                onChange={(event) => setLedgerAccountId(event.target.value)}
              >
                <option value="">Select an account</option>
                {accounts.map((account) => (
                  <option key={account.id} value={account.id}>
                    {account.code} — {account.name}
                  </option>
                ))}
              </select>
            </div>
            <div className="field">
              <label htmlFor="glFrom">From</label>
              <input id="glFrom" type="date" value={ledgerFrom} onChange={(event) => setLedgerFrom(event.target.value)} />
            </div>
            <div className="field">
              <label htmlFor="glTo">To</label>
              <input id="glTo" type="date" value={ledgerTo} onChange={(event) => setLedgerTo(event.target.value)} />
            </div>
            <button className="btn" disabled={busy} onClick={() => void loadLedger()}>
              Refresh
            </button>
          </div>
          {ledger === null ? (
            <div className="empty-state">No ledger loaded.</div>
          ) : (
            <>
              <div className="detail-grid">
                <div>
                  <div className="field-label">Account</div>
                  <div className="field-value">
                    {ledger.accountCode} — {ledger.accountName}
                  </div>
                </div>
                <div>
                  <div className="field-label">Opening balance</div>
                  <div className="field-value">{ledger.openingBalance.toFixed(2)}</div>
                </div>
                <div>
                  <div className="field-label">Closing balance</div>
                  <div className="field-value">{ledger.closingBalance.toFixed(2)}</div>
                </div>
                <div>
                  <div className="field-label">Currency</div>
                  <div className="field-value">{ledger.currencyCode}</div>
                </div>
              </div>
              {ledger.entries.length === 0 ? (
                <div className="empty-state">No posted entries in this range.</div>
              ) : (
                <table style={{ marginTop: 12 }}>
                  <thead>
                    <tr>
                      <th>Date</th>
                      <th>Journal</th>
                      <th>Description</th>
                      <th>Account</th>
                      <th>Debit</th>
                      <th>Credit</th>
                    </tr>
                  </thead>
                  <tbody>
                    {ledger.entries.map((entry, index) => (
                      <tr key={`${entry.journalReference}-${entry.transactionDate}-${index}`}>
                        <td className="muted">{new Date(entry.transactionDate).toLocaleString()}</td>
                        <td>
                          {entry.journalReference}
                          {entry.sourceReference ? (
                            <div className="muted">{entry.sourceReference}</div>
                          ) : null}
                        </td>
                        <td>{entry.lineDescription || entry.journalDescription}</td>
                        <td>
                          {entry.accountCode} — {entry.accountName}
                        </td>
                        <td>{entry.direction === 0 ? entry.amount.toFixed(2) : ''}</td>
                        <td>{entry.direction === 1 ? entry.amount.toFixed(2) : ''}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </>
          )}
        </div>
      ) : null}

      {tab === 'receivable-aging' || tab === 'payable-aging' ? (
        <div className="card">
          <h3>{tab === 'receivable-aging' ? 'Receivable aging' : 'Payable aging'}</h3>
          <div className="toolbar">
            <div className="field">
              <label htmlFor="agingAsOf">As of</label>
              <input
                id="agingAsOf"
                type="date"
                value={tab === 'receivable-aging' ? receivableAsOf : payableAsOf}
                onChange={(event) =>
                  tab === 'receivable-aging'
                    ? setReceivableAsOf(event.target.value)
                    : setPayableAsOf(event.target.value)
                }
              />
            </div>
            <button
              className="btn"
              disabled={busy}
              onClick={() => void (tab === 'receivable-aging' ? loadReceivableAging() : loadPayableAging())}
            >
              Refresh
            </button>
          </div>
          {aging === null ? (
            <div className="empty-state">No aging report loaded.</div>
          ) : (
            <>
              <p className="muted">
                {aging.currencyCode} · as of {new Date(aging.asOf).toLocaleString()} · outstanding{' '}
                {aging.totalOutstanding.toFixed(2)}
              </p>
              <table>
                <thead>
                  <tr>
                    <th>Bucket</th>
                    <th>Days from</th>
                    <th>Days to</th>
                    <th>Documents</th>
                    <th>Outstanding</th>
                  </tr>
                </thead>
                <tbody>
                  {aging.buckets.map((bucket) => (
                    <tr key={bucket.label}>
                      <td>{bucket.label}</td>
                      <td>{bucket.daysFrom}</td>
                      <td>{bucket.daysTo ?? '—'}</td>
                      <td>{bucket.documentCount ?? 0}</td>
                      <td>{bucket.outstandingAmount.toFixed(2)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
              {aging.documents.length === 0 ? (
                <div className="empty-state">No outstanding documents in scope.</div>
              ) : (
                <table style={{ marginTop: 16 }}>
                  <thead>
                    <tr>
                      <th>Reference</th>
                      <th>Counterparty</th>
                      <th>Date</th>
                      <th>Due</th>
                      <th>Days outstanding</th>
                      <th>Bucket</th>
                      <th>Original</th>
                      <th>Allocated</th>
                      <th>Outstanding</th>
                    </tr>
                  </thead>
                  <tbody>
                    {aging.documents.map((document) => (
                      <tr key={document.documentId}>
                        <td>{document.reference}</td>
                        <td>
                          {document.counterpartyName} ({document.counterpartyCode})
                        </td>
                        <td className="muted">{new Date(document.transactionDate).toLocaleString()}</td>
                        <td className="muted">
                          {document.dueDate ? new Date(document.dueDate).toLocaleString() : '—'}
                        </td>
                        <td>{document.daysOutstanding}</td>
                        <td>{document.bucketLabel}</td>
                        <td>{document.originalAmount.toFixed(2)}</td>
                        <td>{document.allocatedAmount.toFixed(2)}</td>
                        <td>{document.outstandingAmount.toFixed(2)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </>
          )}
        </div>
      ) : null}

      {tab === 'cash-position' ? (
        <div className="card">
          <h3>Cash position</h3>
          <div className="toolbar">
            <div className="field">
              <label htmlFor="cashAsOfInput">As of</label>
              <input id="cashAsOfInput" type="date" value={cashAsOf} onChange={(event) => setCashAsOf(event.target.value)} />
            </div>
            <button className="btn" disabled={busy} onClick={() => void loadCashPosition()}>
              Refresh
            </button>
          </div>
          {cashPosition === null ? (
            <div className="empty-state">No cash position loaded.</div>
          ) : (
            <>
              <div className="detail-grid">
                <div>
                  <div className="field-label">Base currency</div>
                  <div className="field-value">{cashPosition.baseCurrencyCode}</div>
                </div>
                <div>
                  <div className="field-label">As of</div>
                  <div className="field-value">{new Date(cashPosition.asOf).toLocaleString()}</div>
                </div>
                <div>
                  <div className="field-label">Total cash</div>
                  <div className="field-value">{cashPosition.totalCash.toFixed(2)}</div>
                </div>
                <div>
                  <div className="field-label">Total bank</div>
                  <div className="field-value">{cashPosition.totalBank.toFixed(2)}</div>
                </div>
              </div>
              {cashPosition.rows.length === 0 ? (
                <div className="empty-state">No cash or bank accounts have posted balances.</div>
              ) : (
                <table style={{ marginTop: 12 }}>
                  <thead>
                    <tr>
                      <th>Code</th>
                      <th>Account</th>
                      <th>Cash kind</th>
                      <th>Debit balance</th>
                      <th>Credit balance</th>
                      <th>Net balance</th>
                      <th>Currency</th>
                    </tr>
                  </thead>
                  <tbody>
                    {cashPosition.rows.map((row) => (
                      <tr key={row.accountCode}>
                        <td>{row.accountCode}</td>
                        <td>{row.accountName}</td>
                        <td>{CASH_KIND[row.cashKind]}</td>
                        <td>{row.debitBalance.toFixed(2)}</td>
                        <td>{row.creditBalance.toFixed(2)}</td>
                        <td>{row.netBalance.toFixed(2)}</td>
                        <td>{row.currencyCode}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </>
          )}
        </div>
      ) : null}

      {tab === 'periods' ? (
        <div className="card">
          <h3>Periods</h3>
          <div className="toolbar">
            <button className="btn" disabled={busy} onClick={() => void loadPeriods()}>
              Refresh
            </button>
          </div>
          {periods === null ? (
            <div className="empty-state">No period report loaded.</div>
          ) : (
            <>
              <p className="muted">
                Current period: {periods.currentPeriod ? periods.currentPeriod.reference : '—'}
                {periods.currentStatus !== null
                  ? ` · ${PERIOD_STATUS[periods.currentStatus]}`
                  : ''}
              </p>
              <table>
                <thead>
                  <tr>
                    <th>Reference</th>
                    <th>Year / month</th>
                    <th>Name</th>
                    <th>Status</th>
                    <th>Window</th>
                    <th>Posted journals</th>
                    <th>Posted debit</th>
                    <th>Posted credit</th>
                  </tr>
                </thead>
                <tbody>
                  {periods.rows.map((row) => (
                    <tr key={row.reference}>
                      <td>{row.reference}</td>
                      <td>
                        {row.year} / {String(row.month).padStart(2, '0')}
                      </td>
                      <td>{row.name}</td>
                      <td>
                        <span className={`status-pill ${row.status === 0 ? 'good' : 'bad'}`}>
                          {PERIOD_STATUS[row.status]}
                        </span>
                      </td>
                      <td className="muted">
                        {new Date(row.startUtc).toLocaleString()} → {new Date(row.endUtc).toLocaleString()}
                      </td>
                      <td>{row.postedJournalCount}</td>
                      <td>{row.postedDebitTotal.toFixed(2)}</td>
                      <td>{row.postedCreditTotal.toFixed(2)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </>
          )}
        </div>
      ) : null}
    </>
  );
}
