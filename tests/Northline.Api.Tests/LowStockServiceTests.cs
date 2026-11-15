using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Northline.Api.Data;
using Northline.Api.Models;
using Northline.Api.Services;

namespace Northline.Api.Tests;

/// <summary>KAN-4: low-stock query against an in-memory database holding exactly five products.</summary>
[TestClass]
public class LowStockServiceTests
{
    // Quantities 3 and 9 are below the default threshold of 10; 10 sits on the boundary and is excluded.
    private static NorthlineDbContext CreateFiveProductDb()
    {
        var options = new DbContextOptionsBuilder<NorthlineDbContext>()
            .UseInMemoryDatabase($"kan4-{Guid.NewGuid()}")
            .Options;
        var db = new NorthlineDbContext(options); // no EnsureCreated: the HasData seed is not applied
        db.Products.AddRange(
            new Product { Id = 101, Sku = "T-101", Name = "Bottle", Price = 32.00m, StockQuantity = 3 },
            new Product { Id = 102, Sku = "T-102", Name = "Socks", Price = 14.00m, StockQuantity = 9 },
            new Product { Id = 103, Sku = "T-103", Name = "Cap", Price = 19.50m, StockQuantity = 10 },
            new Product { Id = 104, Sku = "T-104", Name = "Tote", Price = 22.00m, StockQuantity = 25 },
            new Product { Id = 105, Sku = "T-105", Name = "Tee", Price = 24.00m, StockQuantity = 140 });
        db.SaveChanges();
        return db;
    }

    private static LowStockService CreateService(NorthlineDbContext db) =>
        new(db, NullLogger<LowStockService>.Instance);

    [TestMethod]
    [TestCategory("TICKET-KAN-4")]
    public async Task AC5_AC2_TwoOfFiveBelowThreshold_ReturnsThoseTwoWithIdNameQuantityUnitPrice()
    {
        using var db = CreateFiveProductDb();

        var result = await CreateService(db).GetLowStockAsync(LowStockService.DefaultThreshold, CancellationToken.None);

        CollectionAssert.AreEqual(
            new[]
            {
                new LowStockProduct(101, "Bottle", 3, 32.00m),
                new LowStockProduct(102, "Socks", 9, 14.00m),
            },
            result.ToArray());
    }

    [TestMethod]
    [TestCategory("TICKET-KAN-4")]
    public async Task AC5_AC3_NoneBelowThreshold_ReturnsEmptyList()
    {
        using var db = CreateFiveProductDb();

        var result = await CreateService(db).GetLowStockAsync(3, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    [TestCategory("TICKET-KAN-4")]
    public async Task R7a_InactiveProductBelowThreshold_IsNotReturned()
    {
        // Proposed answer to review prompt R7 (inactive products), comment 10000 on KAN-4.
        using var db = CreateFiveProductDb();
        db.Products.Add(new Product { Id = 106, Sku = "T-106", Name = "Candle", Price = 28.00m, StockQuantity = 1, IsActive = false });
        db.SaveChanges();

        var result = await CreateService(db).GetLowStockAsync(LowStockService.DefaultThreshold, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { 101, 102 }, result.Select(p => p.Id).ToArray());
    }
}
