import { authStore } from '../store/auth.store';

// DataWrapper type matching backend
export interface DataWrapper<T> {
  success: boolean;
  message: string;
  data: T | null;
  errors: string[];
  code: number;
}

const API_BASE = import.meta.env.VITE_API_BASE_URL || '';

/**
 * Get authentication headers with Bearer token
 */
export function getAuthHeaders(): HeadersInit {
  const token = authStore.getAccessToken();
  const headers: HeadersInit = {
    'Content-Type': 'application/json',
  };

  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }

  return headers;
}

/**
 * Authenticated fetch wrapper that automatically includes Bearer token
 */
export async function authenticatedFetch<T>(
  endpoint: string,
  options: RequestInit = {}
): Promise<DataWrapper<T>> {
  const headers = getAuthHeaders();

  const response = await fetch(`${API_BASE}${endpoint}`, {
    ...options,
    headers: {
      ...headers,
      ...options.headers,
    },
    credentials: 'include',
  });

  return response.json();
}
