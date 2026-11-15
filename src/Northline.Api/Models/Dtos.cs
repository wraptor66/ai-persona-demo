using System.ComponentModel.DataAnnotations;

namespace Northline.Api.Models;

/// <summary>Request body for creating an order.</summary>
public record CreateOrderRequest(
    [Required] int CustomerId,
    [Required, MinLength(1)] List<CreateOrderLine> Lines);

/// <summary>One requested line on a new order.</summary>
public record CreateOrderLine(int ProductId, int Quantity);

/// <summary>One item in the low-stock response (KAN-4 AC-2).</summary>
public record LowStockProduct(int Id, string Name, int Quantity, decimal UnitPrice);
