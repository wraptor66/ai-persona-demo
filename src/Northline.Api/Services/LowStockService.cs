using Microsoft.EntityFrameworkCore;
using Northline.Api.Data;
using Northline.Api.Models;

namespace Northline.Api.Services;

/// <summary>Finds active products whose stock has fallen below a reorder threshold.</summary>
public interface ILowStockService
{
    /// <summary>
    /// Returns active products whose stock quantity is strictly below <paramref name="threshold"/>,
    /// ordered by product id. Returns an empty list when none qualify.
    /// </summary>
    Task<IReadOnlyList<LowStockProduct>> GetLowStockAsync(int threshold, CancellationToken ct);
}

/// <summary>EF Core implementation of <see cref="ILowStockService"/> (KAN-4).</summary>
public class LowStockService : ILowStockService
{
    /// <summary>Threshold used when the caller does not supply one (KAN-4 AC-1).</summary>
    public const int DefaultThreshold = 10;

    private const string TicketCid = "TICKET-KAN-4";

    /// <summary>EventIds 4000-4009 are reserved for KAN-4.</summary>
    private static readonly EventId LowStockQueried = new(4000, "LowStockQueried");

    private readonly NorthlineDbContext _db;
    private readonly ILogger<LowStockService> _logger;

    /// <summary>Creates the service.</summary>
    public LowStockService(NorthlineDbContext db, ILogger<LowStockService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LowStockProduct>> GetLowStockAsync(int threshold, CancellationToken ct)
    {
        var items = await _db.Products.AsNoTracking()
            .Where(p => p.IsActive && p.StockQuantity < threshold)
            .OrderBy(p => p.Id)
            .Select(p => new LowStockProduct(p.Id, p.Name, p.StockQuantity, p.Price))
            .ToListAsync(ct);

        _logger.LogInformation(
            LowStockQueried,
            "{Cid} Low-stock query: threshold {Threshold} returned {Count} active products",
            TicketCid, threshold, items.Count);

        return items;
    }
}
