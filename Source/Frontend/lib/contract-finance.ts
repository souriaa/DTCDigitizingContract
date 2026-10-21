import { ContractItemDiscountMode } from "@/services/contract-api";

export interface ContractFinanceItem {
  quantity: number;
  unitPrice: number;
  discountMode?: ContractItemDiscountMode;
  discountPercent?: number;
  fixedDiscountAmount?: number;
  isTaxable?: boolean;
  vatPercent?: number;
}

export interface ContractItemAmounts {
  lineSubtotal: number;
  discountAmount: number;
  vatAmount: number;
  lineTotal: number;
}

export interface ContractFinancialTotals {
  subtotal: number;
  totalDiscount: number;
  totalVat: number;
  totalPayment: number;
}

export const roundContractMoney = (
  value: number,
  currencyCode: string,
) => {
  const decimals = currencyCode.toUpperCase() === "VND" ? 0 : 2;
  const factor = 10 ** decimals;
  return Math.round((value + Number.EPSILON) * factor) / factor;
};

/** Làm tròn từng đợt, cân phần dư ở đợt cuối khi đủ 100%; không phân bổ vượt tổng tiền. */
export const allocateContractPaymentAmounts = (
  total: number, rows: ReadonlyArray<{ paymentPercent: number }>, currencyCode: string,
) => {
  let allocated = 0;
  const balanced = Math.round(rows.reduce((sum, row) => sum + row.paymentPercent, 0) * 10000) === 1000000;
  return rows.map((row, index) => {
    const requested = balanced && index === rows.length - 1
      ? roundContractMoney(total - allocated, currencyCode)
      : roundContractMoney(total * row.paymentPercent / 100, currencyCode);
    const amount = Math.max(0, Math.min(requested, roundContractMoney(total - allocated, currencyCode)));
    allocated = roundContractMoney(allocated + amount, currencyCode);
    return amount;
  });
};

export const calculateContractItemAmounts = (
  item: ContractFinanceItem,
  currencyCode: string,
): ContractItemAmounts => {
  const quantity = Math.max(0, Number(item.quantity) || 0);
  const unitPrice = Math.max(0, Number(item.unitPrice) || 0);
  const lineSubtotal = roundContractMoney(
    quantity * unitPrice,
    currencyCode,
  );
  const discountMode =
    item.discountMode ?? ContractItemDiscountMode.None;

  let discountAmount = 0;
  if (discountMode === ContractItemDiscountMode.Percentage) {
    discountAmount = roundContractMoney(
      (lineSubtotal * Math.max(0, Number(item.discountPercent) || 0)) / 100,
      currencyCode,
    );
  } else if (discountMode === ContractItemDiscountMode.FixedAmount) {
    discountAmount = roundContractMoney(
      Math.max(0, Number(item.fixedDiscountAmount) || 0),
      currencyCode,
    );
  }

  const safeDiscountAmount = Math.min(discountAmount, lineSubtotal);
  const amountAfterDiscount = lineSubtotal - safeDiscountAmount;
  const isTaxable = item.isTaxable ?? true;
  const vatAmount = isTaxable
    ? roundContractMoney(
        (amountAfterDiscount * Math.max(0, Number(item.vatPercent) || 0)) /
          100,
        currencyCode,
      )
    : 0;

  return {
    lineSubtotal,
    discountAmount: safeDiscountAmount,
    vatAmount,
    lineTotal: roundContractMoney(
      amountAfterDiscount + vatAmount,
      currencyCode,
    ),
  };
};

export const calculateContractTotals = (
  items: ContractFinanceItem[],
  currencyCode: string,
): ContractFinancialTotals =>
  items.reduce<ContractFinancialTotals>(
    (totals, item) => {
      const amounts = calculateContractItemAmounts(item, currencyCode);
      return {
        subtotal: totals.subtotal + amounts.lineSubtotal,
        totalDiscount: totals.totalDiscount + amounts.discountAmount,
        totalVat: totals.totalVat + amounts.vatAmount,
        totalPayment: totals.totalPayment + amounts.lineTotal,
      };
    },
    {
      subtotal: 0,
      totalDiscount: 0,
      totalVat: 0,
      totalPayment: 0,
    },
  );
