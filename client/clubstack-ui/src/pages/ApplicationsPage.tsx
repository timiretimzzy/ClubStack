import { useEffect, useState, type FormEvent } from 'react';
import { describeError, json } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import type { MembershipApplication, MembershipApplicationSearch, MembershipCategory, Person } from '../types';
import { Status } from './DashboardPage';

const APPLICATION_STATUSES = ['', 'Draft', 'Submitted', 'UnderReview', 'Approved', 'Activated', 'Rejected'];

export function ApplicationsPage() {
  const { can } = useAuth();
  const [status, setStatus] = useState('');
  const [result, setResult] = useState<MembershipApplicationSearch | null>(null);
  const [categories, setCategories] = useState<MembershipCategory[]>([]);
  const [persons, setPersons] = useState<Person[]>([]);
  const [personQuery, setPersonQuery] = useState('');
  const [creating, setCreating] = useState(false);
  const [form, setForm] = useState({ personId: '', categoryId: '', sponsorName: '', notes: '', requiredDocuments: '' });
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  const canCreate = can('Membership.Application.Create');
  const canApprove = can('Membership.Application.Approve');

  const load = async (searchStatus = status) => {
    setError('');
    try {
      const parameters = new URLSearchParams();
      if (searchStatus) parameters.set('status', searchStatus);
      setResult(await json.get<MembershipApplicationSearch>(`/api/v1/membership-applications?${parameters.toString()}`));
    } catch (err) {
      setError(describeError(err));
    }
  };

  useEffect(() => {
    void load();
    if (canCreate) {
      void json
        .get<MembershipCategory[]>('/api/v1/membership-categories')
        .then(setCategories)
        .catch(() => undefined);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const openCreate = async () => {
    setCreating(true);
    await searchPersons('');
  };

  const searchPersons = async (input: string) => {
    try {
      const result = await json.get<{ items: Person[] }>(`/api/v1/persons?query=${encodeURIComponent(input)}`);
      setPersons(result.items);
    } catch (err) {
      setError(describeError(err));
    }
  };

  const createApplication = async (event: FormEvent) => {
    event.preventDefault();
    setError('');
    setBusy(true);
    try {
      await json.post<MembershipApplication>('/api/v1/membership-applications', {
        personId: form.personId,
        categoryId: form.categoryId,
        sponsorName: form.sponsorName || null,
        notes: form.notes,
        requiredDocuments: form.requiredDocuments,
      });
      setCreating(false);
      setForm({ personId: '', categoryId: '', sponsorName: '', notes: '', requiredDocuments: '' });
      await load();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const transition = async (id: string, action: string, body?: unknown) => {
    setBusy(true);
    try {
      await json.post<MembershipApplication>(`/api/v1/membership-applications/${id}/${action}`, body ?? {});
      await load();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <h1 className="page-title">Membership applications</h1>
      <p className="page-subtitle">Track applications through submission, review and approval.</p>

      {error ? <div className="alert alert-error">{error}</div> : null}

      <div className="toolbar">
        <select aria-label="Status" value={status} onChange={(event) => setStatus(event.target.value)}>
          {APPLICATION_STATUSES.map((value) => (
            <option key={value || 'all'} value={value}>
              {value || 'All statuses'}
            </option>
          ))}
        </select>
        <button className="btn" onClick={() => void load()}>
          Filter
        </button>
        {canCreate ? (
          <button className="btn btn-primary" onClick={() => void openCreate()}>
            New application
          </button>
        ) : null}
      </div>

      {creating ? (
        <div className="card">
          <h3>New application</h3>
          <form className="form-grid" onSubmit={(event) => void createApplication(event)}>
            <div className="field">
              <label htmlFor="persons">Person</label>
              <div className="input-row">
                <input
                  placeholder="Search people…"
                  value={personQuery}
                  onChange={(event) => {
                    setPersonQuery(event.target.value);
                    void searchPersons(event.target.value);
                  }}
                />
                <select
                  id="persons"
                  required
                  value={form.personId}
                  onChange={(event) => setForm({ ...form, personId: event.target.value })}
                >
                  <option value="">Select a person</option>
                  {persons.map((person) => (
                    <option key={person.id} value={person.id}>
                      {person.legalName} {person.memberNumber ? `(${person.memberNumber})` : ''}
                    </option>
                  ))}
                </select>
              </div>
            </div>
            <div className="field">
              <label htmlFor="category">Category</label>
              <select
                id="category"
                required
                value={form.categoryId}
                onChange={(event) => setForm({ ...form, categoryId: event.target.value })}
              >
                <option value="">Select a category</option>
                {categories.map((category) => (
                  <option key={category.id} value={category.id}>
                    {category.name}
                  </option>
                ))}
              </select>
            </div>
            <div className="field">
              <label htmlFor="sponsor">Sponsor</label>
              <input
                id="sponsor"
                value={form.sponsorName}
                onChange={(event) => setForm({ ...form, sponsorName: event.target.value })}
              />
            </div>
            <div className="field">
              <label htmlFor="notes">Notes</label>
              <textarea
                id="notes"
                value={form.notes}
                onChange={(event) => setForm({ ...form, notes: event.target.value })}
              />
            </div>
            <div className="field">
              <label htmlFor="documents">Required documents</label>
              <input
                id="documents"
                value={form.requiredDocuments}
                onChange={(event) => setForm({ ...form, requiredDocuments: event.target.value })}
              />
            </div>
            <button type="submit" className="btn btn-primary" disabled={busy}>
              Create
            </button>
          </form>
        </div>
      ) : null}

      {result === null ? (
        <div className="empty-state">Loading applications…</div>
      ) : result.items.length === 0 ? (
        <div className="empty-state">No applications match the current filter.</div>
      ) : (
        <table>
          <thead>
            <tr>
              <th>Applicant</th>
              <th>Category</th>
              <th>Status</th>
              <th>Member</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {result.items.map((application) => (
              <tr key={application.id}>
                <td>{application.personName}</td>
                <td>{application.categoryName}</td>
                <td>
                  <Status value={application.status} />
                </td>
                <td>{application.memberNumber ?? '—'}</td>
                <td>
                  <div className="row-actions">
                    {application.status === 'Draft' && canCreate ? (
                      <button className="btn btn-primary" disabled={busy} onClick={() => void transition(application.id, 'submit')}>
                        Submit
                      </button>
                    ) : null}
                    {application.status === 'Submitted' && canApprove ? (
                      <button className="btn" disabled={busy} onClick={() => void transition(application.id, 'start-review')}>
                        Start review
                      </button>
                    ) : null}
                    {application.status === 'UnderReview' && canApprove ? (
                      <>
                        <button className="btn btn-primary" disabled={busy} onClick={() => void transition(application.id, 'approve')}>
                          Approve
                        </button>
                        <button className="btn btn-danger" disabled={busy} onClick={() => void reject(application.id)}>
                          Reject
                        </button>
                      </>
                    ) : null}
                    {application.status === 'Approved' && application.memberId ? (
                      <a className="link-button" href={`#/members/${application.memberId}`}>
                        Member 360
                      </a>
                    ) : null}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );

  async function reject(id: string) {
    const reason = window.prompt('Rejection reason');
    if (reason === null) {
      return;
    }
    setBusy(true);
    try {
      await json.post(`/api/v1/membership-applications/${id}/reject`, { reason });
      await load();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  }
}