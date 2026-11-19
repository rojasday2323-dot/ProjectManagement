interface Props {
  message?: string;
}

export function EmptyState({ message = 'No hay datos para mostrar.' }: Props) {
  return (
    <div style={{ padding: 48, textAlign: 'center', color: '#9ca3af' }}>
      <div style={{ fontSize: 40, marginBottom: 12 }}>📭</div>
      <p style={{ fontSize: 15 }}>{message}</p>
    </div>
  );
}
