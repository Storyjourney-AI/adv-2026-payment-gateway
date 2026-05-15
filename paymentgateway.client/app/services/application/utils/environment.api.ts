import type { 
  Dto_EnvironmentRequest, 
  Dto_EnvironmentResponse,
  Dto_SnapTokenResponse
} from "../types/application.types";
import { authenticatedFetch, type DataWrapper } from "@services/auth/utils/api.helper";

export async function getEnvironmentsByApplication(
  applicationId: string
): Promise<DataWrapper<Dto_EnvironmentResponse[]>> {
  return authenticatedFetch<Dto_EnvironmentResponse[]>(
    `/api/environment/by-application/${applicationId}`
  );
}

export async function getEnvironmentById(
  id: string
): Promise<DataWrapper<Dto_EnvironmentResponse>> {
  return authenticatedFetch<Dto_EnvironmentResponse>(
    `/api/environment/${id}`
  );
}

export async function createEnvironment(
  data: Dto_EnvironmentRequest & { applicationId: string }
): Promise<DataWrapper<Dto_EnvironmentResponse>> {
  const { applicationId, ...requestData } = data;
  
  return authenticatedFetch<Dto_EnvironmentResponse>(
    `/api/environment?applicationId=${applicationId}`,
    {
      method: 'POST',
      body: JSON.stringify(requestData)
    }
  );
}

export async function updateEnvironment(
  id: string,
  data: Dto_EnvironmentRequest
): Promise<DataWrapper<Dto_EnvironmentResponse>> {
  return authenticatedFetch<Dto_EnvironmentResponse>(
    `/api/environment/${id}`,
    {
      method: 'PUT',
      body: JSON.stringify(data)
    }
  );
}

export async function regenerateApiKey(
  id: string
): Promise<DataWrapper<Dto_EnvironmentResponse>> {
  return authenticatedFetch<Dto_EnvironmentResponse>(
    `/api/environment/${id}/regenerate-key`,
    {
      method: 'POST'
    }
  );
}

export async function deleteEnvironment(
  id: string
): Promise<DataWrapper<boolean>> {
  return authenticatedFetch<boolean>(
    `/api/environment/${id}`,
    {
      method: 'DELETE'
    }
  );
}

export async function testPurchase(
  environmentId: string
): Promise<DataWrapper<Dto_SnapTokenResponse>> {
  return authenticatedFetch<Dto_SnapTokenResponse>(
    `/api/snap/test/${environmentId}`,
    { method: 'POST' }
  );
}
