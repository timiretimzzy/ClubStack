import { useEffect, useState, type FormEvent } from 'react';
import { describeError, json } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import type { FinancialAccount, FinancialPostingRule } from '../types';

const ACCOUNT_TYPE: Record<number, string> = { 0: 'Asset', 1: 'Liability', 2: 'Equity', 3: 'Revenue', 4: 'Expense' };
const NORMAL_BALANCE: Record<number, string> = { 0: 'Debit', 1: 'Credit' };
const ACCOUNT_STATUS: Record<number, string> = { 0: 'Active', 1: 'Inactive' };
const CASH_KIND: Record<number, string> = { 0: 'None', 1: 'Cash on hand', 2: 'Bank' };
const DIRECTION: Record<number, string> = { 0: 'Debit', 1: 'Credit' };
const STRATEGY: Record<number, string> = { 0: 'Direct', 1: 'Reversal' };

const EMPTY_ACCOUNT_FORM = {
  code: '',
  name: '',
  accountType: '0',
  parentAccountId: '',
  description: '',
  isPostingAllowed: true,
  cashKind: '0',
  currencyCode: '',
};

const EMPTY_MAPPING_FORM = { sequence: '1', direction: '0', financialAccountId: '', selector: '' };

export function ChartOfAccountsPage() {
  const { can } = useAuth();
  const [accounts, setAccounts] = useState<FinancialAccount[]>([]);
  const [baseCurrency, setBaseCurrency] = useState('');
  const [baseCurrencyCode, setBaseCurrencyCode] = useState('');
  const [rules, setRules] = useState<FinancialPostingRule[]>([]);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [creating, setCreating] = useState(false);
  const [form, setForm] = useState(EMPTY_ACCOUNT_FORM);
  const [editing, setEditing] = useState<FinancialAccount | null>(null);
  const [editForm, setEditForm] = useState({ name: '', parentAccountId: '', description: '', isPostingAllowed: true, cashKind: '0' });
  const [mappingRule, setMappingRule] = useState<string | null>(null);
  const [mappingForm, setMappingForm] = useState(EMPTY_MAPPING_FORM);

  const canView = can('Finance.Account.View');
  const canManage = can('Finance.Account.Manage');

  const load = async () => {
    setError('');
    try {
      const [search, currency, postingRules] = await Promise.all([
        json.get<{ items: FinancialAccount[] }>('/api/v1/finance/accounts?pageSize=200'),
        json.get<string>('/api/v1/finance/accounts/base-currency'),
        json.get<FinancialPostingRule[]>('/api/v1/finance/accounts/posting-rules'),
      ]);
      setAccounts(search.items);
      setBaseCurrency(currency);
      setBaseCurrencyCode(currency);
      setRules(postingRules);
    } catch (err) {
      setError(describeError(err));
    }
  };

  useEffect(() => {
    if (canView) {
      void load();
    }
  }, [canView]);

  const create = async (event: FormEvent) => {
    event.preventDefault();
    setBusy(true);
    try {
      await json.post<FinancialAccount>('/api/v1/finance/accounts', {
        code: form.code,
        name: form.name,
        accountType: Number(form.accountType),
        parentAccountId: form.parentAccountId || null,
        description: form.description || null,
        isPostingAllowed: form.isPostingAllowed,
        cashKind: Number(form.cashKind),
        currencyCode: form.currencyCode.toUpperCase(),
      });
      setCreating(false);
      setForm({ ...EMPTY_ACCOUNT_FORM, currencyCode: baseCurrency });
      await load();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const startEdit = (account: FinancialAccount) => {
    setEditing(account);
    setEditForm({
      name: account.name,
      parentAccountId: account.parentAccountId ?? '',
      description: account.description ?? '',
      isPostingAllowed: account.isPostingAllowed,
      cashKind: String(account.cashKind),
    });
  };

  const saveEdit = async (event: FormEvent) => {
    event.preventDefault();
    if (!editing) {
      return;
    }
    setBusy(true);
    try {
      await json.put<FinancialAccount>(`/api/v1/finance/accounts/${editing.id}`, {
        name: editForm.name,
        parentAccountId: editForm.parentAccountId || null,
        description: editForm.description || null,
        isPostingAllowed: editForm.isPostingAllowed,
        cashKind: Number(editForm.cashKind),
      });
      setEditing(null);
      await load();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const deactivate = async (account: FinancialAccount) => {
    setBusy(true);
    try {
      await json.post<FinancialAccount>(`/api/v1/finance/accounts/${account.id}/deactivate`);
      await load();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const saveBaseCurrency = async (event: FormEvent) => {
    event.preventDefault();
    setBusy(true);
    try {
      await json.put('/api/v1/finance/accounts/base-currency', { currencyCode: baseCurrencyCode.toUpperCase() });
      await load();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const saveMapping = async (event: FormEvent) => {
    event.preventDefault();
    if (!mappingRule) {
      return;
    }
    setBusy(true);
    try {
      await json.put(`/api/v1/finance/accounts/posting-rules/${mappingRule}/mappings`, {
        sequence: Number(mappingForm.sequence),
        direction: Number(mappingForm.direction),
        financialAccountId: mappingForm.financialAccountId,
        selector: mappingForm.selector || null,
      });
      setMappingRule(null);
      setMappingForm(EMPTY_MAPPING_FORM);
      await load();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const removeMapping = async (ruleCode: string, mappingId: string) => {
    setBusy(true);
    try {
      await json.delete(`/api/v1/finance/accounts/posting-rules/${ruleCode}/mappings/${mappingId}`);
      await load();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  if (!canView) {
    return <div className="empty-state">You do not have permission to view the chart of accounts.</div>;
  }

  return (
    <>
      <h1 className="page-title">Chart of accounts</h1>
      <p className="page-subtitle">Account master data, the club base currency and the automated posting rules.</p>

      {error ? <div className="alert alert-error">{error}</div> : null}

      <div className="toolbar">
        {canManage ? (
          <button className="btn btn-primary" onClick={() => setCreating((value) => !value)}>
            {creating ? 'Close' : 'New account'}
          </button>
        ) : null}
        <button className="btn" onClick={() => void load()}>
          Refresh
        </button>
        <span className="muted">Base currency: {baseCurrency || '—'}</span>
      </div>

      {creating ? (
        <div className="card">
          <h3>New account</h3>
          <form className="form-grid" onSubmit={(event) => void create(event)}>
            <div className="field">
              <label htmlFor="accountCode">Code *</label>
              <input
                id="accountCode"
                required
                maxLength={64}
                value={form.code}
                onChange={(event) => setForm({ ...form, code: event.target.value })}
              />
            </div>
            <div className="field">
              <label htmlFor="accountName">Name *</label>
              <input
                id="accountName"
                required
                maxLength={256}
                value={form.name}
                onChange={(event) => setForm({ ...form, name: event.target.value })}
              />
            </div>
            <div className="field">
              <label htmlFor="accountType">Account type *</label>
              <select
                id="accountType"
                value={form.accountType}
                onChange={(event) => setForm({ ...form, accountType: event.target.value })}
              >
                {Object.entries(ACCOUNT_TYPE).map(([value, label]) => (
                  <option key={value} value={value}>
                    {label}
                  </option>
                ))}
              </select>
            </div>
            <div className="field">
              <label htmlFor="parentAccount">Parent account</label>
              <select
                id="parentAccount"
                value={form.parentAccountId}
                onChange={(event) => setForm({ ...form, parentAccountId: event.target.value })}
              >
                <option value="">No parent</option>
                {accounts.map((account) => (
                  <option key={account.id} value={account.id}>
                    {account.code} — {account.name}
                  </option>
                ))}
              </select>
            </div>
            <div className="field">
              <label htmlFor="accountDescription">Description</label>
              <input
                id="accountDescription"
                maxLength={1024}
                value={form.description}
                onChange={(event) => setForm({ ...form, description: event.target.value })}
              />
            </div>
            <div className="field">
              <label htmlFor="accountCashKind">Cash kind</label>
              <select
                id="accountCashKind"
                value={form.cashKind}
                onChange={(event) => setForm({ ...form, cashKind: event.target.value })}
              >
                {Object.entries(CASH_KIND).map(([value, label]) => (
                  <option key={value} value={value}>
                    {label}
                  </option>
                ))}
              </select>
            </div>
            <div className="field">
              <label htmlFor="accountCurrency">Currency *</label>
              <input
                id="accountCurrency"
                required
                maxLength={8}
                value={form.currencyCode}
                onChange={(event) => setForm({ ...form, currencyCode: event.target.value })}
              />
            </div>
            <div className="field">
              <label htmlFor="postingAllowed">Posting allowed</label>
              <input
                id="postingAllowed"
                type="checkbox"
                checked={form.isPostingAllowed}
                onChange={(event) => setForm({ ...form, isPostingAllowed: event.target.checked })}
              />
            </div>
            <button type="submit" className="btn btn-primary" disabled={busy}>
              Create account
            </button>
          </form>
        </div>
      ) : null}

      {accounts.length === 0 ? (
        <div className="empty-state">No accounts configured yet.</div>
      ) : (
        <table>
          <thead>
            <tr>
              <th>Code</th>
              <th>Name</th>
              <th>Type</th>
              <th>Normal balance</th>
              <th>Cash kind</th>
              <th>Currency</th>
              <th>Status</th>
              <th>Posting allowed</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {accounts.map((account) => (
              <tr key={account.id}>
                <td>{account.code}</td>
                <td>{account.name}</td>
                <td>{ACCOUNT_TYPE[account.accountType]}</td>
                <td>{NORMAL_BALANCE[account.normalBalance]}</td>
                <td>{CASH_KIND[account.cashKind]}</td>
                <td>{account.currencyCode}</td>
                <td>
                  <span className={`status-pill ${account.status === 0 ? 'good' : 'bad'}`}>
                    {ACCOUNT_STATUS[account.status]}
                  </span>
                </td>
                <td>{account.isPostingAllowed ? 'Yes' : 'No'}</td>
                <td>
                  <div className="row-actions">
                    {canManage ? (
                      <>
                        <button className="btn" onClick={() => startEdit(account)}>
                          Edit
                        </button>
                        {account.status === 0 ? (
                          <button className="btn btn-danger" disabled={busy} onClick={() => void deactivate(account)}>
                            Deactivate
                          </button>
                        ) : null}
                      </>
                    ) : null}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {editing ? (
        <div className="card" style={{ marginTop: 18 }}>
          <h3>Edit account {editing.code}</h3>
          <form className="form-grid" onSubmit={(event) => void saveEdit(event)}>
            <div className="field">
              <label htmlFor="editName">Name *</label>
              <input
                id="editName"
                required
                maxLength={256}
                value={editForm.name}
                onChange={(event) => setEditForm({ ...editForm, name: event.target.value })}
              />
            </div>
            <div className="field">
              <label htmlFor="editParent">Parent account</label>
              <select
                id="editParent"
                value={editForm.parentAccountId}
                onChange={(event) => setEditForm({ ...editForm, parentAccountId: event.target.value })}
              >
                <option value="">No parent</option>
                {accounts
                  .filter((account) => account.id !== editing.id)
                  .map((account) => (
                    <option key={account.id} value={account.id}>
                      {account.code} — {account.name}
                    </option>
                  ))}
              </select>
            </div>
            <div className="field">
              <label htmlFor="editDescription">Description</label>
              <input
                id="editDescription"
                maxLength={1024}
                value={editForm.description}
                onChange={(event) => setEditForm({ ...editForm, description: event.target.value })}
              />
            </div>
            <div className="field">
              <label htmlFor="editCashKind">Cash kind</label>
              <select
                id="editCashKind"
                value={editForm.cashKind}
                onChange={(event) => setEditForm({ ...editForm, cashKind: event.target.value })}
              >
                {Object.entries(CASH_KIND).map(([value, label]) => (
                  <option key={value} value={value}>
                    {label}
                  </option>
                ))}
              </select>
            </div>
            <div className="field">
              <label htmlFor="editPostingAllowed">Posting allowed</label>
              <input
                id="editPostingAllowed"
                type="checkbox"
                checked={editForm.isPostingAllowed}
                onChange={(event) => setEditForm({ ...editForm, isPostingAllowed: event.target.checked })}
              />
            </div>
            <div className="input-row">
              <button type="submit" className="btn btn-primary" disabled={busy}>
                Save account
              </button>
              <button type="button" className="btn" onClick={() => setEditing(null)}>
                Cancel
              </button>
            </div>
          </form>
        </div>
      ) : null}

      {canManage ? (
        <div className="card" style={{ marginTop: 18 }}>
          <h3>Base currency</h3>
          <form className="form-grid" onSubmit={(event) => void saveBaseCurrency(event)}>
            <div className="field">
              <label htmlFor="baseCurrency">Currency code *</label>
              <input
                id="baseCurrency"
                required
                maxLength={8}
                value={baseCurrencyCode}
                onChange={(event) => setBaseCurrencyCode(event.target.value)}
              />
            </div>
            <button type="submit" className="btn btn-primary" disabled={busy}>
              Save base currency
            </button>
          </form>
        </div>
      ) : null}

      <div className="card" style={{ marginTop: 18 }}>
        <h3>Posting rules</h3>
        {rules.length === 0 ? (
          <div className="empty-state">No posting rules are configured.</div>
        ) : (
          rules.map((rule) => (
            <div key={rule.id} style={{ marginBottom: 20 }}>
              <div className="input-row">
                <strong>{rule.code}</strong>
                <span>{rule.name}</span>
                <span className="muted">{rule.sourceSection}</span>
                <span className="status-pill">{STRATEGY[rule.strategy]}</span>
              </div>
              {rule.description ? <p className="muted">{rule.description}</p> : null}
              {rule.mappings.length === 0 ? (
                <div className="empty-state">No mappings for this rule.</div>
              ) : (
                <table>
                  <thead>
                    <tr>
                      <th>Sequence</th>
                      <th>Direction</th>
                      <th>Account</th>
                      <th>Selector</th>
                      <th>Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    {rule.mappings.map((mapping) => (
                      <tr key={mapping.id}>
                        <td>{mapping.sequence}</td>
                        <td>{DIRECTION[mapping.direction]}</td>
                        <td>
                          {mapping.financialAccountCode} — {mapping.financialAccountName}
                        </td>
                        <td>{mapping.selector || '—'}</td>
                        <td>
                          <div className="row-actions">
                            {canManage ? (
                              <>
                                <button
                                  className="link-button"
                                  onClick={() => {
                                    setMappingRule(rule.code);
                                    setMappingForm({
                                      sequence: String(mapping.sequence),
                                      direction: String(mapping.direction),
                                      financialAccountId: mapping.financialAccountId,
                                      selector: mapping.selector ?? '',
                                    });
                                  }}
                                >
                                  Edit
                                </button>
                                <button
                                  className="link-button"
                                  disabled={busy}
                                  onClick={() => void removeMapping(rule.code, mapping.id)}
                                >
                                  Remove
                                </button>
                              </>
                            ) : null}
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
              {canManage ? (
                mappingRule === rule.code ? (
                  <form className="form-grid" style={{ marginTop: 12 }} onSubmit={(event) => void saveMapping(event)}>
                    <div className="field">
                      <label htmlFor={`sequence-${rule.code}`}>Sequence *</label>
                      <input
                        id={`sequence-${rule.code}`}
                        type="number"
                        min={1}
                        step={1}
                        required
                        value={mappingForm.sequence}
                        onChange={(event) => setMappingForm({ ...mappingForm, sequence: event.target.value })}
                      />
                    </div>
                    <div className="field">
                      <label htmlFor={`direction-${rule.code}`}>Direction *</label>
                      <select
                        id={`direction-${rule.code}`}
                        value={mappingForm.direction}
                        onChange={(event) => setMappingForm({ ...mappingForm, direction: event.target.value })}
                      >
                        {Object.entries(DIRECTION).map(([value, label]) => (
                          <option key={value} value={value}>
                            {label}
                          </option>
                        ))}
                      </select>
                    </div>
                    <div className="field">
                      <label htmlFor={`account-${rule.code}`}>Account *</label>
                      <select
                        id={`account-${rule.code}`}
                        required
                        value={mappingForm.financialAccountId}
                        onChange={(event) => setMappingForm({ ...mappingForm, financialAccountId: event.target.value })}
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
                      <label htmlFor={`selector-${rule.code}`}>Selector</label>
                      <input
                        id={`selector-${rule.code}`}
                        maxLength={64}
                        value={mappingForm.selector}
                        onChange={(event) => setMappingForm({ ...mappingForm, selector: event.target.value })}
                      />
                    </div>
                    <div className="input-row">
                      <button type="submit" className="btn btn-primary" disabled={busy}>
                        Save mapping
                      </button>
                      <button type="button" className="btn" onClick={() => setMappingRule(null)}>
                        Cancel
                      </button>
                    </div>
                  </form>
                ) : (
                  <button
                    className="btn"
                    style={{ marginTop: 12 }}
                    onClick={() => {
                      setMappingRule(rule.code);
                      setMappingForm(EMPTY_MAPPING_FORM);
                    }}
                  >
                    Add mapping
                  </button>
                )
              ) : null}
            </div>
          ))
        )}
      </div>
    </>
  );
}
