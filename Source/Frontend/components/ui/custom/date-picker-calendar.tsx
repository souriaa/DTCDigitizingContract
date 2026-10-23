"use client";

import { useEffect, useId, useRef, useState } from "react";
import {
  format,
  getDaysInMonth,
  isAfter,
  isBefore,
  isValid,
  parse,
  startOfDay,
  startOfMonth,
} from "date-fns";
import { Calendar } from "@/components/ui/calendar";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

interface DatePickerCalendarProps {
  date: Date | undefined;
  onChange: (date: Date | undefined) => void;
  onConfirm?: () => void;
  label?: string;
  disabled?: boolean;
  minDate?: Date;
  maxDate?: Date;
}

interface DateParts {
  day: string;
  month: string;
  year: string;
}

const DATE_FORMAT = "dd/MM/yyyy";
const DATE_FIELDS = [
  { key: "day", label: "Ngày", placeholder: "dd", length: 2 },
  { key: "month", label: "Tháng", placeholder: "MM", length: 2 },
  { key: "year", label: "Năm", placeholder: "yyyy", length: 4 },
] as const;

function toDateParts(date: Date): DateParts {
  return {
    day: format(date, "dd"),
    month: format(date, "MM"),
    year: format(date, "yyyy"),
  };
}

function clampDate(date: Date, minDate?: Date, maxDate?: Date) {
  const day = startOfDay(date);
  if (minDate && isBefore(day, startOfDay(minDate))) return startOfDay(minDate);
  if (maxDate && isAfter(day, startOfDay(maxDate))) return startOfDay(maxDate);
  return day;
}

function parseParts(parts: DateParts) {
  if (
    !/^\d{1,2}$/.test(parts.day) ||
    !/^\d{1,2}$/.test(parts.month) ||
    !/^\d{4}$/.test(parts.year)
  ) {
    return undefined;
  }
  const value = `${parts.day.padStart(2, "0")}/${parts.month.padStart(2, "0")}/${parts.year}`;
  const parsed = parse(value, DATE_FORMAT, new Date());
  return isValid(parsed) && format(parsed, DATE_FORMAT) === value
    ? parsed
    : undefined;
}

export function DatePickerCalendar({
  date,
  onChange,
  onConfirm,
  label = "Nhập ngày",
  disabled = false,
  minDate,
  maxDate,
}: DatePickerCalendarProps) {
  const inputId = useId();
  // These are calendar dates: ignore changing clock times in limits such as new Date().
  const selectedTime = date ? startOfDay(date).getTime() : undefined;
  const minTime = minDate ? startOfDay(minDate).getTime() : undefined;
  const maxTime = maxDate ? startOfDay(maxDate).getTime() : undefined;
  const editingField = useRef<keyof DateParts | null>(null);
  const [parts, setParts] = useState(() =>
    toDateParts(clampDate(date ?? new Date(), minDate, maxDate)),
  );
  const [touched, setTouched] = useState(false);
  const [month, setMonth] = useState(() =>
    clampDate(date ?? new Date(), minDate, maxDate),
  );

  useEffect(() => {
    // Parent updates must not reformat the draft while a segment is focused.
    if (editingField.current !== null) return;
    const displayDate = clampDate(
      selectedTime === undefined ? new Date() : new Date(selectedTime),
      minTime === undefined ? undefined : new Date(minTime),
      maxTime === undefined ? undefined : new Date(maxTime),
    );
    setParts(toDateParts(displayDate));
    setTouched(false);
    setMonth(displayDate);
  }, [selectedTime, minTime, maxTime]);

  const getError = (value: DateParts) => {
    const parsed = parseParts(value);
    if (!parsed) return "Nhập đầy đủ ngày, tháng, năm hợp lệ.";
    if (minDate && isBefore(parsed, startOfDay(minDate))) {
      return `Ngày không được trước ${format(minDate, DATE_FORMAT)}.`;
    }
    if (maxDate && isAfter(parsed, startOfDay(maxDate))) {
      return `Ngày không được sau ${format(maxDate, DATE_FORMAT)}.`;
    }
    return undefined;
  };

  const complete =
    parts.day.length > 0 && parts.month.length > 0 && parts.year.length === 4;
  const error = touched || complete ? getError(parts) : undefined;
  let visibleMonth = startOfMonth(month);
  if (minDate && isBefore(visibleMonth, startOfMonth(minDate))) {
    visibleMonth = startOfMonth(minDate);
  }
  if (maxDate && isAfter(visibleMonth, startOfMonth(maxDate))) {
    visibleMonth = startOfMonth(maxDate);
  }

  return (
    <div className="w-[248px] max-w-[calc(100vw-2rem)] min-w-0">
      <div className="space-y-2 border-b p-3">
        <p id={`${inputId}-label`} className="text-sm font-medium">
          {label}
        </p>
        <div
          role="group"
          aria-labelledby={`${inputId}-label`}
          className="grid grid-cols-[minmax(0,1fr)_minmax(0,1fr)_minmax(0,1.4fr)] gap-2"
        >
          {DATE_FIELDS.map((field) => (
            <div key={field.key} className="min-w-0 space-y-1">
              <Label
                htmlFor={`${inputId}-${field.key}`}
                className="text-xs text-muted-foreground"
              >
                {field.label}
              </Label>
              <Input
                id={`${inputId}-${field.key}`}
                type="text"
                inputMode="numeric"
                enterKeyHint="done"
                placeholder={field.placeholder}
                maxLength={field.length}
                size={field.length}
                showControls={false}
                autoComplete="off"
                disabled={disabled}
                value={parts[field.key]}
                className="h-8 text-center tabular-nums"
                aria-invalid={!!error}
                aria-describedby={error ? `${inputId}-error` : undefined}
                onFocus={(event) => {
                  editingField.current = field.key;
                  event.currentTarget.select();
                }}
                onChange={(event) => {
                  const value = event.target.value;
                  if (!/^\d*$/.test(value)) return;
                  const next = { ...parts, [field.key]: value };
                  setParts(next);
                  const parsed = parseParts(next);
                  if (parsed && !getError(next)) {
                    setMonth(parsed);
                    if (parsed.getTime() !== selectedTime) {
                      onChange(parsed);
                    }
                  }
                }}
                onBlur={() => {
                  editingField.current = null;
                  setTouched(true);
                  if (field.key !== "year") {
                    setParts((current) => ({
                      ...current,
                      [field.key]: current[field.key]
                        ? current[field.key].padStart(2, "0")
                        : "",
                    }));
                  }
                }}
                onKeyDown={(event) => {
                  if (event.key !== "Enter" || event.nativeEvent.isComposing) return;
                  event.preventDefault();
                  event.stopPropagation();
                  setTouched(true);
                  const parsed = parseParts(parts);
                  if (disabled || !parsed || getError(parts)) return;
                  setParts(toDateParts(parsed));
                  setMonth(parsed);
                  setTouched(false);
                  if (parsed.getTime() !== selectedTime) onChange(parsed);
                  onConfirm?.();
                }}
              />
            </div>
          ))}
        </div>
        {error && (
          <p
            id={`${inputId}-error`}
            role="status"
            className="max-w-56 text-xs text-destructive"
          >
            {error}
          </p>
        )}
      </div>
      <Calendar
        className="w-full"
        mode="single"
        selected={date}
        month={visibleMonth}
        onMonthChange={(nextMonth) => {
          // Browsing changes the suggested inputs, not the selected filter.
          const suggestedDate = new Date(nextMonth);
          suggestedDate.setDate(
            Math.min(
              Number(parts.day) || new Date().getDate(),
              getDaysInMonth(nextMonth),
            ),
          );
          const displayDate = clampDate(suggestedDate, minDate, maxDate);
          setMonth(displayDate);
          setParts(toDateParts(displayDate));
          setTouched(false);
        }}
        onSelect={(selectedDate) => {
          if (selectedDate) {
            setParts(toDateParts(selectedDate));
            setMonth(selectedDate);
          }
          setTouched(false);
          onChange(selectedDate);
        }}
        startMonth={minDate}
        endMonth={maxDate}
        disabled={
          disabled || [
            ...(minDate ? [{ before: startOfDay(minDate) }] : []),
            ...(maxDate ? [{ after: startOfDay(maxDate) }] : []),
          ]
        }
      />
    </div>
  );
}
