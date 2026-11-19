import apiClient from './apiClient';
import type { CommitmentResponse } from '../types/api';

export interface CommitmentFilters {
  developerId?: number;
  projectId?: number;
  productOwnerId?: number;
  startDate?: string;
  endDate?: string;
  status?: string;
}

export const commitmentService = {
  getAll: (filters?: CommitmentFilters) =>
    apiClient
      .get<CommitmentResponse[]>('/api/commitments', { params: filters })
      .then((r) => r.data),

  getById: (id: number) =>
    apiClient
      .get<CommitmentResponse>(`/api/commitments/${id}`)
      .then((r) => r.data),
};
