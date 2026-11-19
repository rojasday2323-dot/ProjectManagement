import { useApi } from '../hooks/useApi';
import { alertService } from '../services/alertService';
import { commitmentService } from '../services/commitmentService';
import { dashboardService } from '../services/dashboardService';
import { productOwnerService } from '../services/userService';
import { SummaryCard } from '../components/ui/SummaryCard';
import { Card } from '../components/ui/Card';
import { StatusBadge } from '../components/ui/StatusBadge';
import { PriorityBadge } from '../components/ui/PriorityBadge';
import { LoadingSpinner } from '../components/ui/LoadingSpinner';
import { ErrorAlert } from '../components/ui/ErrorAlert';
import { AlertsPanel } from '../components/shared/AlertsPanel';
import { formatDate, getStatusColor } from '../utils/format';
import type { DashboardStatusCountResponse, DashboardWorkloadResponse } from '../types/api';
import './DashboardPage.css';

const openStatuses = new Set(['Pending', 'InProgress', 'Delayed']);
const statusLabels: Record<string, string> = {
  Pending: 'Pendiente',
  InProgress: 'En progreso',
  Completed: 'Completado',
  Delayed: 'Retrasado',
  Cancelled: 'Cancelado',
};

export function DashboardPage() {
  const summary = useApi(() => dashboardService.getSummary());
  const commitments = useApi(() => commitmentService.getAll());
  const productOwners = useApi(() => productOwnerService.getAll());
  const alertSummary = useApi(() => alertService.getSummary());

  const isLoading = summary.loading || commitments.loading || productOwners.loading || alertSummary.loading;
  const error = summary.error || commitments.error || productOwners.error || alertSummary.error;

  const refresh = () => Promise.all([
    summary.refetch(),
    commitments.refetch(),
    productOwners.refetch(),
    alertSummary.refetch(),
  ]);

  if (isLoading) return <LoadingSpinner message="Cargando indicadores del sistema..." />;
  if (error) return <ErrorAlert message={error} onRetry={() => void refresh()} />;
  if (!summary.data) return <ErrorAlert message="No se pudieron cargar los indicadores del dashboard." onRetry={() => void refresh()} />;

  const activeCommitments = (commitments.data ?? [])
    .filter((commitment) => openStatuses.has(commitment.status));

  const upcomingCommitments = activeCommitments
    .map((commitment) => ({ ...commitment, daysLeft: daysUntilDate(commitment.commitmentDate) }))
    .filter((commitment) => commitment.daysLeft !== null && commitment.daysLeft >= 0 && commitment.daysLeft <= 30)
    .sort((a, b) => (a.daysLeft ?? 0) - (b.daysLeft ?? 0));

  const overdueCommitments = activeCommitments
    .map((commitment) => ({ ...commitment, daysLeft: daysUntilDate(commitment.commitmentDate) }))
    .filter((commitment) => commitment.daysLeft !== null && commitment.daysLeft < 0)
    .sort((a, b) => (a.daysLeft ?? 0) - (b.daysLeft ?? 0));

  return (
    <div className="dashboard-page">
      <div className="dashboard-heading">
        <div>
          <p className="dashboard-eyebrow">Resumen ejecutivo</p>
          <h1>Dashboard</h1>
          <p className="dashboard-subtitle">Estado general de proyectos, compromisos y riesgos.</p>
        </div>
        <button className="dashboard-refresh" type="button" onClick={() => void refresh()}>
          ↻ Actualizar datos
        </button>
      </div>

      <section className="dashboard-metrics" aria-label="Indicadores principales">
        <SummaryCard title="Proyectos registrados" value={summary.data.totalProjects} icon="📁" color="#2563eb" />
        <SummaryCard title="Proyectos activos" value={summary.data.activeProjects} icon="🚀" color="#0891b2" />
        <SummaryCard title="Compromisos totales" value={summary.data.totalCommitments} icon="🎯" color="#7c3aed" />
        <SummaryCard title="Pendientes" value={summary.data.pendingCommitments} icon="◷" color="#d97706" />
        <SummaryCard title="En progreso" value={summary.data.inProgressCommitments} icon="⚙️" color="#2563eb" />
        <SummaryCard title="Completados" value={summary.data.completedCommitments} icon="✓" color="#16a34a" />
        <SummaryCard title="Alertas críticas" value={summary.data.criticalAlerts} icon="🔴" color="#dc2626" />
        <SummaryCard title="Advertencias" value={summary.data.warningAlerts} icon="⚠️" color="#ea580c" />
        <SummaryCard title="Desarrolladores registrados" value={summary.data.registeredDevelopers} icon="👨‍💻" color="#7c3aed" />
        <SummaryCard title="Product Owners" value={productOwners.data?.length ?? summary.data.registeredProductOwners} icon="👩‍💼" color="#0891b2" />
      </section>

      <section className="dashboard-chart-grid" aria-label="Análisis de compromisos pendientes">
        <Card>
          <div className="dashboard-card-heading">
            <div>
              <h2>Trabajo sin completar</h2>
              <p>Pendientes, en progreso y retrasados</p>
            </div>
            <span className="dashboard-total">{summary.data.openCommitments}</span>
          </div>
          <div className="dashboard-deadline-stats">
            <div className="deadline-stat deadline-stat-overdue">
              <span className="deadline-indicator" />
              <span>Vencidos</span>
              <strong>{summary.data.overdueCommitments}</strong>
            </div>
            <div className="deadline-stat deadline-stat-upcoming">
              <span className="deadline-indicator" />
              <span>Vencen en 30 días</span>
              <strong>{summary.data.upcomingCommitments}</strong>
            </div>
          </div>
          <div className="dashboard-workload-grid">
            <WorkloadChart title="Por proyecto" items={summary.data.pendingByProject} color="#2563eb" />
            <WorkloadChart title="Por desarrollador" items={summary.data.pendingByDeveloper} color="#7c3aed" />
          </div>
        </Card>

        <Card>
          <div className="dashboard-card-heading">
            <div>
              <h2>Estado de compromisos</h2>
              <p>Distribución de todos los registros</p>
            </div>
          </div>
          <StatusDistribution items={summary.data.statusDistribution} />
        </Card>
      </section>

      <section className="dashboard-table-grid">
        <Card>
          <div className="dashboard-card-heading">
            <div>
              <h2>Próximos compromisos</h2>
              <p>Fechas de entrega dentro de los siguientes 30 días</p>
            </div>
          </div>
          {upcomingCommitments.length === 0 ? (
            <p className="dashboard-empty">No hay compromisos próximos.</p>
          ) : (
            <div className="dashboard-table-scroll">
              <table className="dashboard-table">
                <thead>
                  <tr>
                    <th>Compromiso</th><th>Proyecto</th><th>Fecha</th><th>Días</th><th>Prioridad</th>
                  </tr>
                </thead>
                <tbody>
                  {upcomingCommitments.slice(0, 8).map((commitment) => (
                    <tr key={commitment.id}>
                      <td>{commitment.title}</td>
                      <td>{commitment.projectName}</td>
                      <td>{formatDate(commitment.commitmentDate)}</td>
                      <td className={(commitment.daysLeft ?? 0) <= 7 ? 'dashboard-days-urgent' : 'dashboard-days'}>
                        {commitment.daysLeft}d
                      </td>
                      <td><PriorityBadge priority={commitment.priority} /></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </Card>

        <Card>
          <div className="dashboard-card-heading">
            <div>
              <h2>Compromisos vencidos</h2>
              <p>Trabajo abierto con fecha de entrega superada</p>
            </div>
          </div>
          {overdueCommitments.length === 0 ? (
            <p className="dashboard-all-clear">No hay compromisos vencidos.</p>
          ) : (
            <div className="dashboard-table-scroll">
              <table className="dashboard-table">
                <thead>
                  <tr>
                    <th>Compromiso</th><th>Desarrollador</th><th>Fecha límite</th><th>Retraso</th><th>Estado</th>
                  </tr>
                </thead>
                <tbody>
                  {overdueCommitments.slice(0, 8).map((commitment) => (
                    <tr key={commitment.id}>
                      <td>{commitment.title}</td>
                      <td>{commitment.developerName}</td>
                      <td>{formatDate(commitment.commitmentDate)}</td>
                      <td className="dashboard-days-urgent">{Math.abs(commitment.daysLeft ?? 0)}d</td>
                      <td><StatusBadge status={commitment.status} /></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </Card>
      </section>

      <section className="dashboard-alerts">
        <AlertsPanel
          alerts={alertSummary.data?.recentAlerts ?? null}
          loading={alertSummary.loading}
          title="Alertas y conflictos recientes"
        />
      </section>
    </div>
  );
}

function WorkloadChart({ title, items, color }: {
  title: string;
  items: DashboardWorkloadResponse[];
  color: string;
}) {
  const rankedItems = items.slice(0, 8);
  const maxCount = rankedItems[0]?.count ?? 0;

  return (
    <div className="workload-chart">
      <h3>{title}</h3>
      {maxCount === 0 ? (
        <p className="dashboard-empty">Sin compromisos pendientes.</p>
      ) : (
        <div className="workload-rows">
          {rankedItems.map((item) => (
            <div className="workload-row" key={item.id}>
              <div className="workload-label" title={item.name}>{item.name}</div>
              <div className="workload-track">
                <span style={{ width: `${(item.count / maxCount) * 100}%`, backgroundColor: color }} />
              </div>
              <strong>{item.count}</strong>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

function StatusDistribution({ items }: { items: DashboardStatusCountResponse[] }) {
  const total = items.reduce((sum, item) => sum + item.count, 0);
  let accumulated = 0;
  const segments = items
    .filter((item) => item.count > 0)
    .map((item) => {
      const start = accumulated;
      accumulated += (item.count / total) * 100;
      return `${getStatusColor(item.status)} ${start}% ${accumulated}%`;
    });

  return (
    <div className="status-distribution">
      <div
        className="status-donut"
        role="img"
        aria-label={`Distribución de ${total} compromisos por estado`}
        style={{ background: segments.length ? `conic-gradient(${segments.join(', ')})` : '#e5e7eb' }}
      >
        <div><strong>{total}</strong><span>compromisos</span></div>
      </div>
      <div className="status-legend">
        {items.map((item) => (
          <div className="status-legend-row" key={item.status}>
            <span className="status-legend-color" style={{ backgroundColor: getStatusColor(item.status) }} />
            <span>{statusLabels[item.status] ?? item.status}</span>
            <strong>{item.count}</strong>
          </div>
        ))}
      </div>
    </div>
  );
}

function daysUntilDate(value: string): number {
  const [year, month, day] = value.slice(0, 10).split('-').map(Number);
  const target = Date.UTC(year, month - 1, day);
  const now = new Date();
  const today = Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate());
  return Math.round((target - today) / 86_400_000);
}
