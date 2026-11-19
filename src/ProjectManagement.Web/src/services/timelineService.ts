import apiClient from './apiClient';
import type { TimelineResponse } from '../types/api';

export const timelineService = {
  getProjectTimeline: (projectId: number) =>
    apiClient.get<TimelineResponse>(`/api/projects/${projectId}/timeline`).then((r) => r.data),
};
