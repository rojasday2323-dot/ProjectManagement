import { StatusBadge } from '../ui/StatusBadge';
import { PriorityBadge } from '../ui/PriorityBadge';
import { formatDate } from '../../utils/format';
import type { TimelineCommitment, TimelineMilestone, TimelineAlert } from '../../types/api';

type DetailItem =
  | { type: 'commitment'; data: TimelineCommitment; relatedAlerts: TimelineAlert[] }
  | { type: 'milestone'; data: TimelineMilestone }
  | { type: 'alert'; data: TimelineAlert };

interface Props {
  item: DetailItem | null;
  onClose: () => void;
}

export function TimelineDetails({ item, onClose }: Props) {
  if (!item) return null;

  return (
    <div
      style={{
        position: 'fixed', top: 0, right: 0, bottom: 0, width: 420,
        backgroundColor: '#fff', boxShadow: '-4px 0 20px rgba(0,0,0,0.15)',
        zIndex: 1000, display: 'flex', flexDirection: 'column',
      }}
    >
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '16px 20px', borderBottom: '1px solid #e5e7eb' }}>
        <h3 style={{ margin: 0, fontSize: 16, fontWeight: 700 }}>
          {item.type === 'commitment' ? '🎯 Compromiso' : item.type === 'milestone' ? '🏁 Milestone' : '🚨 Alerta'}
        </h3>
        <button onClick={onClose} style={{ background: 'none', border: 'none', fontSize: 20, cursor: 'pointer', color: '#6b7280' }}>✕</button>
      </div>

      <div style={{ flex: 1, overflowY: 'auto', padding: 20 }}>
        {item.type === 'commitment' && <CommitmentDetail data={item.data} alerts={item.relatedAlerts} />}
        {item.type === 'milestone' && <MilestoneDetail data={item.data} />}
        {item.type === 'alert' && <AlertDetail data={item.data} />}
      </div>
    </div>
  );
}

function CommitmentDetail({ data, alerts }: { data: TimelineCommitment; alerts: TimelineAlert[] }) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
      <h4 style={{ margin: 0, fontSize: 17, fontWeight: 700 }}>{data.title}</h4>
      <div style={{ display: 'flex', gap: 8 }}>
        <StatusBadge status={data.status} />
        <PriorityBadge priority={data.priority} />
      </div>
      <p style={{ fontSize: 13, color: '#6b7280', lineHeight: 1.6, margin: 0 }}>{data.description}</p>

      <div style={sectionStyle}>
        <Row label="Desarrollador" value={data.developerName} />
        <Row label="Product Owner" value={data.productOwnerName} />
        <Row label="Inicio" value={formatDate(data.startDate)} />
        <Row label="Compromiso" value={formatDate(data.commitmentDate)} />
        {data.completedDate && <Row label="Completado" value={formatDate(data.completedDate)} />}
      </div>

      {(data.qaDate || data.preDate || data.protDate) && (
        <div style={sectionStyle}>
          <div style={{ fontSize: 13, fontWeight: 600, marginBottom: 6 }}>Ambientes</div>
          {data.qaDate && <Row label="QA" value={formatDate(data.qaDate)} />}
          {data.preDate && <Row label="PRE" value={formatDate(data.preDate)} />}
          {data.protDate && <Row label="PROT" value={formatDate(data.protDate)} />}
        </div>
      )}

      {alerts.length > 0 && (
        <div style={sectionStyle}>
          <div style={{ fontSize: 13, fontWeight: 600, marginBottom: 6, color: '#dc2626' }}>🚨 Alertas ({alerts.length})</div>
          {alerts.map((a) => (
            <div key={a.id} style={{ padding: '8px 10px', marginBottom: 6, borderRadius: 6, backgroundColor: a.severity === 'Critical' ? '#fef2f2' : '#fffbeb', fontSize: 12, lineHeight: 1.5 }}>
              <strong>{a.severity}:</strong> {a.message}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

function MilestoneDetail({ data }: { data: TimelineMilestone }) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
      <h4 style={{ margin: 0, fontSize: 17, fontWeight: 700 }}>{data.title}</h4>
      <StatusBadge status={data.status} />
      {data.description && <p style={{ fontSize: 13, color: '#6b7280', margin: 0 }}>{data.description}</p>}
      <div style={sectionStyle}>
        <Row label="Fecha límite" value={formatDate(data.dueDate)} />
        {data.completedDate && <Row label="Completado" value={formatDate(data.completedDate)} />}
      </div>
    </div>
  );
}

function AlertDetail({ data }: { data: TimelineAlert }) {
  const sevColor = data.severity === 'Critical' ? '#991b1b' : data.severity === 'Warning' ? '#92400e' : '#1e40af';
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
        <span style={{ fontSize: 11, fontWeight: 700, textTransform: 'uppercase', color: sevColor }}>{data.severity}</span>
        <span style={{ fontSize: 12, color: '#6b7280' }}>{data.alertType}</span>
      </div>
      <p style={{ fontSize: 14, color: '#374151', margin: 0, lineHeight: 1.6 }}>{data.message}</p>
      <div style={sectionStyle}>
        {data.developerName && <Row label="Desarrollador" value={data.developerName} />}
        {data.commitmentTitle && <Row label="Compromiso" value={data.commitmentTitle} />}
        {data.relatedCommitmentTitle && <Row label="Relacionado" value={data.relatedCommitmentTitle} />}
        {data.conflictStartDate && <Row label="Periodo" value={`${formatDate(data.conflictStartDate)} → ${formatDate(data.conflictEndDate)}`} />}
      </div>
    </div>
  );
}

function Row({ label, value }: { label: string; value: string }) {
  return (
    <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 13, padding: '3px 0' }}>
      <span style={{ color: '#6b7280' }}>{label}</span>
      <span style={{ fontWeight: 500 }}>{value}</span>
    </div>
  );
}

const sectionStyle: React.CSSProperties = {
  padding: '10px 12px', borderRadius: 6, backgroundColor: '#f9fafb', border: '1px solid #f3f4f6',
};

export type { DetailItem };
