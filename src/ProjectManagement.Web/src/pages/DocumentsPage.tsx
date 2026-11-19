import { useEffect, useState } from 'react';
import type { ClipboardEvent, FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { ErrorAlert } from '../components/ui/ErrorAlert';
import { LoadingSpinner } from '../components/ui/LoadingSpinner';
import { useApi } from '../hooks/useApi';
import { documentService } from '../services/documentService';
import { projectService } from '../services/projectService';
import { developerService, productOwnerService } from '../services/userService';
import type {
  AiAnalysisContent,
  AiAnalysisProposal,
  AiAnalysisResponse,
  AiMeetingSummary,
  AiSummaryFact,
  DocumentResponse,
  DocumentSummaryResponse,
} from '../types/api';
import { formatDate } from '../utils/format';

const maxFileSize = 10 * 1024 * 1024;
const maxImageSize = 5 * 1024 * 1024;

export function DocumentsPage() {
  const { data: projects, loading: projectsLoading, error: projectsError, refetch } =
    useApi(() => projectService.getAll());
  const developers = useApi(() => developerService.getAll());
  const productOwners = useApi(() => productOwnerService.getAll());
  const [projectId, setProjectId] = useState('');
  const [documentsError, setDocumentsError] = useState<string | null>(null);
  const [notesTitle, setNotesTitle] = useState('');
  const [notesText, setNotesText] = useState('');
  const [savedNoteId, setSavedNoteId] = useState<number | null>(null);
  const [attachments, setAttachments] = useState<{ id: string; name: string; status: 'processing' | 'added' | 'error' }[]>([]);
  const [attachmentError, setAttachmentError] = useState<string | null>(null);
  const [chatQuestion, setChatQuestion] = useState('');
  const [chatMessages, setChatMessages] = useState<{ role: 'user' | 'assistant'; text: string }[]>([]);
  const [chatError, setChatError] = useState<string | null>(null);
  const [chatLoading, setChatLoading] = useState(false);
  const [draftAnalysis, setDraftAnalysis] = useState<AiAnalysisContent | null>(null);
  const [draftAnalysisError, setDraftAnalysisError] = useState<string | null>(null);
  const [draftAnalysisLoading, setDraftAnalysisLoading] = useState(false);
  const [savingNotes, setSavingNotes] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [expandedDocument, setExpandedDocument] = useState<DocumentResponse | null>(null);
  const [analyses, setAnalyses] = useState<AiAnalysisResponse[]>([]);
  const [analysisLoading, setAnalysisLoading] = useState(false);
  const [analysisError, setAnalysisError] = useState<string | null>(null);
  const [aiConfiguration, setAiConfiguration] = useState<{ configured: boolean; missingSettings: string[] } | null>(null);
  const [aiConfigurationError, setAiConfigurationError] = useState<string | null>(null);
  const [checkingAiConfiguration, setCheckingAiConfiguration] = useState(false);
  const [savingSummaryAnalysisId, setSavingSummaryAnalysisId] = useState<number | null>(null);
  const [reviewingProposalId, setReviewingProposalId] = useState<string | null>(null);
  const [proposalDrafts, setProposalDrafts] = useState<Record<string, AiAnalysisProposal>>({});
  const [editingProposalIds, setEditingProposalIds] = useState<string[]>([]);
  const [editingNote, setEditingNote] = useState(false);
  const [noteDraftTitle, setNoteDraftTitle] = useState('');
  const [noteDraftText, setNoteDraftText] = useState('');
  const [noteDraftProjectId, setNoteDraftProjectId] = useState('');
  const [savingNoteEdit, setSavingNoteEdit] = useState(false);
  const [deletingDocumentId, setDeletingDocumentId] = useState<number | null>(null);

  useEffect(() => {
    let active = true;
    documentService.getAiConfigurationStatus()
      .then((status) => {
        if (active) {
          setAiConfiguration(status);
          setAiConfigurationError(null);
        }
      })
      .catch((error: unknown) => {
        if (active) {
          setAiConfigurationError(error instanceof Error ? error.message : 'No se pudo consultar la configuración de IA.');
        }
      });
    return () => {
      active = false;
    };
  }, []);

  useEffect(() => {
    setChatMessages([]);
    setChatError(null);
  }, [projectId]);

  const refreshAiConfiguration = async () => {
    setCheckingAiConfiguration(true);
    setAiConfigurationError(null);
    try {
      const status = await documentService.getAiConfigurationStatus();
      setAiConfiguration(status);
      if (status.configured) setAnalysisError(null);
    } catch (error) {
      setAiConfigurationError(error instanceof Error ? error.message : 'No se pudo consultar la configuración de IA.');
    } finally {
      setCheckingAiConfiguration(false);
    }
  };

  const handleNotes = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setMessage(null);
    setDocumentsError(null);
    if (!notesTitle.trim()) {
      setDocumentsError('Escribe un título para la nota.');
      return;
    }
    if (!notesText.trim()) {
      setDocumentsError('Escribe las notas de la reunión antes de guardarlas.');
      return;
    }

    setSavingNotes(true);
    try {
      const selectedProjectId = projectId ? Number(projectId) : null;
      const request = {
        projectId: selectedProjectId,
        title: notesTitle.trim(),
        text: notesText,
      };
      const document = savedNoteId === null
        ? await documentService.createNotes(request)
        : await documentService.updateNotes(savedNoteId, request);
      setExpandedDocument(document);
      setSavedNoteId(document.id);
      if (savedNoteId === null) {
        setAnalyses([]);
        setProposalDrafts({});
      }
      setEditingNote(false);
      setMessage(savedNoteId === null
        ? 'Las notas se guardaron y están listas para revisión.'
        : 'Los cambios de la nota se guardaron.');
    } catch (error) {
      setDocumentsError(error instanceof Error ? error.message : 'No se pudieron guardar las notas.');
    } finally {
      setSavingNotes(false);
    }
  };

  const addAttachmentText = (text: string, fileName: string) => {
    setDraftAnalysis(null);
    setDraftAnalysisError(null);
    setNotesText((current) => `${current.trimEnd()}${current.trim() ? '\n\n' : ''}[Contenido adjunto: ${fileName}]\n${text}`);
  };

  const handleAttachmentFiles = async (files: File[]) => {
    setAttachmentError(null);
    for (const file of files) {
      const id = `${Date.now()}-${Math.random()}`;
      const extension = file.name.toLowerCase().slice(file.name.lastIndexOf('.'));
      const isImage = file.type.startsWith('image/');
      if (file.size === 0) {
        setAttachmentError(`"${file.name}" está vacío.`);
        continue;
      }
      if (file.size > (isImage ? maxImageSize : maxFileSize)) {
        setAttachmentError(`"${file.name}" supera el límite de ${(isImage ? 5 : 10)} MB.`);
        continue;
      }
      if (!['.pdf', '.docx', '.txt', '.png', '.jpg', '.jpeg', '.gif', '.webp'].includes(extension)) {
        setAttachmentError(`Formato no permitido para "${file.name}".`);
        continue;
      }
      setAttachments((current) => [...current, { id, name: file.name, status: 'processing' }]);
      try {
        const result = await documentService.extractNoteAttachment(file);
        addAttachmentText(result.extractedText, result.fileName);
        setAttachments((current) => current.map((item) => item.id === id ? { ...item, status: 'added' } : item));
      } catch (error) {
        setAttachments((current) => current.map((item) => item.id === id ? { ...item, status: 'error' } : item));
        setAttachmentError(error instanceof Error ? error.message : `No se pudo leer "${file.name}".`);
      }
    }
  };

  const handleNotesPaste = (event: ClipboardEvent<HTMLTextAreaElement>) => {
    const images = Array.from(event.clipboardData.files).filter((file) => file.type.startsWith('image/'));
    if (images.length === 0) return;
    event.preventDefault();
    void handleAttachmentFiles(images);
  };

  const askAboutNotes = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const question = chatQuestion.trim();
    if (!question || chatLoading) return;
    setChatError(null);
    setChatLoading(true);
    setChatMessages((current) => [...current, { role: 'user', text: question }]);
    setChatQuestion('');
    try {
      const selectedProjectId = projectId ? Number(projectId) : null;
      const response = await documentService.askAboutNotes({
        projectId: selectedProjectId,
        currentNoteId: expandedDocument?.id === savedNoteId && expandedDocument.projectId === selectedProjectId
          ? savedNoteId
          : null,
        currentTitle: notesTitle,
        currentText: notesText,
        question,
      });
      setChatMessages((current) => [...current, { role: 'assistant', text: response.answer }]);
    } catch (error) {
      setChatError(error instanceof Error ? error.message : 'No se pudo consultar la nota.');
    } finally {
      setChatLoading(false);
    }
  };

  const analyzeNoteDraft = async () => {
    if (!notesText.trim() || draftAnalysisLoading) return;
    setDraftAnalysis(null);
    setDraftAnalysisError(null);
    setDraftAnalysisLoading(true);
    try {
      const selectedProjectId = projectId ? Number(projectId) : null;
      const result = await documentService.analyzeNoteDraft({
        projectId: selectedProjectId,
        text: notesText,
      });
      setDraftAnalysis(result);
    } catch (error) {
      setDraftAnalysisError(error instanceof Error ? error.message : 'No se pudieron identificar los campos de la nota.');
    } finally {
      setDraftAnalysisLoading(false);
    }
  };

  const showDocument = async (id: number) => {
    if (expandedDocument?.id === id) {
      setExpandedDocument(null);
      return;
    }
    setDocumentsError(null);
    try {
      const document = await documentService.getById(id);
      setExpandedDocument(document);
      if (document.documentType === 'MeetingNote') {
        setSavedNoteId(document.id);
        setProjectId(document.projectId?.toString() ?? '');
        setNotesTitle(document.title);
        setNotesText(document.extractedText);
      }
      setAnalyses(await documentService.getAnalyses(id));
      setEditingNote(false);
      setProposalDrafts({});
      setAnalysisError(null);
    } catch (error) {
      setDocumentsError(error instanceof Error ? error.message : 'No se pudo abrir el documento.');
    }
  };

  const beginNoteEdit = (document: DocumentResponse) => {
    setNoteDraftTitle(document.title);
    setNoteDraftText(document.extractedText);
    setNoteDraftProjectId(document.projectId?.toString() ?? '');
    setEditingNote(true);
    setDocumentsError(null);
  };

  const saveNoteEdit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!expandedDocument) return;
    setSavingNoteEdit(true);
    setDocumentsError(null);
    try {
      const updated = await documentService.updateNotes(expandedDocument.id, {
        title: noteDraftTitle.trim(),
        text: noteDraftText,
        projectId: noteDraftProjectId ? Number(noteDraftProjectId) : null,
      });
      setExpandedDocument(updated);
      setNotesTitle(updated.title);
      setNotesText(updated.extractedText);
      setEditingNote(false);
      setMessage('La nota se actualizó correctamente.');
    } catch (error) {
      setDocumentsError(error instanceof Error ? error.message : 'No se pudo actualizar la nota.');
    } finally {
      setSavingNoteEdit(false);
    }
  };

  const deleteDocument = async (document: DocumentSummaryResponse) => {
    const title = expandedDocument?.id === document.id ? expandedDocument.title : document.title;
    if (!window.confirm(`¿Eliminar "${title}"? Se conservará la trazabilidad del análisis y las propuestas.`))
      return;
    setDeletingDocumentId(document.id);
    setDocumentsError(null);
    try {
      const result = await documentService.delete(document.id);
      if (expandedDocument?.id === document.id) {
        setExpandedDocument(null);
        setAnalyses([]);
        setProposalDrafts({});
        setSavedNoteId(null);
      }
      setMessage(result.message ?? `"${title}" se eliminó. Se conserva la trazabilidad de análisis y propuestas.`);
    } catch (error) {
      setDocumentsError(error instanceof Error ? error.message : 'No se pudo eliminar el registro.');
    } finally {
      setDeletingDocumentId(null);
    }
  };

  const downloadOriginal = async (document: DocumentSummaryResponse) => {
    if (!document.originalFileName) return;
    setDocumentsError(null);
    try {
      await documentService.downloadOriginal(document.id, document.originalFileName);
    } catch (error) {
      setDocumentsError(error instanceof Error ? error.message : 'No se pudo descargar el archivo original.');
    }
  };

  const analyzeDocument = async () => {
    if (!expandedDocument) return;
    if (!aiConfiguration?.configured) {
      setAnalysisError('Azure AI Foundry no está configurado en la API. Configura los valores requeridos y reinicia el backend antes de analizar.');
      return;
    }
    setAnalysisLoading(true);
    setAnalysisError(null);
    setMessage(null);
    try {
      const analysis = await documentService.analyze(expandedDocument.id);
      setAnalyses((current) => [analysis, ...current.filter((item) => item.id !== analysis.id)]);
      setProposalDrafts(Object.fromEntries(
        (analysis.result?.proposals ?? []).map((proposal) => [proposal.id, proposal]),
      ));
      setMessage('El análisis terminó. Revisa las propuestas antes de aceptar o rechazar cada una.');
    } catch (error) {
      setAnalysisError(error instanceof Error ? error.message : 'No se pudo analizar el documento.');
      try {
        setAnalyses(await documentService.getAnalyses(expandedDocument.id));
      } catch {
        setAnalysisError((current) => current ?? 'No se pudo consultar el estado del análisis.');
      }
    } finally {
      setAnalysisLoading(false);
    }
  };

  const reviewProposal = async (
    analysis: AiAnalysisResponse,
    proposal: AiAnalysisProposal,
    decision: 'Accepted' | 'Rejected',
  ) => {
    setReviewingProposalId(proposal.id);
    setAnalysisError(null);
    try {
      const updated = await documentService.reviewProposal(analysis.id, proposal.id, {
        decision,
        correctedProposal: proposalDrafts[proposal.id] ?? proposal,
      });
      const reviewedProposal = updated.result?.proposals.find((item) => item.id === proposal.id);
      setAnalyses((current) => current.map((item) => item.id === updated.id ? updated : item));
      setProposalDrafts((current) => ({
        ...current,
        [proposal.id]: reviewedProposal ?? proposal,
      }));
      setEditingProposalIds((current) => current.filter((id) => id !== proposal.id));
      setMessage(decision === 'Rejected'
        ? 'Propuesta rechazada; no se registró en el proyecto.'
        : reviewedProposal?.registeredEntityType
          ? `${getEntityLabel(reviewedProposal.registeredEntityType)} registrado en el proyecto.`
          : 'Propuesta aceptada para revisión; no tiene un tipo de registro de proyecto compatible.');
    } catch (error) {
      setAnalysisError(error instanceof Error ? error.message : 'No se pudo guardar la decisión.');
    } finally {
      setReviewingProposalId(null);
    }
  };

  const saveSummaryAsNote = async (analysisId: number, title: string, text: string) => {
    setSavingSummaryAnalysisId(analysisId);
    setAnalysisError(null);
    setMessage(null);
    try {
      const updated = await documentService.saveSummaryAsNote(analysisId, { title, text });
      setAnalyses((current) => current.map((item) => item.id === updated.id ? updated : item));
      setMessage(`El resumen se guardó como nota${expandedDocument?.projectName ? ` del proyecto ${expandedDocument.projectName}` : ''}.`);
    } catch (error) {
      setAnalysisError(error instanceof Error ? error.message : 'No se pudo guardar el resumen como nota.');
    } finally {
      setSavingSummaryAnalysisId(null);
    }
  };

  const openSavedSummaryNote = async (noteId: number) => {
    await showDocument(noteId);
  };

  const updateProposalDraft = (
    proposal: AiAnalysisProposal,
    field: 'title' | 'description' | 'developerName' | 'productOwnerName' | 'startDate' | 'endDate' | 'qaDate' | 'preDate' | 'protDate' | 'prodDate',
    value: string,
  ) => {
    setProposalDrafts((current) => ({
      ...current,
      [proposal.id]: {
        ...(current[proposal.id] ?? proposal),
        [field]: value || null,
      },
    }));
  };

  if (projectsLoading) return <LoadingSpinner message="Cargando proyectos..." />;
  if (projectsError) return <ErrorAlert message={projectsError} onRetry={refetch} />;

  const inputStyle = {
    width: '100%',
    padding: '9px 11px',
    border: '1px solid #d1d5db',
    borderRadius: 6,
    fontSize: 14,
    boxSizing: 'border-box' as const,
    backgroundColor: '#fff',
  };
  const cardStyle = {
    backgroundColor: '#fff',
    border: '1px solid #e5e7eb',
    borderRadius: 8,
    padding: 20,
    boxShadow: '0 1px 3px rgba(0,0,0,0.05)',
  };
  const attachmentsProcessing = attachments.some((attachment) => attachment.status === 'processing');
  const meetingSummary = draftAnalysis?.meetingSummary;

  return (
    <div style={{ maxWidth: 1100, margin: '0 auto' }}>
      <div style={{ marginBottom: 20 }}>
        <h1 style={{ fontSize: 22, fontWeight: 700, color: '#111827', margin: '0 0 6px' }}>
          Documentos y notas
        </h1>
        <p style={{ color: '#6b7280', fontSize: 14, margin: 0 }}>
          Escribe notas, incorpora texto desde archivos y consulta la información del proyecto con IA.
        </p>
      </div>

      {documentsError && <ErrorAlert message={documentsError} />}
      {message && (
        <div role="status" style={{ padding: 12, marginBottom: 16, color: '#166534', background: '#f0fdf4', border: '1px solid #bbf7d0', borderRadius: 6 }}>
          {message}
        </div>
      )}

      <section style={{ ...cardStyle, marginBottom: 16 }}>
        <label htmlFor="project-select" style={{ display: 'block', fontWeight: 600, fontSize: 14, marginBottom: 8 }}>
          Proyecto de las notas <span style={{ fontWeight: 400, color: '#6b7280' }}>(opcional)</span>
        </label>
        <select id="project-select" value={projectId} onChange={(event) => {
          setProjectId(event.target.value);
          setDraftAnalysis(null);
          setDraftAnalysisError(null);
        }} style={{ ...inputStyle, maxWidth: 480 }}>
          <option value="">Sin proyecto (registro general)</option>
          {projects?.map((project) => <option key={project.id} value={project.id}>{project.name}</option>)}
        </select>
      </section>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(min(100%, 300px), 1fr))', gap: 16, marginBottom: 20 }}>
        <section style={cardStyle}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 12, marginBottom: 6 }}>
            <h2 style={{ fontSize: 17, margin: 0, color: '#111827' }}>Notas de reunión</h2>
            <span style={savingNotes ? pendingBadge : savedNoteId === null ? pendingBadge : savedBadge}>
              {savingNotes ? 'Guardando…' : savedNoteId === null ? 'Pendiente de guardar' : 'Nota guardada'}
            </span>
          </div>
          <p style={{ margin: '0 0 16px', color: '#6b7280', fontSize: 13 }}>Escribe o pega texto. Los adjuntos se convierten en texto y no se guardan como archivos.</p>
          <form onSubmit={handleNotes}>
            <label htmlFor="notes-title" style={{ display: 'block', fontSize: 13, fontWeight: 600, marginBottom: 6 }}>
              Título
            </label>
            <input
              id="notes-title"
              value={notesTitle}
              onChange={(event) => setNotesTitle(event.target.value)}
              maxLength={300}
              placeholder="Ej. Seguimiento semanal"
              style={{ ...inputStyle, marginBottom: 12 }}
              disabled={savingNotes}
            />
            <label htmlFor="meeting-notes" style={{ display: 'block', fontSize: 13, fontWeight: 600, marginBottom: 6 }}>
              Notas
            </label>
            <textarea
              id="meeting-notes"
              value={notesText}
              onChange={(event) => {
                setNotesText(event.target.value);
                setDraftAnalysis(null);
                setDraftAnalysisError(null);
              }}
              onPaste={handleNotesPaste}
              rows={6}
              maxLength={10 * 1024 * 1024}
              placeholder="Escribe o pega aquí las notas de la reunión. También puedes pegar una imagen."
              style={{ ...inputStyle, resize: 'vertical', fontFamily: 'inherit', marginBottom: 12 }}
              disabled={savingNotes}
            />
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: 10, alignItems: 'center' }}>
              <button
                type="button"
                onClick={() => void analyzeNoteDraft()}
                disabled={!notesText.trim() || draftAnalysisLoading || attachmentsProcessing || !aiConfiguration?.configured}
                style={buttonStyle(!notesText.trim() || draftAnalysisLoading || attachmentsProcessing || !aiConfiguration?.configured)}
              >
                {draftAnalysisLoading ? 'Identificando campos…' : 'Identificar campos con IA'}
              </button>
              <label htmlFor="note-attachments" style={{ ...textButtonStyle, border: '1px solid #cbd5e1', borderRadius: 6, padding: '9px 12px', fontSize: 13 }}>
                Adjuntar archivos
              </label>
              <input
                id="note-attachments"
                type="file"
                multiple
                accept=".pdf,.docx,.txt,.png,.jpg,.jpeg,.gif,.webp"
                onChange={(event) => {
                  if (event.target.files) void handleAttachmentFiles(Array.from(event.target.files));
                  event.target.value = '';
                }}
                style={{ display: 'none' }}
              />
              <button type="submit" disabled={!notesTitle.trim() || !notesText.trim() || savingNotes || attachmentsProcessing} style={buttonStyle(!notesTitle.trim() || !notesText.trim() || savingNotes || attachmentsProcessing)}>
                {savingNotes ? 'Guardando…' : attachmentsProcessing ? 'Extrayendo adjuntos…' : savedNoteId === null ? 'Guardar nota' : 'Guardar cambios'}
              </button>
            </div>
          </form>
          <p style={{ color: '#64748b', fontSize: 12, margin: '10px 0 0' }}>Admite PDF, Word, TXT e imágenes PNG, JPG, GIF o WEBP. Máximo 10 MB por documento y 5 MB por imagen.</p>
          {attachments.length > 0 && (
            <ul aria-label="Adjuntos de la nota" style={{ listStyle: 'none', margin: '14px 0 0', padding: 0 }}>
              {attachments.map((attachment) => (
                <li key={attachment.id} style={{ display: 'flex', justifyContent: 'space-between', gap: 8, padding: '6px 0', fontSize: 12, color: '#475569' }}>
                  <span>{attachment.name}</span>
                  <span>{attachment.status === 'processing' ? 'Extrayendo…' : attachment.status === 'added' ? 'Texto añadido' : 'Error'}</span>
                </li>
              ))}
            </ul>
          )}
          {attachmentError && <div role="alert" style={{ marginTop: 10 }}><ErrorAlert message={attachmentError} /></div>}
          {draftAnalysisError && <div role="alert" style={{ marginTop: 12 }}><ErrorAlert message={draftAnalysisError} /></div>}
          {draftAnalysis && (
            <section aria-label="Resumen inteligente" style={{ marginTop: 18, padding: 16, border: '1px solid #bfdbfe', borderRadius: 8, background: '#f8fbff' }}>
              <h3 style={{ margin: '0 0 12px', fontSize: 16, color: '#1e3a8a' }}>Resumen inteligente</h3>
              {meetingSummary ? (
                <>
                  <h4 style={{ margin: '0 0 5px', fontSize: 14 }}>{meetingSummary.title}</h4>
                  <p style={{ margin: '0 0 14px', color: '#334155', fontSize: 13, whiteSpace: 'pre-wrap' }}>{meetingSummary.generalSummary}</p>
                  <SummaryFactGroup title="Temas tratados" facts={meetingSummary.topics} />
                  <SummaryFactGroup title="Acuerdos" facts={meetingSummary.agreements} />
                  <SummaryFactGroup title="Decisiones" facts={meetingSummary.decisions} />
                  <SummaryFactGroup title="Pendientes" facts={meetingSummary.pendingItems} />
                  <SummaryFactGroup title="Compromisos" facts={meetingSummary.commitments} />
                  <SummaryFactGroup title="Entregables" facts={meetingSummary.deliverables} />
                  <SummaryFactGroup title="Riesgos y observaciones" facts={meetingSummary.risksAndObservations} />
                  <SummaryFactGroup title="Próximos pasos" facts={meetingSummary.nextSteps} />
                  {meetingSummary.responsiblePeople.length > 0 && (
                    <div style={{ marginBottom: 12 }}>
                      <h4 style={summaryHeadingStyle}>Responsables</h4>
                      <ul style={summaryListStyle}>
                        {meetingSummary.responsiblePeople.map((person, index) => (
                          <li key={`${person.name}-${index}`}>
                            {person.name ?? 'Pendiente de confirmar'}: {person.responsibility}
                            {person.needsConfirmation && ' (confirmar)'} <span style={evidenceStyle}>Evidencia: {person.evidence}</span>
                          </li>
                        ))}
                      </ul>
                    </div>
                  )}
                  {meetingSummary.importantDates.length > 0 && (
                    <div style={{ marginBottom: 12 }}>
                      <h4 style={summaryHeadingStyle}>Fechas importantes</h4>
                      <ul style={summaryListStyle}>
                        {meetingSummary.importantDates.map((item, index) => (
                          <li key={`${item.description}-${index}`}>
                            {item.date ?? 'Fecha pendiente de confirmar'}: {item.description}
                            {item.responsiblePerson ? ` — ${item.responsiblePerson}` : ''}
                            {item.needsConfirmation && ' (confirmar)'} <span style={evidenceStyle}>Evidencia: {item.evidence}</span>
                          </li>
                        ))}
                      </ul>
                    </div>
                  )}
                </>
              ) : (
                <p style={{ color: '#334155', fontSize: 13 }}>{draftAnalysis.summary ?? 'El análisis no identificó un resumen estructurado.'}</p>
              )}
              {draftAnalysis.uncertainInformation.length > 0 && (
                <SummaryFactGroup title="Información pendiente de confirmar" facts={draftAnalysis.uncertainInformation.map((text) => ({ text, evidence: '' }))} />
              )}
            </section>
          )}
        </section>

        <section style={cardStyle}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 10, marginBottom: 6 }}>
            <h2 style={{ fontSize: 17, margin: 0, color: '#111827' }}>Consulta tus notas</h2>
            <span style={aiConfiguration?.configured ? savedBadge : pendingBadge}>
              {chatLoading ? 'Consultando…' : aiConfiguration?.configured ? 'IA disponible' : 'IA no configurada'}
            </span>
          </div>
          <p style={{ color: '#6b7280', fontSize: 13, margin: '0 0 12px' }}>
            Pregunta por el borrador y las notas guardadas del proyecto seleccionado.
          </p>
          {aiConfiguration && !aiConfiguration.configured && (
            <div role="status" style={{ padding: 11, marginBottom: 12, color: '#854d0e', background: '#fefce8', border: '1px solid #fde68a', borderRadius: 6, fontSize: 12 }}>
              El chat requiere Azure AI Foundry. Faltan: {aiConfiguration.missingSettings.join(', ') || 'configuración válida'}.
            </div>
          )}
          {chatError && <div role="alert" style={{ marginBottom: 10 }}><ErrorAlert message={chatError} /></div>}
          <div aria-live="polite" style={{ height: 300, overflowY: 'auto', border: '1px solid #e5e7eb', borderRadius: 6, padding: 12, marginBottom: 12, background: '#f8fafc' }}>
            {chatMessages.length === 0 ? (
              <p style={{ ...emptyStyle, textAlign: 'left' }}>Prueba: “¿De qué se habló en la nota?” o “¿Cuándo es la entrega del compromiso?”</p>
            ) : chatMessages.map((chatMessage, index) => (
              <div key={`${index}-${chatMessage.role}`} style={{ marginBottom: 12, textAlign: chatMessage.role === 'user' ? 'right' : 'left' }}>
                <div style={{ display: 'inline-block', maxWidth: '92%', padding: 10, borderRadius: 8, background: chatMessage.role === 'user' ? '#dbeafe' : '#fff', border: '1px solid #e2e8f0', color: '#1f2937', fontSize: 13, whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }}>
                  {chatMessage.text}
                </div>
              </div>
            ))}
            {chatLoading && <p style={{ color: '#64748b', fontSize: 13 }}>Consultando la información disponible…</p>}
          </div>
          <form onSubmit={askAboutNotes}>
            <label htmlFor="note-question" style={{ display: 'block', fontSize: 13, fontWeight: 600, marginBottom: 6 }}>Tu pregunta</label>
            <div style={{ display: 'flex', gap: 8 }}>
              <input
                id="note-question"
                value={chatQuestion}
                onChange={(event) => setChatQuestion(event.target.value)}
                maxLength={2000}
                placeholder="Pregunta sobre acuerdos, fechas o responsables…"
                style={inputStyle}
                disabled={chatLoading}
              />
              <button type="submit" disabled={!chatQuestion.trim() || chatLoading || aiConfiguration?.configured !== true} style={buttonStyle(!chatQuestion.trim() || chatLoading || aiConfiguration?.configured !== true)}>
                Preguntar
              </button>
            </div>
          </form>
          <button type="button" onClick={refreshAiConfiguration} disabled={checkingAiConfiguration} style={{ ...textButtonStyle, marginTop: 12 }}>
            {checkingAiConfiguration ? 'Verificando…' : 'Comprobar configuración de IA'}
          </button>
        </section>
      </div>

      {expandedDocument && (
      <section style={cardStyle}>
          <div style={{ marginTop: 18, border: '1px solid #dbeafe', background: '#f8fafc', borderRadius: 6, padding: 16 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', flexWrap: 'wrap', gap: 12, marginBottom: 12 }}>
              <div>
                <h3 style={{ fontSize: 15, color: '#111827', margin: '0 0 5px' }}>{expandedDocument.title}</h3>
                <p style={{ color: '#6b7280', fontSize: 12, margin: 0 }}>
                  {expandedDocument.documentType === 'MeetingNote' ? 'Nota' : expandedDocument.documentType}
                  {' · '}{expandedDocument.projectName ?? 'General'}
                  {' · Cargado '}{formatDate(expandedDocument.uploadedAt)}
                  {expandedDocument.updatedAt && ` · Modificado ${formatDate(expandedDocument.updatedAt)}`}
                </p>
                {expandedDocument.sourceDocumentId !== null && (
                  <p style={{ color: '#475569', fontSize: 12, margin: '6px 0 0' }}>
                    Resumen generado desde el documento #{expandedDocument.sourceDocumentId}
                    {expandedDocument.sourceAnalysisId !== null && ` · Análisis #${expandedDocument.sourceAnalysisId}`}
                    {' · '}
                    <button
                      type="button"
                      onClick={() => expandedDocument.sourceDocumentId !== null && showDocument(expandedDocument.sourceDocumentId)}
                      style={textButtonStyle}
                    >
                      Ver documento y análisis de origen
                    </button>
                  </p>
                )}
              </div>
              <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8 }}>
                {expandedDocument.hasOriginalFile && expandedDocument.originalFileName && (
                  <button type="button" onClick={() => downloadOriginal(expandedDocument)} style={textButtonStyle}>Descargar original</button>
                )}
                {expandedDocument.documentType === 'MeetingNote' && !editingNote && (
                  <button type="button" onClick={() => beginNoteEdit(expandedDocument)} style={textButtonStyle}>Editar nota</button>
                )}
                <button type="button" onClick={() => deleteDocument(expandedDocument)} disabled={deletingDocumentId === expandedDocument.id} style={{ ...textButtonStyle, color: '#b91c1c' }}>
                  {deletingDocumentId === expandedDocument.id ? 'Eliminando…' : 'Eliminar'}
                </button>
              </div>
            </div>
            {editingNote ? (
              <form onSubmit={saveNoteEdit}>
                <label style={fieldLabel}>Título
                  <input required maxLength={300} value={noteDraftTitle} onChange={(event) => setNoteDraftTitle(event.target.value)} style={inputStyle} />
                </label>
                <label style={fieldLabel}>Proyecto (opcional)
                  <select value={noteDraftProjectId} onChange={(event) => setNoteDraftProjectId(event.target.value)} style={inputStyle}>
                    <option value="">General (sin proyecto)</option>
                    {projects?.map((project) => <option key={project.id} value={project.id}>{project.name}</option>)}
                  </select>
                </label>
                <label style={fieldLabel}>Contenido
                  <textarea required value={noteDraftText} onChange={(event) => setNoteDraftText(event.target.value)} rows={10} style={{ ...inputStyle, resize: 'vertical', fontFamily: 'inherit' }} />
                </label>
                <div style={{ display: 'flex', gap: 8, marginTop: 10 }}>
                  <button type="submit" disabled={savingNoteEdit || !noteDraftTitle.trim() || !noteDraftText.trim()} style={buttonStyle(savingNoteEdit || !noteDraftTitle.trim() || !noteDraftText.trim())}>
                    {savingNoteEdit ? 'Guardando…' : 'Guardar cambios'}
                  </button>
                  <button type="button" onClick={() => setEditingNote(false)} disabled={savingNoteEdit} style={textButtonStyle}>Cancelar</button>
                </div>
              </form>
            ) : (
              <>
                <h4 style={{ fontSize: 13, color: '#374151', margin: '14px 0 8px' }}>Texto extraído / contenido</h4>
                <pre style={{ margin: 0, whiteSpace: 'pre-wrap', overflowWrap: 'anywhere', fontFamily: 'inherit', fontSize: 13, lineHeight: 1.6, color: '#374151', maxHeight: 420, overflowY: 'auto' }}>
                  {expandedDocument.extractedText}
                </pre>
              </>
            )}
            <div style={{ borderTop: '1px solid #e5e7eb', marginTop: 18, paddingTop: 16 }}>
              <div style={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', justifyContent: 'space-between', gap: 12 }}>
                <div>
                  <h3 style={{ fontSize: 16, color: '#111827', margin: '0 0 4px' }}>Análisis inteligente</h3>
                  <p style={{ color: '#6b7280', fontSize: 13, margin: 0 }}>
                    Documentos y notas pueden analizarse si Azure AI Foundry está configurado. Cada propuesta se registra solo al aceptarla.
                  </p>
                </div>
                <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-end', gap: 6 }}>
                  <button
                    type="button"
                    disabled={analysisLoading || checkingAiConfiguration || aiConfiguration?.configured !== true}
                    onClick={analyzeDocument}
                    style={buttonStyle(analysisLoading || checkingAiConfiguration || aiConfiguration?.configured !== true)}
                    title={aiConfiguration?.configured === false ? 'Configura Azure AI Foundry en el backend para habilitar el análisis.' : undefined}
                  >
                    {analysisLoading
                      ? 'Analizando…'
                      : checkingAiConfiguration || aiConfiguration === null
                        ? 'Verificando IA…'
                        : aiConfiguration.configured
                          ? 'Analizar con IA'
                          : 'IA no configurada'}
                  </button>
                  <button
                    type="button"
                    onClick={refreshAiConfiguration}
                    disabled={checkingAiConfiguration}
                    style={{ ...textButtonStyle, color: '#475569' }}
                  >
                    {checkingAiConfiguration ? 'Verificando…' : 'Volver a comprobar'}
                  </button>
                </div>
              </div>
              {analysisError && aiConfiguration?.configured !== false && (
                <div role="alert" style={{ marginTop: 12 }}>
                  <ErrorAlert message={analysisError} />
                </div>
              )}
              {aiConfigurationError && (
                <div role="alert" style={{ marginTop: 12 }}>
                  <ErrorAlert message={`No se pudo verificar la configuración de Azure AI Foundry: ${aiConfigurationError}`} />
                </div>
              )}
              {aiConfiguration && !aiConfiguration.configured && (
                <div role="status" style={{ marginTop: 12, padding: 12, color: '#854d0e', background: '#fefce8', border: '1px solid #fde68a', borderRadius: 6, fontSize: 13 }}>
                  <strong>Azure AI Foundry no está configurado.</strong>
                  <div>Faltan: {aiConfiguration.missingSettings.join(', ') || 'configuración válida'}.</div>
                    <pre style={{ margin: '8px 0 0', whiteSpace: 'pre-wrap', fontSize: 12 }}>
                      {`dotnet user-secrets set --project src\\ProjectManagement.Api\\ProjectManagement.Api.csproj "AzureAiFoundry:Endpoint" "https://<resource-name>.services.ai.azure.com/anthropic/v1/messages"
dotnet user-secrets set --project src\\ProjectManagement.Api\\ProjectManagement.Api.csproj "AzureAiFoundry:Model" "<deployment-name>"
dotnet user-secrets set --project src\\ProjectManagement.Api\\ProjectManagement.Api.csproj "AzureAiFoundry:ApiKey" "<API_KEY>"
dotnet user-secrets set --project src\\ProjectManagement.Api\\ProjectManagement.Api.csproj "AzureAiFoundry:ApiVersion" "2023-06-01"`}
                    </pre>
                    <div>
                      Sustituye los placeholders con los valores del deployment real de Claude en Microsoft Foundry. Después reinicia la API y pulsa “Volver a comprobar”. No compartas la clave ni la pongas en el frontend.
                    </div>
                </div>
              )}
              {analysisLoading && <LoadingSpinner message="Analizando texto del documento…" />}
              {!analysisLoading && analyses.length === 0 && (
                <p style={emptyStyle}>Este documento aún no tiene análisis.</p>
              )}
              {analyses.map((analysis) => (
                <AnalysisResult
                  key={analysis.id}
                  analysis={analysis}
                  projectId={expandedDocument.projectId ?? 0}
                  projectName={expandedDocument.projectName ?? 'Sin proyecto asociado'}
                  drafts={proposalDrafts}
                  developers={developers.data ?? []}
                  productOwners={productOwners.data ?? []}
                  peopleLoading={developers.loading || productOwners.loading}
                  peopleError={developers.error ?? productOwners.error}
                  reviewingProposalId={reviewingProposalId}
                  editingProposalIds={editingProposalIds}
                  onEdit={(proposalId) => setEditingProposalIds((current) =>
                    current.includes(proposalId)
                      ? current.filter((id) => id !== proposalId)
                      : [...current, proposalId],
                  )}
                  onChange={updateProposalDraft}
                  onReview={reviewProposal}
                  savingSummary={savingSummaryAnalysisId === analysis.id}
                  onSaveSummary={saveSummaryAsNote}
                  onOpenSavedNote={openSavedSummaryNote}
                />
              ))}
            </div>
          </div>
      </section>
      )}
    </div>
  );
}

function buttonStyle(disabled: boolean) {
  return {
    border: 0,
    borderRadius: 6,
    padding: '9px 14px',
    color: '#fff',
    background: disabled ? '#9ca3af' : '#2563eb',
    cursor: disabled ? 'not-allowed' : 'pointer',
    fontWeight: 600,
  } as const;
}

const emptyStyle = { padding: 16, textAlign: 'center' as const, color: '#6b7280', fontSize: 14 };
const savedBadge = {
  color: '#166534',
  background: '#f0fdf4',
  border: '1px solid #bbf7d0',
  borderRadius: 999,
  padding: '4px 9px',
  fontSize: 11,
  fontWeight: 600,
  whiteSpace: 'nowrap' as const,
};
const textButtonStyle: React.CSSProperties = {
  border: 0,
  background: 'none',
  padding: 0,
  color: '#2563eb',
  cursor: 'pointer',
  fontWeight: 600,
  fontSize: 12,
};

function formatMeetingSummary(summary: AiMeetingSummary) {
  const sections: [string, string[]][] = [
    ['Temas tratados', summary.topics.map(formatSummaryFact)],
    ['Acuerdos', summary.agreements.map(formatSummaryFact)],
    ['Decisiones', summary.decisions.map(formatSummaryFact)],
    ['Pendientes', summary.pendingItems.map(formatSummaryFact)],
    ['Compromisos identificados (no registrados)', summary.commitments.map(formatSummaryFact)],
    ['Entregables identificados (no registrados)', summary.deliverables.map(formatSummaryFact)],
    ['Responsables', summary.responsiblePeople.map((person) =>
      `- ${person.name ?? 'Pendiente de confirmar'}: ${person.responsibility}${person.needsConfirmation ? ' (confirmar)' : ''}\n  Evidencia: "${person.evidence}"`,
    )],
    ['Fechas importantes', summary.importantDates.map((item) =>
      `- ${item.description}: ${item.date ? formatDate(item.date) : 'Pendiente de confirmar'}${item.responsiblePerson ? ` · Responsable: ${item.responsiblePerson}` : ''}${item.needsConfirmation ? ' (confirmar)' : ''}\n  Evidencia: "${item.evidence}"`,
    )],
    ['Riesgos y observaciones', summary.risksAndObservations.map(formatSummaryFact)],
    ['Próximos pasos', summary.nextSteps.map(formatSummaryFact)],
  ];

  return [
    'Resumen general',
    summary.generalSummary,
    ...sections.flatMap(([title, lines]) => lines.length ? ['', title, ...lines] : []),
  ].join('\n');
}

function formatSummaryFact(fact: { text: string; evidence: string }) {
  return `- ${fact.text}\n  Evidencia: "${fact.evidence}"`;
}

function AnalysisResult({
  analysis,
  projectId,
  projectName,
  drafts,
  developers,
  productOwners,
  peopleLoading,
  peopleError,
  reviewingProposalId,
  editingProposalIds,
  savingSummary,
  onEdit,
  onChange,
  onReview,
  onSaveSummary,
  onOpenSavedNote,
}: {
  analysis: AiAnalysisResponse;
  projectId: number;
  projectName: string;
  drafts: Record<string, AiAnalysisProposal>;
  developers: { id: number; displayName: string }[];
  productOwners: { id: number; displayName: string }[];
  peopleLoading: boolean;
  peopleError: string | null;
  reviewingProposalId: string | null;
  editingProposalIds: string[];
  savingSummary: boolean;
  onEdit: (proposalId: string) => void;
  onChange: (
    proposal: AiAnalysisProposal,
    field: 'title' | 'description' | 'developerName' | 'productOwnerName' | 'startDate' | 'endDate' | 'qaDate' | 'preDate' | 'protDate' | 'prodDate',
    value: string,
  ) => void;
  onReview: (analysis: AiAnalysisResponse, proposal: AiAnalysisProposal, decision: 'Accepted' | 'Rejected') => void;
  onSaveSummary: (analysisId: number, title: string, text: string) => void;
  onOpenSavedNote: (noteId: number) => void;
}) {
  const result = analysis.result;
  const meetingSummary = result?.meetingSummary;
  const [editingSummary, setEditingSummary] = useState(false);
  const [summaryTitle, setSummaryTitle] = useState(meetingSummary?.title ?? '');
  const [summaryText, setSummaryText] = useState(meetingSummary ? formatMeetingSummary(meetingSummary) : '');

  useEffect(() => {
    if (analysis.generatedNoteId !== null) setEditingSummary(false);
  }, [analysis.generatedNoteId]);

  return (
    <article style={{ marginTop: 16, padding: 14, border: '1px solid #e5e7eb', borderRadius: 6, background: '#fff' }}>
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8, alignItems: 'center', marginBottom: 10 }}>
        <strong style={{ color: '#111827' }}>Análisis #{analysis.id}</strong>
        <span style={analysis.status === 'Failed' ? errorBadge : statusBadge}>{analysis.status}</span>
        <span style={{ color: '#9ca3af', fontSize: 12 }}>{formatDate(analysis.createdAt)}</span>
      </div>
      {analysis.processingError && (
        <p role="alert" style={{ color: '#991b1b', fontSize: 13 }}>{analysis.processingError}</p>
      )}
      {result && (
        <>
          {meetingSummary ? (
            <section style={{ margin: '14px 0', padding: 14, border: '1px solid #bfdbfe', borderRadius: 6, background: '#eff6ff' }}>
              <div style={{ display: 'flex', flexWrap: 'wrap', justifyContent: 'space-between', alignItems: 'center', gap: 8 }}>
                <h4 style={{ fontSize: 15, margin: 0, color: '#1e3a8a' }}>Resumen inteligente</h4>
                {analysis.generatedNoteId !== null && <span style={statusBadge}>Nota guardada</span>}
              </div>
              {editingSummary ? (
                <>
                  <label style={fieldLabel}>Título
                    <input
                      required
                      maxLength={300}
                      value={summaryTitle}
                      onChange={(event) => setSummaryTitle(event.target.value)}
                      style={analysisInput}
                    />
                  </label>
                  <label style={fieldLabel}>Resumen editable
                    <textarea
                      required
                      maxLength={10 * 1024 * 1024}
                      rows={14}
                      value={summaryText}
                      onChange={(event) => setSummaryText(event.target.value)}
                      style={{ ...analysisInput, resize: 'vertical', fontFamily: 'inherit' }}
                    />
                  </label>
                </>
              ) : (
                <>
                  <h5 style={{ color: '#111827', margin: '12px 0 6px' }}>{summaryTitle}</h5>
                  <pre style={{ margin: 0, whiteSpace: 'pre-wrap', overflowWrap: 'anywhere', fontFamily: 'inherit', fontSize: 13, lineHeight: 1.6, color: '#374151' }}>
                    {summaryText}
                  </pre>
                </>
              )}
              <div style={{ display: 'flex', flexWrap: 'wrap', gap: 10, marginTop: 12 }}>
                {analysis.generatedNoteId === null && !editingSummary && (
                  <button type="button" onClick={() => setEditingSummary(true)} style={textButtonStyle}>Editar resumen</button>
                )}
                {analysis.generatedNoteId === null && editingSummary && (
                  <>
                    <button
                      type="button"
                      disabled={savingSummary || !summaryTitle.trim() || !summaryText.trim()}
                      onClick={() => onSaveSummary(analysis.id, summaryTitle.trim(), summaryText)}
                      style={buttonStyle(savingSummary || !summaryTitle.trim() || !summaryText.trim())}
                    >
                      {savingSummary ? 'Guardando…' : 'Guardar como nota'}
                    </button>
                    <button
                      type="button"
                      disabled={savingSummary}
                      onClick={() => {
                        setSummaryTitle(meetingSummary.title);
                        setSummaryText(formatMeetingSummary(meetingSummary));
                        setEditingSummary(false);
                      }}
                      style={textButtonStyle}
                    >
                      Cancelar
                    </button>
                  </>
                )}
                {analysis.generatedNoteId !== null && (
                  <button
                    type="button"
                    onClick={() => analysis.generatedNoteId !== null && onOpenSavedNote(analysis.generatedNoteId)}
                    style={textButtonStyle}
                  >
                    Ver / editar nota guardada
                  </button>
                )}
              </div>
              <p style={{ color: '#64748b', fontSize: 12, margin: '10px 0 0' }}>
                Guardar este resumen no crea compromisos ni entregables. Revisa las propuestas por separado antes de aceptarlas.
              </p>
            </section>
          ) : (
            <p style={emptyStyle}>Este análisis histórico no contiene un resumen inteligente. Puedes volver a analizar el documento para generarlo.</p>
          )}
          {result.projectName && (
            <p style={{ margin: '8px 0', fontSize: 13 }}><strong>Proyecto detectado:</strong> {result.projectName}</p>
          )}
          {result.summary && (
            <p style={{ margin: '8px 0 12px', color: '#4b5563', fontSize: 13 }}>{result.summary}</p>
          )}
          {peopleError && <div role="alert"><ErrorAlert message={peopleError} /></div>}
          {result.uncertainInformation.length > 0 && (
            <div style={{ background: '#fffbeb', border: '1px solid #fde68a', padding: 12, borderRadius: 6, margin: '12px 0' }}>
              <strong style={{ color: '#92400e', fontSize: 13 }}>Pendiente de confirmar</strong>
              <ul style={{ margin: '6px 0 0', paddingLeft: 20, color: '#78350f', fontSize: 13 }}>
                {result.uncertainInformation.map((item, index) => <li key={`${index}-${item}`}>{item}</li>)}
              </ul>
            </div>
          )}
          {result.proposals.length === 0 ? (
            <p style={emptyStyle}>El análisis no detectó propuestas de gestión.</p>
          ) : result.proposals.map((proposal) => {
            const draft = drafts[proposal.id] ?? proposal;
            const alreadyReviewed = proposal.decision !== 'Pending';
            const isEditing = editingProposalIds.includes(proposal.id);
            const missingFields = getMissingFields(draft);
            return (
              <section key={proposal.id} style={{ padding: 12, marginTop: 10, border: '1px solid #e5e7eb', borderRadius: 6 }}>
                <div style={{ display: 'flex', flexWrap: 'wrap', justifyContent: 'space-between', gap: 8 }}>
                  <div>
                    <strong style={{ fontSize: 14, color: '#111827' }}>{proposal.category}: {draft.title}</strong>
                    <div style={analysisInfo}>Proyecto: {projectName}</div>
                  </div>
                  <span style={proposal.decision === 'Accepted' ? statusBadge : proposal.decision === 'Rejected' ? errorBadge : pendingBadge}>
                    {proposal.registeredEntityType ? 'Registrada' : proposal.decision}
                  </span>
                </div>
                {proposal.confirmationRequired && (
                  <p style={{ color: '#92400e', fontSize: 12, margin: '8px 0' }}>
                    Confianza baja o responsable/proyecto ambiguo: confirma los datos antes de aceptar.
                  </p>
                )}
                <label style={fieldLabel}>Título</label>
                <input value={draft.title} onChange={(event) => onChange(proposal, 'title', event.target.value)} maxLength={300} style={analysisInput} disabled={alreadyReviewed || !isEditing} />
                <label style={fieldLabel}>Descripción</label>
                <textarea value={draft.description ?? ''} onChange={(event) => onChange(proposal, 'description', event.target.value)} maxLength={4000} rows={2} style={analysisInput} disabled={alreadyReviewed || !isEditing} />
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(min(100%, 180px), 1fr))', gap: 8 }}>
                  <AnalysisPersonField label="Desarrollador" value={draft.developerName} options={developers.map((person) => person.displayName)} onChange={(value) => onChange(proposal, 'developerName', value)} disabled={alreadyReviewed || !isEditing || peopleLoading} />
                  <AnalysisPersonField label="Product Owner" value={draft.productOwnerName} options={productOwners.map((person) => person.displayName)} onChange={(value) => onChange(proposal, 'productOwnerName', value)} disabled={alreadyReviewed || !isEditing || peopleLoading} />
                  <AnalysisField label="Inicio" type="date" value={draft.startDate} onChange={(value) => onChange(proposal, 'startDate', value)} disabled={alreadyReviewed || !isEditing} />
                  <AnalysisField label={draft.category === 'Milestone' ? 'Fecha límite' : 'Fin / compromiso'} type="date" value={draft.endDate} onChange={(value) => onChange(proposal, 'endDate', value)} disabled={alreadyReviewed || !isEditing} />
                  <AnalysisField label="QA" type="date" value={draft.qaDate} onChange={(value) => onChange(proposal, 'qaDate', value)} disabled={alreadyReviewed || !isEditing} />
                  <AnalysisField label="PRE" type="date" value={draft.preDate} onChange={(value) => onChange(proposal, 'preDate', value)} disabled={alreadyReviewed || !isEditing} />
                  <AnalysisField label="PROT" type="date" value={draft.protDate} onChange={(value) => onChange(proposal, 'protDate', value)} disabled={alreadyReviewed || !isEditing} />
                  <AnalysisField label="PROD" type="date" value={draft.prodDate} onChange={(value) => onChange(proposal, 'prodDate', value)} disabled={alreadyReviewed || !isEditing} />
                </div>
                <p style={analysisInfo}><strong>Fechas:</strong> {formatDate(draft.startDate)} → {formatDate(draft.endDate)}</p>
                {proposal.dependencies.length > 0 && <p style={analysisInfo}><strong>Dependencias:</strong> {proposal.dependencies.join(', ')}</p>}
                {proposal.risks.length > 0 && <p style={analysisInfo}><strong>Riesgos:</strong> {proposal.risks.join(', ')}</p>}
                <p style={analysisInfo}><strong>Evidencia:</strong> “{proposal.evidence}” · confianza {Math.round(proposal.confidence * 100)}%</p>
                {!alreadyReviewed && missingFields.length > 0 && (
                  <p role="status" style={{ color: '#92400e', fontSize: 12, margin: '8px 0' }}>
                    Para registrar esta propuesta, completa: {missingFields.join(', ')}.
                  </p>
                )}
                {proposal.registeredEntityType && proposal.registeredEntityId && (
                  <Link
                    to={getRegisteredEntityUrl(proposal.registeredEntityType, projectId, proposal.registeredEntityId)}
                    style={{ display: 'inline-block', marginTop: 10, color: '#2563eb', fontWeight: 600, fontSize: 13 }}
                  >
                    Abrir {getEntityLabel(proposal.registeredEntityType)} registrado
                  </Link>
                )}
                {!alreadyReviewed && (
                  <div style={{ display: 'flex', gap: 8, marginTop: 10 }}>
                    <button type="button" disabled={reviewingProposalId === proposal.id || peopleLoading || missingFields.length > 0} onClick={() => onReview(analysis, { ...draft, confirmationRequired: false }, 'Accepted')} style={buttonStyle(reviewingProposalId === proposal.id || peopleLoading || missingFields.length > 0)}>
                      {['Commitment', 'Activity', 'Milestone', 'Deliverable'].includes(proposal.category) ? 'Aceptar y registrar' : 'Aceptar revisión'}
                    </button>
                    <button type="button" disabled={reviewingProposalId === proposal.id} onClick={() => onReview(analysis, draft, 'Rejected')} style={secondaryButtonStyle(reviewingProposalId === proposal.id)}>Rechazar</button>
                    <button type="button" disabled={reviewingProposalId === proposal.id} onClick={() => onEdit(proposal.id)} style={secondaryButtonStyle(reviewingProposalId === proposal.id)}>
                      {isEditing ? 'Terminar edición' : 'Editar'}
                    </button>
                  </div>
                )}
              </section>
            );
          })}
        </>
      )}
    </article>
  );
}

function getMissingFields(proposal: AiAnalysisProposal): string[] {
  if (!['Commitment', 'Activity', 'Milestone', 'Deliverable'].includes(proposal.category)) return [];
  const missing: string[] = [];
  if (!proposal.title.trim()) missing.push('título');
  if (proposal.category === 'Commitment' || proposal.category === 'Activity') {
    if (!proposal.developerName) missing.push('desarrollador');
    if (!proposal.productOwnerName) missing.push('Product Owner');
    if (!proposal.startDate) missing.push('fecha de inicio');
    if (!proposal.endDate) missing.push('fecha compromiso');
    if (proposal.startDate && proposal.endDate && proposal.endDate < proposal.startDate)
      missing.push('un rango de fechas válido');
  }
  if (proposal.category === 'Milestone' && !proposal.endDate) missing.push('fecha límite');
  return missing;
}

function getEntityLabel(type: 'Commitment' | 'Milestone' | 'Deliverable'): string {
  return type === 'Commitment' ? 'compromiso' : type === 'Milestone' ? 'milestone' : 'entregable';
}

function getRegisteredEntityUrl(
  type: 'Commitment' | 'Milestone' | 'Deliverable',
  projectId: number,
  entityId: number,
): string {
  if (type === 'Commitment')
    return `/projects/${projectId}?tab=timeline&commitmentId=${entityId}`;
  return `/projects/${projectId}#${type === 'Milestone' ? 'milestones' : 'deliverables'}`;
}

function AnalysisField({
  label,
  value,
  onChange,
  disabled,
  type = 'text',
}: {
  label: string;
  value: string | null;
  onChange: (value: string) => void;
  disabled: boolean;
  type?: string;
}) {
  return (
    <div>
      <label style={fieldLabel}>{label}</label>
      <input type={type} value={value ?? ''} onChange={(event) => onChange(event.target.value)} style={analysisInput} disabled={disabled} />
    </div>
  );
}

function AnalysisPersonField({
  label,
  value,
  options,
  onChange,
  disabled,
}: {
  label: string;
  value: string | null;
  options: string[];
  onChange: (value: string) => void;
  disabled: boolean;
}) {
  return (
    <div>
      <label style={fieldLabel}>{label}</label>
      <select
        value={value ?? ''}
        onChange={(event) => onChange(event.target.value)}
        style={analysisInput}
        disabled={disabled}
      >
        <option value="">Selecciona una persona</option>
        {value && !options.includes(value) && <option value={value}>{value} (no coincide)</option>}
        {options.map((option) => <option key={option} value={option}>{option}</option>)}
      </select>
    </div>
  );
}

function SummaryFactGroup({ title, facts }: { title: string; facts: AiSummaryFact[] }) {
  if (facts.length === 0) return null;
  return (
    <div style={{ marginBottom: 12 }}>
      <h4 style={summaryHeadingStyle}>{title}</h4>
      <ul style={summaryListStyle}>
        {facts.map((fact, index) => (
          <li key={`${fact.text}-${index}`}>
            {fact.text}
            {fact.evidence && <span style={evidenceStyle}>Evidencia: {fact.evidence}</span>}
          </li>
        ))}
      </ul>
    </div>
  );
}

const analysisInput = { display: 'block', boxSizing: 'border-box' as const, width: '100%', padding: '7px 9px', margin: '4px 0 9px', border: '1px solid #d1d5db', borderRadius: 5, font: '13px inherit' };
const fieldLabel = { display: 'block', fontSize: 12, fontWeight: 600 as const, color: '#4b5563', marginTop: 8 };
const analysisInfo = { color: '#4b5563', fontSize: 12, margin: '8px 0 0', overflowWrap: 'anywhere' as const };
const summaryHeadingStyle = { margin: '0 0 5px', fontSize: 13, color: '#334155' };
const summaryListStyle = { margin: '0 0 0 18px', padding: 0, color: '#334155', fontSize: 12, lineHeight: 1.6 };
const evidenceStyle = { display: 'block', color: '#64748b', fontSize: 11, overflowWrap: 'anywhere' as const };
const statusBadge = { color: '#166534', background: '#f0fdf4', borderRadius: 999, padding: '4px 8px', fontSize: 12 };
const pendingBadge = { color: '#92400e', background: '#fffbeb', borderRadius: 999, padding: '4px 8px', fontSize: 12 };
const errorBadge = { color: '#991b1b', background: '#fef2f2', borderRadius: 999, padding: '4px 8px', fontSize: 12 };

function secondaryButtonStyle(disabled: boolean) {
  return {
    border: '1px solid #d1d5db',
    borderRadius: 6,
    padding: '9px 14px',
    color: disabled ? '#9ca3af' : '#374151',
    background: '#fff',
    cursor: disabled ? 'not-allowed' : 'pointer',
    fontWeight: 600,
  } as const;
}
