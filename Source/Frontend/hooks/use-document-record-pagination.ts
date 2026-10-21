"use client";

import { useEffect, useMemo, useState } from "react";

export function useDocumentRecordPagination<T>(records: readonly T[], scopeKey: number) {
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(3);
  const totalPages = Math.max(1, Math.ceil(records.length / pageSize));
  const currentPage = Math.min(page, totalPages);

  useEffect(() => {
    setPage(1);
  }, [scopeKey]);

  useEffect(() => {
    if (page > totalPages) setPage(totalPages);
  }, [page, totalPages]);

  const paginatedRecords = useMemo(() => {
    const start = (currentPage - 1) * pageSize;
    return records.slice(start, start + pageSize);
  }, [records, currentPage, pageSize]);

  const changePageSize = (size: number) => {
    setPageSize(size);
    setPage(1);
  };

  return {
    page: currentPage,
    pageSize,
    totalPages,
    paginatedRecords,
    setPage,
    changePageSize,
  };
}
