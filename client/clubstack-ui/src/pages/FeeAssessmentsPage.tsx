import { useEffect, useState, type FormEvent } from 'react';
import { describeError, json } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import type { FeeAssessment, FeeAssessmentSearch, Member, MemberDetail, MembershipCategory, MembershipFee } from '../types';
import { Status } from './DashboardPage';

const ASSESSMENT_STATUSES = ['', 'Outstanding', 'Adjusted', 'Voided'];

export function FeeAssessmentsPage() {
  const { can } = useAuth();
  const [status, setStatus] = useState('');
  const [result, setResult] = useState<FeeAssessmentSearch | null>(null);
  const [members, setMembers] = useState<Member[]>([]);
  const [memberQuery, setMemberQuery] = useState('');
  const [detail, setDetail] = useState<MemberDetail | null>(null);
  const [fees, setFees] = useState<MembershipFee[]>([]);
  const [assessForm, setAssessForm] = useState({ membershipId: '', feeType: '' });
  const [adjustTarget, setAdjustTarget] = useState<FeeAssessment | null>(null);
  const [adjustAmount, setAdjustAmount] = useState('');
  const [adjustReason, setAdjustReason] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  const canAssess = can('Membership.Fee.Assess');
  const canAdjust = can('Membership.Fee.Adjust');

  const load = async (searchStatus = status) => {
    setError('');
    try {
      const parameters = new URLSearchParams();
      if (searchStatus) parameters.set('status', searchStatus);
      setResult(await json.get<FeeAssessmentSearch>(`/api/v1/membership-fee-assessments?${parameters.toString()}`));
    } catch (err) {
      setError(describeError(err));
    }
  };

  useEffect(() => {
    void load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const searchMembers = async (input: string) => {
    try {
      const result = await json.get<{ items: Member[] }>(`/api/v1/members?query=${encodeURIComponent(input)}`);
      setMembers(result.items);
    } catch (err) {
      setError(describeError(err));
    }
  };

  const pickMember = async (memberId: string) => {
    setDetail(null);
    setFees([]);
    setAssessForm({ membershipId: '', feeType: '' });
    try {
      const loaded = await json.get<MemberDetail>(`/api/v1/members/${memberId}`);
      setDetail(loaded);
      setMemberQuery(loaded.memberNumber);
      if (loaded.memberships.length === 1) {
        setAssessForm((current) => ({ ...current, membershipId: loaded.memberships[0].id }));
        void loadCategoryFees(loaded.memberships[0].categoryId);
      }
    } catch (err) {
      setError(describeError(err));
    }
  };

  const loadCategoryFees = async (categoryId: string) => {
    try {
      const categories = await json.get<MembershipCategory[]>('/api/v1/membership-categories');
      const category = categories.find((c) => c.id === categoryId);
      setFees(category ? await json.get<MembershipFee[]>(`/api/v1/membership-categories/${category.id}/fees`) : []);
    } catch (err) {
      setError(describeError(err));
    }
  };

  const assess = async (event: FormEvent) => {
    event.preventDefault();
    if (!assessForm.membershipId || !assessForm.feeType) {
      return;
    }
    setBusy(true);
    try {
      await json.post<FeeAssessment>('/api/v1/membership-fee-assessments', {
        membershipId: assessForm.membershipId,
        feeType: assessForm.feeType,
        assessmentDateUtc: null,
      });
      await load();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const adjust = async (event: FormEvent) => {
    event.preventDefault();
    if (!adjustTarget || !adjustAmount || !adjustReason) {
      return;
    }
    setBusy(true);
    try {
      await json.post<FeeAssessment>(`/api/v1/membership-fee-assessments/${adjustTarget.id}/adjust`, {
        amount: Number(adjustAmount),
        reason: adjustReason,
      });
      setAdjustTarget(null);
      setAdjustAmount('');
      setAdjustReason('');
      await load();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const voidAssessment = async (assessment: FeeAssessment) => {
    const reason = window.prompt(`Reason for voiding ${assessment.feeType} on ${assessment.memberName}?`);
    if (reason === null) {
      return;
    }
    setBusy(true);
    try {
      await json.post<FeeAssessment>(`/api/v1/membership-fee-assessments/${assessment.id}/void`, { reason });
      await load();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const feeTypes = Array.from(new Set([...fees.map((fee) => fee.feeType), ...(result?.items.map((item) => item.feeType) ?? [])]));

  return (
    <>
      <h1 className="page-title">Fee assessments</h1>
      <p className="page-subtitle">Record traceable membership fee assessments. Settlement is handled in a later slice.</p>

      {error ? <div className="alert alert-error">{error}</div> : null}

      <div className="toolbar">
        <select aria-label="Status" value={status} onChange={(event) => setStatus(event.target.value)}>
          {ASSESSMENT_STATUSES.map((value) => (
            <option key={value || 'all'} value={value}>
              {value || 'All statuses'}
            </option>
          ))}
        </select>
        <button className="btn" onClick={() => void load()}>
          Filter
        </button>
      </div>

      {canAssess ? (
        <div className="card">
          <h3>Assess a fee</h3>
          <div className="field">
            <label id="memberPickLabel">Member</label>
            <div className="input-row">
              <input
                placeholder="Search members…"
                value={memberQuery}
                onChange={(event) => {
                  setMemberQuery(event.target.value);
                  void searchMembers(event.target.value);
                }}
              />
              <select aria-labelledby="memberPickLabel" value={detail?.id ?? ''} onChange={(event) => void pickMember(event.target.value)}>
                <option value="">Select a member</option>
                {members.map((member) => (
                  <option key={member.id} value={member.id}>
                    {member.memberNumber} · {member.legalName}
                  </option>
                ))}
              </select>
            </div>
          </div>
          {detail ? (
            <form className="form-grid" style={{ marginTop: 12 }} onSubmit={(event) => void assess(event)}>
              <div className="field">
                <label htmlFor="membershipPick">Membership period</label>
                <select
                  id="membershipPick"
                  required
                  value={assessForm.membershipId}
                  onChange={(event) => {
                    const membership = detail.memberships.find((m) => m.id === event.target.value);
                    setAssessForm({ ...assessForm, membershipId: event.target.value });
                    if (membership) {
                      void loadCategoryFees(membership.categoryId);
                    }
                  }}
                >
                  <option value="">Select a membership</option>
                  {detail.memberships.map((membership) => (
                    <option key={membership.id} value={membership.id}>
                      {membership.categoryName} · {membership.status}
                    </option>
                  ))}
                </select>
              </div>
              <div className="field">
                <label htmlFor="feeTypePick">Fee type *</label>
                <input
                  id="feeTypePick"
                  required
                  maxLength={24}
                  list="feeTypes"
                  value={assessForm.feeType}
                  onChange={(event) => setAssessForm({ ...assessForm, feeType: event.target.value })}
                />
                <datalist id="feeTypes">
                  {feeTypes.map((type) => (
                    <option key={type} value={type} />
                  ))}
                </datalist>
              </div>
              <button type="submit" className="btn btn-primary" disabled={busy}>
                Assess
              </button>
            </form>
          ) : null}
        </div>
      ) : null}

      {result === null ? (
        <div className="empty-state">Loading assessments…</div>
      ) : result.items.length === 0 ? (
        <div className="empty-state">No assessments match the current filter.</div>
      ) : (
        <table>
          <thead>
            <tr>
              <th>Member</th>
              <th>Fee type</th>
              <th>Amount</th>
              <th>Status</th>
              <th>Assessment date</th>
              <th>Due</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {result.items.map((assessment) => (
              <tr key={assessment.id}>
                <td>
                  {assessment.memberName} ({assessment.membershipNumber})
                </td>
                <td>{assessment.feeType}</td>
                <td>
                  {assessment.amount.toLocaleString(undefined, { minimumFractionDigits: 2 })} {assessment.currencyCode}
                  {assessment.isProrated ? ' (pro-rated)' : ''}
                </td>
                <td>
                  <Status value={assessment.status} />
                </td>
                <td className="muted">{formatDate(assessment.assessmentDateUtc)}</td>
                <td className="muted">{assessment.dueDateUtc ? formatDate(assessment.dueDateUtc) : '—'}</td>
                <td>
                  <div className="row-actions">
                    {assessment.status === 'Outstanding' && canAdjust ? (
                      <>
                        <button className="btn" onClick={() => setAdjustTarget(assessment)}>
                          Adjust
                        </button>
                        <button className="btn btn-danger" onClick={() => void voidAssessment(assessment)}>
                          Void
                        </button>
                      </>
                    ) : null}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {adjustTarget ? (
        <div className="card">
          <h3>
            Adjust assessment — {adjustTarget.feeType} on {adjustTarget.memberName}
          </h3>
          <form className="form-grid" onSubmit={(event) => void adjust(event)}>
            <div className="field">
              <label htmlFor="adjustAmount">New amount</label>
              <input
                id="adjustAmount"
                type="number"
                min={0.01}
                step={0.01}
                required
                value={adjustAmount}
                onChange={(event) => setAdjustAmount(event.target.value)}
              />
            </div>
            <div className="field">
              <label htmlFor="adjustReason">Reason *</label>
              <input
                id="adjustReason"
                required
                maxLength={512}
                value={adjustReason}
                onChange={(event) => setAdjustReason(event.target.value)}
              />
            </div>
            <div className="input-row">
              <button type="submit" className="btn btn-primary" disabled={busy}>
                Save adjustment
              </button>
              <button type="button" className="btn" onClick={() => setAdjustTarget(null)}>
                Cancel
              </button>
            </div>
          </form>
        </div>
      ) : null}
    </>
  );
}

function formatDate(value: string): string {
  if (!value) {
    return '—';
  }
  return new Date(value).toLocaleDateString(undefined, { day: '2-digit', month: 'short', year: 'numeric' });
}