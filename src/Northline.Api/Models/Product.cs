using System.ComponentModel.DataAnnotations;

namespace Northline.Api.Models;

/// <summary>A product in the Northline Retail catalog.</summary>
public class Product
{
    public int Id { get; set; }

    [Required, MaxLength(32)]
    public string Sku { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Category { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public int StockQuantity { get; set; }

    public bool IsActive { get; set; } = true;
}
