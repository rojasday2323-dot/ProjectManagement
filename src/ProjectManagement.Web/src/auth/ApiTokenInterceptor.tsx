import { InteractionRequiredAuthError } from '@azure/msal-browser';
import { useMsal } from '@azure/msal-react';
import { useEffect } from 'react';
import apiClient from '../services/apiClient';
import { entraAuthConfig } from './entraConfig';

export function ApiTokenInterceptor() {
  const { instance } = useMsal();
  const apiScope = entraAuthConfig?.apiScope;

  useEffect(() => {
    if (!apiScope) return;

    const interceptorId = apiClient.interceptors.request.use(async (config) => {
      const account = instance.getActiveAccount() ?? instance.getAllAccounts()[0];
      if (!account) {
        throw new Error('Inicia sesión con Microsoft Entra ID para continuar.');
      }

      try {
        const token = await instance.acquireTokenSilent({
          account,
          scopes: [apiScope],
        });
        config.headers.Authorization = `Bearer ${token.accessToken}`;
        return config;
      } catch (error) {
        if (error instanceof InteractionRequiredAuthError) {
          throw new Error('La sesión requiere autenticación o consentimiento adicional. Vuelve a iniciar sesión.');
        }
        throw error;
      }
    });

    return () => apiClient.interceptors.request.eject(interceptorId);
  }, [apiScope, instance]);

  return null;
}
