import { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { formatZurichDatetime, formatZurichTime } from '../utils/zurichTime';
import { clockIn, clockOut, clockInRetrospective, clockOutRetrospective, getHeartbeat, getHistory } from '../api/attendance';
import type { HeartbeatResponse, AttendanceEntry, Anomaly } from '../types';
import RetrospectiveModal from '../components/RetrospectiveModal';
import StatusBadge from '../components/StatusBadge';

export default function Dashboard() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  const [heartbeat, setHeartbeat] = useState<HeartbeatResponse | null>(null);
  const [entries, setEntries] = useState<AttendanceEntry[]>([]);
  const [anomalies, setAnomalies] = useState<Anomaly[]>([]);
  const [totalHours, setTotalHours] = useState(0);
  const [loading, setLoading] = useState(false);
  const [actionError, setActionError] = useState('');
  const [actionSuccess, setActionSuccess] = useState('');
  const [retroModal, setRetroModal] = useState<'ClockIn' | 'ClockOut' | null>(null);
  const [serviceDown, setServiceDown] = useState(false);

  const fetchHeartbeat = useCallback(async () => {
    try {
      const res = await getHeartbeat();
      setHeartbeat(res.data);
      setServiceDown(false);
    } catch (err: any) {
      if (err.response?.status === 503) setServiceDown(true);
    }
  }, []);

  const fetchHistory = useCallback(async () => {
    try {
      const from = new Date();
      from.setDate(from.getDate() - 30);
      const res = await getHistory(from.toISOString(), new Date().toISOString());
      setEntries(res.data.entries);
      setTotalHours(res.data.totalWorkedHours);
      setAnomalies(res.data.anomalies);
    } catch {}
  }, []);

  useEffect(() => {
    fetchHeartbeat();
    fetchHistory();
    const interval = setInterval(fetchHeartbeat, 2 * 60 * 1000);
    return () => clearInterval(interval);
  }, [fetchHeartbeat, fetchHistory]);

  const handleClockIn = async () => {
    setLoading(true);
    setActionError('');
    setActionSuccess('');
    try {
      await clockIn();
      setActionSuccess('Clocked in successfully!');
      await fetchHeartbeat();
      await fetchHistory();
    } catch (err: any) {
      if (err.response?.status === 503)
        setActionError('Time service unavailable — please try again in a moment');
      else
        setActionError(err.response?.data?.error ?? 'Failed to clock in');
    } finally {
      setLoading(false);
    }
  };

  const handleClockOut = async () => {
    setLoading(true);
    setActionError('');
    setActionSuccess('');
    try {
      const res = await clockOut();
      setActionSuccess(`Clocked out. Worked ${res.data.workedHoursToday?.toFixed(2)}h today`);
      await fetchHeartbeat();
      await fetchHistory();
    } catch (err: any) {
      if (err.response?.status === 503)
        setActionError('Time service unavailable — please try again in a moment');
      else
        setActionError(err.response?.data?.error ?? 'Failed to clock out');
    } finally {
      setLoading(false);
    }
  };

  const handleRetrospective = async (type: 'ClockIn' | 'ClockOut', timestamp: string, reason: string) => {
    if (type === 'ClockIn') await clockInRetrospective(timestamp, reason);
    else await clockOutRetrospective(timestamp, reason);
    await fetchHistory();
    setActionSuccess('Retrospective request submitted — awaiting admin approval');
  };

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  return (
    <div className="min-h-screen bg-gray-50">
      {/* Header */}
      <header className="bg-white shadow-sm">
        <div className="max-w-5xl mx-auto px-4 py-4 flex justify-between items-center">
          <div>
            <h1 className="text-xl font-bold text-gray-800">Attendance System</h1>
            <p className="text-sm text-gray-500">Welcome, {user?.username}</p>
          </div>
          <button onClick={handleLogout} className="text-sm text-gray-600 hover:text-gray-900">
            Sign out
          </button>
        </div>
      </header>

      <main className="max-w-5xl mx-auto px-4 py-6 space-y-6">
        {/* Service down warning */}
        {serviceDown && (
          <div className="bg-red-50 border border-red-200 rounded p-3 text-red-700 text-sm">
            ⚠️ Time service is currently unavailable. Clock-in/out is disabled.
          </div>
        )}

        {/* Alerts from heartbeat */}
        {heartbeat?.realtimeAlerts.map((alert, i) => (
          <div key={i} className={`border rounded p-3 text-sm ${
            alert.severity === 'warning'
              ? 'bg-yellow-50 border-yellow-200 text-yellow-800'
              : 'bg-blue-50 border-blue-200 text-blue-800'
          }`}>
            ⚠️ {alert.message}
          </div>
        ))}

        {/* Status Card */}
        <div className="bg-white rounded-lg shadow p-6">
          <div className="flex flex-wrap gap-6 mb-6">
            <div>
              <p className="text-sm text-gray-500">Status</p>
              <p className={`text-lg font-semibold ${heartbeat?.isClockedIn ? 'text-green-600' : 'text-gray-600'}`}>
                {heartbeat?.isClockedIn ? '● Clocked In' : '○ Not Clocked In'}
              </p>
            </div>
            <div>
              <p className="text-sm text-gray-500">Hours Today</p>
              <p className="text-lg font-semibold text-gray-800">
                {heartbeat?.workedHoursToday?.toFixed(2) ?? '0.00'}h
              </p>
            </div>
            <div>
              <p className="text-sm text-gray-500">Zurich Time</p>
              <p className="text-lg font-semibold text-gray-800">
                {heartbeat?.currentZurichTime
                  ? formatZurichTime(heartbeat.currentZurichTime)
                  : '--:--'}
              </p>
            </div>
            <div>
              <p className="text-sm text-gray-500">Hours (30 days)</p>
              <p className="text-lg font-semibold text-gray-800">{totalHours.toFixed(2)}h</p>
            </div>
          </div>

          {/* Action buttons */}
          <div className="flex flex-wrap gap-3">
            <button
              onClick={handleClockIn}
              disabled={loading || heartbeat?.isClockedIn || serviceDown}
              className="px-6 py-2 bg-green-600 text-white rounded hover:bg-green-700 disabled:opacity-50 font-medium"
            >
              Clock In
            </button>
            <button
              onClick={handleClockOut}
              disabled={loading || !heartbeat?.isClockedIn || serviceDown}
              className="px-6 py-2 bg-red-600 text-white rounded hover:bg-red-700 disabled:opacity-50 font-medium"
            >
              Clock Out
            </button>
            <button
              onClick={() => setRetroModal('ClockIn')}
              className="px-4 py-2 bg-gray-100 text-gray-700 rounded hover:bg-gray-200 font-medium text-sm"
            >
              + Retrospective Clock-In
            </button>
            <button
              onClick={() => setRetroModal('ClockOut')}
              className="px-4 py-2 bg-gray-100 text-gray-700 rounded hover:bg-gray-200 font-medium text-sm"
            >
              + Retrospective Clock-Out
            </button>
          </div>

          {actionSuccess && (
            <p className="mt-3 text-green-600 text-sm font-medium">{actionSuccess}</p>
          )}
          {actionError && (
            <p className="mt-3 text-red-600 text-sm">{actionError}</p>
          )}
        </div>

        {/* Anomalies */}
        {anomalies.length > 0 && (
          <div className="bg-white rounded-lg shadow p-6">
            <h2 className="text-lg font-semibold text-gray-800 mb-4">Anomalies Detected</h2>
            <div className="space-y-2">
              {anomalies.map((a, i) => (
                <div key={i} className="bg-yellow-50 border border-yellow-200 rounded p-3 text-sm">
                  <span className="font-medium capitalize">{a.type.replace(/_/g, ' ')}</span>
                  {' — '}{a.date}: {a.description}
                  {a.suggestedCorrection && (
                    <p className="text-gray-600 mt-1 text-xs">Suggestion: {a.suggestedCorrection}</p>
                  )}
                </div>
              ))}
            </div>
          </div>
        )}

        {/* History Table */}
        <div className="bg-white rounded-lg shadow p-6">
          <h2 className="text-lg font-semibold text-gray-800 mb-4">Attendance History (Last 30 days)</h2>
          {entries.length === 0 ? (
            <p className="text-gray-500 text-sm">No entries found.</p>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="text-left text-gray-500 border-b">
                    <th className="pb-2 pr-4">Date & Time</th>
                    <th className="pb-2 pr-4">Type</th>
                    <th className="pb-2 pr-4">Status</th>
                    <th className="pb-2">Reason</th>
                  </tr>
                </thead>
                <tbody>
                  {entries
                    .sort((a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime())
                    .map((e) => (
                    <tr key={e.id} className="border-b last:border-0 hover:bg-gray-50">
                      <td className="py-2 pr-4 font-mono text-xs">
                        {formatZurichDatetime(e.timestamp)}
                      </td>
                      <td className="py-2 pr-4">
                        <span className={`font-medium ${e.eventType === 'ClockIn' ? 'text-green-700' : 'text-red-700'}`}>
                          {e.eventType === 'ClockIn' ? '▶ In' : '◼ Out'}
                        </span>
                      </td>
                      <td className="py-2 pr-4">
                        <StatusBadge isRetrospective={e.isRetrospective} approvalStatus={e.approvalStatus} />
                      </td>
                      <td className="py-2 text-gray-600 text-xs">{e.retrospectiveReason ?? '—'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </main>

      {retroModal && (
        <RetrospectiveModal
          type={retroModal}
          onSubmit={(ts, reason) => handleRetrospective(retroModal, ts, reason)}
          onClose={() => setRetroModal(null)}
        />
      )}
    </div>
  );
}
