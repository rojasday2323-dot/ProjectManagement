import apiClient from './apiClient';
import type { DashboardSummaryResponse } from '../types/api';

export const dashboardService = {
  getSummary: () =>
    apiClient.get<DashboardSummaryResponse>('/api/dashboard/summary').then((r) => r.data),
};
