import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { MsalProvider } from '@azure/msal-react';
import { ApiTokenInterceptor } from './auth/ApiTokenInterceptor';
import { AuthenticationGate } from './auth/AuthenticationGate';
import { entraAuthConfig, msalInstance } from './auth/entraConfig';
import { MainLayout } from './layouts/MainLayout';
import { DashboardPage } from './pages/DashboardPage';
import { ProjectsPage } from './pages/ProjectsPage';
import { ProjectDetailPage } from './pages/ProjectDetailPage';
import { CommitmentsPage } from './pages/CommitmentsPage';
import { DevelopersPage } from './pages/DevelopersPage';
import { ProductOwnersPage } from './pages/ProductOwnersPage';
import { DocumentsPage } from './pages/DocumentsPage';
import { AzureDevOpsPage } from './pages/AzureDevOpsPage';
import { NotFoundPage } from './pages/NotFoundPage';
import { developmentAuthBypass } from './auth/entraConfig';

function App() {
  if (developmentAuthBypass) {
    return <ApplicationRoutes showLogout={false} />;
  }

  if (!entraAuthConfig || !msalInstance) {
    return (
      <main style={{ minHeight: '100vh', display: 'grid', placeItems: 'center', padding: 24, background: '#f3f4f6' }}>
        <section style={{ maxWidth: 620, padding: 28, borderRadius: 12, background: '#fff', color: '#1f2937' }}>
          <h1>Configuración de Microsoft Entra ID pendiente</h1>
          <p>El administrador debe configurar VITE_ENTRA_TENANT_ID, VITE_ENTRA_CLIENT_ID (SPA) y VITE_API_SCOPE para habilitar el inicio de sesión.</p>
          <p>El backend usa el Client ID de la API y requiere una credencial confidencial configurada fuera del repositorio.</p>
        </section>
      </main>
    );
  }

  return (
    <MsalProvider instance={msalInstance}>
      <ApiTokenInterceptor />
      <AuthenticationGate>
        <ApplicationRoutes showLogout />
      </AuthenticationGate>
    </MsalProvider>
  );
}

function ApplicationRoutes({ showLogout }: { showLogout: boolean }) {
  return (
    <BrowserRouter>
      <Routes>
        <Route element={<MainLayout showLogout={showLogout} />}>
          <Route path="/" element={<DashboardPage />} />
          <Route path="/projects" element={<ProjectsPage />} />
          <Route path="/projects/:id" element={<ProjectDetailPage />} />
          <Route path="/commitments" element={<CommitmentsPage />} />
          <Route path="/developers" element={<DevelopersPage />} />
          <Route path="/product-owners" element={<ProductOwnersPage />} />
          <Route path="/documents" element={<DocumentsPage />} />
          <Route path="/azure-devops" element={<AzureDevOpsPage />} />
          <Route path="*" element={<NotFoundPage />} />
        </Route>
      </Routes>
    </BrowserRouter>
  );
}

export default App;
