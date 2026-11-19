export interface ProjectResponse {
  id: number;
  name: string;
  description: string;
  product: string;
  startDate: string;
  endDate: string | null;
  status: string;
  createdAt: string;
  updatedAt: string | null;
  memberCount: number;
  commitmentCount: number;
  pendingCommitmentCount: number;
  inProgressCommitmentCount: number;
  completedCommitmentCount: number;
  productOwnerNames: string[];
}

export interface DashboardWorkloadResponse {
  id: number;
  name: string;
  count: number;
}

export interface DashboardStatusCountResponse {
  status: string;
  count: number;
}

export interface DashboardSummaryResponse {
  totalProjects: number;
  activeProjects: number;
  totalCommitments: number;
  pendingCommitments: number;
  inProgressCommitments: number;
  completedCommitments: number;
  criticalAlerts: number;
  warningAlerts: number;
  registeredDevelopers: number;
  registeredProductOwners: number;
  openCommitments: number;
  overdueCommitments: number;
  upcomingCommitments: number;
  pendingByProject: DashboardWorkloadResponse[];
  pendingByDeveloper: DashboardWorkloadResponse[];
  statusDistribution: DashboardStatusCountResponse[];
}

export interface CreateProjectRequest {
  name: string;
  description: string;
  product: string;
  startDate: string;
  endDate: string | null;
  status: string | null;
}

export interface UpdateProjectRequest {
  name: string;
  description: string;
  product: string;
  startDate: string;
  endDate: string | null;
  status: string;
}

export interface UserResponse {
  id: number;
  displayName: string;
  email: string | null;
  role: string;
  isActive: boolean;
  createdAt: string;
}

export interface DeveloperResponse {
  id: number;
  userId: number;
  displayName: string;
  email: string | null;
  specialty: string;
  seniority: string;
  createdAt: string;
  isActive: boolean;
}

export interface CreateDeveloperRequest {
  displayName: string;
  specialty: string;
  seniority: string;
}

export interface ProductOwnerResponse {
  id: number;
  userId: number;
  displayName: string;
  email: string | null;
  department: string;
  createdAt: string;
}

export interface CommitmentResponse {
  id: number;
  projectId: number;
  projectName: string;
  developerId: number;
  developerName: string;
  productOwnerId: number;
  productOwnerName: string;
  title: string;
  description: string;
  startDate: string;
  commitmentDate: string;
  completedDate: string | null;
  status: string;
  priority: string;
  qaDate: string | null;
  preDate: string | null;
  protDate: string | null;
  notes: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface MilestoneResponse {
  id: number;
  projectId: number;
  projectName: string;
  title: string;
  description: string | null;
  dueDate: string;
  completedDate: string | null;
  status: string;
  createdAt: string;
}

export interface DeliverableResponse {
  id: number;
  projectId: number;
  projectName: string;
  commitmentId: number | null;
  commitmentTitle: string | null;
  title: string;
  description: string | null;
  status: string;
  qaDate: string | null;
  preDate: string | null;
  protDate: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface EnvironmentDeliveryResponse {
  id: number;
  commitmentId: number | null;
  commitmentTitle: string | null;
  deliverableId: number | null;
  deliverableTitle: string | null;
  environment: string;
  plannedDate: string | null;
  actualDate: string | null;
  status: string;
  notes: string | null;
  createdAt: string;
}

export interface ProjectMemberResponse {
  id: number;
  userId: number;
  displayName: string;
  email: string | null;
  role: string;
  joinedAt: string;
  isActive: boolean;
}

export interface AlertResponse {
  id: number;
  projectId: number;
  projectName: string;
  developerId: number | null;
  developerName: string | null;
  commitmentId: number | null;
  commitmentTitle: string | null;
  relatedCommitmentId: number | null;
  relatedCommitmentTitle: string | null;
  alertType: string;
  severity: string;
  message: string;
  status: string;
  conflictStartDate: string | null;
  conflictEndDate: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface AlertSummaryResponse {
  totalOpen: number;
  critical: number;
  warning: number;
  info: number;
  recentAlerts: AlertResponse[];
}

// ===== Timeline =====
export interface TimelineResponse {
  project: ProjectTimelineInfo;
  commitments: TimelineCommitment[];
  milestones: TimelineMilestone[];
  deliverables: TimelineDeliverable[];
  environmentDeliveries: TimelineEnvironmentDelivery[];
  alerts: TimelineAlert[];
}

export interface ProjectTimelineInfo {
  id: number;
  name: string;
  product: string;
  startDate: string;
  endDate: string | null;
  status: string;
}

export interface TimelineCommitment {
  id: number;
  title: string;
  developerId: number;
  developerName: string;
  productOwnerId: number;
  productOwnerName: string;
  startDate: string;
  commitmentDate: string;
  completedDate: string | null;
  status: string;
  priority: string;
  description: string;
  qaDate: string | null;
  preDate: string | null;
  protDate: string | null;
  hasConflict: boolean;
}

export interface TimelineMilestone {
  id: number;
  title: string;
  description: string | null;
  dueDate: string;
  completedDate: string | null;
  status: string;
}

export interface TimelineDeliverable {
  id: number;
  title: string;
  commitmentId: number | null;
  commitmentTitle: string | null;
  status: string;
  qaDate: string | null;
  preDate: string | null;
  protDate: string | null;
}

export interface TimelineEnvironmentDelivery {
  id: number;
  commitmentId: number | null;
  commitmentTitle: string | null;
  deliverableId: number | null;
  deliverableTitle: string | null;
  environment: string;
  plannedDate: string | null;
  actualDate: string | null;
  status: string;
}

export interface TimelineAlert {
  id: number;
  alertType: string;
  severity: string;
  message: string;
  status: string;
  developerId: number | null;
  developerName: string | null;
  commitmentId: number | null;
  commitmentTitle: string | null;
  relatedCommitmentId: number | null;
  relatedCommitmentTitle: string | null;
  conflictStartDate: string | null;
  conflictEndDate: string | null;
}

export interface ApiErrorResponse {
  success: boolean;
  message: string;
}

export interface DocumentSummaryResponse {
  id: number;
  projectId: number | null;
  projectName: string | null;
  title: string;
  documentType: string;
  originalFileName: string | null;
  fileSizeBytes: number | null;
  hasOriginalFile: boolean;
  processingStatus: string;
  uploadedAt: string;
  updatedAt: string | null;
  sourceDocumentId: number | null;
  sourceAnalysisId: number | null;
}

export interface DocumentResponse extends DocumentSummaryResponse {
  projectName: string | null;
  extractedText: string;
}

export interface CreateMeetingNotesRequest {
  projectId: number | null;
  title: string | null;
  text: string;
}

export interface UpdateMeetingNotesRequest {
  projectId: number | null;
  title: string;
  text: string;
}

export interface NoteAssistantQuestionRequest {
  projectId: number | null;
  currentNoteId: number | null;
  currentTitle: string;
  currentText: string;
  question: string;
}

export interface NoteDraftAnalysisRequest {
  projectId: number | null;
  text: string;
}

export interface NoteAssistantAnswerResponse {
  answer: string;
}

export interface NoteAttachmentTextResponse {
  fileName: string;
  extractedText: string;
}

export interface DeleteDocumentResponse {
  deleted: boolean;
  fileRemoved: boolean;
  message: string | null;
}

export interface AiAnalysisProposal {
  id: string;
  category: string;
  title: string;
  description: string | null;
  developerName: string | null;
  productOwnerName: string | null;
  startDate: string | null;
  endDate: string | null;
  qaDate: string | null;
  preDate: string | null;
  protDate: string | null;
  prodDate: string | null;
  dependencies: string[];
  risks: string[];
  evidence: string;
  confidence: number;
  confirmationRequired: boolean;
  decision: string;
  registeredEntityType: 'Commitment' | 'Milestone' | 'Deliverable' | null;
  registeredEntityId: number | null;
}

export interface AiAnalysisContent {
  projectName: string | null;
  summary: string | null;
  proposals: AiAnalysisProposal[];
  uncertainInformation: string[];
  meetingSummary: AiMeetingSummary | null;
}

export interface AiMeetingSummary {
  title: string;
  generalSummary: string;
  topics: AiSummaryFact[];
  agreements: AiSummaryFact[];
  decisions: AiSummaryFact[];
  pendingItems: AiSummaryFact[];
  commitments: AiSummaryFact[];
  deliverables: AiSummaryFact[];
  responsiblePeople: AiSummaryResponsiblePerson[];
  importantDates: AiSummaryDate[];
  risksAndObservations: AiSummaryFact[];
  nextSteps: AiSummaryFact[];
}

export interface AiSummaryFact {
  text: string;
  evidence: string;
}

export interface AiSummaryResponsiblePerson {
  name: string | null;
  responsibility: string;
  needsConfirmation: boolean;
  evidence: string;
}

export interface AiSummaryDate {
  description: string;
  date: string | null;
  responsiblePerson: string | null;
  needsConfirmation: boolean;
  evidence: string;
}

export interface AiAnalysisResponse {
  id: number;
  documentId: number;
  projectId: number | null;
  status: string;
  processingError: string | null;
  createdAt: string;
  processedAt: string | null;
  result: AiAnalysisContent | null;
  generatedNoteId: number | null;
}

export interface AiAnalysisConfigurationResponse {
  configured: boolean;
  missingSettings: string[];
}

export interface SaveAiSummaryNoteRequest {
  title: string;
  text: string;
}

export interface ReviewAiProposalRequest {
  decision: 'Accepted' | 'Rejected';
  correctedProposal: AiAnalysisProposal | null;
}

export interface AzureDevOpsStatusResponse {
  organizationUrl: string;
  connected: boolean;
  visibleProjectCount: number;
}

export interface AzureDevOpsProjectResponse {
  id: string;
  name: string;
  state: string;
  visibility: string | null;
}

export interface AzureDevOpsWorkItemResponse {
  id: number;
  title: string;
  workItemType: string;
  state: string | null;
  assignedTo: string | null;
  startDate: string | null;
  targetDate: string | null;
  createdDate: string | null;
  changedDate: string | null;
  url: string | null;
  projectName: string;
  localProjectId: number | null;
  localProjectName: string | null;
  commitmentId: number | null;
  commitmentTitle: string | null;
}

export interface AzureDevOpsWorkItemDetailResponse {
  workItem: AzureDevOpsWorkItemResponse;
  fields: Record<string, unknown>;
}
