/**
 * Type definitions for authentication store
 */

export interface UserDetails {
  userId: string;
  email: string;
  roles: string[];
}

export interface AuthStoreState {
  accessToken: string | null;
  user: UserDetails | null;
}
