using InventStock.Application.Interfaces;
using InventStock.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InventStock.Tests.Infrastructure;

/// <summary>
/// Backs the repository tests with a real relational engine rather than an in-memory fake, so the
/// LINQ the repositories rely on has to actually translate to SQL.
/// </summary>
public sealed class SqliteTestContext : IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteTestContext(Guid? currentUserId = null)
    {
        CurrentUser = new TestCurrentUser(currentUserId ?? Guid.NewGuid());

        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        Db = new AppDbContext(options, CurrentUser);
        Db.Database.EnsureCreated();
    }

    public AppDbContext Db { get; }

    public TestCurrentUser CurrentUser { get; }

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }
}

public sealed class TestCurrentUser : ICurrentUserService
{
    public TestCurrentUser(Guid userId)
    {
        UserId = userId;
    }

    public Guid? UserId { get; set; }

    public string? Email => "tester@inventstock.local";

    public string? FullName => "Test User";

    public bool IsAuthenticated => UserId is not null;

    public bool IsInRole(string role) => true;
}
