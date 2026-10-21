"use client";

import { ChevronLeft, ChevronRight } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";

export function DocumentRecordPagination({
  totalCount,
  page,
  pageSize,
  totalPages,
  onPageChange,
  onPageSizeChange,
}: {
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  onPageChange: (page: number) => void;
  onPageSizeChange: (size: number) => void;
}) {
  if (totalCount === 0) return null;

  return (
    <div className="flex flex-col gap-2 border-t pt-3 sm:flex-row sm:items-center sm:justify-between">
      <p className="text-xs text-muted-foreground">
        {totalCount} biên bản · Trang {page}/{totalPages}
      </p>
      <div className="flex items-center gap-2">
        <Select value={String(pageSize)} onValueChange={(value) => onPageSizeChange(Number(value))}>
          <SelectTrigger aria-label="Số biên bản mỗi trang" className="h-8 w-[100px] text-xs">
            <SelectValue />
          </SelectTrigger>
          <SelectContent showSearch={false}>
            {[3, 6, 9, 12].map((size) => (
              <SelectItem key={size} value={String(size)}>{size} / trang</SelectItem>
            ))}
          </SelectContent>
        </Select>
        <div className="flex items-center gap-1">
          <Button
            type="button"
            variant="outline"
            size="sm"
            className="h-8 px-2.5 text-xs gap-1"
            disabled={page <= 1}
            onClick={() => onPageChange(Math.max(page - 1, 1))}
          >
            <ChevronLeft className="size-3.5" />
            <span>Trước</span>
          </Button>
          <Button
            type="button"
            variant="outline"
            size="sm"
            className="h-8 px-2.5 text-xs gap-1"
            disabled={page >= totalPages}
            onClick={() => onPageChange(Math.min(page + 1, totalPages))}
          >
            <span>Sau</span>
            <ChevronRight className="size-3.5" />
          </Button>
        </div>
      </div>
    </div>
  );
}
