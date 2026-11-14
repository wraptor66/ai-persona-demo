using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Northline.Api.Data;
using Northline.Api.Models;

namespace Northline.Api.Controllers;

/// <summary>Endpoints for customer orders.</summary>
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly NorthlineDbContext _db;

    /// <summary>Creates the controller.</summary>
    public OrdersController(NorthlineDbContext db) => _db = db;

    /// <summary>Lists all orders with their lines.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var orders = await _db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .OrderByDescending(o => o.OrderDateUtc)
            .ToListAsync(ct);
        return Ok(orders);
    }

    /// <summary>Gets one order by id.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var order = await _db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id, ct);
        return order is null ? NotFound() : Ok(order);
    }

    /// <summary>Places an order for an existing customer.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request, CancellationToken ct)
    {
        if (!await _db.Customers.AnyAsync(c => c.Id == request.CustomerId, ct))
        {
            return BadRequest(new { error = $"Customer {request.CustomerId} does not exist." });
        }

        var productIds = request.Lines.Select(l => l.ProductId).Distinct().ToList();
        var products = await _db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);

        var order = new Order { CustomerId = request.CustomerId, OrderDateUtc = DateTime.UtcNow };
        foreach (var line in request.Lines)
        {
            if (!products.TryGetValue(line.ProductId, out var product) || line.Quantity <= 0)
            {
                return BadRequest(new { error = $"Invalid line for product {line.ProductId}." });
            }

            order.Items.Add(new OrderItem { ProductId = product.Id, Quantity = line.Quantity, UnitPrice = product.Price });
        }

        order.Total = order.Items.Sum(i => i.UnitPrice * i.Quantity);
        _db.Orders.Add(order);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = order.Id }, order);
    }
}
