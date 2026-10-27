"use client";

import { formatDateTime } from "@/lib/format-date-time";

import { useCallback, useEffect, useMemo, useState } from "react";
import {
  AlertCircle,
  Briefcase,
  Building2,
  Calendar,
  Check,
  CheckCircle2,
  Copy,
  CreditCard,
  FileSignature,
  FileText,
  Info,
  Landmark,
  Loader2,
  Lock,
  MapPin,
  Phone,
  Printer,
  Receipt,
  RefreshCw,
  RotateCcw,
  Save,
  Scale,
  ShieldCheck,
  Sparkles,
  User,
} from "lucide-react";

import { PermissionGuard } from "@/components/auth/permission-guard";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { ConfirmDialog } from "@/components/ui/custom/confirm-dialog";
import { Header } from "@/components/ui/custom/header";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Progress } from "@/components/ui/progress";
import { Separator } from "@/components/ui/separator";
import { Skeleton } from "@/components/ui/skeleton";
import { Textarea } from "@/components/ui/textarea";
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from "@/components/ui/tooltip";
import { toast } from "@/components/ui/sonner";
import { getApiErrorMessage, isStaleRowVersion } from "@/lib/api-error";
import { RBAC_PERMISSIONS } from "@/lib/rbac";
import { cn } from "@/lib/utils";
import { TENANT_ENGLISH_FIELDS } from "@/lib/document-english-fields";
import {
  tenantLegalProfileApi,
  type TenantLegalProfileResponse,
  type UpsertTenantLegalProfileRequest,
} from "@/services/tenant-legal-profile-api";

const EMPTY_FORM: UpsertTenantLegalProfileRequest = {
  legalEntityName: "",
  legalEntityNameEn: "",
  addressEn: "",
  representativeTitleEn: "",
  bankNameEn: "",
  taxCode: "",
  address: "",
  representativeName: "",
  representativeTitle: "",
  phoneNumber: "",
  faxNumber: "",
  bankAccountNumber: "",
  bankName: "",
  rowVersion: null,
};

type FormErrors = Partial<
  Record<keyof UpsertTenantLegalProfileRequest, string>
>;

function LegalProfilePageContent() {
  const [profile, setProfile] = useState<TenantLegalProfileResponse | null>(
    null,
  );
  const [form, setForm] = useState<UpsertTenantLegalProfileRequest>(EMPTY_FORM);
  const [initialForm, setInitialForm] =
    useState<UpsertTenantLegalProfileRequest>(EMPTY_FORM);
  const [errors, setErrors] = useState<FormErrors>({});
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [isResetConfirmOpen, setIsResetConfirmOpen] = useState(false);
  const [copiedKey, setCopiedKey] = useState<string | null>(null);

  const loadProfile = useCallback(async () => {
    setIsLoading(true);
    try {
      const data = await tenantLegalProfileApi.get();
      setProfile(data);
      const parsedForm = data ? toForm(data) : EMPTY_FORM;
      setForm(parsedForm);
      setInitialForm(parsedForm);
      setErrors({});
    } catch (error) {
      toast.error(
        getApiErrorMessage(error, "Không thể tải hồ sơ pháp lý doanh nghiệp."),
      );
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadProfile();
  }, [loadProfile]);

  const isDirty = useMemo(() => {
    return (
      form.legalEntityName !== initialForm.legalEntityName ||
      TENANT_ENGLISH_FIELDS.some(({ key }) => form[key] !== initialForm[key]) ||
      form.taxCode !== initialForm.taxCode ||
      form.address !== initialForm.address ||
      form.representativeName !== initialForm.representativeName ||
      form.representativeTitle !== initialForm.representativeTitle ||
      (form.phoneNumber ?? "") !== (initialForm.phoneNumber ?? "") ||
      (form.faxNumber ?? "") !== (initialForm.faxNumber ?? "") ||
      (form.bankAccountNumber ?? "") !==
        (initialForm.bankAccountNumber ?? "") ||
      (form.bankName ?? "") !== (initialForm.bankName ?? "")
    );
  }, [form, initialForm]);

  const completeness = useMemo(() => {
    const requiredItems = [
      ...TENANT_ENGLISH_FIELDS.map(({ key, label }) => ({
        key,
        label,
        isFilled: Boolean(form[key].trim()),
      })),
      {
        key: "legalEntityName",
        label: "Tên doanh nghiệp",
        isFilled: Boolean(form.legalEntityName.trim()),
      },
      {
        key: "taxCode",
        label: "Mã số thuế",
        isFilled: Boolean(form.taxCode.trim()),
      },
      {
        key: "address",
        label: "Địa chỉ trụ sở",
        isFilled: Boolean(form.address.trim()),
      },
      {
        key: "representativeName",
        label: "Người đại diện",
        isFilled: Boolean(form.representativeName.trim()),
      },
      {
        key: "representativeTitle",
        label: "Chức danh",
        isFilled: Boolean(form.representativeTitle.trim()),
      },
    ];

    const optionalItems = [
      {
        key: "phoneNumber",
        label: "Điện thoại liên hệ",
        isFilled: Boolean(form.phoneNumber?.trim()),
      },
      {
        key: "faxNumber",
        label: "Số Fax",
        isFilled: Boolean(form.faxNumber?.trim()),
      },
      {
        key: "bankAccountNumber",
        label: "Số tài khoản ngân hàng",
        isFilled: Boolean(form.bankAccountNumber?.trim()),
      },
      {
        key: "bankName",
        label: "Tên ngân hàng",
        isFilled: Boolean(form.bankName?.trim()),
      },
    ];

    const requiredFilledCount = requiredItems.filter((i) => i.isFilled).length;
    const optionalFilledCount = optionalItems.filter((i) => i.isFilled).length;
    const totalFilled = requiredFilledCount + optionalFilledCount;
    const percentage = Math.round((totalFilled / 13) * 100);
    const isFullyReady = requiredFilledCount === requiredItems.length;

    return {
      requiredItems,
      optionalItems,
      requiredFilledCount,
      requiredTotal: requiredItems.length,
      optionalFilledCount,
      optionalTotal: optionalItems.length,
      percentage,
      isFullyReady,
    };
  }, [form]);

  const updateField = (
    field: keyof UpsertTenantLegalProfileRequest,
    value: string,
  ) => {
    setForm((current) => ({ ...current, [field]: value }));
    if (errors[field]) {
      setErrors((prev) => {
        const next = { ...prev };
        delete next[field];
        return next;
      });
    }
  };

  const copyToClipboard = (text: string, keyName: string, label: string) => {
    if (!text) return;
    navigator.clipboard.writeText(text);
    setCopiedKey(keyName);
    toast.success(`Đã sao chép ${label}`);
    setTimeout(() => {
      setCopiedKey((curr) => (curr === keyName ? null : curr));
    }, 2000);
  };

  const validate = (): boolean => {
    const newErrors: FormErrors = {};
    for (const { key, label, maxLength } of TENANT_ENGLISH_FIELDS) {
      if (!form[key].trim())
        newErrors[key] = `Vui lòng nhập ${label.toLowerCase()}`;
      else if (form[key].length > maxLength)
        newErrors[key] = `Tối đa ${maxLength} ký tự`;
    }
    if (!form.legalEntityName.trim()) {
      newErrors.legalEntityName = "Vui lòng nhập tên doanh nghiệp";
    }
    if (!form.taxCode.trim()) {
      newErrors.taxCode = "Vui lòng nhập mã số thuế";
    }
    if (!form.address.trim()) {
      newErrors.address = "Vui lòng nhập địa chỉ trụ sở chính";
    }
    if (!form.representativeName.trim()) {
      newErrors.representativeName = "Vui lòng nhập tên người đại diện";
    }
    if (!form.representativeTitle.trim()) {
      newErrors.representativeTitle = "Vui lòng nhập chức danh người đại diện";
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleSave = async () => {
    if (!validate()) {
      toast.error("Vui lòng điền đầy đủ các thông tin pháp lý bắt buộc (*).");
      return;
    }

    setIsSaving(true);
    try {
      const saved = await tenantLegalProfileApi.upsert({
        ...form,
        legalEntityName: form.legalEntityName.trim(),
        legalEntityNameEn: form.legalEntityNameEn.trim(),
        addressEn: form.addressEn.trim(),
        representativeTitleEn: form.representativeTitleEn.trim(),
        bankNameEn: form.bankNameEn.trim(),
        taxCode: form.taxCode.trim(),
        address: form.address.trim(),
        representativeName: form.representativeName.trim(),
        representativeTitle: form.representativeTitle.trim(),
        phoneNumber: form.phoneNumber?.trim() || null,
        faxNumber: form.faxNumber?.trim() || null,
        bankAccountNumber: form.bankAccountNumber?.trim() || null,
        bankName: form.bankName?.trim() || null,
      });

      setProfile(saved);
      const parsed = toForm(saved);
      setForm(parsed);
      setInitialForm(parsed);
      setErrors({});
      toast.success("Hồ sơ pháp lý doanh nghiệp đã được cập nhật thành công.");
    } catch (error) {
      if (isStaleRowVersion(error)) {
        toast.error(
          "Dữ liệu đã được người dùng khác cập nhật. Đang tự động tải lại phiên bản mới nhất.",
        );
        await loadProfile();
      } else {
        toast.error(getApiErrorMessage(error, "Không thể lưu hồ sơ pháp lý."));
      }
    } finally {
      setIsSaving(false);
    }
  };

  const handleReset = () => {
    setForm(initialForm);
    setErrors({});
    setIsResetConfirmOpen(false);
    toast.info("Đã khôi phục dữ liệu ban đầu.");
  };

  // Keyboard shortcut Ctrl+S / Cmd+S
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "s") {
        e.preventDefault();
        if (!isLoading && !isSaving) {
          void handleSave();
        }
      }
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  });

  return (
    <TooltipProvider delayDuration={200}>
      <Header title="Hồ sơ pháp lý doanh nghiệp" />

      <div className="grow overflow-y-auto bg-muted/20 p-4 sm:p-6 lg:p-8">
        <div className="mx-auto space-y-6">
          {/* Top Hero Banner */}
          <div className="relative overflow-hidden rounded-2xl border border-border/80 bg-gradient-to-br from-card via-card to-primary/5 p-6 shadow-sm backdrop-blur-sm sm:p-8">
            <div className="absolute -right-16 -top-16 size-64 rounded-full bg-primary/5 blur-3xl" />
            <div className="absolute -bottom-16 -left-16 size-64 rounded-full bg-accent/10 blur-3xl" />

            <div className="relative flex flex-col gap-6 lg:flex-row lg:items-center lg:justify-between">
              {/* Left Identity Area */}
              <div className="flex flex-col gap-4 sm:flex-row sm:items-center">
                <div className="flex size-14 shrink-0 items-center justify-center rounded-2xl border border-primary/20 bg-primary/10 text-primary shadow-inner sm:size-16">
                  <Building2 className="size-7 sm:size-8" />
                </div>

                <div className="space-y-1.5">
                  <div className="flex flex-wrap items-center gap-2.5">
                    <h1 className="text-xl font-bold tracking-tight text-foreground sm:text-2xl lg:text-3xl">
                      {isLoading ? (
                        <Skeleton className="h-8 w-64" />
                      ) : (
                        form.legalEntityName ||
                        "Chưa thiết lập tên doanh nghiệp"
                      )}
                    </h1>

                    {completeness.isFullyReady ? (
                      <Badge
                        variant="secondary"
                        className="gap-1 border-emerald-500/30 bg-emerald-500/10 text-emerald-700 dark:text-emerald-400"
                      >
                        <span className="size-1.5 rounded-full bg-emerald-500 animate-pulse" />
                        Hồ sơ hợp lệ
                      </Badge>
                    ) : (
                      <Badge
                        variant="secondary"
                        className="gap-1 border-amber-500/30 bg-amber-500/10 text-amber-700 dark:text-amber-400"
                      >
                        <AlertCircle className="size-3 text-amber-500" />
                        Chưa hoàn thiện
                      </Badge>
                    )}
                  </div>

                  <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-muted-foreground sm:text-sm">
                    {form.taxCode ? (
                      <span className="flex items-center gap-1 font-mono font-medium text-foreground">
                        <Receipt className="size-3.5 text-primary" />
                        MST: {form.taxCode}
                      </span>
                    ) : (
                      <span className="italic text-muted-foreground">
                        Chưa cấu hình mã số thuế
                      </span>
                    )}

                    <span className="hidden sm:inline text-border">•</span>

                    <span className="flex items-center gap-1">
                      <Calendar className="size-3.5 text-muted-foreground" />
                      Cập nhật:{" "}
                      {isLoading
                        ? "..."
                        : profile?.updatedAt
                          ? formatDateTime(profile.updatedAt)
                          : "Chưa ghi nhận"}
                    </span>
                  </div>
                </div>
              </div>

              {/* Right Action Buttons */}
              <div className="flex flex-wrap items-center gap-2.5">
                {isDirty && (
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    onClick={() => setIsResetConfirmOpen(true)}
                    disabled={isLoading || isSaving}
                    className="gap-1.5 border-dashed"
                  >
                    <RotateCcw className="size-3.5" />
                    <span className="hidden sm:inline">Khôi phục</span>
                  </Button>
                )}

                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => void loadProfile()}
                  disabled={isLoading || isSaving}
                  className="gap-1.5 shadow-sm"
                >
                  <RefreshCw
                    className={cn("size-3.5", isLoading && "animate-spin")}
                  />
                  <span>Tải lại</span>
                </Button>

                <Button
                  type="button"
                  size="sm"
                  onClick={() => void handleSave()}
                  disabled={isLoading || isSaving}
                  className="gap-1.5 bg-primary shadow-sm hover:bg-primary/90"
                >
                  {isSaving ? (
                    <Loader2 className="size-3.5 animate-spin" />
                  ) : (
                    <Save className="size-3.5" />
                  )}
                  <span>Lưu thay đổi</span>
                  <kbd className="hidden rounded bg-primary-foreground/20 px-1.5 py-0.5 text-[10px] font-mono text-primary-foreground sm:inline-block">
                    Ctrl+S
                  </kbd>
                </Button>
              </div>
            </div>

            {/* Completeness Bar */}
            <div className="mt-6 rounded-xl border border-border/60 bg-muted/40 p-3.5 sm:p-4">
              <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
                <div className="flex items-center gap-2">
                  <Sparkles className="size-4 text-primary" />
                  <span className="text-xs font-semibold text-foreground sm:text-sm">
                    Mức độ hoàn thiện hồ sơ pháp lý
                  </span>
                  <span className="text-xs text-muted-foreground">
                    ({completeness.requiredFilledCount}/
                    {completeness.requiredTotal} trường bắt buộc)
                  </span>
                </div>

                <div className="flex items-center gap-3">
                  <span className="text-xs font-bold text-primary sm:text-sm">
                    {completeness.percentage}%
                  </span>
                  <span className="text-xs text-muted-foreground hidden sm:inline">
                    {completeness.isFullyReady
                      ? "Đủ điều kiện xuất bản hợp đồng"
                      : "Cần bổ sung các mục có dấu (*)"}
                  </span>
                </div>
              </div>
              <Progress
                value={completeness.percentage}
                className="mt-2.5 h-2"
              />
            </div>
          </div>

          {/* Unsaved Changes Banner */}
          {isDirty && (
            <div className="flex items-center justify-between rounded-xl border border-amber-500/40 bg-amber-500/10 px-4 py-2.5 text-xs text-amber-800 dark:text-amber-300 sm:text-sm">
              <div className="flex items-center gap-2">
                <Info className="size-4 shrink-0 text-amber-600 dark:text-amber-400" />
                <span>
                  Bạn có các thay đổi chưa được lưu. Hãy bấm{" "}
                  <strong>Lưu thay đổi</strong> hoặc dùng tổ hợp phím{" "}
                  <strong>Ctrl + S</strong>.
                </span>
              </div>
              <Button
                size="sm"
                variant="outline"
                className="h-7 border-amber-500/40 text-xs font-medium hover:bg-amber-500/20"
                onClick={() => void handleSave()}
                disabled={isSaving}
              >
                Lưu ngay
              </Button>
            </div>
          )}

          {/* Main Grid: Form (Left) & Preview/Audit (Right) */}
          <div className="grid grid-cols-1 items-start gap-6 lg:grid-cols-12">
            {/* Left Column: Structured Form Cards */}
            <div className="space-y-6 lg:col-span-7 xl:col-span-8">
              {/* Card 1: Pháp nhân & Thuế */}
              <Card className="border-border/80 shadow-sm transition-shadow hover:shadow">
                <CardHeader className="border-b pb-4">
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-2.5">
                      <div className="flex size-9 items-center justify-center rounded-lg bg-blue-500/10 text-blue-600 dark:text-blue-400">
                        <Building2 className="size-5" />
                      </div>
                      <div>
                        <CardTitle className="text-base font-semibold">
                          Thông tin doanh nghiệp & Thuế
                        </CardTitle>
                        <CardDescription className="text-xs">
                          Tên doanh nghiệp, mã số thuế và trụ sở chính theo ĐKKD
                        </CardDescription>
                      </div>
                    </div>
                    <Badge
                      variant="outline"
                      className="text-[11px] font-normal"
                    >
                      Bắt buộc
                    </Badge>
                  </div>
                </CardHeader>
                <CardContent className="space-y-4 pt-5">
                  {isLoading ? (
                    <div className="space-y-4">
                      <Skeleton className="h-10 w-full" />
                      <div className="grid grid-cols-2 gap-4">
                        <Skeleton className="h-10 w-full" />
                        <Skeleton className="h-10 w-full" />
                      </div>
                      <Skeleton className="h-20 w-full" />
                    </div>
                  ) : (
                    <>
                      <FormField
                        id="legalEntityName"
                        label="Tên doanh nghiệp"
                        required
                        value={form.legalEntityName}
                        onChange={(v) => updateField("legalEntityName", v)}
                        placeholder="Ví dụ: CÔNG TY CỔ PHẦN CÔNG NGHỆ DTC"
                        maxLength={500}
                        error={errors.legalEntityName}
                        helperText="Tên chính thức bằng chữ IN HOA theo Giấy chứng nhận Đăng ký kinh doanh"
                        icon={<Building2 className="size-4" />}
                      />

                      <div className="grid grid-cols-1 gap-4 sm:grid-cols-1">
                        <FormField
                          id="taxCode"
                          label="Mã số thuế"
                          required
                          value={form.taxCode}
                          onChange={(v) => updateField("taxCode", v)}
                          placeholder="Ví dụ: 0101234567"
                          maxLength={50}
                          error={errors.taxCode}
                          icon={<Receipt className="size-4" />}
                          copyable
                          onCopy={() =>
                            copyToClipboard(
                              form.taxCode,
                              "taxCode",
                              "Mã số thuế",
                            )
                          }
                          isCopied={copiedKey === "taxCode"}
                        />
                      </div>

                      <div className="space-y-1.5">
                        <div className="flex items-center justify-between">
                          <Label
                            htmlFor="address"
                            className="flex items-center gap-1 text-xs font-medium"
                          >
                            <MapPin className="size-3.5 text-muted-foreground" />
                            Địa chỉ trụ sở chính{" "}
                            <span className="text-destructive">*</span>
                          </Label>
                          <span className="text-[11px] text-muted-foreground font-mono">
                            {form.address.length}/2000
                          </span>
                        </div>
                        <Textarea
                          id="address"
                          value={form.address}
                          onChange={(e) =>
                            updateField("address", e.target.value)
                          }
                          maxLength={2000}
                          rows={3}
                          placeholder="Số nhà, tên đường/phố, Phường/Xã, Quận/Huyện, Tỉnh/Thành phố..."
                          className={cn(
                            "resize-y text-sm",
                            errors.address &&
                              "border-destructive focus-visible:ring-destructive",
                          )}
                        />
                        {errors.address && (
                          <p className="flex items-center gap-1 text-xs text-destructive">
                            <AlertCircle className="size-3" />
                            {errors.address}
                          </p>
                        )}
                      </div>
                      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                        <FormField
                          id="phoneNumber"
                          label="Điện thoại công ty / Hotline"
                          value={form.phoneNumber ?? ""}
                          onChange={(v) => updateField("phoneNumber", v)}
                          placeholder="Ví dụ: 024 3795 8888"
                          maxLength={30}
                          icon={<Phone className="size-4" />}
                        />
                        <FormField
                          id="faxNumber"
                          label="Số Fax"
                          value={form.faxNumber ?? ""}
                          onChange={(v) => updateField("faxNumber", v)}
                          placeholder="Ví dụ: 024 3795 8889"
                          maxLength={30}
                          icon={<Printer className="size-4" />}
                        />
                      </div>
                    </>
                  )}
                </CardContent>
              </Card>

              {/* Card 2: Đại diện pháp luật */}
              <Card className="border-border/80 shadow-sm transition-shadow hover:shadow">
                <CardHeader className="border-b pb-4">
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-2.5">
                      <div className="flex size-9 items-center justify-center rounded-lg bg-purple-500/10 text-purple-600 dark:text-purple-400">
                        <ShieldCheck className="size-5" />
                      </div>
                      <div>
                        <CardTitle className="text-base font-semibold">
                          Đại diện pháp luật & Ký kết
                        </CardTitle>
                        <CardDescription className="text-xs">
                          Người có thẩm quyền đại diện doanh nghiệp ký hợp đồng
                        </CardDescription>
                      </div>
                    </div>
                    <Badge
                      variant="outline"
                      className="text-[11px] font-normal"
                    >
                      Bắt buộc
                    </Badge>
                  </div>
                </CardHeader>
                <CardContent className="space-y-4 pt-5">
                  {isLoading ? (
                    <div className="grid grid-cols-2 gap-4">
                      <Skeleton className="h-10 w-full" />
                      <Skeleton className="h-10 w-full" />
                    </div>
                  ) : (
                    <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                      <FormField
                        id="representativeName"
                        label="Họ và tên người đại diện"
                        required
                        value={form.representativeName}
                        onChange={(v) => updateField("representativeName", v)}
                        placeholder="Ví dụ: Nguyễn Văn A"
                        maxLength={200}
                        error={errors.representativeName}
                        icon={<User className="size-4" />}
                      />

                      <FormField
                        id="representativeTitle"
                        label="Chức danh / Chức vụ"
                        required
                        value={form.representativeTitle}
                        onChange={(v) => updateField("representativeTitle", v)}
                        placeholder="Ví dụ: Tổng Giám đốc / Giám đốc"
                        maxLength={200}
                        error={errors.representativeTitle}
                        icon={<Briefcase className="size-4" />}
                      />
                    </div>
                  )}
                </CardContent>
              </Card>

              {/* Card 3: Tài khoản ngân hàng & Thanh toán */}
              <Card className="border-border/80 shadow-sm transition-shadow hover:shadow">
                <CardHeader className="border-b pb-4">
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-2.5">
                      <div className="flex size-9 items-center justify-center rounded-lg bg-emerald-500/10 text-emerald-600 dark:text-emerald-400">
                        <Landmark className="size-5" />
                      </div>
                      <div>
                        <CardTitle className="text-base font-semibold">
                          Tài khoản ngân hàng & Thanh toán
                        </CardTitle>
                        <CardDescription className="text-xs">
                          Tài khoản thụ hưởng mặc định trên điều khoản thanh
                          toán hợp đồng
                        </CardDescription>
                      </div>
                    </div>
                    <Badge
                      variant="secondary"
                      className="text-[11px] font-normal text-muted-foreground"
                    >
                      Khuyến nghị
                    </Badge>
                  </div>
                </CardHeader>
                <CardContent className="space-y-4 pt-5">
                  {isLoading ? (
                    <div className="grid grid-cols-2 gap-4">
                      <Skeleton className="h-10 w-full" />
                      <Skeleton className="h-10 w-full" />
                    </div>
                  ) : (
                    <>
                      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                        <FormField
                          id="bankName"
                          label="Tên ngân hàng & Chi nhánh"
                          value={form.bankName ?? ""}
                          onChange={(v) => updateField("bankName", v)}
                          placeholder="Ví dụ: TMCP Ngoại thương Việt Nam (Vietcombank) - CN Hà Nội"
                          maxLength={500}
                          icon={<Landmark className="size-4" />}
                        />

                        <FormField
                          id="bankAccountNumber"
                          label="Số tài khoản thụ hưởng"
                          value={form.bankAccountNumber ?? ""}
                          onChange={(v) => updateField("bankAccountNumber", v)}
                          placeholder="Ví dụ: 0011001234567"
                          maxLength={100}
                          icon={<CreditCard className="size-4" />}
                          copyable
                          onCopy={() =>
                            copyToClipboard(
                              form.bankAccountNumber ?? "",
                              "bankAccountNumber",
                              "Số tài khoản",
                            )
                          }
                          isCopied={copiedKey === "bankAccountNumber"}
                        />
                      </div>

                      <div className="flex items-start gap-2.5 rounded-lg border border-primary/20 bg-primary/5 p-3 text-xs text-muted-foreground">
                        <Info className="size-4 shrink-0 text-primary mt-0.5" />
                        <span>
                          Thông tin tài khoản ngân hàng sẽ được tự động điền vào
                          Phụ lục thanh toán và Biên bản nghiệm thu/Thanh lý của
                          hợp đồng.
                        </span>
                      </div>
                    </>
                  )}
                </CardContent>
              </Card>
              <Card>
                <CardHeader>
                  <CardTitle>Thông tin tiếng Anh</CardTitle>
                  <CardDescription>
                    Bắt buộc để dùng thống nhất trên tài liệu song ngữ.
                  </CardDescription>
                </CardHeader>
                <CardContent className="grid gap-4 sm:grid-cols-2">
                  {TENANT_ENGLISH_FIELDS.map(({ key, label, maxLength }) => (
                    <FormField
                      key={key}
                      id={key}
                      label={label}
                      value={form[key]}
                      onChange={(v) => updateField(key, v)}
                      maxLength={maxLength}
                      required
                      disabled={isLoading || isSaving}
                      error={
                        errors[key] ||
                        (!form[key].trim()
                          ? "Vui lòng nhập thông tin bắt buộc"
                          : undefined)
                      }
                    />
                  ))}
                </CardContent>
              </Card>
            </div>

            {/* Right Column: Live Contract Preview & Snapshot Rules */}
            <div className="space-y-6 lg:sticky lg:top-20 lg:col-span-5 xl:col-span-4">
              {/* Live Contract Preview Card */}
              <Card className="border-border/80 shadow-md">
                <CardHeader className="border-b pb-3">
                  <div className="flex items-center justify-between">
                    <CardTitle className="flex items-center gap-2 text-sm font-semibold">
                      <FileSignature className="size-4 text-primary" />
                      Xem trước trích lục Hợp đồng
                    </CardTitle>
                  </div>
                  <CardDescription className="text-xs">
                    Mô phỏng hiển thị Bên A trên văn bản hợp đồng chính thức
                  </CardDescription>
                </CardHeader>
                <CardContent className="pt-4">
                  {/* Paper Document Mockup */}
                  <div className="relative rounded-xl border border-border/80 bg-background/90 p-4 text-xs shadow-inner space-y-3 font-sans">
                    {/* Official National Motto */}
                    {/* <div className="text-center space-y-0.5 border-b border-border/60 pb-3">
                      <div className="text-[10px] font-bold tracking-wider uppercase text-foreground">
                        CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM
                      </div>
                      <div className="text-[9px] italic text-muted-foreground">
                        Độc lập - Tự do - Hạnh phúc
                      </div>
                      <div className="text-[8px] text-muted-foreground/60 tracking-widest">
                        ─────────
                      </div>
                      <div className="pt-1 text-[11px] font-bold text-primary tracking-wide uppercase">
                        HỢP ĐỒNG KINH TẾ
                      </div>
                    </div> */}

                    {/* Party A Info Block */}
                    <div className="space-y-1.5">
                      <div className="font-semibold text-foreground text-[11px] flex items-center justify-between">
                        <span>ĐẠI DIỆN BÊN A (BÊN CUNG CẤP):</span>
                      </div>

                      <div className="space-y-1.5 pl-2.5 border-l-2 border-primary/50 text-[11px] text-muted-foreground leading-relaxed">
                        <div>
                          <span className="font-medium text-foreground">
                            Tên doanh nghiệp:{" "}
                          </span>
                          <span
                            className={cn(
                              form.legalEntityName
                                ? "text-foreground font-semibold"
                                : "italic text-muted-foreground/70",
                            )}
                          >
                            {form.legalEntityName || "[Chưa nhập tên công ty]"}
                          </span>
                        </div>

                        <div>
                          <span className="font-medium text-foreground">
                            Mã số thuế:{" "}
                          </span>
                          <span
                            className={cn(
                              "font-mono",
                              form.taxCode
                                ? "text-foreground font-medium"
                                : "italic text-muted-foreground/70",
                            )}
                          >
                            {form.taxCode || "[Chưa có MST]"}
                          </span>
                        </div>

                        <div>
                          <span className="font-medium text-foreground">
                            Địa chỉ:{" "}
                          </span>
                          <span
                            className={
                              form.address
                                ? "text-foreground"
                                : "italic text-muted-foreground/70"
                            }
                          >
                            {form.address || "[Chưa nhập địa chỉ trụ sở]"}
                          </span>
                        </div>

                        <div>
                          <span className="font-medium text-foreground">
                            Đại diện bởi:{" "}
                          </span>
                          <span
                            className={
                              form.representativeName
                                ? "text-foreground font-medium"
                                : "italic text-muted-foreground/70"
                            }
                          >
                            {form.representativeName || "[Người đại diện]"}
                          </span>
                          {form.representativeTitle && (
                            <span className="text-muted-foreground">
                              {" "}
                              - Chức vụ: {form.representativeTitle}
                            </span>
                          )}
                        </div>

                        <div>
                          <span className="font-medium text-foreground">
                            Điện thoại:{" "}
                          </span>
                          <span>{form.phoneNumber || "---"}</span>
                          {form.faxNumber && (
                            <span> | Fax: {form.faxNumber}</span>
                          )}
                        </div>

                        <div>
                          <span className="font-medium text-foreground">
                            Tài khoản số:{" "}
                          </span>
                          <span className="font-mono">
                            {form.bankAccountNumber || "---"}
                          </span>
                          {form.bankName && <span> tại {form.bankName}</span>}
                        </div>
                      </div>
                    </div>

                    {/* Signature Preview Simulation */}
                    {/* <div className="pt-3 border-t border-dashed border-border/70 flex justify-between items-end text-[10px]">
                      <div className="italic text-muted-foreground text-[9px] max-w-[120px]">
                        * Tự động đóng dấu & khóa dữ liệu khi ký số
                      </div>

                      <div className="text-center min-w-[110px] space-y-1">
                        <div className="font-bold text-foreground text-[10px] uppercase">
                          ĐẠI DIỆN BÊN A
                        </div>
                        <div className="text-[8px] text-muted-foreground italic">
                          (Ký, ghi rõ họ tên)
                        </div>

                        <div className="py-1 flex items-center justify-center">
                          <div className="size-7 rounded-full border border-primary/30 bg-primary/10 flex items-center justify-center text-primary text-[8px] font-bold shadow-sm">
                            DTC
                          </div>
                        </div>

                        <div className="font-semibold text-foreground text-[10px]">
                          {form.representativeName || "Người đại diện"}
                        </div>
                      </div>
                    </div> */}
                  </div>
                </CardContent>
              </Card>

              {/* Data Freezing & Audit Notice */}
              {/* <Card className="border-border/80 bg-muted/10 shadow-sm">
                <CardHeader className="pb-3">
                  <CardTitle className="flex items-center gap-2 text-sm font-semibold">
                    <Scale className="size-4 text-primary" />
                    Cơ chế đóng băng & Lưu vết pháp lý
                  </CardTitle>
                </CardHeader>
                <CardContent className="space-y-3 text-xs text-muted-foreground">
                  <div className="flex items-start gap-2.5">
                    <Lock className="size-4 shrink-0 text-amber-500 mt-0.5" />
                    <span>
                      <strong>Tính bất biến:</strong> Khi hợp đồng được chuyển
                      sang trạng thái Trình duyệt hoặc Ký số, thông tin pháp lý
                      tại thời điểm đó sẽ được đóng băng vĩnh viễn theo phiên
                      bản hợp đồng.
                    </span>
                  </div>

                  <div className="flex items-start gap-2.5">
                    <FileText className="size-4 shrink-0 text-blue-500 mt-0.5" />
                    <span>
                      <strong>Không ảnh hưởng quá khứ:</strong> Việc cập nhật
                      tại trang này chỉ áp dụng cho các hợp đồng tạo mới hoặc
                      phiên bản sửa đổi tiếp theo.
                    </span>
                  </div>

                  <Separator className="my-2" />

                  <div className="grid grid-cols-2 gap-2 text-[11px] pt-1">
                    <div className="rounded-lg border border-border/60 bg-card p-2">
                      <div className="text-muted-foreground">ID Hồ sơ:</div>
                      <div className="font-mono font-semibold text-foreground">
                        #{profile?.tenantLegalProfileId ?? "---"}
                      </div>
                    </div>

                    <div className="rounded-lg border border-border/60 bg-card p-2">
                      <div className="text-muted-foreground">
                        Khóa phiên bản:
                      </div>
                      <div className="font-mono text-[10px] text-foreground truncate">
                        {profile?.rowVersion ? "Optimistic Lock" : "Chưa khóa"}
                      </div>
                    </div>
                  </div>
                </CardContent>
              </Card> */}

              {/* Readiness Checklist */}
              <Card className="border-border/80 shadow-sm">
                <CardHeader className="pb-3">
                  <CardTitle className="text-sm font-semibold">
                    Danh sách kiểm tra hợp lệ
                  </CardTitle>
                </CardHeader>
                <CardContent className="space-y-2 text-xs">
                  {completeness.requiredItems.map((item) => (
                    <div
                      key={item.key}
                      className="flex items-center justify-between py-1"
                    >
                      <span className="text-muted-foreground">
                        {item.label}
                      </span>
                      {item.isFilled ? (
                        <span className="flex items-center gap-1 font-medium text-emerald-600 dark:text-emerald-400">
                          <CheckCircle2 className="size-3.5" />
                          Đã có
                        </span>
                      ) : (
                        <span className="flex items-center gap-1 font-medium text-destructive">
                          <AlertCircle className="size-3.5" />
                          Bắt buộc
                        </span>
                      )}
                    </div>
                  ))}

                  <Separator className="my-1.5" />

                  <div className="flex items-center justify-between py-1">
                    <span className="text-muted-foreground">
                      Tài khoản ngân hàng
                    </span>
                    {form.bankAccountNumber?.trim() ? (
                      <span className="flex items-center gap-1 font-medium text-emerald-600 dark:text-emerald-400">
                        <CheckCircle2 className="size-3.5" />
                        Đã có
                      </span>
                    ) : (
                      <span className="text-muted-foreground italic">
                        Khuyến nghị
                      </span>
                    )}
                  </div>
                </CardContent>
              </Card>
            </div>
          </div>
        </div>
      </div>

      {/* Discard Changes Confirmation Dialog */}
      <ConfirmDialog
        isOpen={isResetConfirmOpen}
        onClose={() => setIsResetConfirmOpen(false)}
        onConfirm={handleReset}
        title="Khôi phục dữ liệu ban đầu?"
        description="Mọi thay đổi chưa lưu trên biểu mẫu sẽ bị xóa bỏ và khôi phục về trạng thái được tải gần nhất từ máy chủ."
        icon={<RotateCcw className="size-5 text-amber-500" />}
        confirmText="Khôi phục"
        variant="default"
      />
    </TooltipProvider>
  );
}

interface FormFieldProps {
  id: string;
  label: string;
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  maxLength?: number;
  required?: boolean;
  disabled?: boolean;
  error?: string;
  helperText?: string;
  icon?: React.ReactNode;
  copyable?: boolean;
  onCopy?: () => void;
  isCopied?: boolean;
}

function FormField({
  id,
  label,
  value,
  onChange,
  placeholder,
  maxLength,
  required = false,
  disabled = false,
  error,
  helperText,
  icon,
  copyable = false,
  onCopy,
  isCopied = false,
}: FormFieldProps) {
  return (
    <div className="space-y-1.5">
      <div className="flex items-center justify-between">
        <Label
          htmlFor={id}
          className="flex items-center gap-1 text-xs font-medium"
        >
          {icon && <span className="text-muted-foreground">{icon}</span>}
          <span>{label}</span>
          {required && <span className="text-destructive">*</span>}
        </Label>
      </div>

      <div className="flex gap-2">
        <Input
          id={id}
          value={value}
          required={required}
          disabled={disabled}
          onChange={(e) => onChange(e.target.value)}
          maxLength={maxLength}
          placeholder={placeholder}
          className={cn(
            "text-sm h-9",
            copyable && "pr-9",
            error && "border-destructive focus-visible:ring-destructive",
          )}
        />
        {copyable && value && (
          <Tooltip>
            <TooltipTrigger asChild>
              <Button
                type="button"
                onClick={onCopy}
                aria-label="Sao chép"
                variant="outline"
                className="h-9 w-9 rounded-md"
              >
                {isCopied ? (
                  <Check className="size-3.5 text-emerald-500" />
                ) : (
                  <Copy className="size-3.5" />
                )}
              </Button>
            </TooltipTrigger>
            <TooltipContent side="top">
              {isCopied ? "Đã sao chép!" : "Sao chép"}
            </TooltipContent>
          </Tooltip>
        )}
      </div>

      {error ? (
        <p className="flex items-center gap-1 text-xs text-destructive">
          <AlertCircle className="size-3 shrink-0" />
          {error}
        </p>
      ) : helperText ? (
        <p className="text-[11px] text-muted-foreground">{helperText}</p>
      ) : null}
    </div>
  );
}

function toForm(
  profile: TenantLegalProfileResponse,
): UpsertTenantLegalProfileRequest {
  return {
    legalEntityName: profile.legalEntityName,
    legalEntityNameEn: profile.legalEntityNameEn ?? "",
    addressEn: profile.addressEn ?? "",
    representativeTitleEn: profile.representativeTitleEn ?? "",
    bankNameEn: profile.bankNameEn ?? "",
    taxCode: profile.taxCode,
    address: profile.address,
    representativeName: profile.representativeName,
    representativeTitle: profile.representativeTitle,
    phoneNumber: profile.phoneNumber ?? "",
    faxNumber: profile.faxNumber ?? "",
    bankAccountNumber: profile.bankAccountNumber ?? "",
    bankName: profile.bankName ?? "",
    rowVersion: profile.rowVersion,
  };
}

export default function LegalProfilePage() {
  return (
    <PermissionGuard permission={RBAC_PERMISSIONS.tenantLegalProfileManage}>
      <LegalProfilePageContent />
    </PermissionGuard>
  );
}
