using System.Text.Json.Serialization;

namespace InventStock.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CashAccountKind { PhysicalSafe = 1, Whish = 2 }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CashMovementType { OpeningBalance = 1, OutgoingPayment = 2, OutgoingPaymentReversal = 3, SalePayment = 4, SaleChange = 5, CustomerPayment = 6, CustomerChange = 7, CustomerPaymentCorrection = 8 }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaymentFundingSource { ShopAccount = 1, PersonalPocket = 2 }
