import { Card } from '../ui/Card';
import { LoadingSpinner } from '../ui/LoadingSpinner';
import { formatDate } from '../../utils/format';
import type { AlertResponse } from '../../types/api';

interface Props {
  alerts: AlertResponse[] | null;
  loading: boolean;
  title?: string;
}

const severityConfig: Record<string, { icon: string; color: string; bg: string }> = {
  Critical: { icon: '🔴', color: '#991b1b', bg: '#fef2f2' },
  Warning: { icon: '🟠', color: '#92400e', bg: '#fffbeb' },
  Info: { icon: '🔵', color: '#1e40af', bg: '#eff6ff' },
};

const typeLabels: Record<string, string> = {
  Overlap: 'Solapamiento',
  DeliveryConflict: 'Conflicto de entrega',
  Overload: 'Sobrecarga',
  DeadlineApproaching: 'Fecha límite próxima',
  Overdue: 'Vencido',
};

export function AlertsPanel({ alerts, loading, title = '🚨 Alertas y conflictos' }: Props) {
  return (
    <Card>
      <h3 style={{ fontSize: 16, fontWeight: 600, marginBottom: 14, color: '#111827' }}>{title}</h3>

      {loading ? (
        <LoadingSpinner />
      ) : !alerts?.length ? (
        <div style={{ padding: 24, textAlign: 'center', color: '#16a34a' }}>
          <div style={{ fontSize: 32, marginBottom: 8 }}>✅</div>
          <p style={{ fontSize: 14 }}>No se detectaron conflictos ni alertas.</p>
        </div>
      ) : (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
          {alerts.map((alert) => {
            const config = severityConfig[alert.severity] || severityConfig.Info;
            return (
              <div
                key={alert.id}
                style={{
                  padding: '14px 16px',
                  borderRadius: 8,
                  backgroundColor: config.bg,
                  borderLeft: `4px solid ${config.color}`,
                }}
              >
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: 6 }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                    <span>{config.icon}</span>
                    <span style={{ fontSize: 11, fontWeight: 700, textTransform: 'uppercase', color: config.color }}>
                      {alert.severity}
                    </span>
                    <span style={{
                      fontSize: 11, padding: '1px 8px', borderRadius: 10,
                      backgroundColor: `${config.color}20`, color: config.color,
                    }}>
                      {typeLabels[alert.alertType] || alert.alertType}
                    </span>
                  </div>
                  <span style={{
                    fontSize: 11, padding: '2px 8px', borderRadius: 10,
                    backgroundColor: alert.status === 'Open' ? '#fee2e2' : alert.status === 'Acknowledged' ? '#fef3c7' : '#dcfce7',
                    color: alert.status === 'Open' ? '#991b1b' : alert.status === 'Acknowledged' ? '#92400e' : '#166534',
                    fontWeight: 600,
                  }}>
                    {alert.status}
                  </span>
                </div>

                <p style={{ fontSize: 13, color: '#374151', margin: '4px 0 8px', lineHeight: 1.5 }}>
                  {alert.message}
                </p>

                <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap', fontSize: 12, color: '#6b7280' }}>
                  {alert.developerName && <span>👤 {alert.developerName}</span>}
                  <span>📁 {alert.projectName}</span>
                  {alert.conflictStartDate && (
                    <span>📅 {formatDate(alert.conflictStartDate)} → {formatDate(alert.conflictEndDate)}</span>
                  )}
                </div>

                {(alert.commitmentTitle || alert.relatedCommitmentTitle) && (
                  <div style={{ marginTop: 6, fontSize: 12, color: '#6b7280' }}>
                    {alert.commitmentTitle && <span>🎯 {alert.commitmentTitle}</span>}
                    {alert.relatedCommitmentTitle && <span> ↔ {alert.relatedCommitmentTitle}</span>}
                  </div>
                )}
              </div>
            );
          })}
        </div>
      )}
    </Card>
  );
}
