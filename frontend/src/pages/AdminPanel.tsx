import { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { formatZurichDatetime, formatZurichShort } from '../utils/zurichTime';
import { getPendingApprovals, approveEntry, rejectEntry, getReports, exportEmployeePdf, exportAllEmployeesPdf } from '../api/admin';
import type { PendingEntry } from '../types';
import { register } from '../api/auth';

export default function AdminPanel() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  const [tab, setTab] = useState<'approvals' | 'reports' | 'register'>('approvals');
  const [reportView, setReportView] = useState<'attendance' | 'anomalies'>('attendance');
  const [pending, setPending] = useState<PendingEntry[]>([]);
  const [pendingCount, setPendingCount] = useState(0);
  const [reports, setReports] = useState<any[]>([]);
  const [actionMsg, setActionMsg] = useState('');
  const [rejectReason, setRejectReason] = useState<Record<number, string>>({});
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedMonth, setSelectedMonth] = useState(() => {
    const now = new Date();
    return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`;
  });

  const [pdfLoading, setPdfLoading] = useState<Record<number, boolean>>({});
  const [allPdfLoading, setAllPdfLoading] = useState(false);

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

  const availableMonths = (() => {
    const months: { value: string; label: string }[] = [];
    const now = new Date();
    for (let i = 0; i < 12; i++) {
      const d = new Date(now.getFullYear(), now.getMonth() - i, 1);
      const value = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`;
      const label = d.toLocaleString('en-US', { month: 'long', year: 'numeric' });
      months.push({ value, label });
    }
    return months;
  })();

  const fetchReports = useCallback(async () => {
    try {
      const [year, month] = selectedMonth.split('-').map(Number);
      const from = new Date(year, month - 1, 1).toISOString();
      const to = new Date(year, month, 0, 23, 59, 59).toISOString();
      const res = await getReports(from, to);
      setReports(res.data.employees);
    } catch {}
  }, [selectedMonth]);

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

  const downloadBlob = (blob: Blob, filename: string) => {
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    a.click();
    URL.revokeObjectURL(url);
  };

  const getMonthRange = () => {
    const [year, month] = selectedMonth.split('-').map(Number);
    const from = new Date(year, month - 1, 1).toISOString();
    const to = new Date(year, month, 0, 23, 59, 59).toISOString();
    const lastDay = new Date(year, month, 0).getDate();
    const fromStr = `${year}-${String(month).padStart(2, '0')}-01`;
    const toStr = `${year}-${String(month).padStart(2, '0')}-${String(lastDay).padStart(2, '0')}`;
    return { from, to, fromStr, toStr };
  };

  const handleExportEmployeePdf = async (userId: number, username: string) => {
    setPdfLoading(prev => ({ ...prev, [userId]: true }));
    try {
      const { from, to, fromStr, toStr } = getMonthRange();
      const res = await exportEmployeePdf(userId, from, to);
      const safeName = username.replace('.', '').replace(' ', '_');
      downloadBlob(res.data, `Employee_${safeName}_${fromStr}_to_${toStr}.pdf`);
    } catch {
      setActionMsg('Failed to generate employee PDF report');
    } finally {
      setPdfLoading(prev => ({ ...prev, [userId]: false }));
    }
  };

  const handleExportAllPdf = async () => {
    setAllPdfLoading(true);
    try {
      const { from, to, fromStr, toStr } = getMonthRange();
      const res = await exportAllEmployeesPdf(from, to);
      downloadBlob(res.data, `AllEmployees_Report_${fromStr}_to_${toStr}.pdf`);
    } catch {
      setActionMsg('Failed to generate all-employees PDF report');
    } finally {
      setAllPdfLoading(false);
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
          <div className="flex items-center gap-4">
            <button onClick={() => navigate('/dashboard')} className="text-sm text-blue-600 hover:text-blue-800 font-medium">
              My Attendance
            </button>
            <button onClick={() => { logout(); navigate('/login'); }} className="text-sm text-gray-600 hover:text-gray-900">
              Sign out
            </button>
          </div>
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
        {tab === 'reports' && (() => {
          const filtered = reports.filter((emp: any) =>
            emp.username.toLowerCase().includes(searchQuery.toLowerCase())
          );
          const selectedLabel = availableMonths.find(m => m.value === selectedMonth)?.label ?? selectedMonth;
          return (
            <div className="space-y-4">
              {/* Controls */}
              <div className="bg-white rounded-lg shadow p-4 flex flex-wrap gap-3 items-center">
                <input
                  type="text"
                  placeholder="Search employee by name..."
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                  className="border border-gray-300 rounded px-3 py-2 text-sm flex-1 min-w-48 focus:outline-none focus:ring-2 focus:ring-blue-500"
                />
                <select
                  value={selectedMonth}
                  onChange={(e) => setSelectedMonth(e.target.value)}
                  className="border border-gray-300 rounded px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                >
                  {availableMonths.map(m => (
                    <option key={m.value} value={m.value}>{m.label}</option>
                  ))}
                </select>
                <button
                  onClick={handleExportAllPdf}
                  disabled={allPdfLoading || filtered.length === 0}
                  className="px-4 py-2 bg-indigo-600 text-white text-sm rounded hover:bg-indigo-700 disabled:opacity-50 font-medium flex items-center gap-2 whitespace-nowrap"
                >
                  {allPdfLoading ? (
                    <span className="inline-block w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin" />
                  ) : (
                    <span>⬇</span>
                  )}
                  {allPdfLoading ? 'Generating...' : 'Export All to PDF'}
                </button>
              </div>

              {/* Report View Toggle */}
              <div className="flex gap-1 bg-gray-100 rounded-lg p-1 w-fit">
                <button
                  onClick={() => setReportView('attendance')}
                  className={`px-4 py-2 rounded-md text-sm font-medium transition-colors ${
                    reportView === 'attendance'
                      ? 'bg-white text-blue-700 shadow-sm'
                      : 'text-gray-600 hover:text-gray-800'
                  }`}
                >
                  Attendance Report
                </button>
                <button
                  onClick={() => setReportView('anomalies')}
                  className={`px-4 py-2 rounded-md text-sm font-medium transition-colors ${
                    reportView === 'anomalies'
                      ? 'bg-white text-orange-700 shadow-sm'
                      : 'text-gray-600 hover:text-gray-800'
                  }`}
                >
                  Anomalies Report
                </button>
              </div>

              {/* Attendance Report View */}
              {reportView === 'attendance' && (
                <>
                  {/* Monthly Summary Table */}
                  <div className="bg-white rounded-lg shadow p-6">
                    <h2 className="text-lg font-semibold mb-4">Monthly Summary — {selectedLabel}</h2>
                    {filtered.length === 0 ? (
                      <p className="text-gray-500 text-sm">No data.</p>
                    ) : (
                      <table className="w-full text-sm">
                        <thead>
                          <tr className="text-left text-gray-500 border-b">
                            <th className="pb-2 pr-4">Employee Name</th>
                            <th className="pb-2 pr-4">Month</th>
                            <th className="pb-2">Total Hours Worked</th>
                          </tr>
                        </thead>
                        <tbody>
                          {filtered.map((emp: any) => (
                            <tr key={emp.userId} className="border-b last:border-0 hover:bg-gray-50">
                              <td className="py-2 pr-4 font-medium text-gray-800">{emp.username}</td>
                              <td className="py-2 pr-4 text-gray-600">{selectedLabel}</td>
                              <td className="py-2 font-semibold text-gray-800">{emp.totalWorkedHours.toFixed(2)}h</td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    )}
                  </div>

                  {/* Detailed Per-Employee Events */}
                  <div className="bg-white rounded-lg shadow p-6">
                    <h2 className="text-lg font-semibold mb-4">Attendance Details</h2>
                    {filtered.length === 0 ? (
                      <p className="text-gray-500 text-sm">No data.</p>
                    ) : (
                      <div className="space-y-6">
                        {filtered.map((emp: any) => (
                          <div key={emp.userId} className="border rounded-lg p-4">
                            <div className="flex justify-between items-center mb-3">
                              <h3 className="font-semibold text-gray-800">{emp.username}</h3>
                              <div className="flex items-center gap-3">
                                <span className="text-sm font-medium text-gray-600">
                                  Total: <strong>{emp.totalWorkedHours.toFixed(2)}h</strong>
                                </span>
                                <button
                                  onClick={() => handleExportEmployeePdf(emp.userId, emp.username)}
                                  disabled={pdfLoading[emp.userId]}
                                  className="px-3 py-1 bg-indigo-600 text-white text-xs rounded hover:bg-indigo-700 disabled:opacity-50 font-medium flex items-center gap-1"
                                >
                                  {pdfLoading[emp.userId] ? (
                                    <span className="inline-block w-3 h-3 border-2 border-white border-t-transparent rounded-full animate-spin" />
                                  ) : (
                                    <span>⬇</span>
                                  )}
                                  {pdfLoading[emp.userId] ? 'Generating...' : 'Export PDF'}
                                </button>
                              </div>
                            </div>
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
                </>
              )}

              {/* Anomalies Report View */}
              {reportView === 'anomalies' && (
                <div className="bg-white rounded-lg shadow p-6">
                  <h2 className="text-lg font-semibold mb-4">Anomalies — {selectedLabel}</h2>
                  {filtered.length === 0 ? (
                    <p className="text-gray-500 text-sm">No data.</p>
                  ) : (
                    <div className="space-y-4">
                      {filtered.map((emp: any) => {
                        if (!emp.anomalies?.length) return null;
                        return (
                          <div key={emp.userId} className="border rounded-lg p-4">
                            <h3 className="font-semibold text-gray-800 mb-2">{emp.username}</h3>
                            <div className="space-y-2">
                              {emp.anomalies.map((a: any, i: number) => (
                                <div key={i} className="bg-orange-50 border border-orange-200 rounded p-3">
                                  <div className="flex justify-between items-start">
                                    <span className="text-sm font-medium text-orange-800 capitalize">
                                      {a.type?.replace(/_/g, ' ')}
                                    </span>
                                    <span className="text-xs text-orange-600">{a.date}</span>
                                  </div>
                                  <p className="text-sm text-orange-700 mt-1">{a.description}</p>
                                  {a.suggestedCorrection && (
                                    <p className="text-xs text-gray-600 mt-1">
                                      Suggestion: {a.suggestedCorrection}
                                    </p>
                                  )}
                                </div>
                              ))}
                            </div>
                          </div>
                        );
                      })}
                      {filtered.every((emp: any) => !emp.anomalies?.length) && (
                        <p className="text-gray-500 text-sm">No anomalies detected for this period.</p>
                      )}
                    </div>
                  )}
                </div>
              )}
            </div>
          );
        })()}

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
