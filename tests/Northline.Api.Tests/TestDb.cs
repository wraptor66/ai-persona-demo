using Microsoft.EntityFrameworkCore;
using Northline.Api.Data;

namespace Northline.Api.Tests;

/// <summary>Creates an isolated, seeded in-memory database per test.</summary>
internal static class TestDb
{
    public static NorthlineDbContext Create()
    {
        var options = new DbContextOptionsBuilder<NorthlineDbContext>()
            .UseInMemoryDatabase($"northline-{Guid.NewGuid()}")
            .Options;
        var db = new NorthlineDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }
}
