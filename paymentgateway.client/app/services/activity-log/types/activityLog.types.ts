// Activity Log DTOs matching backend

export interface Dto_ActivityLogItem {
  id: string;
  userId: string | null;
  userEmail: string | null;
  sessionToken: string | null;
  category: string;
  action: string;
  timestamp: string;
}

export interface ActivityLogFilterParams {
  page: number;
  pageSize: number;
  search?: string;
  category?: string;
}
