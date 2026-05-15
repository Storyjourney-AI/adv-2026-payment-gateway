import { useState } from "react";
import { useApplicationStore } from "../store/application.store";
import type { 
  Dto_ApplicationRequest, 
  Dto_ApplicationResponse,
  Dto_ApplicationListItem,
  PaginationRequest,
  PaginationWrapper 
} from "../types/application.types";
import * as applicationApi from "../utils/application.api";

export function useApplications() {
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const store = useApplicationStore();

  const fetchApplications = async (params: PaginationRequest) => {
    setIsLoading(true);
    setError(null);
    
    try {
      const result = await applicationApi.getApplications(params);
      
      if (result.success && result.data) {
        store.setCache(result.data);
      } else {
        setError(result.message || "Failed to fetch applications");
      }
      
      setIsLoading(false);
      return result;
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : "An error occurred";
      setError(errorMessage);
      setIsLoading(false);
      throw err;
    }
  };

  const getApplicationById = async (id: string) => {
    setIsLoading(true);
    setError(null);
    
    try {
      const result = await applicationApi.getApplicationById(id);
      
      if (!result.success) {
        setError(result.message || "Failed to fetch application");
      }
      
      setIsLoading(false);
      return result;
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : "An error occurred";
      setError(errorMessage);
      setIsLoading(false);
      throw err;
    }
  };

  const createApplication = async (data: Dto_ApplicationRequest) => {
    setIsLoading(true);
    setError(null);
    
    try {
      const result = await applicationApi.createApplication(data);
      
      if (result.success) {
        store.clearCache(); // Invalidate cache
      } else {
        setError(result.message || "Failed to create application");
      }
      
      setIsLoading(false);
      return result;
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : "An error occurred";
      setError(errorMessage);
      setIsLoading(false);
      throw err;
    }
  };

  const updateApplication = async (id: string, data: Dto_ApplicationRequest) => {
    setIsLoading(true);
    setError(null);
    
    try {
      const result = await applicationApi.updateApplication(id, data);
      
      if (result.success) {
        store.clearCache(); // Invalidate cache
      } else {
        setError(result.message || "Failed to update application");
      }
      
      setIsLoading(false);
      return result;
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : "An error occurred";
      setError(errorMessage);
      setIsLoading(false);
      throw err;
    }
  };

  const deleteApplication = async (id: string) => {
    setIsLoading(true);
    setError(null);
    
    try {
      const result = await applicationApi.deleteApplication(id);
      
      if (result.success) {
        store.clearCache(); // Invalidate cache
      } else {
        setError(result.message || "Failed to delete application");
      }
      
      setIsLoading(false);
      return result;
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : "An error occurred";
      setError(errorMessage);
      setIsLoading(false);
      throw err;
    }
  };

  return {
    applications: store.cache.data,
    isLoading,
    error,
    fetchApplications,
    getApplicationById,
    createApplication,
    updateApplication,
    deleteApplication,
    clearCache: store.clearCache
  };
}
