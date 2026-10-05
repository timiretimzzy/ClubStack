import { useEffect, useState } from 'react';
import { describeError, json } from '../api/client';
import type { Fiscalisation } from '../types';

interface Props {
  saleId: string | null;
}

function stamp(value: string | null): string {
  return value ? new Date(value).toLocaleString() : '—';
}

function pill(status: string): string {
  switch (status) {
    case 'Fiscalised':
      return 'status-pill ok';
    case 'PendingRetry':
    case 'Failed':
      return 'status-pill warn';
    default:
      return 'status-pill';
  }
}

/**
 * Cashier-facing view of the fiscal document owed for a sale. A completed sale always
 * owes one, so this never blocks payment: the document is driven by the background
 * worker and may still be pending when the till receipt prints.
 */
export function FiscalStatusForSale({ saleId }: Props) {
  const [document, setDocument] = useState<Fiscalisation | null>(null);
  const [error, setError] = useState('');

  useEffect(() => {
    if (saleId === null) {
      setDocument(null);
      return;
    }

    let cancelled = false;
    const load = async () => {
      try {
        const result = await json.get<Fiscalisation>(
          `/api/v1/fiscalisation/documents/by-sale/${saleId}`,
        );
        if (!cancelled) {
          setDocument(result);
          setError('');
        }
      } catch (err) {
        if (!cancelled) {
          // 404 simply means the document has not been raised yet.
          setDocument(null);
          setError(err instanceof Error && err.name === 'ApiError' ? '' : describeError(err));
        }
      }
    };

    void load();

    // The worker may still be driving the document to the device.
    const timer = window.setInterval(() => void load(), 5000);
    return () => {
      cancelled = true;
      window.clearInterval(timer);
    };
  }, [saleId]);

  if (error) {
    return <div className="alert alert-error">{error}</div>;
  }

  if (document === null) {
    return null;
  }

  return (
    <div style={{ marginTop: 12 }}>
      <h4>Fiscal document</h4>
      <div className="detail-grid">
        <div className="field">
          <span className="field-label">Invoice</span>
          <span className="field-value">{document.invoiceNumber}</span>
        </div>
        <div className="field">
          <span className="field-label">Status</span>
          <span className="field-value">
            <span className={pill(document.status)}>{document.status}</span>
            {document.pendingSync ? ' (held offline by the device)' : ''}
          </span>
        </div>
        <div className="field">
          <span className="field-label">Fiscal day</span>
          <span className="field-value">{document.fiscalDayNo ?? '—'}</span>
        </div>
        <div className="field">
          <span className="field-label">Receipt</span>
          <span className="field-value">{document.receiptGlobalNo ?? '—'}</span>
        </div>
        <div className="field">
          <span className="field-label">Fiscalised</span>
          <span className="field-value">{stamp(document.fiscalisedAtUtc)}</span>
        </div>
      </div>
      {document.qrUrl ? (
        <p className="muted">
          ZIMRA verification code:{' '}
          <a href={document.qrUrl} target="_blank" rel="noreferrer">
            {document.qrUrl}
          </a>
        </p>
      ) : null}
      {document.status !== 'Fiscalised' && document.message ? (
        <p className="muted">{document.message}</p>
      ) : null}
    </div>
  );
}
