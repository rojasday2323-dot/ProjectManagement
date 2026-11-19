import apiClient from './apiClient';
import type {
  AzureDevOpsProjectResponse,
  AzureDevOpsStatusResponse,
  AzureDevOpsWorkItemDetailResponse,
  AzureDevOpsWorkItemResponse,
} from '../types/api';

export const azureDevOpsService = {
  getStatus: () =>
    apiClient.get<AzureDevOpsStatusResponse>('/api/azure-devops/status').then((response) => response.data),

  getProjects: () =>
    apiClient.get<AzureDevOpsProjectResponse[]>('/api/azure-devops/projects').then((response) => response.data),

  getProjectWorkItems: (projectId: string, top = 100) =>
    apiClient
      .get<AzureDevOpsWorkItemResponse[]>(
        `/api/azure-devops/projects/${encodeURIComponent(projectId)}/work-items`,
        { params: { top } },
      )
      .then((response) => response.data),

  getWorkItem: (id: number) =>
    apiClient
      .get<AzureDevOpsWorkItemDetailResponse>(`/api/azure-devops/work-items/${id}`)
      .then((response) => response.data),
};
