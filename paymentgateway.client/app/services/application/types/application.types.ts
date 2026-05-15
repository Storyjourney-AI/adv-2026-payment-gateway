// Application DTOs matching backend

export interface Dto_ApplicationRequest {
  name: string;
  description?: string;
}

export interface Dto_ApplicationResponse {
  id: string;
  name: string;
  description?: string;
  userId: string;
  createdAt: string;
  updatedAt: string;
  environments: Dto_EnvironmentResponse[];
}

export interface Dto_ApplicationListItem {
  id: string;
  name: string;
  description?: string;
  createdAt: string;
  environmentCount: number;
}

export interface Dto_EnvironmentRequest {
  name: string;
  allowedOrigins?: string;
  webhookUrl?: string;
  successResponseUrl: string;
  failureResponseUrl: string;
  isSandbox?: boolean;
}

export interface Dto_EnvironmentResponse {
  id: string;
  applicationId: string;
  name: string;
  apiKey: string;
  allowedOrigins: string;
  webhookUrl?: string;
  successResponseUrl: string;
  failureResponseUrl: string;
  isSandbox: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface Dto_SnapTokenResponse {
  token: string;
  redirectUrl: string;
}

// Pagination types (shared)
export interface PaginationRequest {
  page: number;
  pageSize: number;
  search?: string;
}

export interface PaginationWrapper<T> {
  pageNumber: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
  items: T[];
}

export interface DataWrapper<T> {
  success: boolean;
  code: number;
  message?: string;
  errors?: string[];
  data?: T;
}
