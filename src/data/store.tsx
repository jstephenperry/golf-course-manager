import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import type { Course, DataState, PlayerTab } from "./types";
import { emptyData, sampleData } from "./seed";
import { DEFAULT_CLOSE, DEFAULT_OPEN } from "./utils";

const STORAGE_KEY = "fairway-hq:data:v1";

type Updater<K extends keyof DataState> = (
  items: DataState[K],
) => DataState[K];

interface StoreApi {
  data: DataState;
  update: <K extends keyof DataState>(key: K, updater: Updater<K>) => void;
  reset: () => void;
  loadSampleData: () => void;
}

const StoreContext = createContext<StoreApi | null>(null);

const normalizeCourse = (c: Course): Course => ({
  ...c,
  openTime: c.openTime || DEFAULT_OPEN,
  closeTime: c.closeTime || DEFAULT_CLOSE,
});

const normalizeTab = (t: PlayerTab): PlayerTab => ({
  ...t,
  items: t.items.filter((li) => Boolean(li.productId)),
});

const load = (): DataState => {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return emptyData;
    const parsed = JSON.parse(raw) as Partial<DataState>;
    const merged = { ...emptyData, ...parsed };
    return {
      ...merged,
      courses: merged.courses.map(normalizeCourse),
      tabs: merged.tabs.map(normalizeTab),
    };
  } catch {
    return emptyData;
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
    setData(emptyData);
  }, []);

  const loadSampleData = useCallback(() => {
    setData(sampleData);
  }, []);

  const value = useMemo(
    () => ({ data, update, reset, loadSampleData }),
    [data, update, reset, loadSampleData],
  );

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
