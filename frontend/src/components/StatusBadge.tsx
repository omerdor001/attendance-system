interface Props {
  isRetrospective: boolean;
  approvalStatus?: string | null;
}

export default function StatusBadge({ isRetrospective, approvalStatus }: Props) {
  if (!isRetrospective) {
    return (
      <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-green-100 text-green-800">
        ✓ Real-time
      </span>
    );
  }

  const map: Record<string, { bg: string; text: string; label: string }> = {
    Pending: { bg: 'bg-yellow-100', text: 'text-yellow-800', label: '⏳ Pending Approval' },
    Approved: { bg: 'bg-green-100', text: 'text-green-800', label: '✓ Approved' },
    Rejected: { bg: 'bg-red-100', text: 'text-red-800', label: '✗ Rejected' },
  };

  const style = map[approvalStatus ?? ''] ?? { bg: 'bg-gray-100', text: 'text-gray-800', label: 'Retrospective' };

  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium ${style.bg} ${style.text}`}>
      {style.label}
    </span>
  );
}
