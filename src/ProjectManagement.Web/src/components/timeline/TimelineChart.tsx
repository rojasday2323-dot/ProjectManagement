import { useMemo, useRef } from 'react';
import { getColumnWidth, generateDateColumns, getBarPosition, getMarkerPosition, formatColumnHeader, formatMonthGroup } from './timelineUtils';
import type { TimeScale } from './timelineUtils';
import type { TimelineCommitment, TimelineMilestone, TimelineDeliverable, TimelineEnvironmentDelivery, TimelineAlert } from '../../types/api';
import type { DetailItem } from './TimelineDetails';
import { getStatusColor } from '../../utils/format';

interface Props {
  commitments: TimelineCommitment[];
  milestones: TimelineMilestone[];
  deliverables: TimelineDeliverable[];
  envDeliveries: TimelineEnvironmentDelivery[];
  alerts: TimelineAlert[];
  scale: TimeScale;
  showCommitments: boolean;
  showMilestones: boolean;
  showDeliverables: boolean;
  showEnvDeliveries: boolean;
  projectStart: Date;
  projectEnd: Date;
  scrollOffset: number;
  onSelect: (item: DetailItem) => void;
}

const ROW_HEIGHT = 36;
const LABEL_WIDTH = 220;

export function TimelineChart({
  commitments, milestones, deliverables, envDeliveries, alerts, scale,
  showCommitments, showMilestones, showDeliverables, showEnvDeliveries,
  projectStart, projectEnd, scrollOffset, onSelect,
}: Props) {
  const containerRef = useRef<HTMLDivElement>(null);
  const colWidth = getColumnWidth(scale);
  const columns = useMemo(() => generateDateColumns(projectStart, projectEnd, scale), [projectStart, projectEnd, scale]);
  const totalWidth = columns.length * colWidth;
  const today = new Date();
  const todayPos = getMarkerPosition(today, projectStart, colWidth, scale);

  const conflictCommitmentIds = useMemo(() => {
    const ids = new Set<number>();
    alerts.forEach((a) => {
      if (a.commitmentId) ids.add(a.commitmentId);
      if (a.relatedCommitmentId) ids.add(a.relatedCommitmentId);
    });
    return ids;
  }, [alerts]);

  const alertsByCommitment = useMemo(() => {
    const map = new Map<number, TimelineAlert[]>();
    alerts.forEach((a) => {
      if (a.commitmentId) {
        const arr = map.get(a.commitmentId) || [];
        arr.push(a);
        map.set(a.commitmentId, arr);
      }
    });
    return map;
  }, [alerts]);

  // Build rows
  type RowData =
    | { kind: 'commitment'; data: TimelineCommitment }
    | { kind: 'milestone'; data: TimelineMilestone }
    | { kind: 'deliverable'; data: TimelineDeliverable }
    | { kind: 'env'; data: TimelineEnvironmentDelivery };

  const rows: RowData[] = [];
  if (showCommitments) commitments.forEach((c) => rows.push({ kind: 'commitment', data: c }));
  if (showMilestones) milestones.forEach((m) => rows.push({ kind: 'milestone', data: m }));
  if (showDeliverables) deliverables.forEach((d) => rows.push({ kind: 'deliverable', data: d }));
  if (showEnvDeliveries) envDeliveries.forEach((e) => rows.push({ kind: 'env', data: e }));

  const gridHeight = rows.length * ROW_HEIGHT;

  // Month groupings for header
  const monthGroups = useMemo(() => {
    const groups: { label: string; span: number }[] = [];
    let current = '';
    let count = 0;
    columns.forEach((d) => {
      const label = formatMonthGroup(d);
      if (label !== current) {
        if (current) groups.push({ label: current, span: count });
        current = label;
        count = 1;
      } else {
        count++;
      }
    });
    if (current) groups.push({ label: current, span: count });
    return groups;
  }, [columns]);

  return (
    <div style={{ border: '1px solid #e5e7eb', borderRadius: 8, backgroundColor: '#fff', overflow: 'hidden' }}>
      <div style={{ display: 'flex' }}>
        {/* Labels column */}
        <div style={{ width: LABEL_WIDTH, flexShrink: 0, borderRight: '2px solid #e5e7eb', zIndex: 10, backgroundColor: '#fff' }}>
          {/* Header spacer */}
          <div style={{ height: 52, borderBottom: '1px solid #e5e7eb', display: 'flex', alignItems: 'center', padding: '0 12px', fontSize: 12, fontWeight: 700, color: '#374151' }}>
            Elemento
          </div>
          {/* Row labels */}
          {rows.map((row, i) => (
            <div
              key={`label-${i}`}
              style={{
                height: ROW_HEIGHT,
                display: 'flex',
                alignItems: 'center',
                padding: '0 10px',
                borderBottom: '1px solid #f3f4f6',
                fontSize: 12,
                overflow: 'hidden',
                whiteSpace: 'nowrap',
                textOverflow: 'ellipsis',
                cursor: row.kind === 'commitment' || row.kind === 'milestone' ? 'pointer' : 'default',
                backgroundColor: row.kind === 'commitment' && conflictCommitmentIds.has(row.data.id) ? '#fef2f2' : 'transparent',
              }}
              title={getRowLabel(row)}
              onClick={() => handleRowClick(row)}
            >
              <span style={{ marginRight: 6 }}>{getRowIcon(row)}</span>
              <span style={{ overflow: 'hidden', textOverflow: 'ellipsis' }}>{getRowLabel(row)}</span>
            </div>
          ))}
        </div>

        {/* Scrollable chart area */}
        <div ref={containerRef} style={{ flex: 1, minWidth: 0, overflowX: 'auto' }}>
          <div style={{ width: totalWidth, minWidth: '100%', position: 'relative' }}>
            {/* Month header row */}
            <div style={{ display: 'flex', height: 22, borderBottom: '1px solid #e5e7eb' }}>
              {monthGroups.map((g, i) => (
                <div key={i} style={{ width: g.span * colWidth, textAlign: 'center', fontSize: 11, fontWeight: 700, color: '#374151', lineHeight: '22px', borderRight: '1px solid #e5e7eb', backgroundColor: '#f9fafb' }}>
                  {g.label}
                </div>
              ))}
            </div>

            {/* Day/week header row */}
            <div style={{ display: 'flex', height: 30, borderBottom: '2px solid #e5e7eb' }}>
              {columns.map((d, i) => {
                const isToday = d.toDateString() === today.toDateString();
                const isWeekend = scale === 'days' && (d.getDay() === 0 || d.getDay() === 6);
                return (
                  <div
                    key={i}
                    style={{
                      width: colWidth,
                      textAlign: 'center',
                      fontSize: 10,
                      lineHeight: '30px',
                      borderRight: '1px solid #f3f4f6',
                      color: isToday ? '#2563eb' : isWeekend ? '#d1d5db' : '#9ca3af',
                      fontWeight: isToday ? 700 : 400,
                      backgroundColor: isToday ? '#eff6ff' : isWeekend ? '#fafafa' : 'transparent',
                    }}
                  >
                    {formatColumnHeader(d, scale)}
                  </div>
                );
              })}
            </div>

            {/* Grid rows */}
            <div style={{ position: 'relative', height: gridHeight }}>
              {/* Grid lines */}
              {columns.map((d, i) => {
                const isWeekend = scale === 'days' && (d.getDay() === 0 || d.getDay() === 6);
                return (
                  <div
                    key={`grid-${i}`}
                    style={{
                      position: 'absolute',
                      left: i * colWidth,
                      top: 0,
                      width: colWidth,
                      height: gridHeight,
                      borderRight: '1px solid #f8f9fa',
                      backgroundColor: isWeekend ? '#fafafa' : 'transparent',
                    }}
                  />
                );
              })}

              {/* Today line */}
              {todayPos >= 0 && todayPos <= totalWidth && (
                <div
                  style={{
                    position: 'absolute',
                    left: todayPos + scrollOffset,
                    top: 0,
                    width: 2,
                    height: gridHeight,
                    backgroundColor: '#dc2626',
                    zIndex: 5,
                  }}
                >
                  <div style={{
                    position: 'absolute', top: -4, left: -12,
                    fontSize: 9, color: '#fff', backgroundColor: '#dc2626',
                    padding: '1px 4px', borderRadius: 3, fontWeight: 700,
                  }}>
                    HOY
                  </div>
                </div>
              )}

              {/* Row items */}
              {rows.map((row, i) => (
                <div key={`row-${i}`} style={{ position: 'absolute', top: i * ROW_HEIGHT, left: 0, width: totalWidth, height: ROW_HEIGHT, borderBottom: '1px solid #f3f4f6' }}>
                  {renderRowContent(row, projectStart, colWidth, scale, conflictCommitmentIds, () => handleRowClick(row))}
                </div>
              ))}
            </div>
          </div>
        </div>
      </div>
    </div>
  );

  function handleRowClick(row: RowData) {
    if (row.kind === 'commitment') {
      onSelect({ type: 'commitment', data: row.data, relatedAlerts: alertsByCommitment.get(row.data.id) || [] });
    } else if (row.kind === 'milestone') {
      onSelect({ type: 'milestone', data: row.data });
    }
  }
}

function renderRowContent(
  row: { kind: string; data: any },
  timelineStart: Date,
  colWidth: number,
  scale: TimeScale,
  conflictIds: Set<number>,
  onClick: () => void,
) {
  if (row.kind === 'commitment') {
    const c = row.data as TimelineCommitment;
    const start = new Date(c.startDate);
    const end = new Date(c.commitmentDate);
    const pos = getBarPosition(start, end, timelineStart, colWidth, scale);
    const hasConflict = conflictIds.has(c.id);
    const barColor = hasConflict ? '#dc2626' : getStatusColor(c.status);

    return (
      <div
        onClick={onClick}
        title={`${c.title} (${c.developerName})`}
        style={{
          position: 'absolute',
          left: pos.left,
          top: 6,
          width: pos.width,
          height: ROW_HEIGHT - 12,
          backgroundColor: barColor,
          borderRadius: 4,
          cursor: 'pointer',
          display: 'flex',
          alignItems: 'center',
          padding: '0 6px',
          fontSize: 10,
          color: '#fff',
          fontWeight: 600,
          overflow: 'hidden',
          whiteSpace: 'nowrap',
          textOverflow: 'ellipsis',
          border: hasConflict ? '2px solid #991b1b' : 'none',
          boxShadow: hasConflict ? '0 0 6px rgba(220,38,38,0.4)' : '0 1px 2px rgba(0,0,0,0.1)',
        }}
      >
        {pos.width > 60 ? c.title : ''}
      </div>
    );
  }

  if (row.kind === 'milestone') {
    const m = row.data as TimelineMilestone;
    const date = new Date(m.dueDate);
    const leftPos = getMarkerPosition(date, timelineStart, colWidth, scale);
    return (
      <div
        onClick={onClick}
        title={m.title}
        style={{
          position: 'absolute',
          left: leftPos - 8,
          top: 6,
          width: 0, height: 0,
          borderLeft: '9px solid transparent',
          borderRight: '9px solid transparent',
          borderBottom: `18px solid ${getStatusColor(m.status)}`,
          cursor: 'pointer',
          filter: 'drop-shadow(0 1px 2px rgba(0,0,0,0.15))',
        }}
      />
    );
  }

  if (row.kind === 'deliverable') {
    const d = row.data as TimelineDeliverable;
    const dates = [d.qaDate, d.preDate, d.protDate].filter(Boolean).map((x) => new Date(x!));
    if (dates.length === 0) return null;
    const envColors = { 0: '#7c3aed', 1: '#0891b2', 2: '#16a34a' };
    return (
      <>
        {dates.map((date, idx) => {
          const leftPos = getMarkerPosition(date, timelineStart, colWidth, scale);
          return (
            <div
              key={idx}
              title={`${d.title} - ${['QA', 'PRE', 'PROT'][idx]}`}
              style={{
                position: 'absolute',
                left: leftPos - 5,
                top: 10,
                width: 10,
                height: 10,
                borderRadius: '50%',
                backgroundColor: (envColors as any)[idx] || '#6b7280',
                border: '2px solid #fff',
                boxShadow: '0 1px 3px rgba(0,0,0,0.2)',
              }}
            />
          );
        })}
      </>
    );
  }

  if (row.kind === 'env') {
    const e = row.data as TimelineEnvironmentDelivery;
    const date = e.actualDate || e.plannedDate;
    if (!date) return null;
    const leftPos = getMarkerPosition(new Date(date), timelineStart, colWidth, scale);
    const envColorMap: Record<string, string> = { QA: '#7c3aed', PRE: '#0891b2', PROT: '#16a34a', PROD: '#dc2626' };
    return (
      <div
        title={`${e.commitmentTitle || e.deliverableTitle || 'Entrega'} - ${e.environment} (${e.status})`}
        style={{
          position: 'absolute',
          left: leftPos - 7,
          top: 7,
          width: 14,
          height: 14,
          borderRadius: 2,
          backgroundColor: envColorMap[e.environment] || '#6b7280',
          border: `2px solid ${e.status === 'Deployed' ? '#fff' : '#fbbf24'}`,
          boxShadow: '0 1px 3px rgba(0,0,0,0.2)',
          transform: 'rotate(45deg)',
        }}
      />
    );
  }

  return null;
}

function getRowIcon(row: { kind: string }): string {
  switch (row.kind) {
    case 'commitment': return '🎯';
    case 'milestone': return '🏁';
    case 'deliverable': return '📦';
    case 'env': return '🚀';
    default: return '';
  }
}

function getRowLabel(row: { kind: string; data: any }): string {
  if (row.kind === 'commitment') return `${row.data.title} (${row.data.developerName})`;
  if (row.kind === 'milestone') return row.data.title;
  if (row.kind === 'deliverable') return row.data.title;
  if (row.kind === 'env') return `${row.data.commitmentTitle || row.data.deliverableTitle || '—'} → ${row.data.environment}`;
  return '';
}
