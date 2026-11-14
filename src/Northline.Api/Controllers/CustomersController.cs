using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Northline.Api.Data;
using Northline.Api.Models;

namespace Northline.Api.Controllers;

/// <summary>CRUD endpoints for customers.</summary>
[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly NorthlineDbContext _db;

    /// <summary>Creates the controller.</summary>
    public CustomersController(NorthlineDbContext db) => _db = db;

    /// <summary>Lists all customers.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var customers = await _db.Customers.AsNoTracking().OrderBy(c => c.Id).ToListAsync(ct);
        return Ok(customers);
    }

    /// <summary>Gets one customer by id.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        return customer is null ? NotFound() : Ok(customer);
    }

    /// <summary>Creates a customer.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Customer customer, CancellationToken ct)
    {
        if (await _db.Customers.AnyAsync(c => c.Email == customer.Email, ct))
        {
            return Conflict(new { error = $"A customer with email '{customer.Email}' already exists." });
        }

        customer.Id = 0;
        customer.CreatedUtc = DateTime.UtcNow;
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = customer.Id }, customer);
    }

    /// <summary>Updates a customer's name and email.</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] Customer input, CancellationToken ct)
    {
        var customer = await _db.Customers.FindAsync(new object[] { id }, ct);
        if (customer is null)
        {
            return NotFound();
        }

        customer.FirstName = input.FirstName;
        customer.LastName = input.LastName;
        customer.Email = input.Email;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Deletes a customer.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var customer = await _db.Customers.FindAsync(new object[] { id }, ct);
        if (customer is null)
        {
            return NotFound();
        }

        _db.Customers.Remove(customer);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
