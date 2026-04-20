import { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { formatZurichDatetime, formatZurichShort } from '../utils/zurichTime';
import { getPendingApprovals, approveEntry, rejectEntry, getReports } from '../api/admin';
import type { PendingEntry } from '../types';
import { register } from '../api/auth';

export default function AdminPanel() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  const [tab, setTab] = useState<'approvals' | 'reports' | 'register'>('approvals');
  const [pending, setPending] = useState<PendingEntry[]>([]);
  const [pendingCount, setPendingCount] = useState(0);
  const [reports, setReports] = useState<any[]>([]);
  const [actionMsg, setActionMsg] = useState('');
  const [rejectReason, setRejectReason] = useState<Record<number, string>>({});

  // Register form state
  const [regUsername, setRegUsername] = useState('');
  const [regPassword, setRegPassword] = useState('');
  const [regRole, setRegRole] = useState('Employee');
  const [regShiftStart, setRegShiftStart] = useState('08:00:00');
  const [regShiftEnd, setRegShiftEnd] = useState('17:00:00');
  const [regError, setRegError] = useState('');
  const [regSuccess, setRegSuccess] = useState('');

  const fetchPending = useCallback(async () => {
    try {
      const res = await getPendingApprovals();
      setPending(res.data.entries);
      setPendingCount(res.data.pendingCount);
    } catch {}
  }, []);

  const fetchReports = useCallback(async () => {
    try {
      const res = await getReports();
      setReports(res.data.employees);
    } catch {}
  }, []);

  useEffect(() => {
    fetchPending();
    fetchReports();
  }, [fetchPending, fetchReports]);

  const handleApprove = async (id: number) => {
    try {
      await approveEntry(id);
      setActionMsg(`Entry #${id} approved`);
      await fetchPending();
    } catch (err: any) {
      setActionMsg(err.response?.data?.error ?? 'Failed to approve');
    }
  };

  const handleReject = async (id: number) => {
    const reason = rejectReason[id] ?? '';
    if (!reason) { setActionMsg('Please enter a rejection reason'); return; }
    try {
      await rejectEntry(id, reason);
      setActionMsg(`Entry #${id} rejected`);
      await fetchPending();
    } catch (err: any) {
      setActionMsg(err.response?.data?.error ?? 'Failed to reject');
    }
  };

  const handleRegister = async (e: React.FormEvent) => {
    e.preventDefault();
    setRegError('');
    setRegSuccess('');
    try {
      await register({
        username: regUsername,
        password: regPassword,
        role: regRole,
        expectedShiftStartTime: regShiftStart,
        expectedShiftEndTime: regShiftEnd,
      });
      setRegSuccess(`User "${regUsername}" created successfully`);
      setRegUsername('');
      setRegPassword('');
    } catch (err: any) {
      setRegError(err.response?.data?.error ?? 'Registration failed');
    }
  };

  return (
    <div className="min-h-screen bg-gray-50">
      <header className="bg-white shadow-sm">
        <div className="max-w-6xl mx-auto px-4 py-4 flex justify-between items-center">
          <div>
            <h1 className="text-xl font-bold text-gray-800">Admin Panel</h1>
            <p className="text-sm text-gray-500">{user?.username}</p>
          </div>
          <button onClick={() => { logout(); navigate('/login'); }} className="text-sm text-gray-600 hover:text-gray-900">
            Sign out
          </button>
        </div>
      </header>

      <main className="max-w-6xl mx-auto px-4 py-6">
        {/* Tabs */}
        <div className="flex gap-2 mb-6">
          {(['approvals', 'reports', 'register'] as const).map((t) => (
            <button
              key={t}
              onClick={() => setTab(t)}
              className={`px-4 py-2 rounded font-medium text-sm capitalize relative ${
                tab === t ? 'bg-blue-600 text-white' : 'bg-white text-gray-600 hover:bg-gray-100'
              }`}
            >
              {t === 'approvals' ? 'Pending Approvals' : t === 'reports' ? 'Reports' : 'Register User'}
              {t === 'approvals' && pendingCount > 0 && (
                <span className="absolute -top-1 -right-1 bg-red-500 text-white text-xs rounded-full w-5 h-5 flex items-center justify-center">
                  {pendingCount}
                </span>
              )}
            </button>
          ))}
        </div>

        {actionMsg && (
          <div className="mb-4 p-3 bg-blue-50 border border-blue-200 rounded text-blue-800 text-sm">
            {actionMsg}
          </div>
        )}

        {/* Pending Approvals Tab */}
        {tab === 'approvals' && (
          <div className="bg-white rounded-lg shadow p-6">
            <h2 className="text-lg font-semibold mb-4">Pending Retrospective Approvals</h2>
            {pending.length === 0 ? (
              <p className="text-gray-500 text-sm">No pending approvals.</p>
            ) : (
              <div className="space-y-4">
                {pending.map((e) => (
                  <div key={e.id} className="border border-yellow-200 bg-yellow-50 rounded-lg p-4">
                    <div className="flex flex-wrap gap-4 mb-3">
                      <div>
                        <p className="text-xs text-gray-500">Employee</p>
                        <p className="font-semibold">{e.employeeName}</p>
                      </div>
                      <div>
                        <p className="text-xs text-gray-500">Type</p>
                        <p className={`font-semibold ${e.eventType === 'ClockIn' ? 'text-green-700' : 'text-red-700'}`}>
                          {e.eventType === 'ClockIn' ? '▶ Clock-In' : '◼ Clock-Out'}
                        </p>
                      </div>
                      <div>
                        <p className="text-xs text-gray-500">Requested Time</p>
                        <p className="font-medium font-mono text-sm">
                          {formatZurichDatetime(e.requestedTimestamp)}
                        </p>
                      </div>
                      <div>
                        <p className="text-xs text-gray-500">Submitted</p>
                        <p className="text-sm">{formatZurichShort(e.submittedAt)}</p>
                      </div>
                    </div>
                    <p className="text-sm text-gray-700 mb-3">
                      <span className="font-medium">Reason: </span>{e.reason}
                    </p>
                    <div className="flex flex-wrap gap-2 items-center">
                      <button
                        onClick={() => handleApprove(e.id)}
                        className="px-4 py-1.5 bg-green-600 text-white text-sm rounded hover:bg-green-700 font-medium"
                      >
                        Approve
                      </button>
                      <input
                        type="text"
                        placeholder="Rejection reason..."
                        value={rejectReason[e.id] ?? ''}
                        onChange={(ev) => setRejectReason((r) => ({ ...r, [e.id]: ev.target.value }))}
                        className="border border-gray-300 rounded px-2 py-1 text-sm flex-1 min-w-48"
                      />
                      <button
                        onClick={() => handleReject(e.id)}
                        className="px-4 py-1.5 bg-red-600 text-white text-sm rounded hover:bg-red-700 font-medium"
                      >
                        Reject
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}

        {/* Reports Tab */}
        {tab === 'reports' && (
          <div className="bg-white rounded-lg shadow p-6">
            <h2 className="text-lg font-semibold mb-4">Employee Reports (Last 30 days)</h2>
            {reports.length === 0 ? (
              <p className="text-gray-500 text-sm">No data.</p>
            ) : (
              <div className="space-y-6">
                {reports.map((emp: any) => (
                  <div key={emp.userId} className="border rounded-lg p-4">
                    <div className="flex justify-between items-center mb-3">
                      <h3 className="font-semibold text-gray-800">{emp.username}</h3>
                      <span className="text-sm font-medium text-gray-600">
                        Total: <strong>{emp.totalWorkedHours.toFixed(2)}h</strong>
                      </span>
                    </div>
                    {emp.anomalies?.length > 0 && (
                      <div className="mb-3 space-y-1">
                        {emp.anomalies.map((a: any, i: number) => (
                          <p key={i} className="text-xs text-orange-700 bg-orange-50 rounded px-2 py-1">
                            ⚠ {a.date}: {a.description}
                          </p>
                        ))}
                      </div>
                    )}
                    <div className="overflow-x-auto">
                      <table className="w-full text-sm">
                        <thead>
                          <tr className="text-left text-gray-500 border-b text-xs">
                            <th className="pb-1 pr-3">Date & Time</th>
                            <th className="pb-1 pr-3">Type</th>
                            <th className="pb-1">Status</th>
                          </tr>
                        </thead>
                        <tbody>
                          {emp.entries?.slice(0, 10).map((e: any) => (
                            <tr key={e.id} className="border-b last:border-0">
                              <td className="py-1 pr-3 font-mono text-xs">
                                {formatZurichShort(e.timestamp)}
                              </td>
                              <td className={`py-1 pr-3 text-xs font-medium ${
                                e.eventType === 'ClockIn' ? 'text-green-700' : 'text-red-700'
                              }`}>
                                {e.eventType === 'ClockIn' ? '▶ In' : '◼ Out'}
                              </td>
                              <td className="py-1 text-xs text-gray-500">
                                {e.isRetrospective ? `Retro (${e.approvalStatus})` : 'Real-time'}
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}

        {/* Register User Tab */}
        {tab === 'register' && (
          <div className="bg-white rounded-lg shadow p-6 max-w-md">
            <h2 className="text-lg font-semibold mb-4">Register New User</h2>
            <form onSubmit={handleRegister} className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Username</label>
                <input type="text" value={regUsername} onChange={(e) => setRegUsername(e.target.value)} required
                  className="w-full border border-gray-300 rounded px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500" />
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Password</label>
                <input type="password" value={regPassword} onChange={(e) => setRegPassword(e.target.value)} required
                  className="w-full border border-gray-300 rounded px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500" />
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Role</label>
                <select value={regRole} onChange={(e) => setRegRole(e.target.value)}
                  className="w-full border border-gray-300 rounded px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500">
                  <option value="Employee">Employee</option>
                  <option value="Admin">Admin</option>
                </select>
              </div>
              <div className="flex gap-3">
                <div className="flex-1">
                  <label className="block text-sm font-medium text-gray-700 mb-1">Shift Start</label>
                  <input type="time" value={regShiftStart.slice(0, 5)} step="60"
                    onChange={(e) => setRegShiftStart(e.target.value + ':00')}
                    className="w-full border border-gray-300 rounded px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500" />
                </div>
                <div className="flex-1">
                  <label className="block text-sm font-medium text-gray-700 mb-1">Shift End</label>
                  <input type="time" value={regShiftEnd.slice(0, 5)} step="60"
                    onChange={(e) => setRegShiftEnd(e.target.value + ':00')}
                    className="w-full border border-gray-300 rounded px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500" />
                </div>
              </div>
              {regError && <p className="text-red-500 text-sm">{regError}</p>}
              {regSuccess && <p className="text-green-600 text-sm font-medium">{regSuccess}</p>}
              <button type="submit"
                className="w-full bg-blue-600 text-white py-2 rounded hover:bg-blue-700 font-medium">
                Create User
              </button>
            </form>
          </div>
        )}
      </main>
    </div>
  );
}
