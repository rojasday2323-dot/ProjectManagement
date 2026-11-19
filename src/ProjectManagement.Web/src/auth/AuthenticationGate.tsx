import { InteractionStatus } from '@azure/msal-browser';
import { useMsal } from '@azure/msal-react';
import type { PropsWithChildren } from 'react';
import { entraAuthConfig } from './entraConfig';

export function AuthenticationGate({ children }: PropsWithChildren) {
  const { instance, accounts, inProgress } = useMsal();

  if (inProgress !== InteractionStatus.None) {
    return <main style={pageStyle}><p>Comprobando la sesión de Microsoft Entra ID…</p></main>;
  }

  if (accounts.length === 0 || !entraAuthConfig) {
    return (
      <main style={pageStyle}>
        <section style={cardStyle}>
          <h1>Iniciar sesión</h1>
          <p>La aplicación requiere una sesión de Microsoft Entra ID para acceder a la API.</p>
          <button
            type="button"
            onClick={() => entraAuthConfig && instance.loginRedirect({ scopes: [entraAuthConfig.apiScope] })}
            disabled={!entraAuthConfig}
          >
            Iniciar sesión con Microsoft
          </button>
        </section>
      </main>
    );
  }

  return children;
}

const pageStyle = {
  minHeight: '100vh',
  display: 'grid',
  placeItems: 'center',
  background: '#f3f4f6',
  padding: 24,
} as const;

const cardStyle = {
  maxWidth: 520,
  width: '100%',
  background: '#fff',
  border: '1px solid #e5e7eb',
  borderRadius: 12,
  padding: 28,
  color: '#1f2937',
} as const;
