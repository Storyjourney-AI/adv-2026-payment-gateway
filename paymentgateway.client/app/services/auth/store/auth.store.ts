/**
 * In-Memory Authentication Store
 * 
 * Secure singleton store for access token and user details.
 * Stored in memory only (not localStorage/sessionStorage) to prevent XSS attacks.
 * 
 * Trade-off: Data is lost on page refresh
 * Solution: Auto-refresh mechanism on app initialization
 */

import type { UserDetails, AuthStoreState } from "../types/authStore.types";

class AuthStoreImpl {
  private accessToken: string | null = null;
  private user: UserDetails | null = null;

  /**
   * Set the access token in memory
   */
  setAccessToken(token: string): void {
    this.accessToken = token;
  }

  /**
   * Get the current access token from memory
   */
  getAccessToken(): string | null {
    return this.accessToken;
  }

  /**
   * Set user details in memory
   */
  setUser(user: UserDetails): void {
    this.user = user;
  }

  /**
   * Get current user details from memory
   */
  getUser(): UserDetails | null {
    return this.user;
  }

  /**
   * Get the full auth state
   */
  getState(): AuthStoreState {
    return {
      accessToken: this.accessToken,
      user: this.user,
    };
  }

  /**
   * Clear all authentication data from memory
   * Called on logout or when refresh fails
   */
  clearAll(): void {
    this.accessToken = null;
    this.user = null;
  }

  /**
   * Check if user is authenticated (has valid token)
   */
  isAuthenticated(): boolean {
    return this.accessToken !== null;
  }
}

// Export singleton instance
export const authStore = new AuthStoreImpl();
