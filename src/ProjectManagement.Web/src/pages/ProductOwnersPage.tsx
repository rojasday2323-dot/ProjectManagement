import { useState } from 'react';
import { useApi } from '../hooks/useApi';
import { productOwnerService } from '../services/userService';
import { commitmentService } from '../services/commitmentService';
import { Card } from '../components/ui/Card';
import { StatusBadge } from '../components/ui/StatusBadge';
import { PriorityBadge } from '../components/ui/PriorityBadge';
import { LoadingSpinner } from '../components/ui/LoadingSpinner';
import { ErrorAlert } from '../components/ui/ErrorAlert';
import { EmptyState } from '../components/ui/EmptyState';
import { formatDate } from '../utils/format';
import type { ProductOwnerResponse } from '../types/api';

export function ProductOwnersPage() {
  const [selected, setSelected] = useState<ProductOwnerResponse | null>(null);
  const { data: owners, loading, error, refetch } = useApi(() => productOwnerService.getAll());

  const poCommitments = useApi(
    () => selected ? commitmentService.getAll({ productOwnerId: selected.id }) : Promise.resolve([]),
    [selected?.id]
  );

  if (loading) return <LoadingSpinner />;
  if (error) return <ErrorAlert message={error} onRetry={refetch} />;
  if (!owners?.length) return <EmptyState message="No hay Product Owners registrados." />;

  const participatingProjects = poCommitments.data
    ? [...new Map(poCommitments.data.map((c) => [c.projectId, { id: c.projectId, name: c.projectName }])).values()]
    : [];

  return (
    <div>
      <h1 style={{ fontSize: 22, fontWeight: 700, marginBottom: 20, color: '#111827' }}>Product Owners</h1>

      <div style={{ display: 'grid', gridTemplateColumns: '300px 1fr', gap: 20 }}>
        {/* List */}
        <div>
          {owners.map((po) => (
            <div
              key={po.id}
              onClick={() => setSelected(po)}
              style={{
                padding: '14px 16px',
                backgroundColor: selected?.id === po.id ? '#f0fdfa' : '#fff',
                border: `1px solid ${selected?.id === po.id ? '#0891b2' : '#e5e7eb'}`,
                borderRadius: 8,
                marginBottom: 8,
                cursor: 'pointer',
                transition: 'all 0.15s',
              }}
            >
              <div style={{ fontWeight: 600, fontSize: 14 }}>{po.displayName}</div>
              <div style={{ fontSize: 12, color: '#6b7280', marginTop: 2 }}>{po.email}</div>
              <div style={{ marginTop: 6, fontSize: 11 }}>
                <span style={{ padding: '1px 8px', borderRadius: 10, backgroundColor: '#f3f4f6', color: '#374151' }}>
                  {po.department}
                </span>
              </div>
            </div>
          ))}
        </div>

        {/* Detail */}
        <div>
          {!selected ? (
            <Card>
              <p style={{ color: '#9ca3af', textAlign: 'center', padding: 40 }}>
                Selecciona un Product Owner para ver sus detalles.
              </p>
            </Card>
          ) : (
            <>
              <Card style={{ marginBottom: 16 }}>
                <h2 style={{ fontSize: 18, fontWeight: 700, marginBottom: 8 }}>{selected.displayName}</h2>
                <div style={{ fontSize: 13, color: '#6b7280' }}>
                  <p>📧 {selected.email}</p>
                  <p>🏢 {selected.department}</p>
                </div>
              </Card>

              {/* Projects */}
              <Card style={{ marginBottom: 16 }}>
                <h3 style={{ fontSize: 15, fontWeight: 600, marginBottom: 10 }}>📁 Proyectos</h3>
                {participatingProjects.length === 0 ? (
                  <p style={{ color: '#9ca3af', fontSize: 13 }}>No participa en proyectos.</p>
                ) : (
                  <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                    {participatingProjects.map((p) => (
                      <span key={p.id} style={{ padding: '4px 12px', borderRadius: 6, backgroundColor: '#f0fdfa', color: '#0891b2', fontSize: 13, fontWeight: 500 }}>
                        {p.name}
                      </span>
                    ))}
                  </div>
                )}
              </Card>

              {/* Commitments */}
              <Card>
                <h3 style={{ fontSize: 15, fontWeight: 600, marginBottom: 10 }}>🎯 Compromisos</h3>
                {poCommitments.loading ? <LoadingSpinner /> : !poCommitments.data?.length ? (
                  <p style={{ color: '#9ca3af', fontSize: 13 }}>No tiene compromisos asignados.</p>
                ) : (
                  <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
                    <thead>
                      <tr style={{ borderBottom: '2px solid #e5e7eb' }}>
                        <th style={th}>Título</th>
                        <th style={th}>Proyecto</th>
                        <th style={th}>Desarrollador</th>
                        <th style={th}>Compromiso</th>
                        <th style={th}>Estado</th>
                        <th style={th}>Prioridad</th>
                      </tr>
                    </thead>
                    <tbody>
                      {poCommitments.data.map((c) => (
                        <tr key={c.id} style={{ borderBottom: '1px solid #f3f4f6' }}>
                          <td style={td}>{c.title}</td>
                          <td style={td}>{c.projectName}</td>
                          <td style={td}>{c.developerName}</td>
                          <td style={td}>{formatDate(c.commitmentDate)}</td>
                          <td style={td}><StatusBadge status={c.status} /></td>
                          <td style={td}><PriorityBadge priority={c.priority} /></td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}
              </Card>
            </>
          )}
        </div>
      </div>
    </div>
  );
}

const th: React.CSSProperties = { textAlign: 'left', padding: '8px 10px', color: '#6b7280', fontWeight: 600 };
const td: React.CSSProperties = { padding: '8px 10px' };
