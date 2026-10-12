using System.Text.Json.Serialization;

namespace InventStock.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SupplierImportStatus { Staged = 1, Committed = 2 }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SupplierImportDecision { Pending = 1, CreateNew = 2, LinkExisting = 3, Skip = 4 }
