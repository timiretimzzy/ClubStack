import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { describeError, json } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import type { GolfCourse, GolfCourseSearch, GolfTee, GolfTeeSlot, GolfTeeSlotSearch } from '../types';

const BASE = '/api/v1/golf';

function today(): string {
  return new Date().toISOString().slice(0, 10);
}

function slotPill(slot: GolfTeeSlot): string {
  if (slot.status === 'Blocked') {
    return 'status-pill bad';
  }
  if (slot.openPlayers <= 0) {
    return 'status-pill warn';
  }
  return 'status-pill ok';
}

/**
 * Phase 3 -42: the tee sheet is a read model of bookable slots, generated in
 * advance from each active tee's configuration. Blocking a slot models course
 * closure without touching confirmed bookings.
 */
export function GolfTeeSheetPage() {
  const { can } = useAuth();
  const canView = can('Golf.TeeSheet.View');
  const canViewCourses = can('Golf.Course.View');
  const canManageSlots = can('Golf.TeeSlot.Manage');
  const canCreateBooking = can('Golf.Booking.Create');

  const [date, setDate] = useState(today());
  const [courseId, setCourseId] = useState('');
  const [teeId, setTeeId] = useState('');
  const [bookableOnly, setBookableOnly] = useState(false);
  const [courses, setCourses] = useState<GolfCourse[]>([]);
  const [rows, setRows] = useState<GolfTeeSlot[]>([]);
  const [selected, setSelected] = useState<GolfTeeSlot | null>(null);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [busy, setBusy] = useState(false);

  const [genTeeId, setGenTeeId] = useState('');
  const [genFrom, setGenFrom] = useState(today());
  const [genTo, setGenTo] = useState(today());
  const [genFirst, setGenFirst] = useState('06:00');
  const [genLast, setGenLast] = useState('16:00');
  const [genInterval, setGenInterval] = useState('10');
  const [genCapacity, setGenCapacity] = useState('');

  const tees = (() => {
    const course = courses.find((c) => c.id === courseId);
    return course?.tees ?? [];
  })();

  useEffect(() => {
    if (!canViewCourses) {
      return;
    }
    void (async () => {
      try {
        const result = await json.get<GolfCourseSearch>(`${BASE}/courses?pageSize=50&status=Active`);
        setCourses(result.items);
        if (!courseId && result.items.length > 0) {
          setCourseId(result.items[0].id);
        }
        if (!genTeeId) {
          const firstTee = result.items.find((course) => course.tees.length > 0)?.tees[0];
          if (firstTee) {
            setGenTeeId(firstTee.id);
          }
        }
      } catch (err) {
        setError(describeError(err));
      }
    })();
  }, [canViewCourses]);

  const load = async () => {
    if (!canView) {
      return;
    }
    setError('');
    try {
      const params = new URLSearchParams({ date, pageSize: '200' });
      if (courseId) {
        params.set('courseId', courseId);
      }
      if (teeId) {
        params.set('teeId', teeId);
      }
      if (bookableOnly) {
        params.set('bookableOnly', 'true');
      }
      setRows((await json.get<GolfTeeSlotSearch>(`${BASE}/tee-sheet?${params.toString()}`)).items);
    } catch (err) {
      setError(describeError(err));
    }
  };

  useEffect(() => {
    void load();
  }, [canView, date, courseId, teeId, bookableOnly]);

  const setSlotStatus = async (slot: GolfTeeSlot, next: 'Open' | 'Blocked') => {
    setBusy(true);
    setError('');
    setNotice('');
    try {
      await json.post(`${BASE}/tee-slots/${slot.id}/status`, { status: next });
      setNotice(`Slot on ${slot.teeName} at ${slot.startTime} is now ${next === 'Open' ? 'open' : 'blocked'}.`);
      if (selected?.id === slot.id) {
        setSelected(null);
      }
      await load();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const generate = async () => {
    setBusy(true);
    setError('');
    setNotice('');
    try {
      const result = await json.post<GolfTeeSlotSearch>(`${BASE}/tee-slots/generate`, {
        teeId: genTeeId,
        fromDate: genFrom,
        toDate: genTo,
        firstStartTime: genFirst,
        lastStartTime: genLast,
        intervalMinutes: Number(genInterval),
        capacity: genCapacity ? Number(genCapacity) : null,
      });
      setNotice(`${result.totalCount} slot(s) now on the sheet.`);
      setDate(genFrom);
      await load();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  if (!canView) {
    return <div className="empty-state">You do not have permission to view the tee sheet.</div>;
  }

  return (
    <>
      <h1 className="page-title">Tee Sheet</h1>
      <p className="page-subtitle">Bookable slots per tee. Blocking a slot models course closure.</p>

      {error ? <div className="alert alert-error">{error}</div> : null}
      {notice ? <div className="alert alert-info">{notice}</div> : null}

      <div className="toolbar">
        <div className="field">
          <label htmlFor="teeSheetDate">Date</label>
          <input id="teeSheetDate" type="date" value={date} onChange={(event) => setDate(event.target.value)} />
        </div>
        <div className="field">
          <label htmlFor="teeSheetCourse">Course</label>
          <select id="teeSheetCourse" value={courseId} onChange={(event) => setCourseId(event.target.value)}>
            <option value="">All courses</option>
            {courses.map((course) => (
              <option key={course.id} value={course.id}>
                {course.code} &middot; {course.name}
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label htmlFor="teeSheetTee">Tee</label>
          <select
            id="teeSheetTee"
            value={teeId}
            onChange={(event) => {
              setTeeId(event.target.value);
              setGenTeeId(event.target.value);
            }}
          >
            <option value="">All tees</option>
            {tees.map((tee: GolfTee) => (
              <option key={tee.id} value={tee.id}>
                {tee.code} &middot; {tee.name}
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label htmlFor="teeSheetBookable">Bookable only</label>
          <input
            id="teeSheetBookable"
            type="checkbox"
            checked={bookableOnly}
            onChange={(event) => setBookableOnly(event.target.checked)}
          />
        </div>
        <button type="button" className="btn" onClick={() => void load()} disabled={busy}>
          Refresh
        </button>
      </div>

      {rows.length === 0 ? (
        <div className="empty-state">No slots on the sheet for this date.</div>
      ) : (
        <table>
          <thead>
            <tr>
              <th>Course</th>
              <th>Tee</th>
              <th>Start</th>
              <th>Duration</th>
              <th>Capacity</th>
              <th>Taken</th>
              <th>Open</th>
              <th>Status</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {rows.map((slot) => (
              <tr key={slot.id}>
                <td>{slot.courseCode}</td>
                <td>{slot.teeName}</td>
                <td>{slot.startTime}</td>
                <td>{slot.durationMinutes} min</td>
                <td>{slot.capacity}</td>
                <td>{slot.takenPlayers}</td>
                <td>{slot.openPlayers}</td>
                <td>
                  <span className={slotPill(slot)}>{slot.status}</span>
                </td>
                <td className="row-actions">
                  <button type="button" className="link-button" onClick={() => setSelected(slot)}>
                    View
                  </button>
                  {canCreateBooking && slot.status === 'Open' && slot.openPlayers > 0 ? (
                    <Link className="link-button" to={`/golf/bookings/new?slotId=${slot.id}`}>
                      Book
                    </Link>
                  ) : null}
                  {canManageSlots ? (
                    <button
                      type="button"
                      className="link-button"
                      onClick={() => void setSlotStatus(slot, slot.status === 'Open' ? 'Blocked' : 'Open')}
                      disabled={busy}
                    >
                      {slot.status === 'Open' ? 'Block' : 'Reopen'}
                    </button>
                  ) : null}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {selected ? (
        <div className="card" style={{ marginTop: 16 }}>
          <h3>
            {selected.courseName} &middot; {selected.teeName}{' '}
            <span className={slotPill(selected)}>{selected.status}</span>
          </h3>
          <div className="detail-grid">
            <div className="field">
              <span className="field-label">Date</span>
              <span className="field-value">{selected.date}</span>
            </div>
            <div className="field">
              <span className="field-label">Start</span>
              <span className="field-value">{selected.startTime}</span>
            </div>
            <div className="field">
              <span className="field-label">Duration</span>
              <span className="field-value">{selected.durationMinutes} minutes</span>
            </div>
            <div className="field">
              <span className="field-label">Places</span>
              <span className="field-value">
                {selected.takenPlayers} taken / {selected.openPlayers} open of {selected.capacity}
              </span>
            </div>
            <div className="field">
              <span className="field-label">Restrictions</span>
              <span className="field-value">
                {selected.membersOnly ? 'Members only' : 'Open tee'}
                {selected.guestsAllowed ? '' : ' · no guests'}
              </span>
            </div>
          </div>
          <div className="row-actions" style={{ marginTop: 12 }}>
            {canCreateBooking && selected.status === 'Open' && selected.openPlayers > 0 ? (
              <Link className="btn" to={`/golf/bookings/new?slotId=${selected.id}`}>
                Book this slot
              </Link>
            ) : null}
            <button type="button" className="link-button" onClick={() => setSelected(null)}>
              Close
            </button>
          </div>
        </div>
      ) : null}

      {canManageSlots ? (
        <div className="card" style={{ marginTop: 16 }}>
          <h3>Generate tee sheet</h3>
          <p className="muted">
            Generating fills gaps only: slots that already exist for the range are never overwritten.
          </p>
          <div className="toolbar">
            <div className="field">
              <label htmlFor="genTee">Tee</label>
              <select id="genTee" value={genTeeId} onChange={(event) => setGenTeeId(event.target.value)}>
                {courses.map((course) =>
                  course.tees.map((tee) => (
                    <option key={tee.id} value={tee.id}>
                      {course.code} &middot; {tee.code} {tee.name}
                    </option>
                  )),
                )}
              </select>
            </div>
            <div className="field">
              <label htmlFor="genFrom">From</label>
              <input id="genFrom" type="date" value={genFrom} onChange={(event) => setGenFrom(event.target.value)} />
            </div>
            <div className="field">
              <label htmlFor="genTo">To</label>
              <input id="genTo" type="date" value={genTo} onChange={(event) => setGenTo(event.target.value)} />
            </div>
            <div className="field">
              <label htmlFor="genFirst">First start</label>
              <input
                id="genFirst"
                type="time"
                value={genFirst}
                onChange={(event) => setGenFirst(event.target.value)}
              />
            </div>
            <div className="field">
              <label htmlFor="genLast">Last start</label>
              <input
                id="genLast"
                type="time"
                value={genLast}
                onChange={(event) => setGenLast(event.target.value)}
              />
            </div>
            <div className="field">
              <label htmlFor="genInterval">Interval (min)</label>
              <input
                id="genInterval"
                type="number"
                min={5}
                value={genInterval}
                onChange={(event) => setGenInterval(event.target.value)}
              />
            </div>
            <div className="field">
              <label htmlFor="genCapacity">Capacity override</label>
              <input
                id="genCapacity"
                type="number"
                min={1}
                value={genCapacity}
                onChange={(event) => setGenCapacity(event.target.value)}
                placeholder="Use tee capacity"
              />
            </div>
            <button
              type="button"
              className="btn"
              onClick={() => void generate()}
              disabled={busy || !genTeeId || !genFrom || !genTo}
            >
              Generate
            </button>
          </div>
        </div>
      ) : null}
    </>
  );
}
