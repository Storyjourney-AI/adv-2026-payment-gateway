import type { 
  Dto_ApplicationRequest, 
  Dto_ApplicationResponse, 
  Dto_ApplicationListItem,
  PaginationRequest,
  PaginationWrapper
} from "../types/application.types";
import { authenticatedFetch, type DataWrapper } from "@services/auth/utils/api.helper";

export async function getApplications(
  params: PaginationRequest
): Promise<DataWrapper<PaginationWrapper<Dto_ApplicationListItem>>> {
  const queryParams = new URLSearchParams({
    page: params.page.toString(),
    pageSize: params.pageSize.toString(),
    ...(params.search && { search: params.search })
  });

  return authenticatedFetch<PaginationWrapper<Dto_ApplicationListItem>>(
    `/api/application?${queryParams}`
  );
}

export async function getApplicationById(
  id: string
): Promise<DataWrapper<Dto_ApplicationResponse>> {
  return authenticatedFetch<Dto_ApplicationResponse>(
    `/api/application/${id}`
  );
}

export async function createApplication(
  data: Dto_ApplicationRequest
): Promise<DataWrapper<Dto_ApplicationResponse>> {
  return authenticatedFetch<Dto_ApplicationResponse>(
    `/api/application`,
    {
      method: 'POST',
      body: JSON.stringify(data)
    }
  );
}

export async function updateApplication(
  id: string,
  data: Dto_ApplicationRequest
): Promise<DataWrapper<Dto_ApplicationResponse>> {
  return authenticatedFetch<Dto_ApplicationResponse>(
    `/api/application/${id}`,
    {
      method: 'PUT',
      body: JSON.stringify(data)
    }
  );
}

export async function deleteApplication(
  id: string
): Promise<DataWrapper<boolean>> {
  return authenticatedFetch<boolean>(
    `/api/application/${id}`,
    {
      method: 'DELETE'
    }
  );
}
