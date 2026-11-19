import { Link } from 'react-router-dom';

export function NotFoundPage() {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', padding: 80 }}>
      <div style={{ fontSize: 64, marginBottom: 16 }}>🔍</div>
      <h1 style={{ fontSize: 28, fontWeight: 700, color: '#111827', marginBottom: 8 }}>404 — Página no encontrada</h1>
      <p style={{ fontSize: 15, color: '#6b7280', marginBottom: 24 }}>La página que buscas no existe.</p>
      <Link
        to="/"
        style={{
          padding: '10px 24px',
          backgroundColor: '#2563eb',
          color: '#fff',
          borderRadius: 8,
          textDecoration: 'none',
          fontWeight: 600,
          fontSize: 14,
        }}
      >
        Volver al Dashboard
      </Link>
    </div>
  );
}
