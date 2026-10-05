import { useEffect, useState, type FormEvent } from 'react';
import { useParams } from 'react-router-dom';
import { ApiError, describeError, json } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import type {
  AccountTransaction,
  AccountTransactionSearch,
  AgingReport,
  Credential,
  CredentialHistoryEntry,
  MembershipCategory,
  MembershipDependant,
  MembershipStatusHistory,
  MemberAccount,
  MemberDetail,
  Statement,
} from '../types';
import { Status } from './DashboardPage';

interface DialogState {
  kind: 'suspend' | 'reinstate' | 'cancel' | 'transfer' | '';
  membershipId?: string;
}

interface CredDialogState {
  kind: '' | 'suspend' | 'lost' | 'stolen' | 'reactivate' | 'replace';
  credentialId?: string;
}

interface AccountDialogState {
  kind: '' | 'charge' | 'payment' | 'credit' | 'adjustment' | 'reverse';
  transactionId?: string;
}

const PAYMENT_METHODS = ['CASH', 'CARD', 'ACCOUNT'];

export function MemberDetailPage() {
  const { memberId } = useParams<{ memberId: string }>();
  const { can } = useAuth();
  const [detail, setDetail] = useState<MemberDetail | null>(null);
  const [history, setHistory] = useState<MembershipStatusHistory[]>([]);
  const [historyFor, setHistoryFor] = useState<string | null>(null);
  const [categories, setCategories] = useState<MembershipCategory[]>([]);
  const [persons, setPersons] = useState<{ id: string; legalName: string; memberNumber?: string | null }[]>([]);
  const [error, setError] = useState('');
  const [dialog, setDialog] = useState<DialogState>({ kind: '' });
  const [reason, setReason] = useState('');
  const [targetCategoryId, setTargetCategoryId] = useState('');
  const [dependantPersonId, setDependantPersonId] = useState('');
  const [dependantRelation, setDependantRelation] = useState('Child');
  const [busy, setBusy] = useState(false);

  // Credentials state.
  const [credentials, setCredentials] = useState<Credential[]>([]);
  const [credentialHistory, setCredentialHistory] = useState<CredentialHistoryEntry[]>([]);
  const [credentialHistoryFor, setCredentialHistoryFor] = useState<string | null>(null);
  const [cardValue, setCardValue] = useState('');
  const [credDialog, setCredDialog] = useState<CredDialogState>({ kind: '' });
  const [credReason, setCredReason] = useState('');
  const [replaceValue, setReplaceValue] = useState('');

  // Account state.
  const [account, setAccount] = useState<MemberAccount | null>(null);
  const [transactions, setTransactions] = useState<AccountTransaction[]>([]);
  const [aging, setAging] = useState<AgingReport | null>(null);
  const [statement, setStatement] = useState<Statement | null>(null);
  const [accountDialog, setAccountDialog] = useState<AccountDialogState>({ kind: '' });
  const [entryKind, setEntryKind] = useState<'charge' | 'payment' | 'credit' | 'adjustment'>('charge');
  const [entryDescription, setEntryDescription] = useState('');
  const [entryAmount, setEntryAmount] = useState('');
  const [entryMethod, setEntryMethod] = useState('CASH');
  const [entryReason, setEntryReason] = useState('');
  const [entryRef, setEntryRef] = useState('');

  const reload = async () => {
    if (!memberId) {
      return;
    }
    setError('');
    try {
      setDetail(await json.get<MemberDetail>(`/api/v1/members/${memberId}`));
    } catch (err) {
      setError(describeError(err));
    }
  };

  const loadCredentials = async () => {
    if (!memberId || !can('Credentials.Card.View')) {
      return;
    }
    try {
      const result = await json.get<{ items: Credential[]; totalCount: number }>(
        `/api/v1/credentials?memberId=${memberId}&pageSize=100`,
      );
      setCredentials(result.items);
    } catch (err) {
      setError(describeError(err));
    }
  };

  const loadAccount = async () => {
    if (!memberId || !can('Accounts.Account.View')) {
      return;
    }
    setAging(null);
    setStatement(null);
    try {
      const account = await json.get<MemberAccount>(`/api/v1/members/${memberId}/account`);
      setAccount(account);
      if (can('Accounts.Transaction.View')) {
        const tx = await json.get<AccountTransactionSearch>(
          `/api/v1/member-accounts/${account.id}/transactions?pageSize=15`,
        );
        setTransactions(tx.items);
      }
      if (can('Accounts.Aging.View')) {
        setAging(await json.get<AgingReport>(`/api/v1/member-accounts/${account.id}/aging`));
      }
    } catch (err) {
      if (err instanceof ApiError && err.status === 403) {
        setAccount(null);
        return;
      }
      setError(describeError(err));
    }
  };

  useEffect(() => {
    void reload();
    void loadCredentials();
    void loadAccount();
    if (can('Membership.MemberTransfer')) {
      void json
        .get<MembershipCategory[]>('/api/v1/membership-categories')
        .then(setCategories)
        .catch(() => undefined);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [memberId]);

  const showHistory = async (membershipId: string) => {
    try {
      setHistory(await json.get<MembershipStatusHistory[]>(`/api/v1/memberships/${membershipId}/history`));
      setHistoryFor(membershipId);
    } catch (err) {
      setError(describeError(err));
    }
  };

  const searchPersons = async (input: string) => {
    try {
      const result = await json.get<{ items: typeof persons }>(`/api/v1/persons?query=${encodeURIComponent(input)}`);
      setPersons(result.items);
    } catch (err) {
      setError(describeError(err));
    }
  };

  const runTransition = async (membershipId: string, action: string, body?: unknown) => {
    setBusy(true);
    try {
      await json.post(`/api/v1/memberships/${membershipId}/${action}`, body ?? {});
      setDialog({ kind: '' });
      setReason('');
      await reload();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const addDependant = async (event: FormEvent) => {
    event.preventDefault();
    if (!memberId) {
      return;
    }
    setBusy(true);
    try {
      await json.post<MembershipDependant>(`/api/v1/members/${memberId}/dependants`, {
        dependantPersonId,
        relationType: dependantRelation,
        notes: '',
      });
      setDependantPersonId('');
      await reload();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  // ---- Credential actions ----

  const issueAndActivateCard = async () => {
    if (!memberId) {
      return;
    }
    setBusy(true);
    try {
      const issued = await json.post<Credential>('/api/v1/credentials', {
        credentialType: 'MagneticStripe',
        credentialValue: cardValue.trim(),
      });
      await json.post<Credential>(`/api/v1/credentials/${issued.id}/activate`, { memberId });
      setCardValue('');
      await loadCredentials();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const activateCredential = async (credentialId: string) => {
    if (!memberId) {
      return;
    }
    setBusy(true);
    try {
      await json.post<Credential>(`/api/v1/credentials/${credentialId}/activate`, { memberId });
      await loadCredentials();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const runCredentialAction = async (credentialId: string, kind: Exclude<CredDialogState['kind'], ''>) => {
    setBusy(true);
    try {
      const route = kind === 'lost' ? 'mark-lost' : kind === 'stolen' ? 'mark-stolen' : kind;
      const body = kind === 'replace' ? { credentialType: 'MagneticStripe', credentialValue: replaceValue.trim(), reason: credReason.trim() } : { reason: credReason.trim() };
      await json.post<Credential>(`/api/v1/credentials/${credentialId}/${route}`, body);
      setCredDialog({ kind: '' });
      setCredReason('');
      setReplaceValue('');
      await loadCredentials();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const showCredentialHistory = async (credentialId: string) => {
    try {
      setCredentialHistory(await json.get<CredentialHistoryEntry[]>(`/api/v1/credentials/${credentialId}/history`));
      setCredentialHistoryFor(credentialId);
    } catch (err) {
      setError(describeError(err));
    }
  };

  // ---- Account actions ----

  const submitAccountEntry = async () => {
    if (!account) {
      return;
    }
    setBusy(true);
    try {
      const base = {
        description: entryDescription,
        amount: Number(entryAmount),
        currencyCode: account.currencyCode,
      };
      if (entryKind === 'payment') {
        await json.post(`/api/v1/member-accounts/${account.id}/payments`, {
          ...base,
          paymentMethodCode: entryMethod,
          externalReference: entryRef.trim() || undefined,
        });
      } else if (entryKind === 'credit') {
        await json.post(`/api/v1/member-accounts/${account.id}/credits`, {
          ...base,
          externalReference: entryRef.trim() || undefined,
        });
      } else if (entryKind === 'adjustment') {
        await json.post(`/api/v1/member-accounts/${account.id}/adjustments`, {
          ...base,
          reason: entryReason.trim(),
        });
      } else {
        await json.post(`/api/v1/member-accounts/${account.id}/charges`, base);
      }
      setEntryDescription('');
      setEntryAmount('');
      setEntryReason('');
      setEntryRef('');
      setAccountDialog({ kind: '' });
      await loadAccount();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const reverseTransaction = async (transactionId: string) => {
    if (!account) {
      return;
    }
    setBusy(true);
    try {
      await json.post(`/api/v1/member-accounts/${account.id}/transactions/${transactionId}/reverse`, {
        reason: entryReason.trim(),
      });
      setEntryReason('');
      setAccountDialog({ kind: '' });
      await loadAccount();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const generateStatement = async () => {
    if (!account) {
      return;
    }
    setBusy(true);
    try {
      setStatement(await json.post<Statement>(`/api/v1/member-accounts/${account.id}/statement`, {}));
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  if (!detail) {
    return (
      <>
        <h1 className="page-title">Member 360</h1>
        {error ? <div className="alert alert-error">{error}</div> : null}
        <div className="empty-state">Loading member…</div>
      </>
    );
  }

  return (
    <>
      <h1 className="page-title">Member 360</h1>
      <p className="page-subtitle">
        {detail.memberNumber} · {detail.legalName} <Status value={detail.status} />
      </p>

      {error ? <div className="alert alert-error">{error}</div> : null}

      <div className="card">
        <h3>Details</h3>
        <div className="detail-grid">
          <div>
            <div className="field-label">Member number</div>
            <div className="field-value">{detail.memberNumber}</div>
          </div>
          <div>
            <div className="field-label">Legal name</div>
            <div className="field-value">{detail.person.legalName}</div>
          </div>
          <div>
            <div className="field-label">Preferred name</div>
            <div className="field-value">{detail.person.preferredName || '—'}</div>
          </div>
          <div>
            <div className="field-label">Email</div>
            <div className="field-value">{detail.person.email || '—'}</div>
          </div>
          <div>
            <div className="field-label">Phone</div>
            <div className="field-value">{detail.person.phone || '—'}</div>
          </div>
          <div>
            <div className="field-label">ID number</div>
            <div className="field-value">{detail.person.idNumber || '—'}</div>
          </div>
        </div>
      </div>

      <div className="card">
        <h3>Memberships</h3>
        {detail.memberships.length === 0 ? (
          <div className="empty-state">No membership periods on record.</div>
        ) : (
          <table>
            <thead>
              <tr>
                <th>Category</th>
                <th>Status</th>
                <th>Period</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {detail.memberships.map((membership) => (
                <tr key={membership.id}>
                  <td>{membership.categoryName}</td>
                  <td>
                    <Status value={membership.status} />
                  </td>
                  <td className="muted">
                    {membership.startDateUtc ? `${formatDate(membership.startDateUtc)} → ${formatDate(membership.endDateUtc ?? '')}` : 'Not yet activated'}
                  </td>
                  <td>
                    <div className="row-actions">
                      {membership.status === 'Pending' && can('Membership.Member.Activate') ? (
                        <button className="btn btn-primary" disabled={busy} onClick={() => void runTransition(membership.id, 'activate')}>
                          Activate
                        </button>
                      ) : null}
                      {membership.status === 'Active' && can('Membership.Member.Suspend') ? (
                        <button className="btn" onClick={() => setDialog({ kind: 'suspend', membershipId: membership.id })}>
                          Suspend
                        </button>
                      ) : null}
                      {membership.status === 'Suspended' && can('Membership.Member.Reinstate') ? (
                        <button className="btn" onClick={() => setDialog({ kind: 'reinstate', membershipId: membership.id })}>
                          Reinstate
                        </button>
                      ) : null}
                      {membership.status === 'Expired' && can('Membership.Member.Renew') ? (
                        <button className="btn btn-primary" disabled={busy} onClick={() => void runTransition(membership.id, 'renew')}>
                          Renew
                        </button>
                      ) : null}
                      {(membership.status === 'Active' || membership.status === 'Pending') && can('Membership.Member.Transfer') ? (
                        <button className="btn" onClick={() => setDialog({ kind: 'transfer', membershipId: membership.id })}>
                          Transfer
                        </button>
                      ) : null}
                      {membership.status === 'Active' && can('Membership.Member.Cancel') ? (
                        <button className="btn btn-danger" onClick={() => setDialog({ kind: 'cancel', membershipId: membership.id })}>
                          Cancel
                        </button>
                      ) : null}
                      <button className="link-button" onClick={() => void showHistory(membership.id)}>
                        History
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}

        {historyFor ? (
          <div className="card" style={{ marginTop: 14 }}>
            <h3>Status history</h3>
            <table>
              <thead>
                <tr>
                  <th>From</th>
                  <th>To</th>
                  <th>Reason</th>
                  <th>Changed</th>
                </tr>
              </thead>
              <tbody>
                {history.map((entry) => (
                  <tr key={entry.id}>
                    <td>{entry.previousState}</td>
                    <td>{entry.newState}</td>
                    <td>{entry.reason}</td>
                    <td className="muted">{formatDate(entry.changedAtUtc)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : null}
      </div>

      {can('Credentials.Card.View') ? (
        <div className="card">
          <h3>Credentials</h3>
          {credentials.length === 0 ? (
            <div className="empty-state">No credentials on record for this member.</div>
          ) : (
            <table>
              <thead>
                <tr>
                  <th>Card number</th>
                  <th>Identifier</th>
                  <th>Status</th>
                  <th>Assigned</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {credentials.map((credential) => (
                  <tr key={credential.id}>
                    <td>
                      {credential.credentialNumber}
                      {credential.replacedByCredentialNumber ? (
                        <span className="muted"> → {credential.replacedByCredentialNumber}</span>
                      ) : null}
                    </td>
                    <td>{credential.credentialValue}</td>
                    <td>
                      <Status value={credential.status} />
                    </td>
                    <td className="muted">{credential.activatedAtUtc ? formatDate(credential.activatedAtUtc) : '—'}</td>
                    <td>
                      <div className="row-actions">
                        {credential.status === 'Issued' && can('Credentials.Card.Activate') ? (
                          <button className="btn btn-primary" disabled={busy} onClick={() => void activateCredential(credential.id)}>
                            Activate
                          </button>
                        ) : null}
                        {credential.status === 'Active' && can('Credentials.Card.Suspend') ? (
                          <button className="btn" onClick={() => setCredDialog({ kind: 'suspend', credentialId: credential.id })}>
                            Suspend
                          </button>
                        ) : null}
                        {credential.status === 'Active' && can('Credentials.Card.MarkLost') ? (
                          <button className="btn" onClick={() => setCredDialog({ kind: 'lost', credentialId: credential.id })}>
                            Mark lost
                          </button>
                        ) : null}
                        {credential.status === 'Active' && can('Credentials.Card.MarkStolen') ? (
                          <button className="btn" onClick={() => setCredDialog({ kind: 'stolen', credentialId: credential.id })}>
                            Mark stolen
                          </button>
                        ) : null}
                        {credential.status === 'Suspended' && can('Credentials.Card.Reactivate') ? (
                          <button className="btn" onClick={() => setCredDialog({ kind: 'reactivate', credentialId: credential.id })}>
                            Reactivate
                          </button>
                        ) : null}
                        {(credential.status === 'Lost' || credential.status === 'Stolen') && can('Credentials.Card.Replace') ? (
                          <button className="btn" onClick={() => setCredDialog({ kind: 'replace', credentialId: credential.id })}>
                            Replace
                          </button>
                        ) : null}
                        <button className="link-button" onClick={() => void showCredentialHistory(credential.id)}>
                          History
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}

          {can('Credentials.Card.Issue') && can('Credentials.Card.Activate') ? (
            <div className="input-row" style={{ marginTop: 12 }}>
              <input
                placeholder="New card identifier"
                value={cardValue}
                onChange={(event) => setCardValue(event.target.value)}
              />
              <button
                className="btn btn-primary"
                disabled={busy || cardValue.trim() === ''}
                onClick={() => void issueAndActivateCard()}
              >
                Issue and activate for this member
              </button>
            </div>
          ) : null}

          {credDialog.kind !== '' && credDialog.credentialId ? (
            <div className="card" style={{ marginTop: 14 }}>
              <h3>
                {credDialog.kind === 'suspend'
                  ? 'Suspend credential'
                  : credDialog.kind === 'lost'
                    ? 'Mark credential lost'
                    : credDialog.kind === 'stolen'
                      ? 'Mark credential stolen'
                      : credDialog.kind === 'reactivate'
                        ? 'Reactivate credential'
                        : 'Replace credential'}
              </h3>
              {credDialog.kind === 'replace' ? (
                <input
                  placeholder="New card identifier"
                  value={replaceValue}
                  onChange={(event) => setReplaceValue(event.target.value)}
                />
              ) : null}
              <input
                placeholder="Reason"
                value={credReason}
                onChange={(event) => setCredReason(event.target.value)}
              />
              <div className="input-row" style={{ marginTop: 12 }}>
                <button
                  className="btn btn-primary"
                  disabled={busy || credReason.trim() === '' || (credDialog.kind === 'replace' && replaceValue.trim() === '')}
                  onClick={() => void runCredentialAction(credDialog.credentialId!, credDialog.kind as Exclude<CredDialogState['kind'], ''>)}
                >
                  Confirm
                </button>
                <button className="btn" onClick={() => setCredDialog({ kind: '' })}>
                  Cancel
                </button>
              </div>
            </div>
          ) : null}

          {credentialHistoryFor ? (
            <div className="card" style={{ marginTop: 14 }}>
              <h3>Credential history</h3>
              <table>
                <thead>
                  <tr>
                    <th>From</th>
                    <th>To</th>
                    <th>Reason</th>
                    <th>Changed by</th>
                    <th>Changed</th>
                  </tr>
                </thead>
                <tbody>
                  {credentialHistory.map((entry) => (
                    <tr key={entry.id}>
                      <td>{entry.previousState}</td>
                      <td>{entry.newState}</td>
                      <td>{entry.reason}</td>
                      <td className="muted">{entry.changedByUserId}</td>
                      <td className="muted">{formatDate(entry.changedAtUtc)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : null}
        </div>
      ) : null}

      {can('Accounts.Account.View') ? (
        <div className="card">
          <h3>Member account</h3>
          {account ? (
            <>
              <div className="detail-grid">
                <div>
                  <div className="field-label">Account number</div>
                  <div className="field-value">{account.accountNumber}</div>
                </div>
                <div>
                  <div className="field-label">Status</div>
                  <div className="field-value">
                    <Status value={account.status} />
                  </div>
                </div>
                <div>
                  <div className="field-label">Amount due</div>
                  <div className="field-value">{formatMoney(account.balance.amountDue, account.currencyCode)}</div>
                </div>
                <div>
                  <div className="field-label">Overdue</div>
                  <div className="field-value">{formatMoney(account.balance.overdueBalance, account.currencyCode)}</div>
                </div>
                <div>
                  <div className="field-label">Credits</div>
                  <div className="field-value">{formatMoney(account.balance.creditsBalance, account.currencyCode)}</div>
                </div>
                <div>
                  <div className="field-label">Unallocated payments</div>
                  <div className="field-value">{formatMoney(account.balance.unallocatedPayments, account.currencyCode)}</div>
                </div>
              </div>

              {aging && aging.buckets.length > 0 ? (
                <div>
                  <div className="input-row" style={{ marginTop: 12 }}>
                    <span className="muted">Outstanding: {formatMoney(aging.totalOutstanding, account.currencyCode)}</span>
                    {aging.buckets.map((bucket) => (
                      <span key={bucket.label} className="muted">
                        {bucket.label}: {formatMoney(bucket.outstandingAmount, account.currencyCode)}
                      </span>
                    ))}
                  </div>
                </div>
              ) : null}

              <div className="input-row" style={{ marginTop: 12 }}>
                {can('Accounts.Transaction.Create') ? (
                  <button className="btn" onClick={() => { setEntryKind('charge'); setAccountDialog({ kind: 'charge' }); }}>
                    Charge
                  </button>
                ) : null}
                {can('Accounts.Payment.Record') ? (
                  <button className="btn" onClick={() => { setEntryKind('payment'); setAccountDialog({ kind: 'payment' }); }}>
                    Record payment
                  </button>
                ) : null}
                {can('Accounts.Credit.Create') ? (
                  <button className="btn" onClick={() => { setEntryKind('credit'); setAccountDialog({ kind: 'credit' }); }}>
                    Credit
                  </button>
                ) : null}
                {can('Accounts.Adjustment.Create') ? (
                  <button className="btn" onClick={() => { setEntryKind('adjustment'); setAccountDialog({ kind: 'adjustment' }); }}>
                    Adjustment
                  </button>
                ) : null}
                {can('Accounts.Statement.Generate') ? (
                  <button className="btn" disabled={busy} onClick={() => void generateStatement()}>
                    Generate statement
                  </button>
                ) : null}
              </div>

              {accountDialog.kind !== '' ? (
                <div className="card" style={{ marginTop: 14 }}>
                  <h3>
                    {accountDialog.kind === 'charge'
                      ? 'New charge'
                      : accountDialog.kind === 'payment'
                        ? 'Record payment'
                        : accountDialog.kind === 'credit'
                          ? 'Apply credit'
                          : accountDialog.kind === 'adjustment'
                            ? 'Adjustment'
                            : 'Reverse transaction'}
                  </h3>
                  {accountDialog.kind === 'reverse' ? (
                    <input
                      placeholder="Reason"
                      value={entryReason}
                      onChange={(event) => setEntryReason(event.target.value)}
                    />
                  ) : (
                    <>
                      <input
                        placeholder="Description"
                        value={entryDescription}
                        onChange={(event) => setEntryDescription(event.target.value)}
                      />
                      <div className="input-row">
                        <input
                          type="number"
                          placeholder="Amount"
                          value={entryAmount}
                          onChange={(event) => setEntryAmount(event.target.value)}
                        />
                        {accountDialog.kind === 'payment' ? (
                          <select value={entryMethod} onChange={(event) => setEntryMethod(event.target.value)}>
                            {PAYMENT_METHODS.map((method) => (
                              <option key={method}>{method}</option>
                            ))}
                          </select>
                        ) : null}
                        {(accountDialog.kind === 'payment' || accountDialog.kind === 'credit') ? (
                          <input
                            placeholder="External reference"
                            value={entryRef}
                            onChange={(event) => setEntryRef(event.target.value)}
                          />
                        ) : null}
                        {accountDialog.kind === 'adjustment' ? (
                          <input
                            placeholder="Reason"
                            value={entryReason}
                            onChange={(event) => setEntryReason(event.target.value)}
                          />
                        ) : null}
                      </div>
                    </>
                  )}
                  <div className="input-row" style={{ marginTop: 12 }}>
                    <button
                      className="btn btn-primary"
                      disabled={
                        busy ||
                        (accountDialog.kind === 'reverse'
                          ? entryReason.trim() === ''
                          : entryDescription.trim() === '' || !(Number(entryAmount) > 0))
                      }
                      onClick={() =>
                        accountDialog.kind === 'reverse'
                          ? void reverseTransaction(accountDialog.transactionId!)
                          : void submitAccountEntry()
                      }
                    >
                      Confirm
                    </button>
                    <button className="btn" onClick={() => setAccountDialog({ kind: '' })}>
                      Cancel
                    </button>
                  </div>
                </div>
              ) : null}

              {transactions.length > 0 ? (
                <table style={{ marginTop: 14 }}>
                  <thead>
                    <tr>
                      <th>Reference</th>
                      <th>Type</th>
                      <th>Description</th>
                      <th>Amount</th>
                      <th>Status</th>
                      <th>Date</th>
                      <th />
                    </tr>
                  </thead>
                  <tbody>
                    {transactions.map((transaction) => (
                      <tr key={transaction.id}>
                        <td className="muted">{transaction.reference}</td>
                        <td>{transaction.transactionType}</td>
                        <td>{transaction.description}</td>
                        <td className={transaction.isDebit ? '' : 'positive'}>{formatSigned(transaction.signedAmount, account.currencyCode)}</td>
                        <td>
                          <Status value={transaction.status} />
                        </td>
                        <td className="muted">{formatDate(transaction.businessDateUtc)}</td>
                        <td>
                          {(transaction.transactionType === 'Payment' || transaction.transactionType === 'Credit') &&
                          transaction.status === 'Posted' &&
                          can('Accounts.Payment.Reverse') ? (
                            <button
                              className="btn btn-danger"
                              onClick={() => {
                                setAccountDialog({ kind: 'reverse', transactionId: transaction.id });
                                setEntryReason('');
                              }}
                            >
                              Reverse
                            </button>
                          ) : null}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              ) : null}

              {statement ? (
                <div className="card" style={{ marginTop: 14 }}>
                  <h3>
                    Statement {statement.statementNumber} · {formatDate(statement.fromDateUtc)} → {formatDate(statement.toDateUtc)}
                  </h3>
                  <div className="detail-grid">
                    <div>
                      <div className="field-label">Opening balance</div>
                      <div className="field-value">{formatMoney(statement.openingBalance, statement.currencyCode)}</div>
                    </div>
                    <div>
                      <div className="field-label">Closing balance</div>
                      <div className="field-value">{formatMoney(statement.closingBalance, statement.currencyCode)}</div>
                    </div>
                  </div>
                </div>
              ) : null}
            </>
          ) : (
            <div className="empty-state">Loading account…</div>
          )}
        </div>
      ) : null}

      {can('Membership.Dependant.Manage') ? (
        <div className="card">
          <h3>Dependants</h3>
          {detail.dependants.length === 0 ? (
            <div className="empty-state">No dependants recorded.</div>
          ) : (
            <table>
              <thead>
                <tr>
                  <th>Name</th>
                  <th>Relation</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {detail.dependants.map((dependant) => (
                  <tr key={dependant.id}>
                    <td>{dependant.dependantName}</td>
                    <td>{dependant.relationType}</td>
                    <td>
                      <button
                        className="link-button"
                        disabled={busy}
                        onClick={() =>
                          void (async () => {
                            try {
                              await json.delete(`/api/v1/members/${detail.id}/dependants/${dependant.dependantPersonId}`);
                              await reload();
                            } catch (err) {
                              setError(describeError(err));
                            }
                          })()
                        }
                      >
                        Remove
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
          <form className="input-row" style={{ marginTop: 12 }} onSubmit={(event) => void addDependant(event)}>
            <select value={dependantPersonId} onChange={(event) => setDependantPersonId(event.target.value)} required>
              <option value="">Select a person</option>
              {persons.map((person) => (
                <option key={person.id} value={person.id}>
                  {person.memberNumber ?? 'Person'} · {person.legalName}
                </option>
              ))}
            </select>
            <select value={dependantRelation} onChange={(event) => setDependantRelation(event.target.value)}>
              <option>Child</option>
              <option>Spouse</option>
              <option>Other</option>
            </select>
            <button type="button" className="btn" onClick={() => void searchPersons('')}>
              Load persons
            </button>
            <button type="submit" className="btn btn-primary" disabled={busy || dependantPersonId === ''}>
              Add dependant
            </button>
          </form>
        </div>
      ) : null}

      {dialog.kind !== '' && dialog.membershipId ? (
        <div className="card">
          <h3>{dialog.kind === 'suspend' ? 'Suspend' : dialog.kind === 'reinstate' ? 'Reinstate' : dialog.kind === 'cancel' ? 'Cancel' : 'Transfer'} membership</h3>
          {dialog.kind === 'transfer' ? (
            <div className="input-row">
              <select value={targetCategoryId} onChange={(event) => setTargetCategoryId(event.target.value)}>
                <option value="">Select target category</option>
                {categories.map((category) => (
                  <option key={category.id} value={category.id}>
                    {category.name}
                  </option>
                ))}
              </select>
            </div>
          ) : (
            <input
              placeholder="Reason"
              value={reason}
              onChange={(event) => setReason(event.target.value)}
            />
          )}
          <div className="input-row" style={{ marginTop: 12 }}>
            <button
              className="btn btn-primary"
              disabled={busy || (dialog.kind === 'transfer' && targetCategoryId === '')}
              onClick={() =>
                dialog.kind === 'transfer'
                  ? void runTransition(dialog.membershipId!, 'transfer', { targetCategoryId })
                  : void runTransition(dialog.membershipId!, dialog.kind, { reason })
              }
            >
              Confirm
            </button>
            <button className="btn" onClick={() => setDialog({ kind: '' })}>
              Cancel
            </button>
          </div>
        </div>
      ) : null}
    </>
  );
}

function formatDate(value: string): string {
  if (!value) {
    return '—';
  }
  return new Date(value).toLocaleDateString(undefined, { day: '2-digit', month: 'short', year: 'numeric' });
}

function formatMoney(value: number, currencyCode: string): string {
  return `${value.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })} ${currencyCode}`;
}

function formatSigned(value: number, currencyCode: string): string {
  const formatted = Math.abs(value).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  return `${value < 0 ? '-' : value > 0 ? '+' : ''}${formatted} ${currencyCode}`;
}