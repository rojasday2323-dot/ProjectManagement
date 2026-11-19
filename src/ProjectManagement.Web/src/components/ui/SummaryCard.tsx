interface Props {
  title: string;
  value: string | number;
  icon: string;
  color?: string;
}

export function SummaryCard({ title, value, icon, color = '#2563eb' }: Props) {
  return (
    <div
      style={{
        backgroundColor: '#fff',
        borderRadius: 8,
        border: '1px solid #e5e7eb',
        padding: '20px 24px',
        display: 'flex',
        alignItems: 'center',
        gap: 16,
        boxShadow: '0 1px 3px rgba(0,0,0,0.06)',
      }}
    >
      <div
        style={{
          width: 48,
          height: 48,
          borderRadius: 12,
          backgroundColor: `${color}15`,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          fontSize: 24,
        }}
      >
        {icon}
      </div>
      <div>
        <div style={{ fontSize: 13, color: '#6b7280', marginBottom: 2 }}>{title}</div>
        <div style={{ fontSize: 28, fontWeight: 700, color: '#111827' }}>{value}</div>
      </div>
    </div>
  );
}
