import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import type { DataState } from "./types";
import { seedData } from "./seed";

const STORAGE_KEY = "fairway-hq:data:v1";

type Updater<K extends keyof DataState> = (
  items: DataState[K],
) => DataState[K];

interface StoreApi {
  data: DataState;
  update: <K extends keyof DataState>(key: K, updater: Updater<K>) => void;
  reset: () => void;
}

const StoreContext = createContext<StoreApi | null>(null);

const load = (): DataState => {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return seedData;
    const parsed = JSON.parse(raw) as Partial<DataState>;
    return { ...seedData, ...parsed };
  } catch {
    return seedData;
  }
};

export function StoreProvider({ children }: { children: ReactNode }) {
  const [data, setData] = useState<DataState>(load);

  useEffect(() => {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(data));
  }, [data]);

  const update = useCallback(
    <K extends keyof DataState>(key: K, updater: Updater<K>) => {
      setData((prev) => ({ ...prev, [key]: updater(prev[key]) }));
    },
    [],
  );

  const reset = useCallback(() => {
    localStorage.removeItem(STORAGE_KEY);
    setData(seedData);
  }, []);

  const value = useMemo(() => ({ data, update, reset }), [data, update, reset]);

  return (
    <StoreContext.Provider value={value}>{children}</StoreContext.Provider>
  );
}

export function useStore(): StoreApi {
  const ctx = useContext(StoreContext);
  if (!ctx) throw new Error("useStore must be used inside StoreProvider");
  return ctx;
}

export function uid(prefix = "id"): string {
  return `${prefix}_${Math.random().toString(36).slice(2, 9)}${Date.now().toString(36).slice(-3)}`;
}
