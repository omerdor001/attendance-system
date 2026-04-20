import { useState } from 'react';
import type { FormEvent } from 'react';
import { zurichInputToUtcIso } from '../utils/zurichTime';

interface Props {
  type: 'ClockIn' | 'ClockOut';
  onSubmit: (timestamp: string, reason: string) => Promise<void>;
  onClose: () => void;
}

export default function RetrospectiveModal({ type, onSubmit, onClose }: Props) {
  const [timestamp, setTimestamp] = useState('');
  const [reason, setReason] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError('');
    if (reason.length < 10) {
      setError('Reason must be at least 10 characters');
      return;
    }
    setLoading(true);
    try {
      await onSubmit(zurichInputToUtcIso(timestamp), reason);
      onClose();
    } catch (err: any) {
      setError(err.response?.data?.error ?? 'Submission failed');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="fixed inset-0 bg-black bg-opacity-40 flex items-center justify-center z-50">
      <div className="bg-white rounded-lg shadow-xl p-6 w-full max-w-md">
        <h2 className="text-xl font-semibold mb-4">
          Add Retrospective {type === 'ClockIn' ? 'Clock-In' : 'Clock-Out'}
        </h2>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Date & Time</label>
            <input
              type="datetime-local"
              value={timestamp}
              onChange={(e) => setTimestamp(e.target.value)}
              required
              className="w-full border border-gray-300 rounded px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Reason (min 10 chars)</label>
            <textarea
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              required
              rows={3}
              placeholder="Explain why this retrospective entry is needed..."
              className="w-full border border-gray-300 rounded px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500 resize-none"
            />
            <p className="text-xs text-gray-500 mt-1">{reason.length} characters</p>
          </div>
          {error && <p className="text-red-500 text-sm">{error}</p>}
          <div className="flex gap-3 pt-2">
            <button
              type="submit"
              disabled={loading}
              className="flex-1 bg-blue-600 text-white py-2 rounded hover:bg-blue-700 disabled:opacity-50 font-medium"
            >
              {loading ? 'Submitting...' : 'Submit Request'}
            </button>
            <button
              type="button"
              onClick={onClose}
              className="flex-1 bg-gray-100 text-gray-700 py-2 rounded hover:bg-gray-200 font-medium"
            >
              Cancel
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
