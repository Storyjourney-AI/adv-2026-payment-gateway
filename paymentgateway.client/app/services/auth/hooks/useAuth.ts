/**
 * useAuth Hook
 * 
 * Reactive authentication state management using Zustand.
 * Handles login, logout, token refresh, and auto-initialization.
 * 
 * Key Features:
 * - Auto-refresh on app initialization
 * - In-memory token storage (secure)
 * - Reactive state updates across components
 * - Token expiration checking
 */

import { create } from "zustand";
import { authStore } from "../store/auth.store";
import type { UserDetails } from "../types/authStore.types";
import type { Dto_LoginRequest, Dto_RegisterRequest } from "../types/auth.types";
import { 
  login as loginAPI, 
  register as registerAPI, 
  logout as logoutAPI,
  refreshToken as refreshTokenAPI,
  validateToken as validateTokenAPI
} from "../utils/auth.api";
import { 
  extractUserDetails, 
  isTokenExpiredInMinutes 
} from "../utils/jwt.utils";

interface AuthState {
  isAuthenticated: boolean;
  isLoading: boolean;
  user: UserDetails | null;
  error: string | null;
}

interface AuthActions {
  login: (loginData: Dto_LoginRequest) => Promise<void>;
  register: (registerData: Dto_RegisterRequest) => Promise<void>;
  logout: () => Promise<void>;
  initializeAuth: () => Promise<boolean>;
  validateAuth: () => Promise<boolean>;
  clearError: () => void;
  hasRole: (roleName: string) => boolean;
}

type AuthStore = AuthState & AuthActions;

export const useAuth = create<AuthStore>((set, get) => ({
  // State
  isAuthenticated: false,
  isLoading: false,
  user: null,
  error: null,

  // Actions
  
  /**
   * Login with email and password
   */
  login: async (loginData: Dto_LoginRequest) => {
    set({ isLoading: true, error: null });

    try {
      const response = await loginAPI(loginData);

      if (!response.success || !response.data) {
        throw new Error(response.message || 'Login failed');
      }

      // Store token in memory
      const token = response.data.token;
      authStore.setAccessToken(token);

      // Extract and store user details from JWT
      const userDetails = extractUserDetails(token);
      authStore.setUser(userDetails);

      // Update reactive state
      set({ 
        isAuthenticated: true, 
        user: userDetails, 
        isLoading: false,
        error: null
      });

      console.log('[Auth] Login successful for user:', userDetails.email);
    } catch (error) {
      console.error('[Auth] Login failed:', error);
      authStore.clearAll();
      set({ 
        isAuthenticated: false, 
        user: null, 
        isLoading: false,
        error: error instanceof Error ? error.message : 'Login failed'
      });
      throw error;
    }
  },

  /**
   * Register a new user account
   */
  register: async (registerData: Dto_RegisterRequest) => {
    set({ isLoading: true, error: null });

    try {
      const response = await registerAPI(registerData);

      if (!response.success || !response.data) {
        throw new Error(response.message || 'Registration failed');
      }

      set({ 
        isLoading: false,
        error: null
      });

      console.log('[Auth] Registration successful:', response.data.email);
      
      // Note: User must login after registration (auto-login could be added)
    } catch (error) {
      console.error('[Auth] Registration failed:', error);
      set({ 
        isLoading: false,
        error: error instanceof Error ? error.message : 'Registration failed'
      });
      throw error;
    }
  },

  /**
   * Logout user
   */
  logout: async () => {
    set({ isLoading: true });

    try {
      // Call logout API (clears refresh token cookie)
      await logoutAPI();
    } catch (error) {
      console.error('[Auth] Logout API failed:', error);
      // Continue with local cleanup anyway
    }

    // Clear in-memory auth state
    authStore.clearAll();

    // Update reactive state
    set({ 
      isAuthenticated: false, 
      user: null, 
      isLoading: false,
      error: null
    });

    console.log('[Auth] Logout successful');
  },

  /**
   * Initialize authentication on app startup
   * Checks for existing token and refreshes if needed
   */
  initializeAuth: async (): Promise<boolean> => {
    set({ isLoading: true, error: null });

    try {
      let currentToken = authStore.getAccessToken();
      let tokenExists = !!currentToken;
      
      // Check if token exists and is expired or expiring soon (15 min threshold)
      let requiresRefresh = !tokenExists || isTokenExpiredInMinutes(currentToken!, 15);

      if (requiresRefresh) {
        console.log('[Auth] Token missing or expiring soon, attempting refresh...');
        
        // Attempt token refresh
        const refreshResponse = await refreshTokenAPI();
        
        if (refreshResponse.success && refreshResponse.data?.token) {
          const newToken = refreshResponse.data.token;
          authStore.setAccessToken(newToken);
          
          // Extract user details from new token
          const userDetails = extractUserDetails(newToken);
          authStore.setUser(userDetails);
          
          // Update reactive state
          set({ 
            isAuthenticated: true, 
            user: userDetails, 
            isLoading: false 
          });
          
          console.log('[Auth] Token refreshed successfully for user:', userDetails.email);
          return true;
        } else {
          // Refresh failed - clear auth state
          console.warn('[Auth] Refresh failed:', refreshResponse.message);
          authStore.clearAll();
          set({ 
            isAuthenticated: false, 
            user: null, 
            isLoading: false 
          });
          return false;
        }
      }

      // Token still valid, validate with backend (optional but recommended)
      const isValid = await get().validateAuth();
      
      if (isValid) {
        const userDetails = extractUserDetails(currentToken!);
        authStore.setUser(userDetails);
        
        set({ 
          isAuthenticated: true, 
          user: userDetails, 
          isLoading: false 
        });
        
        console.log('[Auth] Token validated successfully');
        return true;
      }

      // Validation failed
      authStore.clearAll();
      set({ 
        isAuthenticated: false, 
        user: null, 
        isLoading: false 
      });
      return false;

    } catch (error) {
      console.error('[Auth] Initialization failed:', error);
      authStore.clearAll();
      set({ 
        isAuthenticated: false, 
        user: null, 
        isLoading: false,
        error: error instanceof Error ? error.message : 'Authentication initialization failed'
      });
      return false;
    }
  },

  /**
   * Validate current access token with backend
   */
  validateAuth: async (): Promise<boolean> => {
    const currentToken = authStore.getAccessToken();
    
    if (!currentToken) {
      return false;
    }

    try {
      const response = await validateTokenAPI(currentToken);
      return response.success && response.data?.isValid === true;
    } catch (error) {
      console.error('[Auth] Validation failed:', error);
      return false;
    }
  },

  /**
   * Clear error message
   */
  clearError: () => {
    set({ error: null });
  },

  /**
   * Check if user has a specific role
   */
  hasRole: (roleName: string): boolean => {
    const state = get();
    if (!state.user || !state.user.roles) return false;
    return state.user.roles.includes(roleName);
  },
}));
