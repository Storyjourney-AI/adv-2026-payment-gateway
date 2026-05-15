import type {
  Dto_ActivityLogItem,
  ActivityLogFilterParams,
} from "../types/activityLog.types";
import type { PaginationWrapper } from "@services/application";
import { authenticatedFetch, type DataWrapper } from "@services/auth/utils/api.helper";

export async function getActivityLogs(
  params: ActivityLogFilterParams
): Promise<DataWrapper<PaginationWrapper<Dto_ActivityLogItem>>> {
  const queryParams = new URLSearchParams({
    page: params.page.toString(),
    pageSize: params.pageSize.toString(),
    ...(params.search && { search: params.search }),
    ...(params.category && params.category !== "all" && { category: params.category }),
  });

  return authenticatedFetch<PaginationWrapper<Dto_ActivityLogItem>>(
    `/api/activity-log?${queryParams}`
  );
}
