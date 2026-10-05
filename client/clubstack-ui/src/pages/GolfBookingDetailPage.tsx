import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { describeError, json } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import type { GolfBooking, GolfGuest, GolfGuestSearch, Member, MemberSearch, ProductSearch } from '../types';

const BASE = '/api/v1/golf';

function money(amount: number, currency: string): string {
  return `${amount.toLocaleString(undefined, { minimumFractionDigits: 2 })} ${currency}`;
}

function stamp(value: string | null): string {
  return value ? new Date(value).toLocaleString() : '—';
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

function chargePill(status: string): string {
  if (status === 'Charged') {
    return 'status-pill ok';
  }
  if (status === 'Waived' || status === 'Cancelled') {
    return 'status-pill bad';
  }
  return 'status-pill warn';
}

export function GolfBookingDetailPage() {
  const { bookingId } = useParams();
  const { can } = useAuth();
  const canView = can('Golf.Booking.View');
  const canEdit = can('Golf.Booking.Edit');
  const canCancel = can('Golf.Booking.Cancel');
  const canOverride = can('Golf.Booking.Override');
  const canCheckIn = can('Golf.Booking.CheckIn');
  const canCharge = can('Golf.Charge.Create');

  const [booking, setBooking] = useState<GolfBooking | null>(null);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [busy, setBusy] = useState(false);

  const [playerQuery, setPlayerQuery] = useState('');
  const [playerResults, setPlayerResults] = useState<Member[]>([]);
  const [guestQuery, setGuestQuery] = useState('');
  const [guestResults, setGuestResults] = useState<GolfGuest[]>([]);

  const [productQuery, setProductQuery] = useState('');
  const [productResults, setProductResults] = useState<ProductSearch['items']>([]);
  const [chargeProductId, setChargeProductId] = useState('');
  const [chargeQuantity, setChargeQuantity] = useState('1');
  const [chargePlayerId, setChargePlayerId] = useState('');
  const [chargeDescription, setChargeDescription] = useState('');

  const load = async () => {
    if (!canView || !bookingId) {
      return;
    }
    setError('');
    try {
      setBooking(await json.get<GolfBooking>(`${BASE}/bookings/${bookingId}`));
    } catch (err) {
      setError(describeError(err));
    }
  };

  useEffect(() => {
    void load();
  }, [canView, bookingId]);

  const searchMembers = async (term: string) => {
    if (term.trim().length < 2) {
      setPlayerResults([]);
      return;
    }
    try {
      const query = new URLSearchParams({ query: term.trim(), pageSize: '10' });
      setPlayerResults((await json.get<MemberSearch>(`/api/v1/members?${query.toString()}`)).items);
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

  const searchProducts = async (term: string) => {
    if (term.trim().length < 2) {
      setProductResults([]);
      return;
    }
    try {
      const query = new URLSearchParams({ query: term.trim(), pageSize: '10' });
      setProductResults((await json.get<ProductSearch>(`/api/v1/products?${query.toString()}`)).items);
    } catch (err) {
      setError(describeError(err));
    }
  };

  const addPlayer = async (memberId: string | null, golfGuestId: string | null) => {
    if (!bookingId) {
      return;
    }
    setBusy(true);
    setError('');
    setNotice('');
    try {
      const updated = await json.post<GolfBooking>(`${BASE}/bookings/${bookingId}/players`, {
        memberId,
        golfGuestId,
      });
      setBooking(updated);
      setNotice('Player added to the draft.');
      setPlayerQuery('');
      setPlayerResults([]);
      setGuestQuery('');
      setGuestResults([]);
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const removePlayer = async (playerId: string) => {
    if (!bookingId) {
      return;
    }
    setBusy(true);
    setError('');
    setNotice('');
    try {
      const updated = await json.delete<GolfBooking>(`${BASE}/bookings/${bookingId}/players/${playerId}`);
      setBooking(updated);
      setNotice('Player removed from the draft.');
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const act = async (action: string, body: Record<string, unknown> | null, message: string) => {
    if (!bookingId) {
      return;
    }
    setBusy(true);
    setError('');
    setNotice('');
    try {
      const updated = await json.post<GolfBooking>(`${BASE}/bookings/${bookingId}/${action}`, body ?? {});
      setBooking(updated);
      setNotice(`${message}: booking is now ${updated.status}.`);
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const confirm = async () => {
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
    await act('confirm', body, 'Booking confirmed');
  };

  const cancel = async () => {
    const reason = window.prompt('Reason for cancelling this booking?');
    if (!reason || !reason.trim()) {
      return;
    }
    await act('cancel', { reason: reason.trim() }, 'Booking cancelled');
  };

  const addCharge = async () => {
    if (!bookingId || !chargeProductId) {
      return;
    }
    setBusy(true);
    setError('');
    setNotice('');
    try {
      const updated = await json.post<GolfBooking>(`${BASE}/bookings/${bookingId}/charges`, {
        productId: chargeProductId,
        quantity: Number(chargeQuantity) || 1,
        bookingPlayerId: chargePlayerId || null,
        description: chargeDescription.trim() || null,
        // Adding a charge moves money, so it carries a retry key.
        idempotencyKey: `golf-charge-${crypto.randomUUID()}`,
      });
      setBooking(updated);
      setNotice('Charge added.');
      setChargeProductId('');
      setChargeDescription('');
      setChargeQuantity('1');
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const postCharges = async () => {
    setBusy(true);
    setError('');
    setNotice('');
    try {
      // Posting debits members and credits cash, so it carries a retry key.
      const updated = await json.post<GolfBooking>(`${BASE}/bookings/${bookingId}/charges/post`, {
        idempotencyKey: `golf-post-${crypto.randomUUID()}`,
      });
      setBooking(updated);
      setNotice('Charges posted to the ledger.');
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const waiveCharge = async (chargeId: string) => {
    const reason = window.prompt('Reason for waiving this charge?');
    if (!reason || !reason.trim()) {
      return;
    }
    setBusy(true);
    setError('');
    setNotice('');
    try {
      const updated = await json.post<GolfBooking>(`${BASE}/bookings/${bookingId}/charges/${chargeId}/waive`, {
        reason: reason.trim(),
      });
      setBooking(updated);
      setNotice('Charge waived.');
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  if (!canView) {
    return <div className="empty-state">You do not have permission to view golf bookings.</div>;
  }

  if (!booking) {
    return (
      <>
        <h1 className="page-title">Golf booking</h1>
        {error ? <div className="alert alert-error">{error}</div> : <p className="muted">Loading…</p>}
      </>
    );
  }

  const pendingCharges = booking.charges.filter((charge) => charge.status === 'Pending');

  return (
    <>
      <h1 className="page-title">
        Booking {booking.reference} <span className={bookingPill(booking.status)}>{booking.status}</span>
      </h1>
      <p className="page-subtitle">
        {booking.courseName} &middot; {booking.teeName} &middot; {booking.teeDate} {booking.startTime}
      </p>

      {error ? <div className="alert alert-error">{error}</div> : null}
      {notice ? <div className="alert alert-info">{notice}</div> : null}

      <div className="detail-grid">
        <div className="field">
          <span className="field-label">Players</span>
          <span className="field-value">{booking.playerCount}</span>
        </div>
        <div className="field">
          <span className="field-label">Total fees</span>
          <span className="field-value">
            <strong>{money(booking.totalFee, booking.currencyCode)}</strong>
          </span>
        </div>
        <div className="field">
          <span className="field-label">Created</span>
          <span className="field-value">{stamp(booking.createdAtUtc)}</span>
        </div>
        <div className="field">
          <span className="field-label">Confirmed</span>
          <span className="field-value">{stamp(booking.confirmedAtUtc)}</span>
        </div>
        <div className="field">
          <span className="field-label">Checked in</span>
          <span className="field-value">{stamp(booking.checkedInAtUtc)}</span>
        </div>
        <div className="field">
          <span className="field-label">Completed</span>
          <span className="field-value">{stamp(booking.completedAtUtc)}</span>
        </div>
        {booking.overrideReason ? (
          <div className="field">
            <span className="field-label">Capacity override</span>
            <span className="field-value">{booking.overrideReason}</span>
          </div>
        ) : null}
        {booking.cancellationReason ? (
          <div className="field">
            <span className="field-label">Cancellation</span>
            <span className="field-value">{booking.cancellationReason}</span>
          </div>
        ) : null}
      </div>

      <div className="row-actions" style={{ marginTop: 12 }}>
        {booking.status === 'Draft' && can('Golf.Booking.Create') ? (
          <button type="button" className="btn" onClick={() => void confirm()} disabled={busy}>
            Confirm booking
          </button>
        ) : null}
        {booking.status === 'Confirmed' && canCheckIn ? (
          <button type="button" className="btn" onClick={() => void act('check-in', null, 'Players checked in')} disabled={busy}>
            Check in
          </button>
        ) : null}
        {booking.status === 'CheckedIn' ? (
          <button type="button" className="btn" onClick={() => void act('complete', null, 'Booking completed')} disabled={busy}>
            Complete
          </button>
        ) : null}
        {booking.status === 'Confirmed' && canCancel ? (
          <button type="button" className="btn btn-danger" onClick={() => void cancel()} disabled={busy}>
            Cancel booking
          </button>
        ) : null}
        {booking.status === 'Confirmed' ? (
          <button type="button" className="btn btn-danger" onClick={() => void act('no-show', null, 'Marked as a no-show')} disabled={busy}>
            No-show
          </button>
        ) : null}
        <Link className="link-button" to="/golf/bookings">
          Back to bookings
        </Link>
      </div>

      <div className="card" style={{ marginTop: 16 }}>
        <h3>Players</h3>
        {booking.players.length === 0 ? (
          <div className="empty-state">This booking has no players.</div>
        ) : (
          <table>
            <thead>
              <tr>
                <th>#</th>
                <th>Player</th>
                <th>Type</th>
                <th>Fee</th>
                <th>Status</th>
                <th>Charged</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {booking.players.map((player) => (
                <tr key={player.id}>
                  <td>{player.position}</td>
                  <td>{player.playerType === 'Member' ? (player.memberName ?? player.memberNumber) : player.guestName}</td>
                  <td>{player.playerType}</td>
                  <td>{money(player.fee, player.currencyCode)}</td>
                  <td>{player.status}</td>
                  <td>{player.accountTransactionId ? 'Yes' : 'No'}</td>
                  <td className="row-actions">
                    {booking.status === 'Draft' && canEdit ? (
                      <button
                        type="button"
                        className="link-button"
                        onClick={() => void removePlayer(player.id)}
                        disabled={busy}
                      >
                        Remove
                      </button>
                    ) : null}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}

        {booking.status === 'Draft' && canEdit ? (
          <>
            <div className="toolbar" style={{ marginTop: 12 }}>
              <div className="field">
                <label htmlFor="detailPlayerMember">Add a member</label>
                <input
                  id="detailPlayerMember"
                  value={playerQuery}
                  onChange={(event) => {
                    const value = event.target.value;
                    setPlayerQuery(value);
                    void searchMembers(value);
                  }}
                  placeholder="Member number or name"
                />
              </div>
              <div className="field">
                <label htmlFor="detailPlayerGuest">Add a guest</label>
                <input
                  id="detailPlayerGuest"
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

            {playerResults.length > 0 ? (
              <table>
                <thead>
                  <tr>
                    <th>Member</th>
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
                          onClick={() => void addPlayer(member.id, null)}
                          disabled={busy}
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
                    <th>Guest</th>
                    <th>Reference</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  {guestResults.map((guest) => (
                    <tr key={guest.id}>
                      <td>{guest.fullName}</td>
                      <td>{guest.reference}</td>
                      <td className="row-actions">
                        <button
                          type="button"
                          className="link-button"
                          onClick={() => void addPlayer(null, guest.id)}
                          disabled={busy}
                        >
                          Add
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            ) : null}
          </>
        ) : null}
      </div>

      <div className="card" style={{ marginTop: 16 }}>
        <h3>Charges</h3>
        {booking.charges.length === 0 ? (
          <div className="empty-state">No charges on this booking yet.</div>
        ) : (
          <table>
            <thead>
              <tr>
                <th>Description</th>
                <th>Type</th>
                <th>Amount</th>
                <th>Status</th>
                <th>Transaction</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {booking.charges.map((charge) => (
                <tr key={charge.id}>
                  <td>{charge.description}</td>
                  <td>{charge.chargeType}</td>
                  <td>{money(charge.amount, charge.currencyCode)}</td>
                  <td>
                    <span className={chargePill(charge.status)}>{charge.status}</span>
                  </td>
                  <td>{charge.accountTransactionId ? charge.accountTransactionId.slice(0, 8) : '—'}</td>
                  <td className="row-actions">
                    {charge.status === 'Pending' && canCharge ? (
                      <button
                        type="button"
                        className="link-button"
                        onClick={() => void waiveCharge(charge.id)}
                        disabled={busy}
                      >
                        Waive
                      </button>
                    ) : null}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}

        {canCharge ? (
          <>
            <div className="toolbar" style={{ marginTop: 12 }}>
              <div className="field">
                <label htmlFor="chargeProductSearch">Catalogue product</label>
                <input
                  id="chargeProductSearch"
                  value={productQuery}
                  onChange={(event) => {
                    const value = event.target.value;
                    setProductQuery(value);
                    void searchProducts(value);
                  }}
                  placeholder="Cart, caddie, rental…"
                />
              </div>
            </div>

            {productResults.length > 0 ? (
              <table>
                <thead>
                  <tr>
                    <th>Product</th>
                    <th>SKU</th>
                    <th>Category</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  {productResults.map((product) => (
                    <tr key={product.id}>
                      <td>{product.name}</td>
                      <td>{product.sku}</td>
                      <td>{product.categoryName}</td>
                      <td className="row-actions">
                        <button
                          type="button"
                          className="link-button"
                          onClick={() => {
                            setChargeProductId(product.id);
                            setProductQuery(product.name);
                            setProductResults([]);
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

            <div className="toolbar" style={{ marginTop: 12 }}>
              <div className="field">
                <label htmlFor="chargeQuantity">Quantity</label>
                <input
                  id="chargeQuantity"
                  type="number"
                  min={1}
                  value={chargeQuantity}
                  onChange={(event) => setChargeQuantity(event.target.value)}
                />
              </div>
              <div className="field">
                <label htmlFor="chargePlayer">Charge to player</label>
                <select
                  id="chargePlayer"
                  value={chargePlayerId}
                  onChange={(event) => setChargePlayerId(event.target.value)}
                >
                  <option value="">Whole booking</option>
                  {booking.players.map((player) => (
                    <option key={player.id} value={player.id}>
                      {player.position}. {player.playerType === 'Member' ? player.memberName : player.guestName}
                    </option>
                  ))}
                </select>
              </div>
              <div className="field">
                <label htmlFor="chargeDescription">Description</label>
                <input
                  id="chargeDescription"
                  value={chargeDescription}
                  onChange={(event) => setChargeDescription(event.target.value)}
                />
              </div>
              <button
                type="button"
                className="btn"
                onClick={() => void addCharge()}
                disabled={busy || !chargeProductId}
              >
                Add charge
              </button>
            </div>
          </>
        ) : null}

        {canCharge && pendingCharges.length > 0 ? (
          <div className="row-actions" style={{ marginTop: 12 }}>
            <button type="button" className="btn" onClick={() => void postCharges()} disabled={busy}>
              Post pending charges
            </button>
          </div>
        ) : null}
      </div>
    </>
  );
}
