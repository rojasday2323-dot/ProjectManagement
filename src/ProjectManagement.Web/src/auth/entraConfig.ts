import { PublicClientApplication, type Configuration } from '@azure/msal-browser';

const tenantId = import.meta.env.VITE_ENTRA_TENANT_ID?.trim();
const clientId = import.meta.env.VITE_ENTRA_CLIENT_ID?.trim();
const apiScope = import.meta.env.VITE_API_SCOPE?.trim();

export const developmentAuthBypass =
  import.meta.env.DEV && import.meta.env.VITE_DEV_AUTH_BYPASS === 'true';

export const entraAuthConfig = tenantId && clientId && apiScope
  ? { tenantId, clientId, apiScope }
  : null;

const msalConfiguration: Configuration | null = entraAuthConfig
  ? {
      auth: {
        clientId: entraAuthConfig.clientId,
        authority: `https://login.microsoftonline.com/${entraAuthConfig.tenantId}`,
        redirectUri: window.location.origin,
        postLogoutRedirectUri: window.location.origin,
      },
      cache: {
        cacheLocation: 'sessionStorage',
      },
    }
  : null;

export const msalInstance = msalConfiguration
  ? new PublicClientApplication(msalConfiguration)
  : null;
