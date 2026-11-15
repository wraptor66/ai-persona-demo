using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Northline.Api.Data;
using Northline.Api.Models;
using Northline.Api.Services;

namespace Northline.Api.Controllers;

/// <summary>
/// Catalog endpoints. NOTE: this controller still carries pricing and stock rules inline;
/// see BACKLOG.md story NL-5 for the planned refactor into a service class.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly NorthlineDbContext _db;

    /// <summary>Creates the controller.</summary>
    public ProductsController(NorthlineDbContext db) => _db = db;

    /// <summary>Lists products, optionally filtered by category.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? category, CancellationToken ct)
    {
        var query = _db.Products.AsNoTracking().Where(p => p.IsActive);
        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(p => p.Category == category);
        }

        return Ok(await query.OrderBy(p => p.Id).ToListAsync(ct));
    }

    /// <summary>
    /// Lists active products whose stock is below <paramref name="threshold"/> (default 10).
    /// Requires an authenticated caller; returns 200 with an empty array when none qualify (KAN-4).
    /// </summary>
    [Authorize]
    [HttpGet("low-stock")]
    public async Task<IActionResult> GetLowStock(
        [FromServices] ILowStockService lowStock,
        [FromQuery] int threshold = LowStockService.DefaultThreshold,
        CancellationToken ct = default)
    {
        return Ok(await lowStock.GetLowStockAsync(threshold, ct));
    }

    /// <summary>Gets one product by id.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var product = await _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        return product is null ? NotFound() : Ok(product);
    }

    /// <summary>Creates a product after validating price, stock and SKU.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Product product, CancellationToken ct)
    {
        // Business rules living in the controller (target of NL-5).
        if (product.Price <= 0)
        {
            return BadRequest(new { error = "Price must be greater than zero." });
        }

        if (product.StockQuantity < 0)
        {
            return BadRequest(new { error = "Stock quantity cannot be negative." });
        }

        product.Sku = product.Sku.Trim().ToUpperInvariant();
        if (await _db.Products.AnyAsync(p => p.Sku == product.Sku, ct))
        {
            return Conflict(new { error = $"SKU '{product.Sku}' already exists." });
        }

        product.Id = 0;
        _db.Products.Add(product);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
    }

    /// <summary>Applies a percentage discount to a product's price.</summary>
    [HttpPost("{id:int}/discount")]
    public async Task<IActionResult> ApplyDiscount(int id, [FromQuery] decimal percent, CancellationToken ct)
    {
        var product = await _db.Products.FindAsync(new object[] { id }, ct);
        if (product is null)
        {
            return NotFound();
        }

        if (percent <= 0 || percent > 50)
        {
            return BadRequest(new { error = "Discount must be between 0 and 50 percent." });
        }

        product.Price = Math.Round(product.Price * (1 - percent / 100m), 2, MidpointRounding.AwayFromZero);
        await _db.SaveChangesAsync(ct);
        return Ok(product);
    }

    /// <summary>Adjusts stock on hand by a positive or negative delta.</summary>
    [HttpPost("{id:int}/stock")]
    public async Task<IActionResult> AdjustStock(int id, [FromQuery] int delta, CancellationToken ct)
    {
        var product = await _db.Products.FindAsync(new object[] { id }, ct);
        if (product is null)
        {
            return NotFound();
        }

        if (product.StockQuantity + delta < 0)
        {
            return BadRequest(new { error = "Adjustment would make stock negative." });
        }

        product.StockQuantity += delta;
        await _db.SaveChangesAsync(ct);
        return Ok(product);
    }
}
