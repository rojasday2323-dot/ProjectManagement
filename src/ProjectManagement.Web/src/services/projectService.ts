import apiClient from './apiClient';
import type {
  ProjectResponse,
  ProjectMemberResponse,
  CommitmentResponse,
  MilestoneResponse,
  DeliverableResponse,
} from '../types/api';

export const projectService = {
  getAll: () =>
    apiClient.get<ProjectResponse[]>('/api/projects').then((r) => r.data),

  getById: (id: number) =>
    apiClient.get<ProjectResponse>(`/api/projects/${id}`).then((r) => r.data),

  getMembers: (projectId: number) =>
    apiClient
      .get<ProjectMemberResponse[]>(`/api/projects/${projectId}/members`)
      .then((r) => r.data),

  getCommitments: (projectId: number) =>
    apiClient
      .get<CommitmentResponse[]>('/api/commitments', { params: { projectId } })
      .then((r) => r.data),

  getMilestones: (projectId: number) =>
    apiClient
      .get<MilestoneResponse[]>('/api/milestones', { params: { projectId } })
      .then((r) => r.data),

  getDeliverables: (projectId: number) =>
    apiClient
      .get<DeliverableResponse[]>('/api/deliverables', { params: { projectId } })
      .then((r) => r.data),
};
