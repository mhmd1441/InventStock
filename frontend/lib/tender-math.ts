import type { SalePaymentAccount, SaleTenderRequest } from "./types";
import { centsNumber, decimalUnits, usdCents } from "./sale-math";

export type TenderDraft = { key: string; accountId: string; native: string; usd: string };

export const preferredTenderAccount = (accounts: SalePaymentAccount[]) =>
  accounts.find((account) => account.kind === "PhysicalSafe" && account.currency === "USD") ?? accounts[0];

export function parseTenderDrafts(rows: TenderDraft[], accounts: SalePaymentAccount[]): SaleTenderRequest[] {
  // A blank starter row must not block a legitimate all-debt or all-credit sale.
  // A partially entered row is never ignored.
  return rows.filter((row) => row.native.trim() || row.usd.trim()).map((row) => {
    const account = accounts.find((x) => x.id === row.accountId);
    if (!account || !row.native.trim() || !row.usd.trim())
      throw new Error("Choose an account and enter its native amount and confirmed USD value.");
    const nativeUnits = decimalUnits(row.native, 6);
    const usd = usdCents(row.usd);
    if (nativeUnits === null || nativeUnits <= BigInt(0) || usd === null || usd <= BigInt(0))
      throw new Error("Enter positive amounts; native amounts allow six decimals and USD settlements allow two.");
    const nativeAmount = Number(row.native);
    if (!Number.isFinite(nativeAmount) || nativeAmount > 999999999999)
      throw new Error("Native tender amount is too large.");
    if (account.currency === "USD" && usdCents(row.native) !== usd)
      throw new Error("A USD tender's native amount must equal its confirmed USD settlement.");
    return { cashAccountId: account.id, currency: account.currency, nativeAmount, confirmedUsdAmount: centsNumber(usd) };
  });
}
