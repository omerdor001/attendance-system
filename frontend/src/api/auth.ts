import api from './client';

export const login = (username: string, password: string) =>
  api.post('/auth/login', { username, password });

export const register = (data: {
  username: string;
  password: string;
  role: string;
  expectedShiftStartTime: string;
  expectedShiftEndTime: string;
}) => api.post('/auth/register', data);
