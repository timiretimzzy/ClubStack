import { useEffect, useState } from 'react';
import { describeError, json } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import type { GolfGuest, GolfGuestSearch, Member, MemberSearch } from '../types';

const BASE = '/api/v1/golf';

function stamp(value: string | null): string {
  return value ? new Date(value).toLocaleString() : '—';
}

/**
 * GOLF-008/GOLF-009: a golf guest is a visitor record owned by golf, never a
 * D2 member. Golf can therefore host a guest of a member without touching the
 * member directory.
 */
export function GolfGuestsPage() {
  const { can } = useAuth();
  const canView = can('Golf.TeeSheet.View');
  const canManage = can('Golf.Guest.Manage');

  const [rows, setRows] = useState<GolfGuest[]>([]);
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('');
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [busy, setBusy] = useState(false);

  const [fullName, setFullName] = useState('');
  const [contactDetails, setContactDetails] = useState('');
  const [notes, setNotes] = useState('');
  const [hostQuery, setHostQuery] = useState('');
  const [hostId, setHostId] = useState('');
  const [hostResults, setHostResults] = useState<Member[]>([]);

  const load = async () => {
    if (!canView) {
      return;
    }
    setError('');
    try {
      const query = new URLSearchParams({ pageSize: '50' });
      if (search.trim()) {
        query.set('search', search.trim());
      }
      if (status) {
        query.set('status', status);
      }
      const result = await json.get<GolfGuestSearch>(`${BASE}/guests?${query.toString()}`);
      setRows(result.items);
    } catch (err) {
      setError(describeError(err));
    }
  };

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void load();
    }, 250);
    return () => window.clearTimeout(timer);
  }, [canView, search, status]);

  useEffect(() => {
    if (hostQuery.trim().length < 2) {
      setHostResults([]);
      return;
    }
    void (async () => {
      try {
        const query = new URLSearchParams({ query: hostQuery.trim(), pageSize: '10' });
        setHostResults((await json.get<MemberSearch>(`/api/v1/members?${query.toString()}`)).items);
      } catch (err) {
        setError(describeError(err));
      }
    })();
  }, [hostQuery]);

  const createGuest = async () => {
    setBusy(true);
    setError('');
    setNotice('');
    try {
      const created = await json.post<GolfGuest>(`${BASE}/guests`, {
        fullName: fullName.trim(),
        contactDetails: contactDetails.trim() || null,
        hostMemberId: hostId || null,
        notes: notes.trim() || null,
      });
      setNotice(`Guest ${created.reference} created.`);
      setFullName('');
      setContactDetails('');
      setNotes('');
      setHostId('');
      setHostQuery('');
      await load();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const setGuestStatus = async (guest: GolfGuest, next: 'Active' | 'Inactive') => {
    setBusy(true);
    setError('');
    setNotice('');
    try {
      const updated = await json.post<GolfGuest>(`${BASE}/guests/${guest.id}/status`, { status: next });
      setNotice(`Guest ${updated.reference} is now ${next.toLowerCase()}.`);
      await load();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  if (!canView) {
    return <div className="empty-state">You do not have permission to view golf guests.</div>;
  }

  return (
    <>
      <h1 className="page-title">Golf Guests</h1>
      <p className="page-subtitle">Visitors hosted by members. A guest is never created as a member.</p>

      {error ? <div className="alert alert-error">{error}</div> : null}
      {notice ? <div className="alert alert-info">{notice}</div> : null}

      <div className="toolbar">
        <div className="field">
          <label htmlFor="guestFilter">Search</label>
          <input
            id="guestFilter"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder="Name, reference or contact detail"
          />
        </div>
        <div className="field">
          <label htmlFor="guestStatus">Status</label>
          <select id="guestStatus" value={status} onChange={(event) => setStatus(event.target.value)}>
            <option value="">All</option>
            <option value="Active">Active</option>
            <option value="Inactive">Inactive</option>
          </select>
        </div>
        <button type="button" className="btn" onClick={() => void load()} disabled={busy}>
          Refresh
        </button>
      </div>

      {rows.length === 0 ? (
        <div className="empty-state">No guests match the current filter.</div>
      ) : (
        <table>
          <thead>
            <tr>
              <th>Reference</th>
              <th>Name</th>
              <th>Contact</th>
              <th>Status</th>
              <th>Visits</th>
              <th>Last visit</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {rows.map((guest) => (
              <tr key={guest.id}>
                <td>{guest.reference}</td>
                <td>{guest.fullName}</td>
                <td>{guest.contactDetails ?? '—'}</td>
                <td>
                  <span className={guest.status === 'Active' ? 'status-pill ok' : 'status-pill bad'}>
                    {guest.status}
                  </span>
                </td>
                <td>{guest.visitCount}</td>
                <td>{stamp(guest.lastVisitedAtUtc)}</td>
                <td className="row-actions">
                  {canManage ? (
                    <button
                      type="button"
                      className="link-button"
                      onClick={() => void setGuestStatus(guest, guest.status === 'Active' ? 'Inactive' : 'Active')}
                      disabled={busy}
                    >
                      {guest.status === 'Active' ? 'Deactivate' : 'Activate'}
                    </button>
                  ) : null}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {canManage ? (
        <div className="card" style={{ marginTop: 16 }}>
          <h3>Register a guest</h3>
          <div className="toolbar">
            <div className="field">
              <label htmlFor="newGuestName">Full name</label>
              <input id="newGuestName" value={fullName} onChange={(event) => setFullName(event.target.value)} />
            </div>
            <div className="field">
              <label htmlFor="newGuestContact">Contact details</label>
              <input
                id="newGuestContact"
                value={contactDetails}
                onChange={(event) => setContactDetails(event.target.value)}
              />
            </div>
            <div className="field">
              <label htmlFor="newGuestHostSearch">Hosting member</label>
              <input
                id="newGuestHostSearch"
                value={hostQuery}
                onChange={(event) => setHostQuery(event.target.value)}
                placeholder="Optional member number or name"
              />
            </div>
            <div className="field">
              <label htmlFor="newGuestNotes">Notes</label>
              <input id="newGuestNotes" value={notes} onChange={(event) => setNotes(event.target.value)} />
            </div>
            <button
              type="button"
              className="btn"
              onClick={() => void createGuest()}
              disabled={busy || !fullName.trim()}
            >
              Create guest
            </button>
          </div>

          {hostResults.length > 0 ? (
            <table>
              <thead>
                <tr>
                  <th>Member</th>
                  <th>Number</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {hostResults.map((member) => (
                  <tr key={member.id}>
                    <td>{member.preferredName || member.legalName}</td>
                    <td>{member.memberNumber}</td>
                    <td className="row-actions">
                      <button
                        type="button"
                        className="link-button"
                        onClick={() => {
                          setHostId(member.id);
                          setHostQuery(`${member.preferredName || member.legalName} (${member.memberNumber})`);
                          setHostResults([]);
                        }}
                      >
                        Use
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          ) : null}
        </div>
      ) : null}
    </>
  );
}
