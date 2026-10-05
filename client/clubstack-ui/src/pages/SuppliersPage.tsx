import { useEffect, useState, type FormEvent } from 'react';
import { describeError, json } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import type { Supplier, SupplierSearch } from '../types';

export function SuppliersPage() {
  const { can } = useAuth();
  const [suppliers, setSuppliers] = useState<Supplier[]>([]);
  const [error, setError] = useState('');
  const [creating, setCreating] = useState(false);
  const [busy, setBusy] = useState(false);
  const [form, setForm] = useState({
    supplierCode: '',
    name: '',
    contactPerson: '',
    email: '',
    phone: '',
    paymentTerms: '',
    taxId: '',
  });

  const canView = can('Procurement.Supplier.View');
  const canManage = can('Procurement.Supplier.Manage');

  const load = async () => {
    setError('');
    try {
      const search = await json.get<SupplierSearch>('/api/v1/suppliers?pageSize=200');
      setSuppliers(search.items);
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
      await json.post<Supplier>('/api/v1/suppliers', {
        ...form,
        contactPerson: form.contactPerson || null,
        email: form.email || null,
        phone: form.phone || null,
        paymentTerms: form.paymentTerms || null,
        taxId: form.taxId || null,
      });
      setCreating(false);
      setForm({ supplierCode: '', name: '', contactPerson: '', email: '', phone: '', paymentTerms: '', taxId: '' });
      await load();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  if (!canView) {
    return <div className="empty-state">You do not have permission to view suppliers.</div>;
  }

  return (
    <>
      <h1 className="page-title">Suppliers</h1>
      <p className="page-subtitle">Supplier master data used by the product catalogue (PROC-001).</p>

      {error ? <div className="alert alert-error">{error}</div> : null}

      <div className="toolbar">
        {canManage ? (
          <button className="btn btn-primary" onClick={() => setCreating((value) => !value)}>
            {creating ? 'Close' : 'New supplier'}
          </button>
        ) : null}
      </div>

      {creating ? (
        <div className="card">
          <h3>New supplier</h3>
          <form className="form-grid" onSubmit={(event) => void create(event)}>
            <div className="field">
              <label htmlFor="code">Supplier code *</label>
              <input id="code" required maxLength={24} value={form.supplierCode} onChange={(event) => setForm({ ...form, supplierCode: event.target.value })} />
            </div>
            <div className="field">
              <label htmlFor="name">Name *</label>
              <input id="name" required maxLength={160} value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} />
            </div>
            <div className="field">
              <label htmlFor="contact">Contact person</label>
              <input id="contact" maxLength={160} value={form.contactPerson} onChange={(event) => setForm({ ...form, contactPerson: event.target.value })} />
            </div>
            <div className="field">
              <label htmlFor="email">Email</label>
              <input id="email" type="email" maxLength={256} value={form.email} onChange={(event) => setForm({ ...form, email: event.target.value })} />
            </div>
            <div className="field">
              <label htmlFor="phone">Phone</label>
              <input id="phone" maxLength={32} value={form.phone} onChange={(event) => setForm({ ...form, phone: event.target.value })} />
            </div>
            <div className="field">
              <label htmlFor="terms">Payment terms</label>
              <input id="terms" maxLength={128} value={form.paymentTerms} onChange={(event) => setForm({ ...form, paymentTerms: event.target.value })} />
            </div>
            <div className="field">
              <label htmlFor="tax">Tax / VAT no.</label>
              <input id="tax" maxLength={64} value={form.taxId} onChange={(event) => setForm({ ...form, taxId: event.target.value })} />
            </div>
            <button type="submit" className="btn btn-primary" disabled={busy}>
              Create supplier
            </button>
          </form>
        </div>
      ) : null}

      {suppliers.length === 0 ? (
        <div className="empty-state">No suppliers yet.</div>
      ) : (
        <table>
          <thead>
            <tr>
              <th>Code</th>
              <th>Name</th>
              <th>Contact</th>
              <th>Email</th>
              <th>Phone</th>
              <th>Terms</th>
              <th>Status</th>
            </tr>
          </thead>
          <tbody>
            {suppliers.map((supplier) => (
              <tr key={supplier.id}>
                <td>{supplier.supplierCode}</td>
                <td>{supplier.name}</td>
                <td>{supplier.contactPerson || '—'}</td>
                <td>{supplier.email || '—'}</td>
                <td>{supplier.phone || '—'}</td>
                <td>{supplier.paymentTerms || '—'}</td>
                <td><span className={`status-pill ${supplier.status === 'Active' ? 'good' : 'bad'}`}>{supplier.status}</span></td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
}