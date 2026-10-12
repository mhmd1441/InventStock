using System.Text.Json.Serialization;

namespace InventStock.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MoneyCurrency { USD = 1, EUR = 2, LBP = 3 }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TaxTreatment { Unknown = 1, Included = 2, Excluded = 3 }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CostSource { SupplierQuote = 1, SupplierCatalogue = 2, ActualPurchase = 3, Legacy = 4 }
