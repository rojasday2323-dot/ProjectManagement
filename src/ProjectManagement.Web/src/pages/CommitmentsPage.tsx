import { useState } from 'react';
import { useApi } from '../hooks/useApi';
import { commitmentService, type CommitmentFilters } from '../services/commitmentService';
import { projectService } from '../services/projectService';
import { developerService, productOwnerService } from '../services/userService';
import { Card } from '../components/ui/Card';
import { StatusBadge } from '../components/ui/StatusBadge';
import { PriorityBadge } from '../components/ui/PriorityBadge';
import { LoadingSpinner } from '../components/ui/LoadingSpinner';
import { ErrorAlert } from '../components/ui/ErrorAlert';
import { EmptyState } from '../components/ui/EmptyState';
import { formatDate } from '../utils/format';

const STATUSES = ['', 'Pending', 'InProgress', 'Completed', 'Delayed', 'Cancelled'];

export function CommitmentsPage() {
  const [filters, setFilters] = useState<CommitmentFilters>({});

  const projects = useApi(() => projectService.getAll());
  const developers = useApi(() => developerService.getAll());
  const productOwners = useApi(() => productOwnerService.getAll());
  const commitments = useApi(
    () => commitmentService.getAll(filters),
    [JSON.stringify(filters)]
  );

  return (
    <div>
      <h1 style={{ fontSize: 22, fontWeight: 700, marginBottom: 20, color: '#111827' }}>Compromisos</h1>

      {/* Filters */}
      <Card style={{ marginBottom: 20 }}>
        <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', alignItems: 'flex-end' }}>
          <FilterSelect
            label="Proyecto"
            value={filters.projectId?.toString() ?? ''}
            options={projects.data?.map((p) => ({ value: p.id.toString(), label: p.name })) ?? []}
            onChange={(v) => setFilters((f) => ({ ...f, projectId: v ? Number(v) : undefined }))}
          />
          <FilterSelect
            label="Desarrollador"
            value={filters.developerId?.toString() ?? ''}
            options={developers.data?.map((d) => ({ value: d.id.toString(), label: d.displayName })) ?? []}
            onChange={(v) => setFilters((f) => ({ ...f, developerId: v ? Number(v) : undefined }))}
          />
          <FilterSelect
            label="Product Owner"
            value={filters.productOwnerId?.toString() ?? ''}
            options={productOwners.data?.map((po) => ({ value: po.id.toString(), label: po.displayName })) ?? []}
            onChange={(v) => setFilters((f) => ({ ...f, productOwnerId: v ? Number(v) : undefined }))}
          />
          <FilterSelect
            label="Estado"
            value={filters.status ?? ''}
            options={STATUSES.filter(Boolean).map((s) => ({ value: s, label: s }))}
            onChange={(v) => setFilters((f) => ({ ...f, status: v || undefined }))}
          />
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            <label style={labelStyle}>Desde</label>
            <input
              type="date"
              value={filters.startDate ?? ''}
              onChange={(e) => setFilters((f) => ({ ...f, startDate: e.target.value || undefined }))}
              style={inputStyle}
            />
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            <label style={labelStyle}>Hasta</label>
            <input
              type="date"
              value={filters.endDate ?? ''}
              onChange={(e) => setFilters((f) => ({ ...f, endDate: e.target.value || undefined }))}
              style={inputStyle}
            />
          </div>
          <button
            onClick={() => setFilters({})}
            style={{
              padding: '8px 16px',
              backgroundColor: '#6b7280',
              color: '#fff',
              border: 'none',
              borderRadius: 6,
              cursor: 'pointer',
              fontSize: 13,
              height: 36,
            }}
          >
            Limpiar
          </button>
        </div>
      </Card>

      {/* Results */}
      {commitments.loading ? (
        <LoadingSpinner />
      ) : commitments.error ? (
        <ErrorAlert message={commitments.error} onRetry={commitments.refetch} />
      ) : !commitments.data?.length ? (
        <EmptyState message="No se encontraron compromisos con los filtros seleccionados." />
      ) : (
        <Card>
          <div style={{ fontSize: 13, color: '#6b7280', marginBottom: 12 }}>
            {commitments.data.length} compromiso(s) encontrado(s)
          </div>
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
            <thead>
              <tr style={{ borderBottom: '2px solid #e5e7eb' }}>
                <th style={th}>Título</th>
                <th style={th}>Proyecto</th>
                <th style={th}>Desarrollador</th>
                <th style={th}>Product Owner</th>
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
                  <td style={{ ...td, fontWeight: 500, maxWidth: 200, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                    {c.title}
                  </td>
                  <td style={td}>{c.projectName}</td>
                  <td style={td}>{c.developerName}</td>
                  <td style={td}>{c.productOwnerName}</td>
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
        </Card>
      )}
    </div>
  );
}

function FilterSelect({
  label,
  value,
  options,
  onChange,
}: {
  label: string;
  value: string;
  options: { value: string; label: string }[];
  onChange: (value: string) => void;
}) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
      <label style={labelStyle}>{label}</label>
      <select value={value} onChange={(e) => onChange(e.target.value)} style={inputStyle}>
        <option value="">Todos</option>
        {options.map((o) => (
          <option key={o.value} value={o.value}>{o.label}</option>
        ))}
      </select>
    </div>
  );
}

const labelStyle: React.CSSProperties = { fontSize: 12, fontWeight: 600, color: '#374151' };
const inputStyle: React.CSSProperties = {
  padding: '6px 10px',
  border: '1px solid #d1d5db',
  borderRadius: 6,
  fontSize: 13,
  height: 36,
  minWidth: 140,
};
const th: React.CSSProperties = { textAlign: 'left', padding: '8px 10px', color: '#6b7280', fontWeight: 600, whiteSpace: 'nowrap' };
const td: React.CSSProperties = { padding: '8px 10px', whiteSpace: 'nowrap' };
