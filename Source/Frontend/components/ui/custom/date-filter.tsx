"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@/components/ui/popover";
import { DatePickerCalendar } from "@/components/ui/custom/date-picker-calendar";
import { CalendarIcon, Delete } from "lucide-react";
import { format } from "date-fns";
import { cn } from "@/lib/utils";
import { useMediaQuery } from "@/hooks/use-media-query";

export interface DateFilterLimit {
  from?: Date;
  to?: Date;
}

interface DateFilterProps {
  date: Date | undefined;
  onChange: (date: Date | undefined) => void;
  id?: string;
  placeholder?: string;
  className?: string;
  disabled?: boolean;
  limit?: DateFilterLimit;
}

export function DateFilter({
  date,
  onChange,
  id,
  placeholder = "Ngày tạo",
  className = "w-[160px]",
  disabled = false,
  limit,
}: DateFilterProps) {
  const isMobile = useMediaQuery("(max-width: 767px)");
  const [open, setOpen] = useState(false);

  return (
    <div className={`flex items-center gap-1 ${isMobile ? "w-full" : ""}`}>
      <Popover open={open} onOpenChange={setOpen}>
        <PopoverTrigger asChild>
          <Button
            id={id}
            type="button"
            variant="outline"
            disabled={disabled}
            className={cn(
              "justify-start text-left font-normal bg-transparent",
              isMobile ? "flex-1 w-full" : className,
              !date && "text-muted-foreground",
            )}
          >
            <CalendarIcon className="mr-2 h-4 w-4" />
            <span className="truncate">
              {date ? format(date, "dd/MM/yyyy") : placeholder}
            </span>
          </Button>
        </PopoverTrigger>
        <PopoverContent className="w-auto p-0" align="start">
          <DatePickerCalendar
            date={date}
            onChange={onChange}
            onConfirm={() => setOpen(false)}
            disabled={disabled}
            minDate={limit?.from}
            maxDate={limit?.to}
          />
        </PopoverContent>
      </Popover>

      {date && (
        <Button
          type="button"
          disabled={disabled}
          variant="ghost"
          size="sm"
          className="h-9 w-9 p-0 text-muted-foreground hover:bg-destructive/10 hover:text-destructive shrink-0"
          onClick={() => onChange(undefined)}
          title="Xóa lọc ngày"
        >
          <Delete className="h-4 w-4" />
        </Button>
      )}
    </div>
  );
}
