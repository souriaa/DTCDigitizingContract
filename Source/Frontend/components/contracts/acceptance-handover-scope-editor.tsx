"use client";
import { useEffect, useState } from "react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { DocumentQuantitySelector } from "@/components/contract-documents/document-quantity-selector";
import {
  contractAcceptanceApi,
  type AcceptanceHandoverScope,
} from "@/services/contract-acceptance-api";
import { getApiErrorMessage } from "@/lib/api-error";
import { formatDate } from "@/lib/format-date";
import { RotateCcw } from "lucide-react";

export function AcceptanceHandoverScopeEditor({
  contractId,
  recordId,
  rowVersion,
  revision,
  selected,
  onChange,
  disabled,
  blocker,
}: {
  contractId: number;
  recordId: number;
  rowVersion: string;
  revision: number;
  selected: AcceptanceHandoverScope[];
  onChange: (value: AcceptanceHandoverScope[]) => void;
  disabled: boolean;
  blocker?: string | null;
}) {
  const [options, setOptions] = useState<AcceptanceHandoverScope[]>([]);
  const [message, setMessage] = useState("");
  const [loading, setLoading] = useState(true);
  const [reload, setReload] = useState(0);
  useEffect(() => {
    let current = true;
    setLoading(true);
    void contractAcceptanceApi
      .handoverOptions(contractId, recordId)
      .then((result) => {
        if (current) {
          setOptions(result.items);
          setMessage(result.blocker ?? "");
        }
      })
      .catch((e) => {
        if (current)
          setMessage(getApiErrorMessage(e, "Không thể tải phạm vi bàn giao."));
      })
      .finally(() => {
        if (current) setLoading(false);
      });
    return () => {
      current = false;
    };
  }, [contractId, recordId, rowVersion, revision, reload]);
  return (
    <Card>
      <CardHeader>
        <CardTitle>Căn cứ bàn giao và phạm vi nghiệm thu</CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        <p className="text-sm text-muted-foreground">
          Chỉ tính bàn giao đã ký cùng phiên bản. Chọn lượng nghiệm thu từng
          dòng; đợt thanh toán được chọn riêng.
        </p>
        {(blocker || message) && (
          <Alert>
            <AlertDescription>
              {blocker || message}{" "}
              <a href="#contract-handover" className="underline">
                Mở bàn giao
              </a>
            </AlertDescription>
          </Alert>
        )}
        <Button
          variant="outline"
          disabled={loading}
          onClick={() => setReload((v) => v + 1)}
        >
          <RotateCcw className="mr-2" />
          Tải lại phạm vi
        </Button>
        {loading ? (
          <p>Đang tải phạm vi…</p>
        ) : (
          <DocumentQuantitySelector
            disabled={disabled}
            rows={options.map((x) => ({
              id: x.handoverLineId,
              name: x.nameVi,
              unit: x.unitVi,
              available: x.availableQuantity,
              description: `${x.handoverCode} · ký ngày ${formatDate(x.signDate)} · ${x.itemCode}`,
              wholePackage: x.scopeMode === 2,
              quantity: selected.find(
                (s) => s.handoverLineId === x.handoverLineId,
              )?.quantity,
            }))}
            onChange={(id, qty) => {
              const source = options.find((x) => x.handoverLineId === id);
              if (!source) return;
              onChange(
                qty === null
                  ? selected.filter((x) => x.handoverLineId !== id)
                  : selected.some((x) => x.handoverLineId === id)
                    ? selected.map((x) =>
                        x.handoverLineId === id ? { ...x, quantity: qty } : x,
                      )
                    : [...selected, { ...source, quantity: qty }],
              );
            }}
          />
        )}
        {selected.some(
          (x) => !options.some((o) => o.handoverLineId === x.handoverLineId),
        ) && (
          <Alert variant="destructive">
            <AlertDescription>
              Có nguồn không còn hợp lệ.{" "}
              <Button
                variant="link"
                disabled={disabled}
                onClick={() =>
                  onChange(
                    selected.filter((x) =>
                      options.some(
                        (o) => o.handoverLineId === x.handoverLineId,
                      ),
                    ),
                  )
                }
              >
                Rút dòng không hợp lệ
              </Button>
            </AlertDescription>
          </Alert>
        )}
      </CardContent>
    </Card>
  );
}
