import { useEffect, useState } from 'react';
import { azureDevOpsService } from '../services/azureDevOpsService';
import type {
  AzureDevOpsProjectResponse,
  AzureDevOpsStatusResponse,
  AzureDevOpsWorkItemDetailResponse,
  AzureDevOpsWorkItemResponse,
} from '../types/api';
import { ApiRequestError } from '../services/apiClient';

export function AzureDevOpsPage() {
  const [status, setStatus] = useState<AzureDevOpsStatusResponse | null>(null);
  const [projects, setProjects] = useState<AzureDevOpsProjectResponse[]>([]);
  const [projectId, setProjectId] = useState('');
  const [workItems, setWorkItems] = useState<AzureDevOpsWorkItemResponse[]>([]);
  const [selectedItem, setSelectedItem] = useState<AzureDevOpsWorkItemDetailResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadingItems, setLoadingItems] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    const load = async () => {
      setLoading(true);
      setError(null);
      try {
        const [connection, visibleProjects] = await Promise.all([
          azureDevOpsService.getStatus(),
          azureDevOpsService.getProjects(),
        ]);
        if (!active) return;
        setStatus(connection);
        setProjects(visibleProjects);
      } catch (loadError) {
        if (active) setError(formatAzureDevOpsError(loadError));
      } finally {
        if (active) setLoading(false);
      }
    };
    void load();
    return () => { active = false; };
  }, []);

  const loadWorkItems = async (value: string) => {
    setProjectId(value);
    setWorkItems([]);
    setSelectedItem(null);
    if (!value) return;

    setLoadingItems(true);
    setError(null);
    try {
      setWorkItems(await azureDevOpsService.getProjectWorkItems(value));
    } catch (loadError) {
      setError(formatAzureDevOpsError(loadError));
    } finally {
      setLoadingItems(false);
    }
  };

  const showDetails = async (id: number) => {
    setError(null);
    try {
      setSelectedItem(await azureDevOpsService.getWorkItem(id));
    } catch (loadError) {
      setError(formatAzureDevOpsError(loadError));
    }
  };

  return (
    <section style={{ maxWidth: 1200, margin: '0 auto' }}>
      <header style={{ marginBottom: 24 }}>
        <h1 style={{ margin: 0, color: '#111827' }}>Azure DevOps</h1>
        <p style={{ color: '#6b7280' }}>
          Consulta proyectos y work items con los permisos de tu sesión de Microsoft Entra ID.
        </p>
      </header>

      {loading && <p role="status">Comprobando la conexión con Azure DevOps…</p>}
      {!loading && status && (
        <div style={statusStyle} role="status">
          <strong>Conexión comprobada</strong>
          <span>Organización: {status.organizationUrl}</span>
          <span>Proyectos visibles: {status.visibleProjectCount}</span>
        </div>
      )}
      {error && <div role="alert" style={errorStyle}>{error}</div>}

      {!loading && status && (
        <section style={cardStyle}>
          <label htmlFor="ado-project" style={{ display: 'block', fontWeight: 600, marginBottom: 8 }}>
            Proyecto de Azure DevOps
          </label>
          <select
            id="ado-project"
            value={projectId}
            onChange={(event) => void loadWorkItems(event.target.value)}
            style={inputStyle}
          >
            <option value="">Selecciona un proyecto visible</option>
            {projects.map((project) => (
              <option key={project.id} value={project.id}>{project.name} · {project.state}</option>
            ))}
          </select>

          {loadingItems && <p role="status">Cargando work items…</p>}
          {projectId && !loadingItems && workItems.length === 0 && !error && (
            <p>No hay work items visibles en este proyecto.</p>
          )}
          {workItems.length > 0 && (
            <div style={{ overflowX: 'auto', marginTop: 16 }}>
              <table style={{ width: '100%', borderCollapse: 'collapse', minWidth: 760 }}>
                <thead>
                  <tr>
                    {['ID', 'Título', 'Tipo', 'Estado', 'Asignado a', 'Inicio', 'Fecha objetivo', ''].map((header) => (
                      <th key={header} style={cellHeaderStyle}>{header}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {workItems.map((item) => (
                    <tr key={item.id}>
                      <td style={cellStyle}>{item.id}</td>
                      <td style={cellStyle}>{item.title}</td>
                      <td style={cellStyle}>{item.workItemType}</td>
                      <td style={cellStyle}>{item.state ?? '—'}</td>
                      <td style={cellStyle}>{item.assignedTo ?? 'Sin asignar'}</td>
                      <td style={cellStyle}>{formatDate(item.startDate)}</td>
                      <td style={cellStyle}>{formatDate(item.targetDate)}</td>
                      <td style={cellStyle}>
                        <button type="button" onClick={() => void showDetails(item.id)}>Ver detalle</button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </section>
      )}

      {selectedItem && (
        <section style={{ ...cardStyle, marginTop: 20 }} aria-label="Detalle de work item">
          <h2 style={{ marginTop: 0 }}>#{selectedItem.workItem.id} — {selectedItem.workItem.title}</h2>
          <dl style={detailGridStyle}>
            <dt>Proyecto</dt><dd>{selectedItem.workItem.projectName}</dd>
            <dt>Tipo / estado</dt><dd>{selectedItem.workItem.workItemType} · {selectedItem.workItem.state ?? '—'}</dd>
            <dt>Asignado a</dt><dd>{selectedItem.workItem.assignedTo ?? 'Sin asignar'}</dd>
            <dt>Creado</dt><dd>{formatDate(selectedItem.workItem.createdDate)}</dd>
            <dt>Modificado</dt><dd>{formatDate(selectedItem.workItem.changedDate)}</dd>
            <dt>Proyecto local relacionado</dt>
            <dd>{selectedItem.workItem.localProjectName ?? (selectedItem.workItem.localProjectId ? `ID ${selectedItem.workItem.localProjectId}` : 'Sin vínculo local')}</dd>
            <dt>Compromiso local relacionado</dt>
            <dd>{selectedItem.workItem.commitmentTitle ?? 'Sin vínculo local'}</dd>
          </dl>
          {selectedItem.workItem.url && (
            <a href={selectedItem.workItem.url} target="_blank" rel="noreferrer">Abrir en Azure DevOps</a>
          )}
        </section>
      )}
    </section>
  );
}

function formatAzureDevOpsError(error: unknown): string {
  if (error instanceof ApiRequestError) {
    switch (error.code) {
      case 'entra_not_configured':
      case 'azure_devops_not_configured':
        return 'El administrador debe configurar Entra ID y la URL HTTPS de la organización en Azure DevOps.';
      case 'backend_credential_not_configured':
        return 'El administrador debe configurar un secreto o certificado del backend en un almacén seguro.';
      case 'consent_required':
        return 'Falta consentimiento para el permiso delegado de Azure DevOps. Solicita el consentimiento al administrador y vuelve a iniciar sesión.';
      case 'permission_denied':
        return 'Azure DevOps denegó el acceso. Confirma que tu usuario tenga permisos en la organización y el proyecto.';
      case 'token_acquisition_failed':
        return 'Microsoft Entra ID no pudo emitir el token delegado. Revisa credenciales, consentimiento y configuración OBO.';
      default:
        return error.message;
    }
  }
  return error instanceof Error ? error.message : 'No fue posible consultar Azure DevOps.';
}

function formatDate(value: string | null): string {
  return value ? new Date(value).toLocaleDateString() : '—';
}

const cardStyle = {
  padding: 20,
  background: '#fff',
  border: '1px solid #e5e7eb',
  borderRadius: 10,
  color: '#1f2937',
} as const;
const statusStyle = {
  display: 'flex',
  flexWrap: 'wrap',
  gap: '8px 20px',
  padding: 14,
  marginBottom: 16,
  color: '#166534',
  background: '#f0fdf4',
  border: '1px solid #bbf7d0',
  borderRadius: 8,
} as const;
const errorStyle = {
  padding: 14,
  marginBottom: 16,
  color: '#991b1b',
  background: '#fef2f2',
  border: '1px solid #fecaca',
  borderRadius: 8,
} as const;
const inputStyle = {
  width: '100%',
  maxWidth: 560,
  padding: '10px 12px',
  border: '1px solid #d1d5db',
  borderRadius: 6,
  background: '#fff',
} as const;
const cellHeaderStyle = {
  textAlign: 'left',
  padding: 10,
  borderBottom: '2px solid #e5e7eb',
  color: '#374151',
  whiteSpace: 'nowrap',
} as const;
const cellStyle = {
  padding: 10,
  borderBottom: '1px solid #e5e7eb',
  verticalAlign: 'top',
} as const;
const detailGridStyle = {
  display: 'grid',
  gridTemplateColumns: 'max-content 1fr',
  gap: '8px 18px',
} as const;
