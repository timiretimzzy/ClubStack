import { useEffect, useState } from 'react';
import { describeError, json } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import type { GolfCourse, GolfCourseSearch, GolfTee } from '../types';

const BASE = '/api/v1/golf';

function stamp(value: string): string {
  return new Date(value).toLocaleString();
}

/**
 * GOLF-001: the courses and their tees. A tee is where a club's playable
 * configuration lives - capacity, who may play it, and which catalogue product
 * prices a green fee - so it is configured here rather than on the tee sheet.
 */
export function GolfCoursesPage() {
  const { can } = useAuth();
  const canManage = can('Golf.Course.Manage');
  const canView = can('Golf.Course.View');

  const [rows, setRows] = useState<GolfCourse[]>([]);
  const [code, setCode] = useState('');
  const [status, setStatus] = useState('');
  const [selected, setSelected] = useState<GolfCourse | null>(null);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [busy, setBusy] = useState(false);

  const [courseCode, setCourseCode] = useState('');
  const [courseName, setCourseName] = useState('');
  const [holes, setHoles] = useState('18');
  const [currency, setCurrency] = useState('ZAR');
  const [description, setDescription] = useState('');

  const [teeCourseId, setTeeCourseId] = useState('');
  const [teeCode, setTeeCode] = useState('');
  const [teeName, setTeeName] = useState('');
  const [teeCapacity, setTeeCapacity] = useState('4');
  const [membersOnly, setMembersOnly] = useState(false);
  const [guestsAllowed, setGuestsAllowed] = useState(true);
  const [minimumPlayers, setMinimumPlayers] = useState('');
  const [maximumPlayers, setMaximumPlayers] = useState('');

  const load = async () => {
    setError('');
    try {
      const params = new URLSearchParams({ pageSize: '50' });
      if (code.trim()) {
        params.set('code', code.trim());
      }
      if (status) {
        params.set('status', status);
      }
      const result = await json.get<GolfCourseSearch>(`${BASE}/courses?${params.toString()}`);
      setRows(result.items);
      if (!teeCourseId && result.items.length > 0) {
        setTeeCourseId(result.items[0].id);
      }
    } catch (err) {
      setError(describeError(err));
    }
  };

  useEffect(() => {
    if (canView) {
      void load();
    }
  }, [canView, code, status]);

  const open = async (id: string) => {
    setBusy(true);
    setError('');
    try {
      setSelected(await json.get<GolfCourse>(`${BASE}/courses/${id}`));
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const createCourse = async () => {
    setBusy(true);
    setError('');
    setNotice('');
    try {
      const created = await json.post<GolfCourse>(`${BASE}/courses`, {
        code: courseCode.trim(),
        name: courseName.trim(),
        numberOfHoles: Number(holes),
        currencyCode: currency.trim().toUpperCase(),
        description: description.trim() || null,
      });
      setNotice(`Course ${created.code} created.`);
      setCourseCode('');
      setCourseName('');
      setDescription('');
      await load();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const setCourseStatus = async (id: string, next: 'Active' | 'Inactive') => {
    setBusy(true);
    setError('');
    setNotice('');
    try {
      const updated = await json.post<GolfCourse>(`${BASE}/courses/${id}/status`, { status: next });
      setSelected(updated);
      setNotice(`Course ${updated.code} is now ${next.toLowerCase()}.`);
      await load();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const createTee = async () => {
    setBusy(true);
    setError('');
    setNotice('');
    try {
      const tee = await json.post<GolfTee>(`${BASE}/courses/${teeCourseId}/tees`, {
        code: teeCode.trim(),
        name: teeName.trim(),
        capacity: Number(teeCapacity),
        membersOnly,
        guestsAllowed,
        minimumPlayers: minimumPlayers ? Number(minimumPlayers) : null,
        maximumPlayers: maximumPlayers ? Number(maximumPlayers) : null,
      });
      setNotice(`Tee ${tee.code} created.`);
      setTeeCode('');
      setTeeName('');
      if (selected) {
        await open(selected.id);
      }
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const setTeeStatus = async (teeId: string, next: 'Active' | 'Inactive') => {
    setBusy(true);
    setError('');
    setNotice('');
    try {
      await json.post<GolfTee>(`${BASE}/tees/${teeId}/status`, { status: next });
      setNotice(`Tee is now ${next.toLowerCase()}.`);
      if (selected) {
        await open(selected.id);
      }
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  if (!canView) {
    return <div className="empty-state">You do not have permission to view golf courses.</div>;
  }

  return (
    <>
      <h1 className="page-title">Golf Courses</h1>
      <p className="page-subtitle">Courses, their holes, and the tees a club plays from.</p>

      {error ? <div className="alert alert-error">{error}</div> : null}
      {notice ? <div className="alert alert-info">{notice}</div> : null}

      <div className="toolbar">
        <div className="field">
          <label htmlFor="courseCodeFilter">Filter</label>
          <input
            id="courseCodeFilter"
            value={code}
            onChange={(event) => setCode(event.target.value)}
            placeholder="Course code or name"
          />
        </div>
        <div className="field">
          <label htmlFor="courseStatus">Status</label>
          <select id="courseStatus" value={status} onChange={(event) => setStatus(event.target.value)}>
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
        <div className="empty-state">No courses match the current filter.</div>
      ) : (
        <table>
          <thead>
            <tr>
              <th>Code</th>
              <th>Name</th>
              <th>Holes</th>
              <th>Currency</th>
              <th>Status</th>
              <th>Tees</th>
              <th>Updated</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {rows.map((course) => (
              <tr key={course.id}>
                <td>{course.code}</td>
                <td>{course.name}</td>
                <td>{course.numberOfHoles}</td>
                <td>{course.currencyCode}</td>
                <td>
                  <span className={course.status === 'Active' ? 'status-pill ok' : 'status-pill bad'}>
                    {course.status}
                  </span>
                </td>
                <td>{course.tees.length}</td>
                <td>{stamp(course.updatedAtUtc)}</td>
                <td className="row-actions">
                  <button type="button" className="link-button" onClick={() => void open(course.id)}>
                    View
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {selected ? (
        <div className="card" style={{ marginTop: 16 }}>
          <h3>
            {selected.code} &middot; {selected.name}{' '}
            <span className={selected.status === 'Active' ? 'status-pill ok' : 'status-pill bad'}>
              {selected.status}
            </span>
          </h3>
          {selected.description ? <p className="muted">{selected.description}</p> : null}

          {canManage ? (
            <div className="row-actions" style={{ marginTop: 8 }}>
              {selected.status === 'Active' ? (
                <button
                  type="button"
                  className="btn btn-danger"
                  onClick={() => void setCourseStatus(selected.id, 'Inactive')}
                  disabled={busy}
                >
                  Deactivate course
                </button>
              ) : (
                <button
                  type="button"
                  className="btn"
                  onClick={() => void setCourseStatus(selected.id, 'Active')}
                  disabled={busy}
                >
                  Activate course
                </button>
              )}
              <button type="button" className="link-button" onClick={() => setSelected(null)}>
                Close
              </button>
            </div>
          ) : null}

          <h4 style={{ marginTop: 16 }}>Holes</h4>
          {selected.holes.length === 0 ? (
            <div className="empty-state">This course has no holes recorded yet.</div>
          ) : (
            <table>
              <thead>
                <tr>
                  <th>#</th>
                  <th>Par</th>
                  <th>Yardage</th>
                  <th>Name</th>
                </tr>
              </thead>
              <tbody>
                {selected.holes.map((hole) => (
                  <tr key={hole.id}>
                    <td>{hole.holeNumber}</td>
                    <td>{hole.par}</td>
                    <td>{hole.yardage ?? '—'}</td>
                    <td>{hole.name ?? '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}

          <h4 style={{ marginTop: 16 }}>Tees</h4>
          {selected.tees.length === 0 ? (
            <div className="empty-state">This course has no tees configured yet.</div>
          ) : (
            <table>
              <thead>
                <tr>
                  <th>Code</th>
                  <th>Name</th>
                  <th>Capacity</th>
                  <th>Restrictions</th>
                  <th>Status</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {selected.tees.map((tee) => (
                  <tr key={tee.id}>
                    <td>{tee.code}</td>
                    <td>{tee.name}</td>
                    <td>{tee.capacity}</td>
                    <td>
                      {tee.membersOnly ? 'Members only' : 'Open'}
                      {tee.minimumPlayers ? ` · min ${tee.minimumPlayers}` : ''}
                      {tee.maximumPlayers ? ` · max ${tee.maximumPlayers}` : ''}
                    </td>
                    <td>
                      <span className={tee.status === 'Active' ? 'status-pill ok' : 'status-pill bad'}>
                        {tee.status}
                      </span>
                    </td>
                    <td className="row-actions">
                      {canManage ? (
                        <button
                          type="button"
                          className="link-button"
                          onClick={() => void setTeeStatus(tee.id, tee.status === 'Active' ? 'Inactive' : 'Active')}
                          disabled={busy}
                        >
                          {tee.status === 'Active' ? 'Deactivate' : 'Activate'}
                        </button>
                      ) : null}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      ) : null}

      {canManage ? (
        <div className="card" style={{ marginTop: 16 }}>
          <h3>Create a course</h3>
          <div className="toolbar">
            <div className="field">
              <label htmlFor="newCourseCode">Code</label>
              <input id="newCourseCode" value={courseCode} onChange={(event) => setCourseCode(event.target.value)} />
            </div>
            <div className="field">
              <label htmlFor="newCourseName">Name</label>
              <input id="newCourseName" value={courseName} onChange={(event) => setCourseName(event.target.value)} />
            </div>
            <div className="field">
              <label htmlFor="newCourseHoles">Holes</label>
              <input
                id="newCourseHoles"
                type="number"
                min={1}
                max={36}
                value={holes}
                onChange={(event) => setHoles(event.target.value)}
              />
            </div>
            <div className="field">
              <label htmlFor="newCourseCurrency">Currency</label>
              <input
                id="newCourseCurrency"
                value={currency}
                onChange={(event) => setCurrency(event.target.value)}
                placeholder="ZAR"
              />
            </div>
            <div className="field">
              <label htmlFor="newCourseDescription">Description</label>
              <input
                id="newCourseDescription"
                value={description}
                onChange={(event) => setDescription(event.target.value)}
              />
            </div>
            <button
              type="button"
              className="btn"
              onClick={() => void createCourse()}
              disabled={busy || !courseCode.trim() || !courseName.trim()}
            >
              Create course
            </button>
          </div>
        </div>
      ) : null}

      {canManage ? (
        <div className="card" style={{ marginTop: 16 }}>
          <h3>Add a tee</h3>
          <div className="toolbar">
            <div className="field">
              <label htmlFor="teeCourse">Course</label>
              <select id="teeCourse" value={teeCourseId} onChange={(event) => setTeeCourseId(event.target.value)}>
                {rows.map((course) => (
                  <option key={course.id} value={course.id}>
                    {course.code} &middot; {course.name}
                  </option>
                ))}
              </select>
            </div>
            <div className="field">
              <label htmlFor="teeCode">Code</label>
              <input id="teeCode" value={teeCode} onChange={(event) => setTeeCode(event.target.value)} />
            </div>
            <div className="field">
              <label htmlFor="teeName">Name</label>
              <input id="teeName" value={teeName} onChange={(event) => setTeeName(event.target.value)} />
            </div>
            <div className="field">
              <label htmlFor="teeCapacity">Capacity</label>
              <input
                id="teeCapacity"
                type="number"
                min={1}
                value={teeCapacity}
                onChange={(event) => setTeeCapacity(event.target.value)}
              />
            </div>
            <div className="field">
              <label htmlFor="teeMin">Minimum players</label>
              <input
                id="teeMin"
                type="number"
                min={1}
                value={minimumPlayers}
                onChange={(event) => setMinimumPlayers(event.target.value)}
              />
            </div>
            <div className="field">
              <label htmlFor="teeMax">Maximum players</label>
              <input
                id="teeMax"
                type="number"
                min={1}
                value={maximumPlayers}
                onChange={(event) => setMaximumPlayers(event.target.value)}
              />
            </div>
            <div className="field">
              <label htmlFor="teeMembersOnly">Members only</label>
              <input
                id="teeMembersOnly"
                type="checkbox"
                checked={membersOnly}
                onChange={(event) => setMembersOnly(event.target.checked)}
              />
            </div>
            <div className="field">
              <label htmlFor="teeGuestsAllowed">Guests allowed</label>
              <input
                id="teeGuestsAllowed"
                type="checkbox"
                checked={guestsAllowed}
                onChange={(event) => setGuestsAllowed(event.target.checked)}
              />
            </div>
            <button
              type="button"
              className="btn"
              onClick={() => void createTee()}
              disabled={busy || !teeCourseId || !teeCode.trim() || !teeName.trim()}
            >
              Create tee
            </button>
          </div>
        </div>
      ) : null}
    </>
  );
}
