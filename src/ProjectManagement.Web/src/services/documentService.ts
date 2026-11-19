import apiClient from './apiClient';
import type {
  CreateMeetingNotesRequest,
  AiAnalysisResponse,
  AiAnalysisConfigurationResponse,
  DocumentResponse,
  DocumentSummaryResponse,
  ReviewAiProposalRequest,
  SaveAiSummaryNoteRequest,
  UpdateMeetingNotesRequest,
  DeleteDocumentResponse,
  NoteAssistantAnswerResponse,
  NoteAssistantQuestionRequest,
  AiAnalysisContent,
  NoteDraftAnalysisRequest,
  NoteAttachmentTextResponse,
} from '../types/api';

export const documentService = {
  getAiConfigurationStatus: () =>
    apiClient.get<AiAnalysisConfigurationResponse>('/api/ai/status').then((response) => response.data),

  uploadFile: (projectId: number | null, file: File) => {
    const formData = new FormData();
    if (projectId !== null) formData.append('projectId', String(projectId));
    formData.append('file', file);
    return apiClient
      .post<DocumentResponse>('/api/documents/files', formData, {
        headers: { 'Content-Type': 'multipart/form-data' },
        timeout: 60_000,
      })
      .then((response) => response.data);
  },

  createNotes: (request: CreateMeetingNotesRequest) =>
    apiClient.post<DocumentResponse>('/api/documents/notes', request).then((response) => response.data),

  updateNotes: (id: number, request: UpdateMeetingNotesRequest) =>
    apiClient.put<DocumentResponse>(`/api/documents/${id}/notes`, request).then((response) => response.data),

  extractNoteAttachment: (file: File) => {
    const formData = new FormData();
    formData.append('file', file);
    return apiClient
      .post<NoteAttachmentTextResponse>('/api/notes/attachments/extract', formData, {
        headers: { 'Content-Type': 'multipart/form-data' },
        timeout: 180_000,
      })
      .then((response) => response.data);
  },

  askAboutNotes: (request: NoteAssistantQuestionRequest) =>
    apiClient
      .post<NoteAssistantAnswerResponse>('/api/notes/chat', request, { timeout: 180_000 })
      .then((response) => response.data),

  analyzeNoteDraft: (request: NoteDraftAnalysisRequest) =>
    apiClient
      .post<AiAnalysisContent>('/api/notes/analyze', request, { timeout: 180_000 })
      .then((response) => response.data),

  getAll: (filters: { search?: string; projectId?: number; unassignedOnly?: boolean; type?: string } = {}) =>
    apiClient
      .get<DocumentSummaryResponse[]>('/api/documents', { params: filters })
      .then((response) => response.data),

  getByProject: (projectId: number) =>
    apiClient
      .get<DocumentSummaryResponse[]>(`/api/documents/projects/${projectId}`)
      .then((response) => response.data),

  getById: (id: number) =>
    apiClient.get<DocumentResponse>(`/api/documents/${id}`).then((response) => response.data),

  delete: (id: number) =>
    apiClient.delete<DeleteDocumentResponse>(`/api/documents/${id}`).then((response) => response.data),

  downloadOriginal: async (id: number, fileName: string) => {
    const response = await apiClient.get<Blob>(`/api/documents/${id}/file`, { responseType: 'blob' });
    const url = URL.createObjectURL(response.data);
    const link = window.document.createElement('a');
    link.href = url;
    link.download = fileName;
    link.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  },

  analyze: (id: number) =>
    apiClient
      .post<AiAnalysisResponse>(`/api/documents/${id}/analyze`, null, { timeout: 75_000 })
      .then((response) => response.data),

  getAnalyses: (id: number) =>
    apiClient
      .get<AiAnalysisResponse[]>(`/api/documents/${id}/analyses`)
      .then((response) => response.data),

  reviewProposal: (analysisId: number, proposalId: string, request: ReviewAiProposalRequest) =>
    apiClient
      .patch<AiAnalysisResponse>(
        `/api/analyses/${analysisId}/proposals/${encodeURIComponent(proposalId)}`,
        request,
      )
      .then((response) => response.data),

  saveSummaryAsNote: (analysisId: number, request: SaveAiSummaryNoteRequest) =>
    apiClient
      .post<AiAnalysisResponse>(`/api/analyses/${analysisId}/summary-note`, request)
      .then((response) => response.data),
};
