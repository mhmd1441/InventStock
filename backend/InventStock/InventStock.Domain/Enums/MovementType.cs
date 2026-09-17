using System.Text.Json.Serialization;

namespace InventStock.Domain.Enums;

/// <summary>Serialised by name so the API contract stays readable and stable if values are reordered.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MovementType
{
    In = 1,
    Out = 2,
    Adjustment = 3
}
