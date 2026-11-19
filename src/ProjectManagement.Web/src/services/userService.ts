import apiClient from './apiClient';
import type {
  DeveloperResponse,
  CreateDeveloperRequest,
  ProductOwnerResponse,
  UserResponse,
} from '../types/api';

export const userService = {
  getAll: () =>
    apiClient.get<UserResponse[]>('/api/users').then((r) => r.data),

  getById: (id: number) =>
    apiClient.get<UserResponse>(`/api/users/${id}`).then((r) => r.data),
};

export const developerService = {
  getAll: (includeInactive = false) =>
    apiClient
      .get<DeveloperResponse[]>('/api/developers', { params: { includeInactive } })
      .then((r) => r.data),

  getById: (id: number) =>
    apiClient.get<DeveloperResponse>(`/api/developers/${id}`).then((r) => r.data),

  create: (request: CreateDeveloperRequest) =>
    apiClient.post<DeveloperResponse>('/api/developers', request).then((r) => r.data),

  update: (id: number, request: CreateDeveloperRequest) =>
    apiClient.put<DeveloperResponse>(`/api/developers/${id}`, request).then((r) => r.data),

  setActive: (id: number, isActive: boolean) =>
    apiClient.patch<DeveloperResponse>(`/api/developers/${id}/status`, { isActive }).then((r) => r.data),
};

export const productOwnerService = {
  getAll: () =>
    apiClient
      .get<ProductOwnerResponse[]>('/api/product-owners')
      .then((r) => r.data),

  getById: (id: number) =>
    apiClient
      .get<ProductOwnerResponse>(`/api/product-owners/${id}`)
      .then((r) => r.data),
};
