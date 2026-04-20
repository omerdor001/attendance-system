import api from './client';
import type { HeartbeatResponse, HistoryResponse } from '../types';

export const clockIn = () => api.post('/attendance/clock-in');
export const clockOut = () => api.post('/attendance/clock-out');

export const clockInRetrospective = (timestamp: string, reason: string) =>
  api.post('/attendance/clock-in-retrospective', { timestamp, reason });

export const clockOutRetrospective = (timestamp: string, reason: string) =>
  api.post('/attendance/clock-out-retrospective', { timestamp, reason });

export const getHeartbeat = () =>
  api.get<HeartbeatResponse>('/attendance/heartbeat');

export const getHistory = (from?: string, to?: string) =>
  api.get<HistoryResponse>('/attendance/my-history', { params: { from, to } });
