import apiClient from './apiClient';
import type { EnvironmentDeliveryResponse } from '../types/api';

export const environmentDeliveryService = {
  getAll: (params?: { commitmentId?: number; projectId?: number; environment?: string }) =>
    apiClient
      .get<EnvironmentDeliveryResponse[]>('/api/environment-deliveries', { params })
      .then((r) => r.data),
};
