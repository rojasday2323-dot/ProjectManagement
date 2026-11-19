import apiClient from './apiClient';
import type { AlertResponse, AlertSummaryResponse } from '../types/api';

export interface AlertFilters {
  developerId?: number;
  projectId?: number;
  severity?: string;
  status?: string;
  startDate?: string;
  endDate?: string;
}

export const alertService = {
  getAll: (filters?: AlertFilters) =>
    apiClient.get<AlertResponse[]>('/api/alerts', { params: filters }).then((r) => r.data),

  getSummary: () =>
    apiClient.get<AlertSummaryResponse>('/api/alerts/summary').then((r) => r.data),

  getByDeveloper: (developerId: number) =>
    apiClient.get<AlertResponse[]>(`/api/developers/${developerId}/alerts`).then((r) => r.data),

  getByProject: (projectId: number) =>
    apiClient.get<AlertResponse[]>(`/api/projects/${projectId}/alerts`).then((r) => r.data),

  updateStatus: (id: number, status: string) =>
    apiClient.patch(`/api/alerts/${id}/status`, { status }).then((r) => r.data),

  runDetection: () =>
    apiClient.post('/api/alerts/detect').then((r) => r.data),
};
