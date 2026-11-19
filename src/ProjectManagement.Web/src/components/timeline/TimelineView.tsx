import { useState, useMemo, useEffect } from 'react';
import { useApi } from '../../hooks/useApi';
import { timelineService } from '../../services/timelineService';
import { LoadingSpinner } from '../ui/LoadingSpinner';
import { ErrorAlert } from '../ui/ErrorAlert';
import { EmptyState } from '../ui/EmptyState';
import { TimelineFilters, type TimelineFilterState } from './TimelineFilters';
import { TimelineChart } from './TimelineChart';
import { TimelineDetails, type DetailItem } from './TimelineDetails';

interface Props {
  projectId: number;
  initialCommitmentId?: number;
}

export function TimelineView({ projectId, initialCommitmentId }: Props) {
  const { data, loading, error, refetch } = useApi(
    () => timelineService.getProjectTimeline(projectId),
    [projectId]
  );

  const [filters, setFilters] = useState<TimelineFilterState>({
    scale: 'weeks',
    showCommitments: true,
    showMilestones: true,
    showDeliverables: true,
    showEnvDeliveries: true,
    developerFilter: '',
    statusFilter: '',
    priorityFilter: '',
  });

  const [selectedItem, setSelectedItem] = useState<DetailItem | null>(null);
  const [dateOffset, setDateOffset] = useState(0);

  useEffect(() => {
    if (!data || !initialCommitmentId) return;
    const commitment = data.commitments.find((item) => item.id === initialCommitmentId);
    if (!commitment) return;
    setSelectedItem({
      type: 'commitment',
      data: commitment,
      relatedAlerts: data.alerts.filter(
        (alert) => alert.commitmentId === commitment.id || alert.relatedCommitmentId === commitment.id,
      ),
    });
  }, [data, initialCommitmentId]);

  const developers = useMemo(() => {
    if (!data) return [];
    return [...new Set(data.commitments.map((c) => c.developerName))].sort();
  }, [data]);

  const filteredData = useMemo(() => {
    if (!data) return null;

    let commitments = data.commitments;
    if (filters.developerFilter)
      commitments = commitments.filter((c) => c.developerName === filters.developerFilter);
    if (filters.statusFilter)
      commitments = commitments.filter((c) => c.status === filters.statusFilter);
    if (filters.priorityFilter)
      commitments = commitments.filter((c) => c.priority === filters.priorityFilter);

    return { ...data, commitments };
  }, [data, filters]);

  const { projectStart, projectEnd } = useMemo(() => {
    if (!data) return { projectStart: new Date(), projectEnd: new Date() };

    const allDates: Date[] = [];
    allDates.push(new Date(data.project.startDate));
    if (data.project.endDate) allDates.push(new Date(data.project.endDate));
    data.commitments.forEach((c) => {
      allDates.push(new Date(c.startDate));
      allDates.push(new Date(c.commitmentDate));
    });
    data.milestones.forEach((m) => allDates.push(new Date(m.dueDate)));
    data.deliverables.forEach((d) => {
      [d.qaDate, d.preDate, d.protDate].forEach((date) => {
        if (date) allDates.push(new Date(date));
      });
    });
    data.environmentDeliveries.forEach((delivery) => {
      [delivery.plannedDate, delivery.actualDate].forEach((date) => {
        if (date) allDates.push(new Date(date));
      });
    });
    data.alerts.forEach((alert) => {
      [alert.conflictStartDate, alert.conflictEndDate].forEach((date) => {
        if (date) allDates.push(new Date(date));
      });
    });

    const min = new Date(Math.min(...allDates.map((d) => d.getTime())));
    const max = new Date(Math.max(...allDates.map((d) => d.getTime())));

    // Add padding
    min.setDate(min.getDate() - 14);
    max.setDate(max.getDate() + 14);

    // Apply navigation offset
    const offsetDays = dateOffset * (filters.scale === 'days' ? 30 : filters.scale === 'weeks' ? 60 : 90);
    min.setDate(min.getDate() + offsetDays);
    max.setDate(max.getDate() + offsetDays);

    return { projectStart: min, projectEnd: max };
  }, [data, dateOffset, filters.scale]);

  const handleNavigate = (dir: 'prev' | 'today' | 'next') => {
    if (dir === 'today') setDateOffset(0);
    else if (dir === 'prev') setDateOffset((o) => o - 1);
    else setDateOffset((o) => o + 1);
  };

  if (loading) return <LoadingSpinner message="Cargando timeline..." />;
  if (error) return <ErrorAlert message={error} onRetry={refetch} />;
  if (
    !filteredData ||
    (filteredData.commitments.length === 0 &&
      filteredData.milestones.length === 0 &&
      filteredData.deliverables.length === 0 &&
      filteredData.environmentDeliveries.length === 0)
  )
    return <EmptyState message="No hay datos suficientes para mostrar el timeline." />;

  return (
    <div>
      <TimelineFilters
        filters={filters}
        onChange={setFilters}
        developers={developers}
        onNavigate={handleNavigate}
      />

      {/* Legend */}
      <div style={{ display: 'flex', gap: 16, marginBottom: 12, fontSize: 11, color: '#6b7280', flexWrap: 'wrap' }}>
        <span>🎯 Compromiso</span>
        <span>🏁 Milestone</span>
        <span>📦 Entregable</span>
        <span>🚀 Ambiente</span>
        <span style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
          <span style={{ width: 12, height: 12, borderRadius: 3, backgroundColor: '#dc2626', display: 'inline-block', border: '1px solid #991b1b' }} />
          Con conflicto
        </span>
        <span style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
          <span style={{ width: 14, height: 2, backgroundColor: '#dc2626', display: 'inline-block' }} />
          Hoy
        </span>
        <span style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
          <span style={{ width: 10, height: 10, borderRadius: '50%', display: 'inline-block', border: '2px solid #fff', boxShadow: '0 0 0 1px #7c3aed', backgroundColor: '#7c3aed' }} /> QA
          <span style={{ width: 10, height: 10, borderRadius: '50%', display: 'inline-block', border: '2px solid #fff', boxShadow: '0 0 0 1px #0891b2', backgroundColor: '#0891b2' }} /> PRE
          <span style={{ width: 10, height: 10, borderRadius: '50%', display: 'inline-block', border: '2px solid #fff', boxShadow: '0 0 0 1px #16a34a', backgroundColor: '#16a34a' }} /> PROT
        </span>
      </div>

      <TimelineChart
        commitments={filteredData.commitments}
        milestones={filteredData.milestones}
        deliverables={filteredData.deliverables}
        envDeliveries={filteredData.environmentDeliveries}
        alerts={filteredData.alerts}
        scale={filters.scale}
        showCommitments={filters.showCommitments}
        showMilestones={filters.showMilestones}
        showDeliverables={filters.showDeliverables}
        showEnvDeliveries={filters.showEnvDeliveries}
        projectStart={projectStart}
        projectEnd={projectEnd}
        scrollOffset={0}
        onSelect={setSelectedItem}
      />

      <TimelineDetails item={selectedItem} onClose={() => setSelectedItem(null)} />
    </div>
  );
}
