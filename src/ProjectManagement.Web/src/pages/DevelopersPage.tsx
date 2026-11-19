import { useState } from 'react';
import type { FormEvent } from 'react';
import { useApi } from '../hooks/useApi';
import { developerService } from '../services/userService';
import { commitmentService } from '../services/commitmentService';
import { alertService } from '../services/alertService';
import { Card } from '../components/ui/Card';
import { StatusBadge } from '../components/ui/StatusBadge';
import { PriorityBadge } from '../components/ui/PriorityBadge';
import { LoadingSpinner } from '../components/ui/LoadingSpinner';
import { ErrorAlert } from '../components/ui/ErrorAlert';
import { EmptyState } from '../components/ui/EmptyState';
import { AlertsPanel } from '../components/shared/AlertsPanel';
import { formatDate } from '../utils/format';
import type { DeveloperResponse } from '../types/api';

export function DevelopersPage() {
  const [selected, setSelected] = useState<DeveloperResponse | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [displayName, setDisplayName] = useState('');
  const [specialty, setSpecialty] = useState('');
  const [seniority, setSeniority] = useState('');
  const [saving, setSaving] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const { data: developers, loading, error, refetch } = useApi(() => developerService.getAll(true));

  const devCommitments = useApi(
    () => selected ? commitmentService.getAll({ developerId: selected.id }) : Promise.resolve([]),
    [selected?.id]
  );
  const devAlerts = useApi(
    () => selected ? alertService.getByDeveloper(selected.id) : Promise.resolve([]),
    [selected?.id]
  );

  const resetForm = () => {
    setFormOpen(false);
    setEditingId(null);
    setDisplayName('');
    setSpecialty('');
    setSeniority('');
  };

  const startCreate = () => {
    resetForm();
    setErrorMessage(null);
    setMessage(null);
    setFormOpen(true);
  };

  const startEdit = (developer: DeveloperResponse) => {
    setEditingId(developer.id);
    setDisplayName(developer.displayName);
    setSpecialty(developer.specialty);
    setSeniority(developer.seniority);
    setErrorMessage(null);
    setMessage(null);
    setFormOpen(true);
  };

  const saveDeveloper = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setSaving(true);
    setErrorMessage(null);
    setMessage(null);
    try {
      const request = { displayName: displayName.trim(), specialty: specialty.trim(), seniority: seniority.trim() };
      const saved = editingId === null
        ? await developerService.create(request)
        : await developerService.update(editingId, request);
      setSelected(saved);
      setMessage(editingId === null ? 'Desarrollador creado.' : 'Cambios guardados.');
      resetForm();
      await refetch();
    } catch (saveError) {
      setErrorMessage(saveError instanceof Error ? saveError.message : 'No se pudo guardar el desarrollador.');
    } finally {
      setSaving(false);
    }
  };

  const toggleActive = async (developer: DeveloperResponse) => {
    const nextIsActive = !developer.isActive;
    if (!nextIsActive && !window.confirm(`¿Desactivar a ${developer.displayName}? Se conservarán sus compromisos e historial.`))
      return;

    setErrorMessage(null);
    setMessage(null);
    try {
      const updated = await developerService.setActive(developer.id, nextIsActive);
      setSelected((current) => current?.id === updated.id ? updated : current);
      setMessage(nextIsActive ? 'Desarrollador reactivado.' : 'Desarrollador desactivado; su historial se conserva.');
      await refetch();
    } catch (statusError) {
      setErrorMessage(statusError instanceof Error ? statusError.message : 'No se pudo cambiar el estado.');
    }
  };

  if (loading) return <LoadingSpinner />;
  if (error) return <ErrorAlert message={error} onRetry={refetch} />;

  const participatingProjects = devCommitments.data
    ? [...new Map(devCommitments.data.map((c) => [c.projectId, { id: c.projectId, name: c.projectName }])).values()]
    : [];
  const inputStyle: React.CSSProperties = {
    width: '100%',
    padding: '9px 11px',
    border: '1px solid #d1d5db',
    borderRadius: 6,
    fontSize: 14,
    boxSizing: 'border-box',
    marginTop: 5,
  };
  const buttonStyle: React.CSSProperties = {
    padding: '8px 12px',
    border: '1px solid #d1d5db',
    borderRadius: 6,
    background: '#fff',
    color: '#1f2937',
    cursor: 'pointer',
    fontWeight: 600,
  };

  return (
    <div>
      <header style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 12, marginBottom: 20 }}>
        <div>
          <h1 style={{ fontSize: 22, fontWeight: 700, margin: '0 0 5px', color: '#111827' }}>Desarrolladores</h1>
          <p style={{ color: '#6b7280', margin: 0, fontSize: 13 }}>Administra el catálogo sin eliminar compromisos ni historial.</p>
        </div>
        <button type="button" onClick={startCreate} style={{ ...buttonStyle, background: '#2563eb', color: '#fff', borderColor: '#2563eb' }}>
          Agregar desarrollador
        </button>
      </header>

      {errorMessage && <ErrorAlert message={errorMessage} />}
      {message && <p role="status" style={{ color: '#166534', background: '#f0fdf4', padding: 10, borderRadius: 6 }}>{message}</p>}

      {formOpen && (
        <Card style={{ marginBottom: 16 }}>
          <h2 style={{ fontSize: 17, margin: '0 0 14px' }}>{editingId === null ? 'Nuevo desarrollador' : 'Editar desarrollador'}</h2>
          <form onSubmit={saveDeveloper}>
            <label style={{ display: 'block', marginBottom: 12, fontSize: 13, fontWeight: 600 }}>
              Nombre completo
              <input required maxLength={200} value={displayName} onChange={(event) => setDisplayName(event.target.value)} style={inputStyle} />
            </label>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: 12 }}>
              <label style={{ display: 'block', fontSize: 13, fontWeight: 600 }}>
                Especialidad <span style={{ fontWeight: 400, color: '#6b7280' }}>(opcional)</span>
                <input maxLength={50} value={specialty} onChange={(event) => setSpecialty(event.target.value)} style={inputStyle} />
              </label>
              <label style={{ display: 'block', fontSize: 13, fontWeight: 600 }}>
                Seniority <span style={{ fontWeight: 400, color: '#6b7280' }}>(opcional)</span>
                <input maxLength={50} value={seniority} onChange={(event) => setSeniority(event.target.value)} style={inputStyle} />
              </label>
            </div>
            <p style={{ color: '#6b7280', fontSize: 12 }}>No se solicita ni se genera correo, teléfono o cuenta de acceso.</p>
            <div style={{ display: 'flex', gap: 8 }}>
              <button type="submit" disabled={saving} style={{ ...buttonStyle, background: '#2563eb', color: '#fff', borderColor: '#2563eb' }}>
                {saving ? 'Guardando…' : 'Guardar'}
              </button>
              <button type="button" onClick={resetForm} disabled={saving} style={buttonStyle}>Cancelar</button>
            </div>
          </form>
        </Card>
      )}

      <div style={{ display: 'grid', gridTemplateColumns: 'minmax(260px, 320px) minmax(0, 1fr)', gap: 20 }}>
        <section aria-label="Catálogo de desarrolladores">
          {!developers?.length ? (
            <EmptyState message="No hay desarrolladores registrados." />
          ) : developers.map((developer) => (
            <article
              key={developer.id}
              style={{
                padding: 14,
                background: selected?.id === developer.id ? '#eff6ff' : '#fff',
                border: `1px solid ${selected?.id === developer.id ? '#3b82f6' : '#e5e7eb'}`,
                borderRadius: 8,
                marginBottom: 8,
              }}
            >
              <button
                type="button"
                onClick={() => setSelected(developer)}
                style={{ width: '100%', textAlign: 'left', padding: 0, border: 0, background: 'none', cursor: 'pointer', color: '#111827' }}
              >
                <strong style={{ display: 'block', fontSize: 14 }}>{developer.displayName}</strong>
                <span style={{ display: 'block', color: '#6b7280', fontSize: 12, marginTop: 4 }}>
                  {developer.specialty || 'Especialidad sin especificar'} · {developer.seniority || 'Seniority sin especificar'}
                </span>
              </button>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: 10, gap: 6 }}>
                <span style={{ color: developer.isActive ? '#166534' : '#92400e', background: developer.isActive ? '#f0fdf4' : '#fffbeb', borderRadius: 999, padding: '3px 8px', fontSize: 11 }}>
                  {developer.isActive ? 'Activo' : 'Inactivo'}
                </span>
                <div style={{ display: 'flex', gap: 6 }}>
                  <button type="button" onClick={() => startEdit(developer)} style={{ ...buttonStyle, padding: '5px 8px', fontSize: 12 }}>Editar</button>
                  <button type="button" onClick={() => toggleActive(developer)} style={{ ...buttonStyle, padding: '5px 8px', fontSize: 12 }}>
                    {developer.isActive ? 'Desactivar' : 'Reactivar'}
                  </button>
                </div>
              </div>
            </article>
          ))}
        </section>

        <section>
          {!selected ? (
            <Card><p style={{ color: '#9ca3af', textAlign: 'center', padding: 40 }}>Selecciona un desarrollador para ver sus compromisos, proyectos y alertas.</p></Card>
          ) : (
            <>
              <Card style={{ marginBottom: 16 }}>
                <h2 style={{ fontSize: 18, fontWeight: 700, marginBottom: 8 }}>{selected.displayName}</h2>
                <p style={{ fontSize: 13, color: '#6b7280', margin: 0 }}>
                  {selected.specialty || 'Especialidad sin especificar'} · {selected.seniority || 'Seniority sin especificar'}
                  {!selected.email && ' · Sin correo registrado'}
                </p>
              </Card>
              <Card style={{ marginBottom: 16 }}>
                <h3 style={{ fontSize: 15, fontWeight: 600, marginBottom: 10 }}>Proyectos con compromisos</h3>
                {participatingProjects.length === 0 ? (
                  <p style={{ color: '#9ca3af', fontSize: 13 }}>No participa en proyectos con compromisos.</p>
                ) : (
                  <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                    {participatingProjects.map((project) => (
                      <span key={project.id} style={{ padding: '4px 12px', borderRadius: 6, background: '#eff6ff', color: '#2563eb', fontSize: 13, fontWeight: 500 }}>{project.name}</span>
                    ))}
                  </div>
                )}
              </Card>
              <Card>
                <h3 style={{ fontSize: 15, fontWeight: 600, marginBottom: 10 }}>Compromisos asociados</h3>
                {devCommitments.loading ? <LoadingSpinner /> : devCommitments.error ? <ErrorAlert message={devCommitments.error} /> : !devCommitments.data?.length ? (
                  <p style={{ color: '#9ca3af', fontSize: 13 }}>No tiene compromisos asignados.</p>
                ) : (
                  <div style={{ overflowX: 'auto' }}>
                    <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
                      <thead><tr style={{ borderBottom: '2px solid #e5e7eb' }}>
                        <th style={th}>Título</th><th style={th}>Proyecto</th><th style={th}>Compromiso</th><th style={th}>Estado</th><th style={th}>Prioridad</th>
                      </tr></thead>
                      <tbody>{devCommitments.data.map((commitment) => (
                        <tr key={commitment.id} style={{ borderBottom: '1px solid #f3f4f6' }}>
                          <td style={td}>{commitment.title}</td><td style={td}>{commitment.projectName}</td>
                          <td style={td}>{formatDate(commitment.commitmentDate)}</td>
                          <td style={td}><StatusBadge status={commitment.status} /></td>
                          <td style={td}><PriorityBadge priority={commitment.priority} /></td>
                        </tr>
                      ))}</tbody>
                    </table>
                  </div>
                )}
              </Card>
              <div style={{ marginTop: 16 }}>
                <AlertsPanel alerts={devAlerts.data ?? null} loading={devAlerts.loading} />
              </div>
            </>
          )}
        </section>
      </div>
    </div>
  );
}

const th: React.CSSProperties = { textAlign: 'left', padding: '8px 10px', color: '#6b7280', fontWeight: 600 };
const td: React.CSSProperties = { padding: '8px 10px' };
