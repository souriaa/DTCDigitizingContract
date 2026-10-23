"use client";

import { useEffect, useRef, useState, type FormEvent } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { ArrowLeft, ChevronLeft, ChevronRight, Loader2, Plus, Save, Trash2 } from "lucide-react";
import { PermissionGuard } from "@/components/auth/permission-guard";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { DecimalInput } from "@/components/ui/custom/decimal-input";
import { Header } from "@/components/ui/custom/header";
import { IntegerInput } from "@/components/ui/custom/integer-input";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { toast } from "@/components/ui/sonner";
import { getApiErrorMessage } from "@/lib/api-error";
import { formatCurrency } from "@/lib/format-currency";
import { RBAC_PERMISSIONS } from "@/lib/rbac";
import { catalogPickerApi, type CatalogPickerItem } from "@/services/catalog/picker-api";
import { ContractItemType } from "@/services/contract-api";
import { customerApi, CustomerStatus, type CustomerLookupResponse } from "@/services/customers-api";
import { quotationApi } from "@/services/quotations-api";

type Line = { productId: number; productName: string; quantity: number; unitPrice: number };

function CreateQuotationContent() {
  const router = useRouter();
  const saveLock = useRef(false);
  const [customers, setCustomers] = useState<CustomerLookupResponse[]>([]);
  const [customerId, setCustomerId] = useState("");
  const [customersLoading, setCustomersLoading] = useState(true);
  const [customerError, setCustomerError] = useState<string | null>(null);
  const [products, setProducts] = useState<CatalogPickerItem[]>([]);
  const [query, setQuery] = useState("");
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [productsLoading, setProductsLoading] = useState(true);
  const [productError, setProductError] = useState<string | null>(null);
  const [reload, setReload] = useState(0);
  const [lines, setLines] = useState<Line[]>([]);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setCustomersLoading(true);
    setCustomerError(null);
    void customerApi.lookup().then((items) => {
      if (!cancelled) setCustomers(items.filter((item) => item.status === CustomerStatus.Active));
    }).catch((cause) => {
      if (!cancelled) setCustomerError(getApiErrorMessage(cause, "Không thể tải khách hàng."));
    }).finally(() => {
      if (!cancelled) setCustomersLoading(false);
    });
    return () => { cancelled = true; };
  }, [reload]);

  useEffect(() => {
    const controller = new AbortController();
    setProductsLoading(true);
    setProductError(null);
    const timer = setTimeout(async () => {
      try {
        const result = await catalogPickerApi.search({
          page, pageSize: 10, keyword: query.trim(), itemType: ContractItemType.Product,
        }, controller.signal);
        if (controller.signal.aborted) return;
        const lastPage = Math.max(1, result.totalPages);
        setTotalPages(lastPage);
        if (page > lastPage) { setPage(lastPage); return; }
        setProducts(result.items);
      } catch (cause) {
        if (!controller.signal.aborted) {
          setProducts([]);
          setProductError(getApiErrorMessage(cause, "Không thể tải sản phẩm."));
        }
      } finally {
        if (!controller.signal.aborted) setProductsLoading(false);
      }
    }, 300);
    return () => { clearTimeout(timer); controller.abort(); };
  }, [page, query, reload]);

  const total = lines.reduce((sum, line) => sum + line.quantity * line.unitPrice, 0);
  const updateLine = (productId: number, patch: Partial<Line>) => {
    setLines((current) => current.map((line) => line.productId === productId ? { ...line, ...patch } : line));
  };
  const addProduct = (product: CatalogPickerItem) => {
    setLines((current) => current.some((line) => line.productId === product.sourceId) ? current : [
      ...current,
      { productId: product.sourceId, productName: product.name || "Sản phẩm chưa có tên", quantity: 1, unitPrice: product.price ?? 0 },
    ]);
  };
  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (saveLock.current) return;
    if (!customers.some((item) => item.customerId === Number(customerId))) {
      setError("Vui lòng chọn khách hàng đang hoạt động."); return;
    }
    if (!lines.length || lines.some((line) =>
      !Number.isInteger(line.quantity) || line.quantity < 1 || line.quantity > 2_147_483_647 ||
      !Number.isFinite(line.unitPrice) || line.unitPrice < 0 || line.unitPrice !== Number(line.unitPrice.toFixed(2)))) {
      setError("Thêm ít nhất một sản phẩm. Số lượng phải là số nguyên dương; đơn giá không âm và có tối đa 2 chữ số thập phân."); return;
    }
    if (!Number.isFinite(total) || total >= 1e16) {
      setError("Tổng giá trị vượt giới hạn lưu trữ. Vui lòng giảm số lượng hoặc đơn giá."); return;
    }
    saveLock.current = true;
    setSaving(true);
    setError(null);
    try {
      const result = await quotationApi.create({
        customerId: Number(customerId),
        quotationItems: lines.map(({ productId, quantity, unitPrice }) => ({ productId, quantity, unitPrice })),
      });
      toast.success("Đã tạo báo giá.");
      router.replace(`/quotations/${result.quotationId}`);
    } catch (cause) {
      setError(getApiErrorMessage(cause, "Không thể tạo báo giá."));
      saveLock.current = false;
      setSaving(false);
    }
  };

  return <>
    <Header />
    <main className="grow overflow-y-auto p-2 lg:p-10 space-y-6">
      <Button asChild variant="ghost" size="sm"><Link href="/quotations"><ArrowLeft className="size-4" /> Quay lại báo giá</Link></Button>
      <div><h1 className="text-2xl font-bold">Tạo báo giá mới</h1><p className="mt-1 text-sm text-muted-foreground">Mã báo giá, ngày tạo và trạng thái bản nháp được hệ thống tự sinh.</p></div>
      <form onSubmit={submit} className="space-y-6">
        <fieldset disabled={saving} className="space-y-6 disabled:opacity-70">
          <Card>
            <CardHeader><CardTitle>Khách hàng</CardTitle></CardHeader>
            <CardContent className="space-y-2">
              <Label htmlFor="quotation-customer">Khách hàng <span className="text-red-500">*</span></Label>
              <Select value={customerId} onValueChange={setCustomerId} disabled={customersLoading}>
                <SelectTrigger id="quotation-customer" className="w-full"><SelectValue placeholder={customersLoading ? "Đang tải khách hàng..." : "Chọn khách hàng"} /></SelectTrigger>
                <SelectContent>{customers.map((customer) => <SelectItem key={customer.customerId} value={String(customer.customerId)}>{customer.customerCompany || customer.customerFullName || "Khách hàng chưa có tên"}{customer.customerCode ? ` · ${customer.customerCode}` : ""}</SelectItem>)}</SelectContent>
              </Select>
              {customerError && <Alert variant="destructive"><AlertDescription>{customerError}</AlertDescription></Alert>}
            </CardContent>
          </Card>
          <Card>
            <CardHeader><CardTitle>Chọn sản phẩm</CardTitle></CardHeader>
            <CardContent className="space-y-3">
              <Input aria-label="Tìm sản phẩm" placeholder="Tìm theo mã hoặc tên sản phẩm..." value={query} onChange={(event) => { setQuery(event.target.value); setPage(1); }} />
              {productsLoading ? <p role="status" className="py-4 text-sm text-muted-foreground">Đang tải sản phẩm...</p> : productError ? <Alert variant="destructive"><AlertDescription>{productError}</AlertDescription></Alert> : !products.length ? <p className="py-4 text-sm text-muted-foreground">Không tìm thấy sản phẩm đang hoạt động.</p> : <div className="max-h-72 overflow-y-auto space-y-2">
                {products.map((product) => {
                  const added = lines.some((line) => line.productId === product.sourceId);
                  return <div key={product.sourceId} className="flex items-center justify-between gap-3 rounded-lg border p-3">
                    <div className="min-w-0"><p className="font-medium break-words">{product.name || "Sản phẩm chưa có tên"}</p><p className="text-sm text-muted-foreground">{formatCurrency(product.price)}</p></div>
                    <Button type="button" variant="outline" size="sm" disabled={added} onClick={() => addProduct(product)}><Plus className="size-4" />{added ? "Đã chọn" : "Thêm"}</Button>
                  </div>;
                })}
              </div>}
              <div className="flex flex-wrap items-center justify-between gap-2 border-t pt-3">
                <Button type="button" variant="ghost" size="sm" onClick={() => setReload((value) => value + 1)}>Tải lại danh mục</Button>
                <div className="flex items-center gap-2"><Button type="button" variant="outline" size="sm" disabled={productsLoading || page <= 1} onClick={() => setPage((value) => value - 1)}><ChevronLeft className="size-4" />Trước</Button><span className="text-sm">Trang {page}/{totalPages}</span><Button type="button" variant="outline" size="sm" disabled={productsLoading || page >= totalPages} onClick={() => setPage((value) => value + 1)}>Sau<ChevronRight className="size-4" /></Button></div>
              </div>
            </CardContent>
          </Card>
          <Card>
            <CardHeader><CardTitle>Sản phẩm đã chọn ({lines.length})</CardTitle></CardHeader>
            <CardContent>
              <Table><TableHeader><TableRow><TableHead>Sản phẩm</TableHead><TableHead>Số lượng</TableHead><TableHead>Đơn giá (VND)</TableHead><TableHead className="text-right">Thành tiền</TableHead><TableHead><span className="sr-only">Thao tác</span></TableHead></TableRow></TableHeader>
                <TableBody>{!lines.length && <TableRow><TableCell colSpan={5} className="text-center text-muted-foreground">Chưa có sản phẩm.</TableCell></TableRow>}{lines.map((line) => <TableRow key={line.productId}>
                  <TableCell className="min-w-48 whitespace-normal font-medium">{line.productName}</TableCell>
                  <TableCell className="min-w-36"><IntegerInput aria-label={`Số lượng ${line.productName}`} value={line.quantity} min={1} max={2_147_483_647} onValueChange={(quantity) => updateLine(line.productId, { quantity })} /></TableCell>
                  <TableCell className="min-w-44"><DecimalInput aria-label={`Đơn giá ${line.productName}`} value={line.unitPrice} min={0} onValueChange={(unitPrice) => updateLine(line.productId, { unitPrice })} /></TableCell>
                  <TableCell className="text-right">{formatCurrency(line.quantity * line.unitPrice)}</TableCell>
                  <TableCell><Button type="button" variant="ghost" size="icon" className="text-destructive" aria-label={`Bỏ ${line.productName}`} onClick={() => setLines((current) => current.filter((item) => item.productId !== line.productId))}><Trash2 className="size-4" /></Button></TableCell>
                </TableRow>)}</TableBody>
              </Table>
              <p className="mt-4 text-right text-lg font-semibold">Tổng giá trị: {formatCurrency(total)}</p>
            </CardContent>
          </Card>
          {error && <Alert variant="destructive"><AlertDescription role="alert">{error}</AlertDescription></Alert>}
          <div className="flex justify-end"><Button type="submit" disabled={saving || customersLoading || !customerId || !lines.length}>{saving ? <Loader2 className="size-4 animate-spin" /> : <Save className="size-4" />}Tạo báo giá</Button></div>
        </fieldset>
      </form>
    </main>
  </>;
}

export default function CreateQuotationPage() {
  return <PermissionGuard permission={RBAC_PERMISSIONS.quotationManage}><CreateQuotationContent /></PermissionGuard>;
}
