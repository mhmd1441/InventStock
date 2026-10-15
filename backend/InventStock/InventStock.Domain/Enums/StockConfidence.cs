using System.Text.Json.Serialization;

namespace InventStock.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StockConfidence
{
    NotCounted = 1,
    Counted = 2,
    NeedsRecount = 3
}
