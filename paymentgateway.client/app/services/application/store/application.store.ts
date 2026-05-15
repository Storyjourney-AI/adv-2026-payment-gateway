import { create } from "zustand";
import type { Dto_ApplicationListItem, PaginationWrapper } from "../types/application.types";

interface ApplicationStore {
  cache: {
    data: PaginationWrapper<Dto_ApplicationListItem> | null;
    timestamp: number;
    ttl: number;
  };
  isCacheValid: () => boolean;
  setCache: (data: PaginationWrapper<Dto_ApplicationListItem>) => void;
  clearCache: () => void;
}

export const useApplicationStore = create<ApplicationStore>((set, get) => ({
  cache: {
    data: null,
    timestamp: 0,
    ttl: 5 * 60 * 1000 // 5 minutes
  },
  isCacheValid: () => {
    const cache = get().cache;
    return cache.data !== null && Date.now() - cache.timestamp < cache.ttl;
  },
  setCache: (data) => set((state) => ({
    cache: {
      ...state.cache,
      data,
      timestamp: Date.now()
    }
  })),
  clearCache: () => set((state) => ({
    cache: {
      ...state.cache,
      data: null,
      timestamp: 0
    }
  }))
}));
