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

export const exportEmployeePdf = (userId: number, from: string, to: string) =>
  api.get(`/admin/export-employee-pdf/${userId}`, { params: { from, to }, responseType: 'blob' });

export const exportAllEmployeesPdf = (from: string, to: string) =>
  api.get('/admin/export-all-employees-pdf', { params: { from, to }, responseType: 'blob' });
