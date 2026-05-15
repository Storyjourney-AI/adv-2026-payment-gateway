import type {
  Dto_RegisterRequest,
  Dto_Register,
  Dto_LoginRequest,
  Dto_Login,
  Dto_RefreshTokenResponse,
  Dto_TokenValidation,
} from "../types/auth.types";
import { authenticatedFetch } from './api.helper';
import { getTurnstileToken, isTurnstileEnabled } from "./turnstile.client";

// DataWrapper type matching backend
export interface DataWrapper<T> {
  success: boolean;
  message: string;
  data: T | null;
  errors: string[];
  code: number;
}

const API_BASE = import.meta.env.VITE_API_BASE_URL || '';
const TURNSTILE_HEADER = "X-Turnstile-Token";
const DEV_BYPASS_TOKEN =
  import.meta.env.VITE_TURNSTILE_DEV_BYPASS_TOKEN ||
  (import.meta.env.DEV ? "dev-turnstile-bypass" : "");

/**
 * Deduplication for refresh token calls
 * Prevents multiple simultaneous refresh requests on page load
 */
let refreshTokenPromise: Promise<DataWrapper<Dto_RefreshTokenResponse>> | null = null;

/**
 * Register a new user account
 */
export async function register(
  request: Dto_RegisterRequest
): Promise<DataWrapper<Dto_Register>> {
  const response = await fetch(`${API_BASE}/api/auth/register`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    credentials: "include",
    body: JSON.stringify(request),
  });

  return response.json();
}

/**
 * Login with email and password
 * Refresh token is automatically set as HttpOnly cookie by backend
 */
export async function login(
  request: Dto_LoginRequest
): Promise<DataWrapper<Dto_Login>> {
  const headers: Record<string, string> = { "Content-Type": "application/json" };
  if (isTurnstileEnabled()) {
    const token = await getTurnstileToken("auth-login");
    headers[TURNSTILE_HEADER] = token;
  } else if (DEV_BYPASS_TOKEN) {
    headers[TURNSTILE_HEADER] = DEV_BYPASS_TOKEN;
  }

  const response = await fetch(`${API_BASE}/api/auth/login`, {
    method: "POST",
    headers,
    credentials: "include",
    body: JSON.stringify(request),
  });

  return response.json();
}

/**
 * Validate the current access token
 */
export async function validateToken(
  accessToken: string
): Promise<DataWrapper<Dto_TokenValidation>> {
  const response = await fetch(`${API_BASE}/api/auth/validate`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${accessToken}`,
    },
    credentials: "include",
  });

  return response.json();
}

/**
 * Refresh access token using HttpOnly cookie
 * Uses promise deduplication to prevent race conditions on page load
 */
export async function refreshToken(): Promise<
  DataWrapper<Dto_RefreshTokenResponse>
> {
  // If refresh already in progress, reuse the existing promise
  if (refreshTokenPromise) {
    console.log('[Auth] Refresh already in progress, reusing existing promise');
    return refreshTokenPromise;
  }

  console.log('[Auth] Starting new refresh token request');

  // Create new refresh request
  refreshTokenPromise = (async () => {
    try {
      const headers: Record<string, string> = { "Content-Type": "application/json" };
      if (isTurnstileEnabled()) {
        const token = await getTurnstileToken("auth-refresh");
        headers[TURNSTILE_HEADER] = token;
      } else if (DEV_BYPASS_TOKEN) {
        headers[TURNSTILE_HEADER] = DEV_BYPASS_TOKEN;
      }

      const response = await fetch(`${API_BASE}/api/auth/refresh`, {
        method: "POST",
        headers,
        credentials: "include",
      });

      const data = await response.json();
      
      // Clear promise after short delay to allow concurrent callers to complete
      setTimeout(() => { 
        refreshTokenPromise = null;
        console.log('[Auth] Refresh promise cleared');
      }, 100);
      
      return data;
    } catch (error) {
      refreshTokenPromise = null;
      console.error('[Auth] Refresh token request failed:', error);
      throw error;
    }
  })();

  return refreshTokenPromise;
}

/**
 * Logout user by invalidating refresh token
 */
export async function logout(): Promise<DataWrapper<boolean>> {
  const response = await fetch(`${API_BASE}/api/auth/logout`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    credentials: "include",
  });

  return response.json();
}

/**
 * Change user password
 */
export async function changePassword(data: {
  email: string;
  currentPassword: string;
  newPassword: string;
}): Promise<DataWrapper<boolean>> {
  return authenticatedFetch<boolean>('/api/auth/change-password', {
    method: 'POST',
    body: JSON.stringify(data),
  });
}
