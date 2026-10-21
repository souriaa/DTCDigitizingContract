"use client";
import { Checkbox } from "@/components/ui/checkbox";
import { DecimalInput } from "@/components/ui/custom/decimal-input";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";

export interface DocumentQuantityRow {
  id: number;
  name: string;
  description?: string;
  unit: string;
  available: number;
  quantity?: number;
  wholePackage?: boolean;
}
export function DocumentQuantitySelector({
  rows,
  onChange,
  disabled = false,
}: {
  rows: DocumentQuantityRow[];
  onChange: (id: number, quantity: number | null) => void;
  disabled?: boolean;
}) {
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>Chọn</TableHead>
          <TableHead>Hạng mục / nguồn</TableHead>
          <TableHead>Còn khả dụng</TableHead>
          <TableHead>
            Số lượng lần này <span className="text-red-500">*</span>
          </TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {rows.map((row) => (
          <TableRow key={row.id}>
            <TableCell>
              <Checkbox
                aria-label={`Chọn ${row.name}`}
                checked={row.quantity !== undefined}
                disabled={
                  disabled || (row.available <= 0 && row.quantity === undefined)
                }
                onCheckedChange={(checked) =>
                  onChange(
                    row.id,
                    checked === true
                      ? row.wholePackage
                        ? 1
                        : row.available
                      : null,
                  )
                }
              />
            </TableCell>
            <TableCell>
              <p className="font-medium">{row.name}</p>
              <p className="text-xs text-muted-foreground">{row.description}</p>
              {row.wholePackage && (
                <p className="text-xs text-muted-foreground">
                  Nguyên gói, không chia nhỏ
                </p>
              )}
            </TableCell>
            <TableCell>
              {row.available} {row.unit}
            </TableCell>
            <TableCell>
              {row.quantity !== undefined ? (
                <DecimalInput
                  aria-label={`Số lượng ${row.name}`}
                  value={row.quantity}
                  min={0}
                  max={row.available}
                  disabled={disabled || row.wholePackage}
                  onValueChange={(value) => onChange(row.id, value)}
                />
              ) : (
                "—"
              )}
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}
