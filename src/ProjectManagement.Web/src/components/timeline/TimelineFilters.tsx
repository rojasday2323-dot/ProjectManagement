import { Card } from '../ui/Card';
import type { TimeScale } from './timelineUtils';

export interface TimelineFilterState {
  scale: TimeScale;
  showCommitments: boolean;
  showMilestones: boolean;
  showDeliverables: boolean;
  showEnvDeliveries: boolean;
  developerFilter: string;
  statusFilter: string;
  priorityFilter: string;
}

interface Props {
  filters: TimelineFilterState;
  onChange: (f: TimelineFilterState) => void;
  developers: string[];
  onNavigate: (dir: 'prev' | 'today' | 'next') => void;
}

export function TimelineFilters({ filters, onChange, developers, onNavigate }: Props) {
  const set = (partial: Partial<TimelineFilterState>) => onChange({ ...filters, ...partial });

  return (
    <Card style={{ marginBottom: 16 }}>
      <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', alignItems: 'center' }}>
        {/* Scale */}
        <div style={groupStyle}>
          <label style={labelStyle}>Escala</label>
          <div style={{ display: 'flex', gap: 2 }}>
            {(['days', 'weeks', 'months'] as TimeScale[]).map((s) => (
              <button
                key={s}
                onClick={() => set({ scale: s })}
                style={{
                  ...btnStyle,
                  backgroundColor: filters.scale === s ? '#2563eb' : '#f3f4f6',
                  color: filters.scale === s ? '#fff' : '#374151',
                }}
              >
                {s === 'days' ? 'Días' : s === 'weeks' ? 'Semanas' : 'Meses'}
              </button>
            ))}
          </div>
        </div>

        {/* Navigation */}
        <div style={groupStyle}>
          <label style={labelStyle}>Navegar</label>
          <div style={{ display: 'flex', gap: 2 }}>
            <button onClick={() => onNavigate('prev')} style={btnStyle}>← Ant</button>
            <button onClick={() => onNavigate('today')} style={{ ...btnStyle, backgroundColor: '#2563eb', color: '#fff' }}>Hoy</button>
            <button onClick={() => onNavigate('next')} style={btnStyle}>Sig →</button>
          </div>
        </div>

        {/* Toggles */}
        <div style={groupStyle}>
          <label style={labelStyle}>Mostrar</label>
          <div style={{ display: 'flex', gap: 6 }}>
            <Toggle label="Compromisos" checked={filters.showCommitments} onChange={(v) => set({ showCommitments: v })} />
            <Toggle label="Milestones" checked={filters.showMilestones} onChange={(v) => set({ showMilestones: v })} />
            <Toggle label="Entregables" checked={filters.showDeliverables} onChange={(v) => set({ showDeliverables: v })} />
            <Toggle label="Ambientes" checked={filters.showEnvDeliveries} onChange={(v) => set({ showEnvDeliveries: v })} />
          </div>
        </div>

        {/* Developer filter */}
        <div style={groupStyle}>
          <label style={labelStyle}>Desarrollador</label>
          <select value={filters.developerFilter} onChange={(e) => set({ developerFilter: e.target.value })} style={selectStyle}>
            <option value="">Todos</option>
            {developers.map((d) => <option key={d} value={d}>{d}</option>)}
          </select>
        </div>

        {/* Status */}
        <div style={groupStyle}>
          <label style={labelStyle}>Estado</label>
          <select value={filters.statusFilter} onChange={(e) => set({ statusFilter: e.target.value })} style={selectStyle}>
            <option value="">Todos</option>
            {['Pending', 'InProgress', 'Completed', 'Delayed'].map((s) => <option key={s} value={s}>{s}</option>)}
          </select>
        </div>

        {/* Priority */}
        <div style={groupStyle}>
          <label style={labelStyle}>Prioridad</label>
          <select value={filters.priorityFilter} onChange={(e) => set({ priorityFilter: e.target.value })} style={selectStyle}>
            <option value="">Todas</option>
            {['Low', 'Medium', 'High', 'Critical'].map((p) => <option key={p} value={p}>{p}</option>)}
          </select>
        </div>
      </div>
    </Card>
  );
}

function Toggle({ label, checked, onChange }: { label: string; checked: boolean; onChange: (v: boolean) => void }) {
  return (
    <label style={{ display: 'flex', alignItems: 'center', gap: 4, fontSize: 12, cursor: 'pointer', color: '#374151' }}>
      <input type="checkbox" checked={checked} onChange={(e) => onChange(e.target.checked)} />
      {label}
    </label>
  );
}

const groupStyle: React.CSSProperties = { display: 'flex', flexDirection: 'column', gap: 4 };
const labelStyle: React.CSSProperties = { fontSize: 11, fontWeight: 600, color: '#6b7280' };
const btnStyle: React.CSSProperties = {
  padding: '4px 10px', border: '1px solid #d1d5db', borderRadius: 4,
  fontSize: 12, cursor: 'pointer', backgroundColor: '#f3f4f6', color: '#374151',
};
const selectStyle: React.CSSProperties = {
  padding: '4px 8px', border: '1px solid #d1d5db', borderRadius: 4, fontSize: 12, minWidth: 110,
};
