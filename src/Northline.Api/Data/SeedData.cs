using Microsoft.EntityFrameworkCore;
using Northline.Api.Models;

namespace Northline.Api.Data;

/// <summary>Deterministic sample data for the Northline Retail demo.</summary>
public static class SeedData
{
    private static readonly DateTime Base = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>Registers seed rows with the model builder.</summary>
    public static void Apply(ModelBuilder b)
    {
        b.Entity<Customer>().HasData(
            new Customer { Id = 1, FirstName = "Ava", LastName = "Martinez", Email = "ava.martinez@example.com", CreatedUtc = Base },
            new Customer { Id = 2, FirstName = "Liam", LastName = "Chen", Email = "liam.chen@example.com", CreatedUtc = Base.AddDays(2) },
            new Customer { Id = 3, FirstName = "Sofia", LastName = "Patel", Email = "sofia.patel@example.com", CreatedUtc = Base.AddDays(5) },
            new Customer { Id = 4, FirstName = "Noah", LastName = "Johnson", Email = "noah.johnson@example.com", CreatedUtc = Base.AddDays(9) },
            new Customer { Id = 5, FirstName = "Mia", LastName = "Okafor", Email = "mia.okafor@example.com", CreatedUtc = Base.AddDays(12) },
            new Customer { Id = 6, FirstName = "Ethan", LastName = "Rossi", Email = "ethan.rossi@example.com", CreatedUtc = Base.AddDays(20) });

        b.Entity<Product>().HasData(
            new Product { Id = 1, Sku = "NL-TEE-001", Name = "Organic Cotton Tee", Category = "Apparel", Price = 24.00m, StockQuantity = 140 },
            new Product { Id = 2, Sku = "NL-HOOD-002", Name = "Midweight Hoodie", Category = "Apparel", Price = 58.00m, StockQuantity = 6 },
            new Product { Id = 3, Sku = "NL-CAP-003", Name = "Canvas Cap", Category = "Accessories", Price = 19.50m, StockQuantity = 75 },
            new Product { Id = 4, Sku = "NL-BTL-004", Name = "Insulated Bottle 750ml", Category = "Home", Price = 32.00m, StockQuantity = 3 },
            new Product { Id = 5, Sku = "NL-MUG-005", Name = "Stoneware Mug", Category = "Home", Price = 16.00m, StockQuantity = 210 },
            new Product { Id = 6, Sku = "NL-TOTE-006", Name = "Market Tote", Category = "Accessories", Price = 22.00m, StockQuantity = 0 },
            new Product { Id = 7, Sku = "NL-SOCK-007", Name = "Merino Socks", Category = "Apparel", Price = 14.00m, StockQuantity = 9 },
            new Product { Id = 8, Sku = "NL-CNDL-008", Name = "Cedar Candle", Category = "Home", Price = 28.00m, StockQuantity = 44, IsActive = false });

        b.Entity<Order>().HasData(
            new Order { Id = 1, CustomerId = 1, OrderDateUtc = Base.AddDays(10), Total = 72.00m },
            new Order { Id = 2, CustomerId = 2, OrderDateUtc = Base.AddDays(14), Total = 58.00m },
            new Order { Id = 3, CustomerId = 1, OrderDateUtc = Base.AddDays(21), Total = 51.50m },
            new Order { Id = 4, CustomerId = 3, OrderDateUtc = Base.AddDays(30), Total = 96.00m },
            new Order { Id = 5, CustomerId = 5, OrderDateUtc = Base.AddDays(41), Total = 42.00m });

        b.Entity<OrderItem>().HasData(
            new OrderItem { Id = 1, OrderId = 1, ProductId = 1, Quantity = 3, UnitPrice = 24.00m },
            new OrderItem { Id = 2, OrderId = 2, ProductId = 2, Quantity = 1, UnitPrice = 58.00m },
            new OrderItem { Id = 3, OrderId = 3, ProductId = 3, Quantity = 1, UnitPrice = 19.50m },
            new OrderItem { Id = 4, OrderId = 3, ProductId = 4, Quantity = 1, UnitPrice = 32.00m },
            new OrderItem { Id = 5, OrderId = 4, ProductId = 5, Quantity = 6, UnitPrice = 16.00m },
            new OrderItem { Id = 6, OrderId = 5, ProductId = 7, Quantity = 3, UnitPrice = 14.00m });
    }
}
