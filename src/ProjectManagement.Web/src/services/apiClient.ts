import axios from 'axios';

export class ApiRequestError extends Error {
  readonly status: number;
  readonly code: string | undefined;

  constructor(
    message: string,
    status: number,
    code: string | undefined,
  ) {
    super(message);
    this.name = 'ApiRequestError';
    this.status = status;
    this.code = code;
  }
}

const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_URL || '/',
  headers: { 'Content-Type': 'application/json' },
  timeout: 15000,
});

apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response) {
      const data = error.response.data;
      const message = data?.detail || data?.message || data?.title || `Error ${error.response.status}`;
      return Promise.reject(new ApiRequestError(message, error.response.status, data?.code));
    }
    if (error.request) {
      return Promise.reject(new Error('No se pudo conectar con el servidor.'));
    }
    return Promise.reject(error);
  }
);

export default apiClient;
