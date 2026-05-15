// DTOs matching backend AuthController

export interface Dto_RegisterRequest {
  email: string;
  password: string;
  confirmPassword: string;
  agreement: boolean;
}

export interface Dto_Register {
  email: string;
  registeredOn: string;  // Backend: DateTime
  isInitialUser?: boolean;  // Backend: bool? (nullable, only set when true)
}

export interface Dto_LoginRequest {
  email: string;
  password: string;
}

export interface Dto_Login {
  email: string;
  token: string;  // Backend: Token (not accessToken!)
  isNewUser: boolean;
  // Note: refreshToken is in HttpOnly cookie, not in response
  // Note: expiresAt not returned, decode from JWT
  // Note: roles not returned, decode from JWT
}

export interface Dto_RefreshTokenResponse {
  token: string;  // Backend: Token (not accessToken!)
  // Note: refreshToken is in HttpOnly cookie, not in response
  // Note: expiresAt not returned, decode from JWT
}

export interface Dto_TokenValidation {
  userId: string;
  email: string;
  roles: string[];
  expiresAt: string;
  issuedAt: string | null;
  isValid: boolean;
}

