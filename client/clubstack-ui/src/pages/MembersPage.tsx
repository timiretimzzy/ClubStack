import { useEffect, useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { describeError, json } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import type { MemberSearch, PersonCreated } from '../types';
import { Status } from './DashboardPage';

const MEMBER_STATUSES = ['', 'Prospect', 'Active', 'Suspended', 'Cancelled'];

export function MembersPage() {
  const { can } = useAuth();
  const [query, setQuery] = useState('');
  const [status, setStatus] = useState('');
  const [result, setResult] = useState<MemberSearch | null>(null);
  const [error, setError] = useState('');
  const [creating, setCreating] = useState(false);
  const [form, setForm] = useState({ legalName: '', email: '', phone: '', idNumber: '' });
  const [warnings, setWarnings] = useState<string[]>([]);

  const canCreate = can('Membership.Member.Create');

  const load = async (searchQuery = query, searchStatus = status) => {
    setError('');
    try {
      const parameters = new URLSearchParams();
      if (searchQuery) parameters.set('query', searchQuery);
      if (searchStatus) parameters.set('status', searchStatus);
      setResult(await json.get<MemberSearch>(`/api/v1/members?${parameters.toString()}`));
    } catch (err) {
      setError(describeError(err));
    }
  };

  useEffect(() => {
    void load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const createPerson = async (event: FormEvent) => {
    event.preventDefault();
    setError('');
    setWarnings([]);
    try {
      const created = await json.post<PersonCreated>('/api/v1/persons', form);
      setWarnings(created.duplicateWarnings);
      setForm({ legalName: '', email: '', phone: '', idNumber: '' });
      setCreating(false);
      await load();
    } catch (err) {
      setError(describeError(err));
    }
  };

  return (
    <>
      <h1 className="page-title">Member directory</h1>
      <p className="page-subtitle">Find a member by name, number or contact detail.</p>

      {error ? <div className="alert alert-error">{error}</div> : null}

      <div className="toolbar">
        <input
          aria-label="Search members"
          placeholder="Search members…"
          value={query}
          onChange={(event) => setQuery(event.target.value)}
        />
        <select aria-label="Status" value={status} onChange={(event) => setStatus(event.target.value)}>
          {MEMBER_STATUSES.map((value) => (
            <option key={value || 'all'} value={value}>
              {value || 'All statuses'}
            </option>
          ))}
        </select>
        <button className="btn" onClick={() => void load()}>
          Search
        </button>
        {canCreate ? (
          <button className="btn btn-primary" onClick={() => setCreating((value) => !value)}>
            {creating ? 'Close' : 'Add a person'}
          </button>
        ) : null}
      </div>

      {creating ? (
        <div className="card">
          <h3>Add a person</h3>
          <form className="form-grid" onSubmit={(event) => void createPerson(event)}>
            <div className="field">
              <label htmlFor="legalName">Legal name *</label>
              <input
                id="legalName"
                required
                value={form.legalName}
                onChange={(event) => setForm({ ...form, legalName: event.target.value })}
              />
            </div>
            <div className="field">
              <label htmlFor="email">Email</label>
              <input
                id="email"
                value={form.email}
                onChange={(event) => setForm({ ...form, email: event.target.value })}
              />
            </div>
            <div className="field">
              <label htmlFor="phone">Phone</label>
              <input
                id="phone"
                value={form.phone}
                onChange={(event) => setForm({ ...form, phone: event.target.value })}
              />
            </div>
            <div className="field">
              <label htmlFor="idNumber">ID number</label>
              <input
                id="idNumber"
                value={form.idNumber}
                onChange={(event) => setForm({ ...form, idNumber: event.target.value })}
              />
            </div>
            <button type="submit" className="btn btn-primary">
              Save person
            </button>
          </form>
          {warnings.length > 0 ? (
            <div className="alert alert-info" style={{ marginTop: 12 }}>
              Possible duplicates: {warnings.join('; ')}
            </div>
          ) : null}
        </div>
      ) : null}

      {result === null ? (
        <div className="empty-state">Loading members…</div>
      ) : result.items.length === 0 ? (
        <div className="empty-state">No members match the current filters.</div>
      ) : (
        <table>
          <thead>
            <tr>
              <th>Member</th>
              <th>Name</th>
              <th>Status</th>
              <th>Contact</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {result.items.map((member) => (
              <tr key={member.id}>
                <td>{member.memberNumber}</td>
                <td>{member.legalName}</td>
                <td>
                  <Status value={member.status} />
                </td>
                <td className="muted">
                  {member.email ?? ''}
                  {member.phone ? ` · ${member.phone}` : ''}
                </td>
                <td>
                  <Link to={`/members/${member.id}`}>Member 360</Link>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
}