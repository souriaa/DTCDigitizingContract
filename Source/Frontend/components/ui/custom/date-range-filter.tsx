"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@/components/ui/popover";
import { DatePickerCalendar } from "@/components/ui/custom/date-picker-calendar";
import { CalendarDays, Delete } from "lucide-react";
import { format } from "date-fns";
import { cn } from "@/lib/utils";
import { useMediaQuery } from "@/hooks/use-media-query";

export interface DateRange {
  from: Date | undefined;
  to: Date | undefined;
}

interface DateRangeFilterProps {
  dateRange: DateRange;
  onChange: (range: DateRange) => void;
  buttonClassName?: string;
}

export function DateRangeFilter({
  dateRange,
  onChange,
  buttonClassName = "bg-background",
}: DateRangeFilterProps) {
  const isMobile = useMediaQuery("(max-width: 767px)");
  const [fromOpen, setFromOpen] = useState(false);
  const [toOpen, setToOpen] = useState(false);

  return (
    <div className={`flex items-center gap-2 ${isMobile ? "flex-1" : ""}`}>
      <Popover open={fromOpen} onOpenChange={setFromOpen}>
        <PopoverTrigger asChild>
          <Button
            type="button"
            variant="outline"
            className={cn(
              "justify-start text-left font-normal flex-1 bg-transparent",
              buttonClassName,
              !dateRange.from && "text-muted-foreground",
            )}
          >
            <CalendarDays className="mr-2 h-4 w-4" />
            {dateRange.from ? (
              format(dateRange.from, "dd/MM/yyyy")
            ) : (
              <span>Từ ngày</span>
            )}
          </Button>
        </PopoverTrigger>
        <PopoverContent className="w-auto p-0" align="start">
          <DatePickerCalendar
            label="Từ ngày"
            date={dateRange.from}
            onChange={(date) => onChange({ ...dateRange, from: date })}
            onConfirm={() => setFromOpen(false)}
            maxDate={dateRange.to}
          />
        </PopoverContent>
      </Popover>

      <span className="text-muted-foreground">-</span>

      <Popover open={toOpen} onOpenChange={setToOpen}>
        <PopoverTrigger asChild>
          <Button
            type="button"
            variant="outline"
            className={cn(
              "justify-start text-left font-normal flex-1",
              buttonClassName,
              !dateRange.to && "text-muted-foreground",
            )}
          >
            <CalendarDays className="mr-2 h-4 w-4" />
            {dateRange.to ? (
              format(dateRange.to, "dd/MM/yyyy")
            ) : (
              <span>Đến ngày</span>
            )}
          </Button>
        </PopoverTrigger>
        <PopoverContent className="w-auto p-0" align="start">
          <DatePickerCalendar
            label="Đến ngày"
            date={dateRange.to}
            onChange={(date) => onChange({ ...dateRange, to: date })}
            onConfirm={() => setToOpen(false)}
            minDate={dateRange.from}
          />
        </PopoverContent>
      </Popover>

      {(dateRange.from || dateRange.to) && (
        <Button
          type="button"
          variant="ghost"
          size="sm"
          className="h-8 text-xs text-muted-foreground px-2 hover:bg-destructive/10 hover:text-destructive"
          onClick={() => onChange({ from: undefined, to: undefined })}
        >
          <Delete className="h-4 w-4" />
        </Button>
      )}
    </div>
  );
}
