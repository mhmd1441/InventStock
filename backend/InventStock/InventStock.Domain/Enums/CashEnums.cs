using System.Text.Json.Serialization;

namespace InventStock.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CashAccountKind { PhysicalSafe = 1, Whish = 2 }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CashMovementType { OpeningBalance = 1, OutgoingPayment = 2, OutgoingPaymentReversal = 3, SalePayment = 4, SaleChange = 5, CustomerPayment = 6, CustomerChange = 7, CustomerPaymentCorrection = 8, SaleRefund = 9, LegacySaleRefund = 10, SupplierPayment = 11, SupplierPaymentFee = 12, SupplierPaymentReversal = 13, TransferOut = 14, TransferIn = 15, TransferFee = 16, TransferReversal = 17, SaleTenderAccountCorrectionOut = 18, SaleTenderAccountCorrectionIn = 19, SaleTenderAccountCorrectionReversal = 20 }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaymentFundingSource { ShopAccount = 1, PersonalPocket = 2 }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CashReconciliationOpeningSource { PreviousClose = 1, AdminCount = 2 }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SupplierPaymentLineKind { Settlement = 1, Fee = 2 }
