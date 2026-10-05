import { useEffect, useState, type FormEvent } from 'react';
import { describeError, json } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import type { Household, Person } from '../types';

export function HouseholdsPage() {
  const { can } = useAuth();
  const [households, setHouseholds] = useState<Household[] | null>(null);
  const [persons, setPersons] = useState<Person[]>([]);
  const [creating, setCreating] = useState(false);
  const [createForm, setCreateForm] = useState({ name: '', primaryMemberId: '' });
  const [memberForms, setMemberForms] = useState<Record<string, { personId: string; role: string }>>({});
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  const canCreate = can('Membership.Household.Manage');

  const load = async () => {
    setError('');
    try {
      setHouseholds(await json.get<Household[]>('/api/v1/households'));
    } catch (err) {
      setError(describeError(err));
    }
  };

  useEffect(() => {
    void load();
    if (canCreate) {
      void searchPersons('');
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const searchPersons = async (input: string) => {
    try {
      const result = await json.get<{ items: Person[] }>(`/api/v1/persons?query=${encodeURIComponent(input)}`);
      setPersons(result.items);
    } catch (err) {
      setError(describeError(err));
    }
  };

  const createHousehold = async (event: FormEvent) => {
    event.preventDefault();
    setBusy(true);
    try {
      await json.post<Household>('/api/v1/households', {
        name: createForm.name,
        primaryMemberId: createForm.primaryMemberId || null,
      });
      setCreating(false);
      setCreateForm({ name: '', primaryMemberId: '' });
      await load();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const addMember = async (event: FormEvent, householdId: string) => {
    event.preventDefault();
    const form = memberForms[householdId];
    if (!form?.personId) {
      return;
    }
    setBusy(true);
    try {
      const updated = await json.post<Household>(`/api/v1/households/${householdId}/members`, {
        personId: form.personId,
        role: form.role || 'Other',
      });
      setHouseholds((current) => current?.map((h) => (h.id === householdId ? updated : h)) ?? [updated]);
      setMemberForms((current) => ({ ...current, [householdId]: { personId: '', role: '' } }));
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const removeMember = async (householdId: string, personId: string) => {
    setBusy(true);
    try {
      await json.delete<void>(`/api/v1/households/${householdId}/members/${personId}`);
      await load();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <h1 className="page-title">Households</h1>
      <p className="page-subtitle">Group related persons under a household. Relationship roles are explicit, never inferred.</p>

      {error ? <div className="alert alert-error">{error}</div> : null}

      <div className="toolbar">
        {canCreate ? (
          <button className="btn btn-primary" onClick={() => setCreating((value) => !value)}>
            {creating ? 'Close' : 'New household'}
          </button>
        ) : null}
      </div>

      {creating ? (
        <div className="card">
          <h3>New household</h3>
          <form className="form-grid" onSubmit={(event) => void createHousehold(event)}>
            <div className="field">
              <label htmlFor="householdName">Household name *</label>
              <input
                id="householdName"
                required
                maxLength={160}
                value={createForm.name}
                onChange={(event) => setCreateForm({ ...createForm, name: event.target.value })}
              />
            </div>
            <div className="field">
              <label id="primaryMemberLabel">Primary member (optional)</label>
              <div className="input-row">
                <input aria-label="Search people" placeholder="Search people…" onChange={(event) => void searchPersons(event.target.value)} />
                <select
                  aria-labelledby="primaryMemberLabel"
                  value={createForm.primaryMemberId}
                  onChange={(event) => setCreateForm({ ...createForm, primaryMemberId: event.target.value })}
                >
                  <option value="">None</option>
                  {persons.map((person) => (
                    <option key={person.id} value={person.id}>
                      {person.memberNumber ? `${person.memberNumber} · ` : ''}
                      {person.legalName}
                    </option>
                  ))}
                </select>
              </div>
            </div>
            <button type="submit" className="btn btn-primary" disabled={busy}>
              Create household
            </button>
          </form>
        </div>
      ) : null}

      {households === null ? (
        <div className="empty-state">Loading households…</div>
      ) : households.length === 0 ? (
        <div className="empty-state">No households yet.</div>
      ) : (
        households.map((household) => (
          <div className="card" key={household.id}>
            <div className="input-row">
              <strong>{household.name}</strong>
              <span className="muted">{household.reference}</span>
              {household.primaryMemberName ? (
                <span className="muted">
                  Primary: {household.primaryMemberName}
                  {household.primaryMemberNumber ? ` (${household.primaryMemberNumber})` : ''}
                </span>
              ) : null}
              <span className="muted">{household.members.length} members</span>
            </div>

            {household.members.length > 0 ? (
              <table>
                <thead>
                  <tr>
                    <th>Person</th>
                    <th>Role</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {household.members.map((member) => (
                    <tr key={`${member.personId}-${member.role}`}>
                      <td>
                        {member.personName}
                        {member.memberNumber ? ` (${member.memberNumber})` : ''}
                      </td>
                      <td>{member.role}</td>
                      <td>
                        {canCreate ? (
                          <button
                            className="link-button"
                            disabled={busy}
                            onClick={() => void removeMember(household.id, member.personId)}
                          >
                            Remove
                          </button>
                        ) : null}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            ) : (
              <div className="empty-state">No members in this household.</div>
            )}

            {canCreate ? (
              <form className="input-row" style={{ marginTop: 12 }} onSubmit={(event) => void addMember(event, household.id)}>
                <select
                  aria-label="Person"
                  value={memberForms[household.id]?.personId ?? ''}
                  onChange={(event) =>
                    setMemberForms((current) => ({ ...current, [household.id]: { ...current[household.id], personId: event.target.value } }))
                  }
                >
                  <option value="">Select a person</option>
                  {persons.map((person) => (
                    <option key={person.id} value={person.id}>
                      {person.memberNumber ? `${person.memberNumber} · ` : ''}
                      {person.legalName}
                    </option>
                  ))}
                </select>
                <input
                  aria-label="Role"
                  maxLength={24}
                  placeholder="Role e.g. Adult, Child"
                  value={memberForms[household.id]?.role ?? ''}
                  onChange={(event) =>
                    setMemberForms((current) => ({ ...current, [household.id]: { ...current[household.id], role: event.target.value } }))
                  }
                />
                <button type="submit" className="btn btn-primary" disabled={busy || !memberForms[household.id]?.personId}>
                  Add member
                </button>
              </form>
            ) : null}
          </div>
        ))
      )}
    </>
  );
}