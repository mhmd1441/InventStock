using System.Text.Json.Serialization;

namespace InventStock.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StockUnit
{
    Piece = 1,
    Centimeter = 2
}
