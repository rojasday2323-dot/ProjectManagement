interface Props {
  message: string;
  onRetry?: () => void;
}

export function ErrorAlert({ message, onRetry }: Props) {
  return (
    <div
      style={{
        padding: 16,
        margin: 16,
        backgroundColor: '#fef2f2',
        border: '1px solid #fecaca',
        borderRadius: 8,
        color: '#991b1b',
      }}
    >
      <strong>Error: </strong>
      {message}
      {onRetry && (
        <button
          onClick={onRetry}
          style={{
            marginLeft: 12,
            padding: '4px 12px',
            backgroundColor: '#dc2626',
            color: '#fff',
            border: 'none',
            borderRadius: 4,
            cursor: 'pointer',
            fontSize: 13,
          }}
        >
          Reintentar
        </button>
      )}
    </div>
  );
}
