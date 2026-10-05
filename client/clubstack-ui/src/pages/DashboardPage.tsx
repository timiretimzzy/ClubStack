import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { describeError } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import type { MembershipApplication, Member } from '../types';

export function DashboardPage() {
  const { user, can } = useAuth();
  const [members, setMembers] = useState<Member[]>([]);
  const [applications, setApplications] = useState<MembershipApplication[]>([]);
  const [error, setError] = useState('');

  useEffect(() => {
    void (async () => {
      try {
        const [memberResult, applicationResult] = await Promise.all([
          can('Membership.Member.View') ? fetchJson<{ items: Member[] }>('/api/v1/members?pageSize=5') : null,
          can('Membership.Application.View') ? fetchJson<{ items: MembershipApplication[] }>('/api/v1/membership-applications?pageSize=5') : null,
        ]);
        setMembers(memberResult?.items ?? []);
        setApplications(applicationResult?.items ?? []);
      } catch (err) {
        setError(describeError(err));
      }
    })();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [can]);

  const openApplications = applications.filter((a) => a.status === 'UnderReview' || a.status === 'Submitted').length;

  return (
    <>
      <h1 className="page-title">Dashboard</h1>
      <p className="page-subtitle">Welcome back, {user?.displayName}. Recent club activity.</p>

      {error ? <div className="alert alert-error">{error}</div> : null}

      <div className="card">
        <h3>At a glance</h3>
        <div className="input-row">
          <div className="field">
            <span className="detail-grid">
              <span className="field-label">Members</span>
              <span className="field-value">{members.length > 0 ? 'Recent' : '0 visible'}</span>
            </span>
          </div>
          <div className="field">
            <span className="detail-grid">
              <span className="field-label">Open applications</span>
              <span className="field-value">{openApplications}</span>
            </span>
          </div>
        </div>
      </div>

      {can('Membership.Member.View') ? (
        <div className="card">
          <h3>Recent members</h3>
          {members.length === 0 ? (
            <div className="empty-state">No members yet. Approve an application to create the first member.</div>
          ) : (
            <table>
              <thead>
                <tr>
                  <th>Member</th>
                  <th>Status</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {members.map((member) => (
                  <tr key={member.id}>
                    <td>
                      <Link to={`/members/${member.id}`}>{member.memberNumber}</Link> — {member.legalName}
                    </td>
                    <td>
                      <Status value={member.status} />
                    </td>
                    <td>
                      <Link to={`/members/${member.id}`}>Open</Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      ) : null}

      {can('Membership.Application.View') ? (
        <div className="card">
          <h3>Recent applications</h3>
          {applications.length === 0 ? (
            <div className="empty-state">No applications yet.</div>
          ) : (
            <table>
              <thead>
                <tr>
                  <th>Applicant</th>
                  <th>Category</th>
                  <th>Status</th>
                </tr>
              </thead>
              <tbody>
                {applications.map((application) => (
                  <tr key={application.id}>
                    <td>{application.personName}</td>
                    <td>{application.categoryName}</td>
                    <td>
                      <Status value={application.status} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      ) : null}
    </>
  );
}

async function fetchJson<T>(path: string): Promise<T> {
  const response = await fetch(path, { credentials: 'include' });
  if (!response.ok) {
    throw new Error(`Request failed (${response.status}).`);
  }
  return (await response.json()) as T;
}

export function Status({ value }: { value: string }) {
  const tone = value === 'Active' || value === 'Approved' || value === 'Paid'
    ? 'ok'
    : value === 'Suspended' || value === 'Rejected' || value === 'Voided' || value === 'Cancelled'
      ? 'bad'
      : value === 'Pending' || value === 'Submitted' || value === 'UnderReview' || value === 'Expired'
        ? 'warn'
        : '';
  return <span className={`status-pill ${tone}`}>{value}</span>;
}