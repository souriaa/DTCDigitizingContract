import type { CatalogGroup, CatalogKind, CatalogListFilter } from "@/services/catalog/workspace-api";

export const catalogKey = (kind: CatalogKind, group: number | null | "search") => `${kind}:${group ?? "unassigned"}`;
export const catalogQueryKey = (scope: string, kind: CatalogKind, filter: CatalogListFilter) => JSON.stringify([scope, kind, filter.page, filter.pageSize, filter.groupId ?? null, !!filter.unassignedOnly, filter.status ?? null, filter.fromDate ?? "", filter.toDate ?? "", filter.keyword ?? ""]);
export const catalogRootGroups = (groups: CatalogGroup[], kind: CatalogKind) =>
  groups.filter((group) => kind === "services" || group.parentId == null);

export function catalogTreePageForGroup(groups: CatalogGroup[], kind: CatalogKind, id: number, pageSize: number): number | undefined {
  if (!groups.some((group) => group.id === id)) return undefined;
  const rootKey = ancestorKeys(groups, id, kind).at(-1);
  const index = catalogRootGroups(groups, kind).findIndex((group) => catalogKey(kind, group.id) === rootKey);
  return index < 0 ? undefined : Math.floor(index / pageSize) + 1;
}

export function ancestorKeys(groups: CatalogGroup[], id: number, kind: CatalogKind): string[] {
  const map = new Map(groups.map((group) => [group.id, group]));
  const seen = new Set<number>();
  const keys: string[] = [];
  let current: number | null = id;
  while (current != null) {
    if (seen.has(current) || !map.has(current)) throw new Error("Cây danh mục không hợp lệ.");
    seen.add(current); keys.push(catalogKey(kind, current)); current = map.get(current)!.parentId;
  }
  return keys;
}

/** Per-workspace concurrency limit; queued requests can be cancelled before they reach the API. */
export function createCatalogQueue(limit = 3) {
  let running = 0;
  const queue: Array<() => void> = [];
  const drain = () => { while (running < limit && queue.length) queue.shift()!(); };
  return {
    run<T>(work: () => Promise<T>, signal: AbortSignal): Promise<T> {
      return new Promise<T>((resolve, reject) => {
        let started = false;
        const abort = () => {
          if (started) return;
          const index = queue.indexOf(start);
          if (index >= 0) queue.splice(index, 1);
          reject(new DOMException("Aborted", "AbortError"));
        };
        const start = () => {
          signal.removeEventListener("abort", abort);
          if (signal.aborted) { reject(new DOMException("Aborted", "AbortError")); return; }
          started = true; running++;
          void Promise.resolve().then(work).then(resolve, reject).finally(() => { running--; drain(); });
        };
        if (signal.aborted) { abort(); return; }
        signal.addEventListener("abort", abort, { once: true });
        queue.push(start); drain();
      });
    },
  };
}
