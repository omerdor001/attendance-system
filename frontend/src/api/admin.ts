import api from './client';
import type { PendingEntry } from '../types';

export const getPendingApprovals = () =>
  api.get<{ pendingCount: number; entries: PendingEntry[] }>('/admin/pending-approvals');

export const approveEntry = (eventId: number) =>
  api.post(`/admin/approve-retrospective/${eventId}`);

export const rejectEntry = (eventId: number, rejectionReason: string) =>
  api.post(`/admin/reject-retrospective/${eventId}`, { rejectionReason });

export const getReports = (from?: string, to?: string, userId?: number) =>
  api.get('/admin/reports', { params: { from, to, userId } });
