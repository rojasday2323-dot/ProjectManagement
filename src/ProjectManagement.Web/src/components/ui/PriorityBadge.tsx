import { getPriorityColor } from '../../utils/format';

interface Props {
  priority: string;
}

export function PriorityBadge({ priority }: Props) {
  const color = getPriorityColor(priority);
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
      {priority}
    </span>
  );
}
