import { useEffect, useState } from 'react';
import { describeError, json } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import type { FinanceOverview } from '../types';

const PERIOD_STATUS: Record<number, string> = { 0: 'Open', 1: 'Closed' };

export function FinanceOverviewPage() {
  const { can } = useAuth();
  const [overview, setOverview] = useState<FinanceOverview | null>(null);
  const [error, setError] = useState('');

  const canView = can('Finance.Report.View');

  const load = async () => {
    setError('');
    try {
      setOverview(await json.get<FinanceOverview>('/api/v1/finance/reports/overview'));
    } catch (err) {
      setError(describeError(err));
    }
  };

  useEffect(() => {
    if (canView) {
      void load();
    }
  }, [canView]);

  if (!canView) {
    return <div className="empty-state">You do not have permission to view the finance overview.</div>;
  }

  return (
    <>
      <h1 className="page-title">Finance overview</h1>
      <p className="page-subtitle">Position of the club books, derived entirely from posted journals.</p>

      {error ? <div className="alert alert-error">{error}</div> : null}

      {overview === null ? (
        <div className="empty-state">Loading finance overview…</div>
      ) : (
        <>
          {overview.trialBalanceDifference !== 0 ? (
            <div className="alert alert-error">
              The trial balance does not balance: difference {overview.trialBalanceDifference.toFixed(2)}{' '}
              {overview.baseCurrencyCode}.
            </div>
          ) : null}

          <div className="card">
            <h3>Position</h3>
            <div className="detail-grid">
              <div>
                <div className="field-label">Base currency</div>
                <div className="field-value">{overview.baseCurrencyCode}</div>
              </div>
              <div>
                <div className="field-label">As of</div>
                <div className="field-value">{new Date(overview.asOf).toLocaleString()}</div>
              </div>
              <div>
                <div className="field-label">Current period</div>
                <div className="field-value">
                  {overview.currentPeriod ? (
                    <>
                      {overview.currentPeriod.reference}{' '}
                      <span className={`status-pill ${overview.currentPeriod.status === 0 ? 'good' : 'bad'}`}>
                        {PERIOD_STATUS[overview.currentPeriod.status]}
                      </span>
                    </>
                  ) : (
                    'No current period'
                  )}
                </div>
              </div>
              <div>
                <div className="field-label">Open receivables</div>
                <div className="field-value">{overview.openReceivables.toFixed(2)}</div>
              </div>
              <div>
                <div className="field-label">Open payables</div>
                <div className="field-value">{overview.openPayables.toFixed(2)}</div>
              </div>
              <div>
                <div className="field-label">Cash and bank</div>
                <div className="field-value">{overview.cashAndBank.toFixed(2)}</div>
              </div>
              <div>
                <div className="field-label">Trial balance debits</div>
                <div className="field-value">{overview.trialBalanceDebits.toFixed(2)}</div>
              </div>
              <div>
                <div className="field-label">Trial balance credits</div>
                <div className="field-value">{overview.trialBalanceCredits.toFixed(2)}</div>
              </div>
              <div>
                <div className="field-label">Difference</div>
                <div className="field-value">{overview.trialBalanceDifference.toFixed(2)}</div>
              </div>
              <div>
                <div className="field-label">Draft journals</div>
                <div className="field-value">{overview.draftJournalCount}</div>
              </div>
              <div>
                <div className="field-label">Unreconciled cash accounts</div>
                <div className="field-value">{overview.unreconciledCashAccountCount}</div>
              </div>
            </div>
          </div>
        </>
      )}
    </>
  );
}
