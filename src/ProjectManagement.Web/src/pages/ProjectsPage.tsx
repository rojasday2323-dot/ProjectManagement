import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useApi } from '../hooks/useApi';
import { projectService } from '../services/projectService';
import { StatusBadge } from '../components/ui/StatusBadge';
import { LoadingSpinner } from '../components/ui/LoadingSpinner';
import { ErrorAlert } from '../components/ui/ErrorAlert';
import { formatDate } from '../utils/format';
import './ProjectsPage.css';

const projectStatuses = ['Planning', 'Active', 'OnHold', 'Completed', 'Cancelled'];

export function ProjectsPage() {
  const { data: projects, loading, error, refetch } = useApi(() => projectService.getAll());
  const [nameFilter, setNameFilter] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [ownerFilter, setOwnerFilter] = useState('');

  const owners = useMemo(
    () => [...new Set((projects ?? []).flatMap((project) => project.productOwnerNames ?? []))]
      .sort((a, b) => a.localeCompare(b, 'es')),
    [projects],
  );

  const filteredProjects = (projects ?? []).filter((project) => {
    const matchesName = project.name.toLocaleLowerCase('es').includes(nameFilter.trim().toLocaleLowerCase('es'));
    const matchesStatus = !statusFilter || project.status === statusFilter;
    const matchesOwner = !ownerFilter || project.productOwnerNames?.includes(ownerFilter);
    return matchesName && matchesStatus && matchesOwner;
  });

  if (loading) return <LoadingSpinner message="Cargando proyectos..." />;
  if (error) return <ErrorAlert message={error} onRetry={refetch} />;

  return (
    <div className="projects-page">
      <header className="projects-heading">
        <div>
          <p className="projects-eyebrow">Portafolio</p>
          <h1>Proyectos</h1>
          <p className="projects-subtitle">Consulta responsables, fechas y avance de compromisos por proyecto.</p>
        </div>
        <div className="projects-count">
          <strong>{filteredProjects.length}</strong>
          <span>de {projects?.length ?? 0} proyectos</span>
        </div>
      </header>

      <section className="projects-filters" aria-label="Filtros de proyectos">
        <label className="projects-filter projects-name-filter">
          <span>Buscar por nombre</span>
          <input
            type="search"
            value={nameFilter}
            onChange={(event) => setNameFilter(event.target.value)}
            placeholder="Ej. Portal Banca Digital"
          />
        </label>
        <label className="projects-filter">
          <span>Estado</span>
          <select value={statusFilter} onChange={(event) => setStatusFilter(event.target.value)}>
            <option value="">Todos los estados</option>
            {projectStatuses.map((status) => <option value={status} key={status}>{status}</option>)}
          </select>
        </label>
        <label className="projects-filter">
          <span>Product Owner</span>
          <select value={ownerFilter} onChange={(event) => setOwnerFilter(event.target.value)}>
            <option value="">Todos los responsables</option>
            {owners.map((owner) => <option value={owner} key={owner}>{owner}</option>)}
          </select>
        </label>
        {(nameFilter || statusFilter || ownerFilter) && (
          <button
            className="projects-clear-filters"
            type="button"
            onClick={() => {
              setNameFilter('');
              setStatusFilter('');
              setOwnerFilter('');
            }}
          >
            Limpiar filtros
          </button>
        )}
      </section>

      {filteredProjects.length === 0 ? (
        <div className="projects-empty">
          <span aria-hidden="true">⌕</span>
          <h2>{projects?.length ? 'No hay proyectos que coincidan' : 'No hay proyectos registrados'}</h2>
          <p>{projects?.length ? 'Prueba con otros filtros.' : 'Cuando se registren proyectos, aparecerán aquí.'}</p>
        </div>
      ) : (
        <section className="project-card-grid" aria-label="Lista de proyectos">
          {filteredProjects.map((project) => (
            <Link className="project-card-link" key={project.id} to={`/projects/${project.id}`}>
              <article className="project-card">
                <div className="project-card-header">
                  <div className="project-card-title">
                    <span className="project-card-icon" aria-hidden="true">▦</span>
                    <h2>{project.name}</h2>
                  </div>
                  <StatusBadge status={project.status} />
                </div>

                <p className="project-card-description">
                  {project.description.length > 150
                    ? `${project.description.substring(0, 150)}…`
                    : project.description || 'Sin descripción'}
                </p>

                <div className="project-card-product">
                  <span>Producto</span>
                  <strong>{project.product || '—'}</strong>
                </div>

                <div className="project-card-dates">
                  <span>Inicio <strong>{formatDate(project.startDate)}</strong></span>
                  <span>Fin <strong>{formatDate(project.endDate)}</strong></span>
                </div>

                <div className="project-card-owners">
                  <span>Product Owner{project.productOwnerNames?.length === 1 ? '' : 's'}</span>
                  <strong>{project.productOwnerNames?.length ? project.productOwnerNames.join(', ') : 'Sin asignar'}</strong>
                </div>

                <div className="project-card-metrics">
                  <div>
                    <span>Compromisos</span>
                    <strong>{project.commitmentCount}</strong>
                  </div>
                  <div className="project-metric-pending">
                    <span>Pendientes</span>
                    <strong>{project.pendingCommitmentCount}</strong>
                  </div>
                  <div className="project-metric-progress">
                    <span>En progreso</span>
                    <strong>{project.inProgressCommitmentCount}</strong>
                  </div>
                  <div className="project-metric-completed">
                    <span>Completados</span>
                    <strong>{project.completedCommitmentCount}</strong>
                  </div>
                </div>

                <div className="project-card-footer">
                  <span>{project.memberCount} miembros</span>
                  <span>Ver detalle <span aria-hidden="true">→</span></span>
                </div>
              </article>
            </Link>
          ))}
        </section>
      )}
    </div>
  );
}
