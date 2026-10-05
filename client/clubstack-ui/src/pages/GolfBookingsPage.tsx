import { useEffect, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { describeError, json } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import type {
  GolfBooking,
  GolfBookingSearch,
  GolfGuest,
  GolfGuestSearch,
  GolfTeeSlot,
  Member,
  MemberSearch,
} from '../types';

const BASE = '/api/v1/golf';

function money(amount: number, currency: string): string {
  return `${amount.toLocaleString(undefined, { minimumFractionDigits: 2 })} ${currency}`;
}

function today(): string {
  return new Date().toISOString().slice(0, 10);
}

function bookingPill(status: GolfBooking['status']): string {
  if (status === 'Confirmed' || status === 'CheckedIn' || status === 'Completed') {
    return 'status-pill ok';
  }
  if (status === 'Cancelled' || status === 'NoShow') {
    return 'status-pill bad';
  }
  return 'status-pill warn';
}

type Line = { key: string; label: string; memberId: string | null; golfGuestId: string | null };

export function GolfBookingsPage() {
  const { can } = useAuth();
  const [params] = useSearchParams();
  const slotId = params.get('slotId') ?? '';
  const composing = slotId.length > 0;

  const canView = can('Golf.Booking.View');
  const canCreate = can('Golf.Booking.Create');
  const canCancel = can('Golf.Booking.Cancel');
  const canOverride = can('Golf.Booking.Override');
  const canCheckIn = can('Golf.Booking.CheckIn');

  const [rows, setRows] = useState<GolfBooking[]>([]);
  const [status, setStatus] = useState('');
  const [fromDate, setFromDate] = useState('');
  const [toDate, setToDate] = useState('');
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [busy, setBusy] = useState(false);

  const [slot, setSlot] = useState<GolfTeeSlot | null>(null);
  const [bookedBy, setBookedBy] = useState('');
  const [bookerResults, setBookerResults] = useState<Member[]>([]);
  const [playerQuery, setPlayerQuery] = useState('');
  const [playerResults, setPlayerResults] = useState<Member[]>([]);
  const [guestQuery, setGuestQuery] = useState('');
  const [guestResults, setGuestResults] = useState<GolfGuest[]>([]);
  const [lines, setLines] = useState<Line[]>([]);

  const load = async () => {
    if (!canView) {
      return;
    }
    setError('');
    try {
      const query = new URLSearchParams({ pageSize: '50' });
      if (status) {
        query.set('status', status);
      }
      if (fromDate) {
        query.set('fromDate', fromDate);
      }
      if (toDate) {
        query.set('toDate', toDate);
      }
      setRows((await json.get<GolfBookingSearch>(`${BASE}/bookings?${query.toString()}`)).items);
    } catch (err) {
      setError(describeError(err));
    }
  };

  useEffect(() => {
    void load();
  }, [canView, status, fromDate, toDate]);

  useEffect(() => {
    if (!composing) {
      return;
    }
    void (async () => {
      setError('');
      try {
        setSlot(await json.get<GolfTeeSlot>(`${BASE}/tee-slots/${slotId}`));
      } catch (err) {
        setError(describeError(err));
      }
    })();
  }, [composing, slotId]);

  const searchMembers = async (term: string, apply: (members: Member[]) => void) => {
    if (term.trim().length < 2) {
      apply([]);
      return;
    }
    try {
      const query = new URLSearchParams({ query: term.trim(), pageSize: '10' });
      apply((await json.get<MemberSearch>(`/api/v1/members?${query.toString()}`)).items);
    } catch (err) {
      setError(describeError(err));
    }
  };

  const searchGuests = async () => {
    if (guestQuery.trim().length < 2) {
      setGuestResults([]);
      return;
    }
    try {
      const query = new URLSearchParams({ search: guestQuery.trim(), pageSize: '10' });
      setGuestResults((await json.get<GolfGuestSearch>(`${BASE}/guests?${query.toString()}`)).items);
    } catch (err) {
      setError(describeError(err));
    }
  };

  const addLine = (line: Omit<Line, 'key'>) => {
    setLines((current) => [...current, { ...line, key: crypto.randomUUID() }]);
  };

  const createBooking = async () => {
    setBusy(true);
    setError('');
    setNotice('');
    try {
      // A fresh key per submission keeps a retried create from booking the slot twice.
      const booking = await json.post<GolfBooking>(`${BASE}/bookings`, {
        teeSlotId: slotId,
        bookedByMemberId: bookedBy,
        players: lines.map((line, index) => ({
          memberId: line.memberId,
          golfGuestId: line.golfGuestId,
          position: index + 1,
        })),
        idempotencyKey: `golf-create-${crypto.randomUUID()}`,
      });
      setNotice(`Booking ${booking.reference} created as ${booking.status}. Confirm it to reserve the places.`);
      setLines([]);
      setGuestQuery('');
      await load();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const act = async (
    id: string,
    action: string,
    body: Record<string, unknown> | null,
    message: string,
  ) => {
    setBusy(true);
    setError('');
    setNotice('');
    try {
      const updated = await json.post<GolfBooking>(`${BASE}/bookings/${id}/${action}`, body ?? {});
      setNotice(`${message}: ${updated.reference} is now ${updated.status}.`);
      await load();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const confirmBooking = async (booking: GolfBooking) => {
    const body: Record<string, unknown> = {
      // Confirming consumes capacity and prices fees, so it carries a retry key.
      idempotencyKey: `golf-confirm-${crypto.randomUUID()}`,
    };
    if (canOverride) {
      const reason = window.prompt('Override reason (leave blank to confirm within capacity)');
      if (reason === null) {
        return;
      }
      if (reason.trim()) {
        body.overrideCapacity = true;
        body.overrideReason = reason.trim();
      }
    }
    await act(booking.id, 'confirm', body, 'Booking confirmed');
  };

  const cancelBooking = async (booking: GolfBooking) => {
    const reason = window.prompt('Reason for cancelling this booking?');
    if (!reason || !reason.trim()) {
      return;
    }
    await act(booking.id, 'cancel', { reason: reason.trim() }, 'Booking cancelled');
  };

  if (!canView) {
    return <div className="empty-state">You do not have permission to view golf bookings.</div>;
  }

  return (
    <>
      <h1 className="page-title">Golf Bookings</h1>
      <p className="page-subtitle">Bookings from draft through check-in, completion, cancellation and no-show.</p>

      {error ? <div className="alert alert-error">{error}</div> : null}
      {notice ? <div className="alert alert-info">{notice}</div> : null}

      {composing ? (
        <div className="card">
          <h3>New booking</h3>
          {slot ? (
            <p className="muted">
              {slot.courseName} &middot; {slot.teeName} &middot; {slot.date} {slot.startTime} &middot;{' '}
              {slot.openPlayers} of {slot.capacity} places open
            </p>
          ) : (
            <p className="muted">Loading slot…</p>
          )}

          <div className="toolbar">
            <div className="field">
              <label htmlFor="bookerSearch">Booked by (member)</label>
              <input
                id="bookerSearch"
                value={bookedBy}
                onChange={(event) => {
                  const value = event.target.value;
                  setBookedBy(value);
                  void searchMembers(value, setBookerResults);
                }}
                placeholder="Member number or name"
              />
            </div>
            <div className="field">
              <label htmlFor="playerSearch">Add a member player</label>
              <input
                id="playerSearch"
                value={playerQuery}
                onChange={(event) => {
                  const value = event.target.value;
                  setPlayerQuery(value);
                  void searchMembers(value, setPlayerResults);
                }}
                placeholder="Member number or name"
              />
            </div>
            <div className="field">
              <label htmlFor="guestSearch">Add a golf guest</label>
              <input
                id="guestSearch"
                value={guestQuery}
                onChange={(event) => setGuestQuery(event.target.value)}
                onKeyDown={(event) => {
                  if (event.key === 'Enter') {
                    event.preventDefault();
                    void searchGuests();
                  }
                }}
                placeholder="Guest name or reference"
              />
            </div>
            <button type="button" className="btn" onClick={() => void searchGuests()} disabled={busy}>
              Search guests
            </button>
          </div>

          {bookerResults.length > 0 ? (
            <table>
              <thead>
                <tr>
                  <th>Booker</th>
                  <th>Number</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {bookerResults.map((member) => (
                  <tr key={member.id}>
                    <td>{member.preferredName || member.legalName}</td>
                    <td>{member.memberNumber}</td>
                    <td className="row-actions">
                      <button
                        type="button"
                        className="link-button"
                        onClick={() => {
                          setBookedBy(member.id);
                          setBookerResults([]);
                        }}
                      >
                        Use
                      </button>
                      <button
                        type="button"
                        className="link-button"
                        onClick={() => {
                          addLine({
                            label: member.preferredName || member.legalName,
                            memberId: member.id,
                            golfGuestId: null,
                          });
                          setPlayerQuery('');
                          setPlayerResults([]);
                        }}
                      >
                        Add as player
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          ) : null}

          {playerResults.length > 0 ? (
            <table>
              <thead>
                <tr>
                  <th>Member player</th>
                  <th>Number</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {playerResults.map((member) => (
                  <tr key={member.id}>
                    <td>{member.preferredName || member.legalName}</td>
                    <td>{member.memberNumber}</td>
                    <td className="row-actions">
                      <button
                        type="button"
                        className="link-button"
                        onClick={() => {
                          addLine({
                            label: member.preferredName || member.legalName,
                            memberId: member.id,
                            golfGuestId: null,
                          });
                          setPlayerQuery('');
                          setPlayerResults([]);
                        }}
                      >
                        Add
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          ) : null}

          {guestResults.length > 0 ? (
            <table>
              <thead>
                <tr>
                  <th>Guest player</th>
                  <th>Reference</th>
                  <th>Visits</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {guestResults.map((guest) => (
                  <tr key={guest.id}>
                    <td>{guest.fullName}</td>
                    <td>{guest.reference}</td>
                    <td>{guest.visitCount}</td>
                    <td className="row-actions">
                      <button
                        type="button"
                        className="link-button"
                        onClick={() => {
                          addLine({ label: guest.fullName, memberId: null, golfGuestId: guest.id });
                          setGuestQuery('');
                          setGuestResults([]);
                        }}
                      >
                        Add
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          ) : null}

          <h4 style={{ marginTop: 16 }}>Players</h4>
          {lines.length === 0 ? (
            <div className="empty-state">Add the members playing, then the guests.</div>
          ) : (
            <table>
              <thead>
                <tr>
                  <th>#</th>
                  <th>Player</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {lines.map((line, index) => (
                  <tr key={line.key}>
                    <td>{index + 1}</td>
                    <td>{line.label}</td>
                    <td className="row-actions">
                      <button
                        type="button"
                        className="link-button"
                        onClick={() => setLines((current) => current.filter((l) => l.key !== line.key))}
                      >
                        Remove
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}

          <div className="row-actions" style={{ marginTop: 12 }}>
            <button
              type="button"
              className="btn"
              onClick={() => void createBooking()}
              disabled={busy || !slotId || !bookedBy || lines.length === 0}
            >
              Create draft booking
            </button>
            <Link className="link-button" to="/golf/bookings">
              Cancel
            </Link>
          </div>
        </div>
      ) : null}

      <div className="toolbar" style={{ marginTop: composing ? 16 : 0 }}>
        <div className="field">
          <label htmlFor="bookingStatus">Status</label>
          <select id="bookingStatus" value={status} onChange={(event) => setStatus(event.target.value)}>
            <option value="">All</option>
            <option value="Draft">Draft</option>
            <option value="Confirmed">Confirmed</option>
            <option value="CheckedIn">CheckedIn</option>
            <option value="Completed">Completed</option>
            <option value="NoShow">NoShow</option>
            <option value="Cancelled">Cancelled</option>
          </select>
        </div>
        <div className="field">
          <label htmlFor="bookingFrom">From</label>
          <input id="bookingFrom" type="date" value={fromDate} onChange={(event) => setFromDate(event.target.value)} />
        </div>
        <div className="field">
          <label htmlFor="bookingTo">To</label>
          <input id="bookingTo" type="date" value={toDate} onChange={(event) => setToDate(event.target.value)} />
        </div>
        <button
          type="button"
          className="btn"
          onClick={() => {
            setFromDate(today());
            setToDate(today());
          }}
        >
          Today
        </button>
        <button type="button" className="btn" onClick={() => void load()} disabled={busy}>
          Refresh
        </button>
      </div>

      {rows.length === 0 ? (
        <div className="empty-state">No bookings match the current filter.</div>
      ) : (
        <table>
          <thead>
            <tr>
              <th>Reference</th>
              <th>Course / tee</th>
              <th>Date</th>
              <th>Start</th>
              <th>Status</th>
              <th>Players</th>
              <th>Total</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {rows.map((booking) => (
              <tr key={booking.id}>
                <td>{booking.reference}</td>
                <td>
                  {booking.courseName} &middot; {booking.teeName}
                </td>
                <td>{booking.teeDate}</td>
                <td>{booking.startTime}</td>
                <td>
                  <span className={bookingPill(booking.status)}>{booking.status}</span>
                  {booking.capacityOverridden ? <span className="status-pill warn"> Override</span> : null}
                </td>
                <td>{booking.playerCount}</td>
                <td>{money(booking.totalFee, booking.currencyCode)}</td>
                <td className="row-actions">
                  {booking.status === 'Draft' && canCreate ? (
                    <button
                      type="button"
                      className="link-button"
                      onClick={() => void confirmBooking(booking)}
                      disabled={busy}
                    >
                      Confirm
                    </button>
                  ) : null}
                  {booking.status === 'Confirmed' && canCheckIn ? (
                    <button
                      type="button"
                      className="link-button"
                      onClick={() => void act(booking.id, 'check-in', null, 'Players checked in')}
                      disabled={busy}
                    >
                      Check in
                    </button>
                  ) : null}
                  {booking.status === 'CheckedIn' ? (
                    <button
                      type="button"
                      className="link-button"
                      onClick={() => void act(booking.id, 'complete', null, 'Booking completed')}
                      disabled={busy}
                    >
                      Complete
                    </button>
                  ) : null}
                  {booking.status === 'Confirmed' && canCancel ? (
                    <button
                      type="button"
                      className="link-button"
                      onClick={() => void cancelBooking(booking)}
                      disabled={busy}
                    >
                      Cancel
                    </button>
                  ) : null}
                  {booking.status === 'Confirmed' ? (
                    <button
                      type="button"
                      className="link-button"
                      onClick={() => void act(booking.id, 'no-show', null, 'Marked as a no-show')}
                      disabled={busy}
                    >
                      No-show
                    </button>
                  ) : null}
                  <Link className="link-button" to={`/golf/bookings/${booking.id}`}>
                    Open
                  </Link>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
}
