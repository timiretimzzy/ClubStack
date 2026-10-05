import { useEffect, useState, type FormEvent } from 'react';
import { describeError, json } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import type { MembershipCategory, MembershipFee } from '../types';

export function CategoriesPage() {
  const { can } = useAuth();
  const [categories, setCategories] = useState<MembershipCategory[]>([]);
  const [error, setError] = useState('');
  const [creating, setCreating] = useState(false);
  const [form, setForm] = useState({ code: '', name: '', description: '', renewalPeriodMonths: 12 });
  const [expandedCategoryId, setExpandedCategoryId] = useState<string | null>(null);
  const [fees, setFees] = useState<MembershipFee[]>([]);
  const [feeForm, setFeeForm] = useState({ feeType: '', description: '', amount: '', currencyCode: 'ZAR', isProrationEnabled: false });
  const [busy, setBusy] = useState(false);

  const canManageCategory = can('Membership.Category.Manage');
  const canManageFees = can('Membership.Fee.Manage');

  const load = async () => {
    setError('');
    try {
      setCategories(await json.get<MembershipCategory[]>('/api/v1/membership-categories'));
    } catch (err) {
      setError(describeError(err));
    }
  };

  useEffect(() => {
    void load();
  }, []);

  const createCategory = async (event: FormEvent) => {
    event.preventDefault();
    setBusy(true);
    try {
      await json.post<MembershipCategory>('/api/v1/membership-categories', {
        ...form,
        renewalPeriodMonths: Number(form.renewalPeriodMonths),
      });
      setCreating(false);
      setForm({ code: '', name: '', description: '', renewalPeriodMonths: 12 });
      await load();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const toggleFees = async (categoryId: string) => {
    if (expandedCategoryId === categoryId) {
      setExpandedCategoryId(null);
      return;
    }
    try {
      setFees(await json.get<MembershipFee[]>(`/api/v1/membership-categories/${categoryId}/fees`));
      setExpandedCategoryId(categoryId);
    } catch (err) {
      setError(describeError(err));
    }
  };

  const createFee = async (event: FormEvent, categoryId: string) => {
    event.preventDefault();
    setBusy(true);
    try {
      await json.post<MembershipFee>(`/api/v1/membership-categories/${categoryId}/fees`, {
        feeType: feeForm.feeType,
        description: feeForm.description,
        amount: Number(feeForm.amount),
        currencyCode: feeForm.currencyCode,
        isProrationEnabled: feeForm.isProrationEnabled,
        isActive: true,
      });
      setFeeForm({ feeType: '', description: '', amount: '', currencyCode: 'ZAR', isProrationEnabled: false });
      setFees(await json.get<MembershipFee[]>(`/api/v1/membership-categories/${categoryId}/fees`));
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <h1 className="page-title">Membership categories</h1>
      <p className="page-subtitle">Configure categories and their fee schedules.</p>

      {error ? <div className="alert alert-error">{error}</div> : null}

      <div className="toolbar">
        {canManageCategory ? (
          <button className="btn btn-primary" onClick={() => setCreating((value) => !value)}>
            {creating ? 'Close' : 'New category'}
          </button>
        ) : null}
      </div>

      {creating ? (
        <div className="card">
          <h3>New category</h3>
          <form className="form-grid" onSubmit={(event) => void createCategory(event)}>
            <div className="field">
              <label htmlFor="code">Code *</label>
              <input
                id="code"
                required
                maxLength={16}
                value={form.code}
                onChange={(event) => setForm({ ...form, code: event.target.value })}
              />
            </div>
            <div className="field">
              <label htmlFor="name">Name *</label>
              <input
                id="name"
                required
                value={form.name}
                onChange={(event) => setForm({ ...form, name: event.target.value })}
              />
            </div>
            <div className="field">
              <label htmlFor="months">Renewal period (months)</label>
              <input
                id="months"
                type="number"
                min={1}
                max={120}
                value={form.renewalPeriodMonths}
                onChange={(event) => setForm({ ...form, renewalPeriodMonths: Number(event.target.value) })}
              />
            </div>
            <div className="field">
              <label htmlFor="description">Description</label>
              <textarea
                id="description"
                value={form.description}
                onChange={(event) => setForm({ ...form, description: event.target.value })}
              />
            </div>
            <button type="submit" className="btn btn-primary" disabled={busy}>
              Create category
            </button>
          </form>
        </div>
      ) : null}

      {categories.length === 0 ? (
        <div className="empty-state">No categories configured yet.</div>
      ) : (
        categories.map((category) => (
          <div className="card" key={category.id}>
            <div className="input-row">
              <strong>{category.name}</strong>
              <span className="muted">{category.code}</span>
              <span className="muted">
                {category.renewalPeriodMonths} month period
              </span>
              {!category.isActive ? <span className="status-pill bad">Inactive</span> : null}
              <button className="link-button" onClick={() => void toggleFees(category.id)}>
                {expandedCategoryId === category.id ? 'Hide fees' : 'Fees'}
              </button>
            </div>
            {category.description ? <p className="muted">{category.description}</p> : null}

            {expandedCategoryId === category.id ? (
              <div style={{ marginTop: 14 }}>
                {fees.length > 0 ? (
                  <table>
                    <thead>
                      <tr>
                        <th>Type</th>
                        <th>Description</th>
                        <th>Amount</th>
                        <th>Proration</th>
                      </tr>
                    </thead>
                    <tbody>
                      {fees.map((fee) => (
                        <tr key={fee.id}>
                          <td>{fee.feeType}</td>
                          <td>{fee.description || '—'}</td>
                          <td>
                            {fee.amount.toLocaleString(undefined, { minimumFractionDigits: 2 })} {fee.currencyCode}
                          </td>
                          <td>{fee.isProrationEnabled ? 'Pro-rated' : 'Full'}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                ) : (
                  <div className="empty-state">No fees configured for this category.</div>
                )}
                {canManageFees ? (
                  <form className="form-grid" style={{ marginTop: 12 }} onSubmit={(event) => void createFee(event, category.id)}>
                    <div className="field">
                      <label>Fee type *</label>
                      <input
                        required
                        maxLength={24}
                        placeholder="e.g. AnnualFee"
                        value={feeForm.feeType}
                        onChange={(event) => setFeeForm({ ...feeForm, feeType: event.target.value })}
                      />
                    </div>
                    <div className="field">
                      <label>Description</label>
                      <input
                        value={feeForm.description}
                        onChange={(event) => setFeeForm({ ...feeForm, description: event.target.value })}
                      />
                    </div>
                    <div className="field">
                      <label>Amount</label>
                      <input
                        type="number"
                        min={0.01}
                        step={0.01}
                        required
                        value={feeForm.amount}
                        onChange={(event) => setFeeForm({ ...feeForm, amount: event.target.value })}
                      />
                    </div>
                    <div className="field">
                      <label>Currency</label>
                      <input
                        maxLength={8}
                        value={feeForm.currencyCode}
                        onChange={(event) => setFeeForm({ ...feeForm, currencyCode: event.target.value })}
                      />
                    </div>
                    <label className="field" style={{ flexDirection: 'row', gap: 6, alignItems: 'center' }}>
                      <input
                        type="checkbox"
                        checked={feeForm.isProrationEnabled}
                        onChange={(event) => setFeeForm({ ...feeForm, isProrationEnabled: event.target.checked })}
                      />
                      Pro-rate mid-period assessments
                    </label>
                    <button type="submit" className="btn btn-primary" disabled={busy}>
                      Add fee
                    </button>
                  </form>
                ) : null}
              </div>
            ) : null}
          </div>
        ))
      )}
    </>
  );
}