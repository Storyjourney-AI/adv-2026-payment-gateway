/**
 * JWT Utility Functions
 * Handles JWT token decoding, validation, and user extraction
 */

import type { UserDetails } from "../types/authStore.types";

export interface JwtPayload {
  sub?: string;
  sub_id?: string;
  email?: string;
  role?: string | string[];
  roles?: string | string[];
  exp?: number;
  iat?: number;
  jti?: string;
  iss?: string;
  aud?: string;
  [key: string]: any;
}

/**
 * Decode a JWT token without verification
 * @param token JWT token string
 * @returns Decoded JWT payload
 */
export function decodeJwt(token: string): JwtPayload {
  try {
    const parts = token.split('.');
    if (parts.length !== 3) {
      throw new Error('Invalid JWT format: token must have 3 parts');
    }

    const payload = parts[1];
    
    // Decode base64url to JSON
    // Replace URL-safe characters and add padding
    const base64 = payload.replace(/-/g, '+').replace(/_/g, '/');
    const padded = base64.padEnd(base64.length + (4 - (base64.length % 4)) % 4, '=');
    const decoded = atob(padded);
    
    return JSON.parse(decoded);
  } catch (error) {
    throw new Error(`Failed to decode JWT: ${error instanceof Error ? error.message : 'Unknown error'}`);
  }
}

/**
 * Extract user details from JWT token
 * @param token JWT token string
 * @returns User details object
 */
export function extractUserDetails(token: string): UserDetails {
  const decoded = decodeJwt(token);
  
  // Extract user ID (backend uses 'sub_id')
  const userId = decoded.sub_id || decoded.sub || decoded.userId || '';
  
  // Extract email
  const email = decoded.email || '';
  
  // Extract roles - ASP.NET uses the full claim type URI
  let roles: string[] = [];
  const aspNetRoleClaim = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';
  
  if (decoded[aspNetRoleClaim]) {
    roles = Array.isArray(decoded[aspNetRoleClaim]) ? decoded[aspNetRoleClaim] : [decoded[aspNetRoleClaim]];
  } else if (decoded.role) {
    // Fallback to simple 'role' claim
    roles = Array.isArray(decoded.role) ? decoded.role : [decoded.role];
  } else if (decoded.roles) {
    // Fallback to 'roles' claim
    roles = Array.isArray(decoded.roles) ? decoded.roles : [decoded.roles];
  }
  
  return {
    userId,
    email,
    roles,
  };
}

/**
 * Check if a JWT token is expired
 * @param token JWT token string
 * @returns True if token is expired, false otherwise
 */
export function isTokenExpired(token: string): boolean {
  try {
    const decoded = decodeJwt(token);
    
    if (!decoded.exp) {
      // No expiration claim, consider it never expires
      return false;
    }
    
    const expirationTime = decoded.exp * 1000; // Convert to milliseconds
    const currentTime = Date.now();
    
    return currentTime >= expirationTime;
  } catch (error) {
    // If decoding fails, consider token invalid/expired
    return true;
  }
}

/**
 * Check if a JWT token will expire within a specified number of minutes
 * Useful for auto-refresh logic
 * @param token JWT token string
 * @param minutes Number of minutes to check ahead
 * @returns True if token will expire within the specified minutes
 */
export function isTokenExpiredInMinutes(token: string, minutes: number): boolean {
  try {
    const decoded = decodeJwt(token);
    
    if (!decoded.exp) {
      // No expiration claim, won't expire
      return false;
    }
    
    const expirationTime = decoded.exp * 1000; // Convert to milliseconds
    const currentTime = Date.now();
    const thresholdTime = currentTime + (minutes * 60 * 1000);
    
    return thresholdTime >= expirationTime; // Will expire within X minutes
  } catch (error) {
    // If decoding fails, consider token expiring
    return true;
  }
}

/**
 * Get the expiration date of a JWT token
 * @param token JWT token string
 * @returns Expiration date or null if no expiration claim
 */
export function getTokenExpiration(token: string): Date | null {
  try {
    const decoded = decodeJwt(token);
    
    if (!decoded.exp) {
      return null;
    }
    
    return new Date(decoded.exp * 1000);
  } catch (error) {
    return null;
  }
}

/**
 * Get the issued-at date of a JWT token
 * @param token JWT token string
 * @returns Issued-at date or null if no iat claim
 */
export function getTokenIssuedAt(token: string): Date | null {
  try {
    const decoded = decodeJwt(token);
    
    if (!decoded.iat) {
      return null;
    }
    
    return new Date(decoded.iat * 1000);
  } catch (error) {
    return null;
  }
}
