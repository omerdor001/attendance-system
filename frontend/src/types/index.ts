export interface User {
  id: number;
  username: string;
  role: 'Employee' | 'Admin';
}

export interface AuthState {
  token: string | null;
  user: User | null;
}

export interface AttendanceEntry {
  id: number;
  eventType: 'ClockIn' | 'ClockOut';
  timestamp: string;
  isRetrospective: boolean;
  retrospectiveReason?: string;
  approvalStatus?: 'Pending' | 'Approved' | 'Rejected' | null;
  approvedAt?: string;
}

export interface Alert {
  type: string;
  message: string;
  severity: 'info' | 'warning' | 'error';
}

export interface HeartbeatResponse {
  currentZurichTime: string;
  isClockedIn: boolean;
  workedHoursToday: number;
  realtimeAlerts: Alert[];
  pendingApprovalCount: number;
}

export interface Anomaly {
  type: string;
  date: string;
  description: string;
  confidence: number;       //IDK if needed
  suggestedCorrection?: string;
}

export interface HistoryResponse {
  entries: AttendanceEntry[];
  totalWorkedHours: number;    //IDK if needed
  anomalies: Anomaly[];        //IDK if needed
}

export interface PendingEntry {
  id: number;
  employeeName: string;
  eventType: 'ClockIn' | 'ClockOut';
  requestedTimestamp: string;
  reason: string;
  submittedAt: string;
}
