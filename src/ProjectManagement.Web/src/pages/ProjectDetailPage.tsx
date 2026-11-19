import { useState } from 'react';
import { useParams, Link, useSearchParams } from 'react-router-dom';
import { useApi } from '../hooks/useApi';
import { projectService } from '../services/projectService';
import { environmentDeliveryService } from '../services/environmentDeliveryService';
import { alertService } from '../services/alertService';
import { Card } from '../components/ui/Card';
import { StatusBadge } from '../components/ui/StatusBadge';
import { PriorityBadge } from '../components/ui/PriorityBadge';
import { LoadingSpinner } from '../components/ui/LoadingSpinner';
import { ErrorAlert } from '../components/ui/ErrorAlert';
import { AlertsPanel } from '../components/shared/AlertsPanel';
import { TimelineView } from '../components/timeline/TimelineView';
import { formatDate } from '../utils/format';

type Tab = 'detail' | 'timeline';

export function ProjectDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [searchParams, setSearchParams] = useSearchParams();
  const [activeTab, setActiveTab] = useState<Tab>(
    searchParams.get('tab') === 'timeline' ? 'timeline' : 'detail',
  );
  const projectId = Number(id);

  const project = useApi(() => projectService.getById(projectId), [projectId]);
  const members = useApi(() => projectService.getMembers(projectId), [projectId]);
  const commitments = useApi(() => projectService.getCommitments(projectId), [projectId]);
  const milestones = useApi(() => projectService.getMilestones(projectId), [projectId]);
  const deliverables = useApi(() => projectService.getDeliverables(projectId), [projectId]);
  const envDeliveries = useApi(() => environmentDeliveryService.getAll({ projectId }), [projectId]);
  const projectAlerts = useApi(() => alertService.getByProject(projectId), [projectId]);

  if (project.loading) return <LoadingSpinner message="Cargando proyecto..." />;
  if (project.error) return <ErrorAlert message={project.error} />;
  if (!project.data) return <ErrorAlert message="Proyecto no encontrado." />;

  const p = project.data;

  return (
    <div>
      <Link to="/projects" style={{ fontSize: 13, color: '#2563eb', textDecoration: 'none', marginBottom: 12, display: 'inline-block' }}>
        ← Volver a proyectos
      </Link>

      {/* Header */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 24 }}>
        <div>
          <h1 style={{ fontSize: 24, fontWeight: 700, color: '#111827', margin: 0 }}>{p.name}</h1>
          <p style={{ fontSize: 14, color: '#6b7280', marginTop: 4 }}>{p.description}</p>
        </div>
        <StatusBadge status={p.status} />
      </div>

      {/* Tab navigation */}
      <div style={{ display: 'flex', gap: 0, borderBottom: '2px solid #e5e7eb', marginBottom: 24 }}>
        {([['detail', '📋 Detalle'], ['timeline', '📊 Timeline']] as [Tab, string][]).map(([tab, label]) => (
          <button
            key={tab}
            onClick={() => {
              setActiveTab(tab);
              setSearchParams((current) => {
                const next = new URLSearchParams(current);
                next.set('tab', tab);
                return next;
              }, { replace: true });
            }}
            style={{
              padding: '10px 24px',
              fontSize: 14,
              fontWeight: activeTab === tab ? 700 : 400,
              color: activeTab === tab ? '#2563eb' : '#6b7280',
              backgroundColor: 'transparent',
              border: 'none',
              borderBottom: activeTab === tab ? '2px solid #2563eb' : '2px solid transparent',
              cursor: 'pointer',
              marginBottom: -2,
            }}
          >
            {label}
          </button>
        ))}
      </div>

      {activeTab === 'timeline' && (
        <TimelineView
          projectId={projectId}
          initialCommitmentId={Number(searchParams.get('commitmentId')) || undefined}
        />
      )}

      {activeTab === 'detail' && <>
      {/* Info grid */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(155px, 1fr))', gap: 14, marginBottom: 24 }}>
        <Card>
          <div style={{ fontSize: 12, color: '#6b7280' }}>Producto</div>
          <div style={{ fontSize: 16, fontWeight: 600, marginTop: 4 }}>{p.product || '—'}</div>
        </Card>
        <Card>
          <div style={{ fontSize: 12, color: '#6b7280' }}>Fechas</div>
          <div style={{ fontSize: 14, fontWeight: 500, marginTop: 4 }}>{formatDate(p.startDate)} → {formatDate(p.endDate)}</div>
        </Card>
        <Card>
          <div style={{ fontSize: 12, color: '#6b7280' }}>Product Owner</div>
          <div style={{ fontSize: 14, fontWeight: 600, marginTop: 4 }}>
            {p.productOwnerNames?.length ? p.productOwnerNames.join(', ') : 'Sin asignar'}
          </div>
        </Card>
        <Card>
          <div style={{ fontSize: 12, color: '#6b7280' }}>Miembros</div>
          <div style={{ fontSize: 22, fontWeight: 700, marginTop: 4 }}>{p.memberCount}</div>
        </Card>
        <Card>
          <div style={{ fontSize: 12, color: '#6b7280' }}>Total de compromisos</div>
          <div style={{ fontSize: 22, fontWeight: 700, marginTop: 4 }}>{p.commitmentCount}</div>
        </Card>
        <Card>
          <div style={{ fontSize: 12, color: '#6b7280' }}>Pendientes</div>
          <div style={{ fontSize: 22, fontWeight: 700, color: '#b45309', marginTop: 4 }}>{p.pendingCommitmentCount}</div>
        </Card>
        <Card>
          <div style={{ fontSize: 12, color: '#6b7280' }}>En progreso</div>
          <div style={{ fontSize: 22, fontWeight: 700, color: '#2563eb', marginTop: 4 }}>{p.inProgressCommitmentCount}</div>
        </Card>
        <Card>
          <div style={{ fontSize: 12, color: '#6b7280' }}>Completados</div>
          <div style={{ fontSize: 22, fontWeight: 700, color: '#16a34a', marginTop: 4 }}>{p.completedCommitmentCount}</div>
        </Card>
      </div>

      {/* Members */}
      <Card id="milestones" style={{ marginBottom: 20 }}>
        <h3 style={sectionTitle}>👥 Miembros del equipo</h3>
        {members.loading ? <LoadingSpinner /> : !members.data?.length ? (
          <p style={emptyText}>No hay miembros asignados.</p>
        ) : (
          <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
            {members.data.map((m) => (
              <div key={m.id} style={{ padding: '8px 14px', borderRadius: 8, backgroundColor: '#f3f4f6', fontSize: 13 }}>
                <strong>{m.displayName}</strong>
                <div style={{ color: '#6b7280', fontSize: 12 }}>{m.role} · {m.email}</div>
              </div>
            ))}
          </div>
        )}
      </Card>

      {/* Commitments */}
      <Card id="deliverables" style={{ marginBottom: 20 }}>
        <h3 style={sectionTitle}>🎯 Compromisos</h3>
        {commitments.loading ? <LoadingSpinner /> : !commitments.data?.length ? (
          <p style={emptyText}>No hay compromisos.</p>
        ) : (
          <table style={tableStyle}>
            <thead>
              <tr style={{ borderBottom: '2px solid #e5e7eb' }}>
                <th style={th}>Título</th>
                <th style={th}>Desarrollador</th>
                <th style={th}>Inicio</th>
                <th style={th}>Compromiso</th>
                <th style={th}>QA</th>
                <th style={th}>PRE</th>
                <th style={th}>PROT</th>
                <th style={th}>Estado</th>
                <th style={th}>Prioridad</th>
              </tr>
            </thead>
            <tbody>
              {commitments.data.map((c) => (
                <tr key={c.id} style={{ borderBottom: '1px solid #f3f4f6' }}>
                  <td style={td}>{c.title}</td>
                  <td style={td}>{c.developerName}</td>
                  <td style={td}>{formatDate(c.startDate)}</td>
                  <td style={td}>{formatDate(c.commitmentDate)}</td>
                  <td style={td}>{formatDate(c.qaDate)}</td>
                  <td style={td}>{formatDate(c.preDate)}</td>
                  <td style={td}>{formatDate(c.protDate)}</td>
                  <td style={td}><StatusBadge status={c.status} /></td>
                  <td style={td}><PriorityBadge priority={c.priority} /></td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </Card>

      {/* Milestones */}
      <Card style={{ marginBottom: 20 }}>
        <h3 style={sectionTitle}>🏁 Milestones</h3>
        {milestones.loading ? <LoadingSpinner /> : !milestones.data?.length ? (
          <p style={emptyText}>No hay milestones.</p>
        ) : (
          <table style={tableStyle}>
            <thead>
              <tr style={{ borderBottom: '2px solid #e5e7eb' }}>
                <th style={th}>Título</th>
                <th style={th}>Fecha límite</th>
                <th style={th}>Completado</th>
                <th style={th}>Estado</th>
              </tr>
            </thead>
            <tbody>
              {milestones.data.map((m) => (
                <tr key={m.id} style={{ borderBottom: '1px solid #f3f4f6' }}>
                  <td style={td}>{m.title}</td>
                  <td style={td}>{formatDate(m.dueDate)}</td>
                  <td style={td}>{formatDate(m.completedDate)}</td>
                  <td style={td}><StatusBadge status={m.status} /></td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </Card>

      {/* Deliverables */}
      <Card style={{ marginBottom: 20 }}>
        <h3 style={sectionTitle}>📦 Entregables</h3>
        {deliverables.loading ? <LoadingSpinner /> : !deliverables.data?.length ? (
          <p style={emptyText}>No hay entregables.</p>
        ) : (
          <table style={tableStyle}>
            <thead>
              <tr style={{ borderBottom: '2px solid #e5e7eb' }}>
                <th style={th}>Título</th>
                <th style={th}>Compromiso</th>
                <th style={th}>QA</th>
                <th style={th}>PRE</th>
                <th style={th}>PROT</th>
                <th style={th}>Estado</th>
              </tr>
            </thead>
            <tbody>
              {deliverables.data.map((d) => (
                <tr key={d.id} style={{ borderBottom: '1px solid #f3f4f6' }}>
                  <td style={td}>{d.title}</td>
                  <td style={td}>{d.commitmentTitle ?? '—'}</td>
                  <td style={td}>{formatDate(d.qaDate)}</td>
                  <td style={td}>{formatDate(d.preDate)}</td>
                  <td style={td}>{formatDate(d.protDate)}</td>
                  <td style={td}><StatusBadge status={d.status} /></td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </Card>

      {/* Environment Deliveries */}
      {envDeliveries.data && envDeliveries.data.length > 0 && (
        <Card>
          <h3 style={sectionTitle}>🚀 Entregas por ambiente</h3>
          <table style={tableStyle}>
            <thead>
              <tr style={{ borderBottom: '2px solid #e5e7eb' }}>
                <th style={th}>Compromiso</th>
                <th style={th}>Ambiente</th>
                <th style={th}>Fecha planeada</th>
                <th style={th}>Fecha real</th>
                <th style={th}>Estado</th>
              </tr>
            </thead>
            <tbody>
              {envDeliveries.data.map((ed) => (
                <tr key={ed.id} style={{ borderBottom: '1px solid #f3f4f6' }}>
                  <td style={td}>{ed.commitmentTitle ?? ed.deliverableTitle ?? '—'}</td>
                  <td style={td}>
                    <span style={{ padding: '2px 8px', borderRadius: 4, fontSize: 11, fontWeight: 700, backgroundColor: envColor(ed.environment), color: '#fff' }}>
                      {ed.environment}
                    </span>
                  </td>
                  <td style={td}>{formatDate(ed.plannedDate)}</td>
                  <td style={td}>{formatDate(ed.actualDate)}</td>
                  <td style={td}><StatusBadge status={ed.status} /></td>
                </tr>
              ))}
            </tbody>
          </table>
        </Card>
      )}

      {/* Alerts */}
      <div style={{ marginTop: 20 }}>
        <AlertsPanel alerts={projectAlerts.data ?? null} loading={projectAlerts.loading} />
      </div>
      </>}
    </div>
  );
}

function envColor(env: string): string {
  switch (env) {
    case 'QA': return '#7c3aed';
    case 'PRE': return '#0891b2';
    case 'PROT': return '#16a34a';
    case 'PROD': return '#dc2626';
    default: return '#6b7280';
  }
}

const sectionTitle: React.CSSProperties = { fontSize: 16, fontWeight: 600, marginBottom: 14, color: '#111827' };
const emptyText: React.CSSProperties = { color: '#9ca3af', fontSize: 14 };
const tableStyle: React.CSSProperties = { width: '100%', borderCollapse: 'collapse', fontSize: 13 };
const th: React.CSSProperties = { textAlign: 'left', padding: '8px 10px', color: '#6b7280', fontWeight: 600 };
const td: React.CSSProperties = { padding: '8px 10px' };
