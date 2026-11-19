import { getStatusColor } from '../../utils/format';

interface Props {
  status: string;
}

export function StatusBadge({ status }: Props) {
  const color = getStatusColor(status);
  return (
    <span
      style={{
        display: 'inline-block',
        padding: '2px 10px',
        borderRadius: 12,
        fontSize: 12,
        fontWeight: 600,
        color: '#fff',
        backgroundColor: color,
      }}
    >
      {status}
    </span>
  );
}
