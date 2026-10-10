using DomainEntities = InventStock.Domain.Entities;
using InventStock.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InventStock.Tests.Infrastructure;

/// <summary>
/// The rest of the suite runs on SQLite for speed. These build the model against the Npgsql provider
/// so a mapping that only works on SQLite fails here instead of on first deployment. No connection is
/// opened: EF builds the model and emits DDL without touching a server.
/// </summary>
public class PostgresModelTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=never-connected;Username=none;Password=none")
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public void Model_BuildsAgainstPostgres()
    {
        using var db = CreateContext();

        var entityTypes = db.Model.GetEntityTypes().Select(e => e.ClrType.Name).ToList();

        Assert.Contains("Part", entityTypes);
        Assert.Contains("StockMovement", entityTypes);
        Assert.Contains("Category", entityTypes);
        Assert.Contains("Supplier", entityTypes);
        Assert.Contains("RefreshToken", entityTypes);
    }

    [Fact]
    public void CreateScript_ContainsTheConstraintsTheDomainReliesOn()
    {
        using var db = CreateContext();

        var script = db.Database.GenerateCreateScript();

        Assert.Contains("CK_StockMovements_QuantityChange_NotZero", script);
        Assert.Contains("IX_Parts_PartNumber", script);
        Assert.Contains("IX_RefreshTokens_TokenHash", script);
        Assert.Contains("timestamp with time zone", script);
        Assert.Contains("pg_trgm", script);
        Assert.Contains("gin_trgm_ops", script);
        Assert.Contains("GENERATED ALWAYS AS", script);
    }

    [Fact]
    public void MovementType_IsStoredAsText()
    {
        using var db = CreateContext();

        var property = db.Model
            .FindEntityType(typeof(DomainEntities.StockMovement))!
            .FindProperty(nameof(DomainEntities.StockMovement.MovementType))!;

        Assert.Equal("character varying(20)", property.GetColumnType());
    }

    [Fact]
    public void UnitPrice_UsesFixedPrecision()
    {
        using var db = CreateContext();

        var property = db.Model
            .FindEntityType(typeof(DomainEntities.Part))!
            .FindProperty(nameof(DomainEntities.Part.UnitPrice))!;

        Assert.Equal(18, property.GetPrecision());
        Assert.Equal(2, property.GetScale());
    }
}
