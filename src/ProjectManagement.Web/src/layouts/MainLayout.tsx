import { NavLink, Outlet } from 'react-router-dom';
import { useMsal } from '@azure/msal-react';

interface MainLayoutProps {
  showLogout: boolean;
}

const navItems = [
  { to: '/', label: 'Dashboard', icon: '📊' },
  { to: '/projects', label: 'Proyectos', icon: '📁' },
  { to: '/commitments', label: 'Compromisos', icon: '🎯' },
  { to: '/developers', label: 'Desarrolladores', icon: '👨‍💻' },
  { to: '/product-owners', label: 'Product Owners', icon: '👩‍💼' },
  { to: '/documents', label: 'Documentos y notas', icon: '📄' },
  { to: '/azure-devops', label: 'Azure DevOps', icon: '🔗' },
];

export function MainLayout({ showLogout }: MainLayoutProps) {
  return (
    <div style={{ display: 'flex', minHeight: '100vh', backgroundColor: '#f3f4f6' }}>
      {/* Sidebar */}
      <aside
        style={{
          width: 240,
          backgroundColor: '#111827',
          color: '#fff',
          display: 'flex',
          flexDirection: 'column',
          flexShrink: 0,
        }}
      >
        <div
          style={{
            padding: '20px 20px 16px',
            borderBottom: '1px solid #374151',
            fontSize: 17,
            fontWeight: 700,
            letterSpacing: -0.3,
          }}
        >
          📋 ProjectMgmt
        </div>
        <nav style={{ flex: 1, padding: '12px 0' }}>
          {navItems.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.to === '/'}
              style={({ isActive }) => ({
                display: 'flex',
                alignItems: 'center',
                gap: 10,
                padding: '10px 20px',
                color: isActive ? '#fff' : '#9ca3af',
                backgroundColor: isActive ? '#1f2937' : 'transparent',
                textDecoration: 'none',
                fontSize: 14,
                fontWeight: isActive ? 600 : 400,
                borderLeft: isActive ? '3px solid #3b82f6' : '3px solid transparent',
                transition: 'all 0.15s',
              })}
            >
              <span>{item.icon}</span>
              <span>{item.label}</span>
            </NavLink>
          ))}
        </nav>
        <div style={{ padding: '12px 20px', borderTop: '1px solid #374151', fontSize: 11, color: '#6b7280' }}>
          v1.0.0 — Desarrollo
        </div>
      </aside>

      {/* Main content */}
      <div style={{ flex: 1, display: 'flex', flexDirection: 'column', minWidth: 0 }}>
        <header
          style={{
            height: 56,
            backgroundColor: '#fff',
            borderBottom: '1px solid #e5e7eb',
            display: 'flex',
            alignItems: 'center',
            padding: '0 24px',
            justifyContent: 'space-between',
          }}
        >
          <h2 style={{ fontSize: 15, fontWeight: 600, color: '#374151', margin: 0 }}>
            Seguimiento y Administración de Proyectos
          </h2>
          {showLogout && <SessionLogoutButton />}
        </header>
        <main style={{ flex: 1, padding: 24, overflowY: 'auto' }}>
          <Outlet />
        </main>
      </div>
    </div>
  );
}

function SessionLogoutButton() {
  const { instance } = useMsal();

  return (
    <button
      type="button"
      onClick={() => void instance.logoutRedirect({ postLogoutRedirectUri: window.location.origin })}
      style={{ border: 0, background: 'transparent', color: '#4b5563', cursor: 'pointer' }}
    >
      Sesión Entra ID · Cerrar sesión
    </button>
  );
}
